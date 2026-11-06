package com.cavaleiro.atalhospc;

import android.Manifest;
import android.bluetooth.BluetoothAdapter;
import android.content.Context;
import android.content.SharedPreferences;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.os.Build;
import android.os.Handler;
import android.os.Looper;
import android.util.Base64;
import android.util.LruCache;

import org.json.JSONArray;
import org.json.JSONException;
import org.json.JSONObject;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.Iterator;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;

/** Estado do app: PCs pareados, conexao automatica, lista de atalhos e icones. Vive enquanto o processo viver. */
final class Hub {
    static final int NO_PC = 0, CONNECTING = 1, ONLINE = 2, OFFLINE = 3, UNPAIRED = 4;
    static final int MODE_AUTO = 0, MODE_USB = 1, MODE_WIFI = 2, MODE_BT = 3;

    private static final int LINGER_MS = 30000;
    private static final int QUEUE_MS = 8000;
    private static final int RETRY_MS = 4000;

    interface Listener {
        void onHubChanged();
    }

    interface MeterListener {
        void onMeter(float peak);
    }

    interface Done {
        void on(JSONObject reply);
    }

    static final class Item {
        String id, name, path, icon;
        boolean dir, missing, sound;
        int volume = 80;
    }

    static final class Paired {
        String pid, pc, bt;
        int port = Transport.DEFAULT_PORT;
        byte[] secret;
        ArrayList<String> targets = new ArrayList<>();
    }

    private static final class Pending {
        JSONObject query;
        Done done;
    }

    private static Hub instance;

    static synchronized Hub get(Context c) {
        if (instance == null) instance = new Hub(c.getApplicationContext());
        return instance;
    }

    private final Context app;
    private final SharedPreferences prefs;
    private final Handler main = new Handler(Looper.getMainLooper());
    private final List<Listener> listeners = new ArrayList<>();
    private final Object wake = new Object();
    private final ExecutorService iconWorker = Executors.newSingleThreadExecutor();
    private final LruCache<String, Bitmap> memory;
    private final HashSet<String> iconsInFlight = new HashSet<>();
    private final HashSet<String> iconsFailed = new HashSet<>();
    private final List<Pending> queue = new ArrayList<>();
    private final Runnable stopLink = this::stopNow;

    private volatile Link link;
    private volatile int state = NO_PC;
    private volatile String detail = "";
    private volatile List<Item> items = new ArrayList<>();
    private volatile List<Item> sounds = new ArrayList<>();
    private volatile HashSet<String> playing = new HashSet<>();
    private volatile int master = 100;
    private volatile boolean muted;
    private volatile boolean meterWanted;
    private volatile MeterListener meterListener;
    private int settingsRevision;
    private volatile boolean browseAllowed = true;
    private volatile boolean wanted;
    private int visible;
    private boolean kicked;

    private Hub(Context c) {
        app = c;
        prefs = c.getSharedPreferences("atalhos", Context.MODE_PRIVATE);
        int kb = (int) (Runtime.getRuntime().maxMemory() / 1024 / 8);
        memory = new LruCache<String, Bitmap>(kb) {
            @Override
            protected int sizeOf(String key, Bitmap value) {
                return value.getByteCount() / 1024;
            }
        };
        Paired p = current();
        if (p != null) {
            items = loadCached("items_", p.pid, false);
            sounds = loadCached("sounds_", p.pid, true);
            state = OFFLINE;
        }
        Thread t = new Thread(this::connectLoop, "atalhos-conexao");
        t.setDaemon(true);
        t.start();
    }

    // ---------- ciclo de vida e avisos ----------

    void addListener(Listener l) {
        listeners.add(l);
    }

    void removeListener(Listener l) {
        listeners.remove(l);
    }

    private void changed() {
        main.post(() -> {
            for (Listener l : new ArrayList<>(listeners)) l.onHubChanged();
        });
    }

    void activityStarted() {
        visible++;
        main.removeCallbacks(stopLink);
        wanted = true;
        kick();
    }

    /** Sem tela aberta, a conexao fica mais um pouco para o proximo toque ser imediato, depois fecha para poupar bateria. */
    void activityStopped() {
        visible--;
        if (visible <= 0) {
            visible = 0;
            main.postDelayed(stopLink, LINGER_MS);
        }
    }

    private void stopNow() {
        wanted = false;
        Link l = link;
        if (l != null) l.close();
    }

    void kick() {
        synchronized (wake) {
            kicked = true;
            wake.notifyAll();
        }
    }

    private void setState(int s, String d) {
        state = s;
        detail = d;
        changed();
    }

    // ---------- ajustes deste aparelho ----------

    int settingsRevision() {
        return settingsRevision;
    }

    int themeId() {
        return prefs.getInt("theme", 2);
    }

    void setTheme(int id) {
        prefs.edit().putInt("theme", id).apply();
        settingsRevision++;
    }

    private static String areaKey(boolean soundArea) {
        return soundArea ? "s" : "a";
    }

    int size(boolean soundArea) {
        return prefs.getInt("size_" + areaKey(soundArea), 0);
    }

    void setSize(boolean soundArea, int size) {
        prefs.edit().putInt("size_" + areaKey(soundArea), size).apply();
        settingsRevision++;
    }

    boolean names(boolean soundArea) {
        return prefs.getBoolean("names_" + areaKey(soundArea), true);
    }

    void setNames(boolean soundArea, boolean show) {
        prefs.edit().putBoolean("names_" + areaKey(soundArea), show).apply();
        settingsRevision++;
    }

    boolean soundsArea() {
        return prefs.getBoolean("sounds_area", false);
    }

    void setSoundsArea(boolean on) {
        prefs.edit().putBoolean("sounds_area", on).apply();
        setMeterWanted(on);
    }

    // ---------- sons ----------

    List<Item> sounds() {
        return sounds;
    }

    boolean isPlaying(String id) {
        return playing.contains(id);
    }

    int master() {
        return master;
    }

    boolean muted() {
        return muted;
    }

    Item selectedSound() {
        List<Item> all = sounds;
        String id = prefs.getString("selected_sound", null);
        for (Item it : all) if (it.id.equals(id)) return it;
        return all.isEmpty() ? null : all.get(0);
    }

    void setSelectedSound(String id) {
        prefs.edit().putString("selected_sound", id).apply();
    }

    void setSoundVolume(Item it, int volume, boolean finished) {
        it.volume = volume;
        Link l = link;
        if (l != null) l.ask(query("sound_volume", "id", it.id, "volume", volume, "final", finished), null);
    }

    void setMaster(int volume, boolean mute, boolean finished) {
        master = volume;
        muted = mute;
        Link l = link;
        if (l != null) l.ask(query("sound_master", "volume", volume, "muted", mute, "final", finished), null);
        changed();
    }

    void setMeterListener(MeterListener l) {
        meterListener = l;
    }

    /** O PC so mede a saida enquanto a area de sons estiver na tela. */
    void setMeterWanted(boolean on) {
        meterWanted = on;
        Link l = link;
        if (l != null) l.ask(query("meter", "on", on), null);
    }

    int state() {
        return state;
    }

    String detail() {
        return detail;
    }

    Link link() {
        return link;
    }

    List<Item> items() {
        return items;
    }

    boolean browseAllowed() {
        return browseAllowed;
    }

    // ---------- PCs pareados ----------

    String deviceId() {
        String id = prefs.getString("device_id", null);
        if (id == null) {
            id = UUID.randomUUID().toString().replace("-", "");
            prefs.edit().putString("device_id", id).apply();
        }
        return id;
    }

    String deviceName() {
        String maker = Build.MANUFACTURER == null ? "" : Build.MANUFACTURER;
        String model = Build.MODEL == null ? "Android" : Build.MODEL;
        return model.toLowerCase().startsWith(maker.toLowerCase()) ? model : maker + " " + model;
    }

    int mode() {
        return prefs.getInt("mode", MODE_AUTO);
    }

    void setMode(int m) {
        prefs.edit().putInt("mode", m).apply();
        Link l = link;
        if (l != null) l.close();
        kick();
    }

    synchronized List<Paired> pairedList() {
        List<Paired> list = new ArrayList<>();
        try {
            JSONArray a = new JSONArray(prefs.getString("paired", "[]"));
            for (int i = 0; i < a.length(); i++) {
                JSONObject o = a.getJSONObject(i);
                Paired p = new Paired();
                p.pid = o.getString("pid");
                p.pc = o.optString("pc", "PC");
                p.bt = o.isNull("bt") ? null : o.optString("bt", null);
                p.port = o.optInt("port", Transport.DEFAULT_PORT);
                p.secret = Base64.decode(o.getString("secret"), Base64.DEFAULT);
                JSONArray t = o.optJSONArray("targets");
                if (t != null) for (int j = 0; j < t.length(); j++) p.targets.add(t.getString(j));
                list.add(p);
            }
        } catch (JSONException ignored) {
        }
        return list;
    }

    private synchronized void savePaired(List<Paired> list) {
        JSONArray a = new JSONArray();
        try {
            for (Paired p : list) {
                JSONObject o = new JSONObject();
                o.put("pid", p.pid).put("pc", p.pc).put("port", p.port);
                o.put("secret", Base64.encodeToString(p.secret, Base64.NO_WRAP));
                if (p.bt != null) o.put("bt", p.bt);
                o.put("targets", new JSONArray(p.targets));
                a.put(o);
            }
        } catch (JSONException e) {
            throw new IllegalStateException(e);
        }
        prefs.edit().putString("paired", a.toString()).apply();
    }

    Paired paired(String pid) {
        for (Paired p : pairedList()) if (p.pid.equals(pid)) return p;
        return null;
    }

    Paired current() {
        String pid = prefs.getString("current", null);
        return pid == null ? null : paired(pid);
    }

    /** Guarda um pareamento novo, faz dele o PC atual e conecta. */
    void addPairing(Link.Pairing r, String target) {
        List<Paired> list = pairedList();
        for (Iterator<Paired> it = list.iterator(); it.hasNext(); ) if (it.next().pid.equals(r.pid)) it.remove();
        Paired p = new Paired();
        p.pid = r.pid;
        p.pc = r.pc;
        p.port = r.port;
        p.secret = r.secret;
        rememberTarget(p, target);
        list.add(p);
        savePaired(list);
        select(r.pid, target);
    }

    private static void rememberTarget(Paired p, String target) {
        if (target.startsWith("bt:")) {
            p.bt = target.substring(3);
            return;
        }
        p.targets.remove(target);
        p.targets.add(0, target);
        while (p.targets.size() > 6) p.targets.remove(p.targets.size() - 1);
    }

    void select(String pid, String target) {
        Paired p = paired(pid);
        if (p == null) return;
        if (target != null) {
            List<Paired> list = pairedList();
            for (Paired q : list) if (q.pid.equals(pid)) rememberTarget(q, target);
            savePaired(list);
            prefs.edit().putString("last_" + pid, target).apply();
        }
        boolean samePc = pid.equals(prefs.getString("current", null));
        prefs.edit().putString("current", pid).apply();
        if (!samePc) {
            items = loadCached("items_", pid, false);
            sounds = loadCached("sounds_", pid, true);
            changed();
        }
        Link l = link;
        if (l != null && (!samePc || target != null)) l.close();
        wanted = true;
        kick();
    }

    void forget(String pid) {
        List<Paired> list = pairedList();
        for (Iterator<Paired> it = list.iterator(); it.hasNext(); ) if (it.next().pid.equals(pid)) it.remove();
        savePaired(list);
        SharedPreferences.Editor e = prefs.edit().remove("items_" + pid).remove("sounds_" + pid).remove("last_" + pid);
        if (pid.equals(prefs.getString("current", null))) {
            e.putString("current", list.isEmpty() ? null : list.get(0).pid);
            items = list.isEmpty() ? new ArrayList<Item>() : loadCached("items_", list.get(0).pid, false);
            sounds = list.isEmpty() ? new ArrayList<Item>() : loadCached("sounds_", list.get(0).pid, true);
            Link l = link;
            if (l != null) l.close();
            if (list.isEmpty()) setState(NO_PC, "");
        }
        e.apply();
        changed();
        kick();
    }

    // ---------- conexao automatica ----------

    private void connectLoop() {
        while (true) {
            Paired p = wanted ? current() : null;
            if (p == null) {
                if (current() == null) setState(NO_PC, "");
                else if (state == ONLINE || state == CONNECTING) setState(OFFLINE, "");
                sleepUntilKick(0);
                continue;
            }
            setState(CONNECTING, "");
            boolean needPair = false;
            String lastError = "";
            Link l = null;
            for (String target : candidates(p)) {
                if (!wanted) break;
                try {
                    l = Link.connect(target, deviceId(), p.pid, p.secret, this::onPush);
                    break;
                } catch (Link.NeedPair e) {
                    needPair = true;
                    break;
                } catch (IOException e) {
                    lastError = e.getMessage() == null ? "" : e.getMessage();
                }
            }
            if (l != null) {
                online(p, l);
                l.awaitClosed();
                link = null;
                iconsFailed.clear();
                setState(OFFLINE, "");
                continue; // tenta voltar logo
            }
            if (needPair) {
                setState(UNPAIRED, "");
                sleepUntilKick(30000);
            } else {
                setState(OFFLINE, lastError);
                sleepUntilKick(RETRY_MS);
            }
        }
    }

    private void online(Paired p, Link l) {
        link = l;
        prefs.edit().putString("last_" + p.pid, l.target).apply();
        if (!l.target.startsWith("bt:") && !l.target.startsWith("tcp:127.")) {
            List<Paired> list = pairedList();
            for (Paired q : list) if (q.pid.equals(p.pid)) rememberTarget(q, l.target);
            savePaired(list);
        }
        setState(ONLINE, l.via);
        refresh();
        if (meterWanted) l.ask(query("meter", "on", true), null);
        List<Pending> copy;
        synchronized (queue) {
            copy = new ArrayList<>(queue);
            queue.clear();
        }
        for (Pending q : copy) send(l, q.query, q.done);
    }

    private void sleepUntilKick(int ms) {
        synchronized (wake) {
            long end = System.currentTimeMillis() + ms;
            while (!kicked) {
                long left = ms == 0 ? 0 : end - System.currentTimeMillis();
                if (ms != 0 && left <= 0) break;
                try {
                    wake.wait(left);
                } catch (InterruptedException e) {
                    return;
                }
            }
            kicked = false;
        }
    }

    /** Ordem: o ultimo caminho que funcionou, tunel adb, redes encontradas (USB antes do Wi-Fi), IPs salvos, Bluetooth. */
    private List<String> candidates(Paired p) {
        int mode = mode();
        List<String> list = new ArrayList<>();
        String last = prefs.getString("last_" + p.pid, null);
        if (mode == MODE_AUTO && last != null) list.add(last);
        if (mode != MODE_BT) {
            if (mode != MODE_WIFI) addOnce(list, "tcp:127.0.0.1:" + p.port);
            List<Transport.Found> found = Transport.discover(500);
            if (mode != MODE_WIFI) {
                for (Transport.Found f : found) if (f.pid.equals(p.pid) && f.label.equals("USB")) addOnce(list, f.target);
            }
            if (mode != MODE_USB) {
                for (Transport.Found f : found) if (f.pid.equals(p.pid) && !f.label.equals("USB")) addOnce(list, f.target);
                for (String t : p.targets) addOnce(list, t);
            }
        }
        if ((mode == MODE_AUTO || mode == MODE_BT) && p.bt != null && bluetoothReady()) addOnce(list, "bt:" + p.bt);
        return list;
    }

    private static void addOnce(List<String> list, String v) {
        if (!list.contains(v)) list.add(v);
    }

    boolean bluetoothAllowed() {
        return Build.VERSION.SDK_INT < 31 || app.checkSelfPermission(Manifest.permission.BLUETOOTH_CONNECT) == PackageManager.PERMISSION_GRANTED;
    }

    private boolean bluetoothReady() {
        if (!bluetoothAllowed()) return false;
        BluetoothAdapter a = BluetoothAdapter.getDefaultAdapter();
        try {
            return a != null && a.isEnabled();
        } catch (SecurityException e) {
            return false;
        }
    }

    private void onPush(JSONObject message) {
        String type = message.optString("t");
        if ("changed".equals(type)) {
            refresh();
        } else if ("sound_state".equals(type)) {
            playing = idSet(message.optJSONArray("playing"));
            changed();
        } else if ("meter".equals(type)) {
            final float peak = (float) message.optDouble("p", 0);
            main.post(() -> {
                MeterListener l = meterListener;
                if (l != null) l.onMeter(peak);
            });
        }
    }

    private static HashSet<String> idSet(JSONArray a) {
        HashSet<String> set = new HashSet<>();
        if (a != null) for (int i = 0; i < a.length(); i++) set.add(a.optString(i));
        return set;
    }

    // ---------- pedidos ----------

    /** Sem conexao, o pedido espera alguns segundos: o toque nao se perde enquanto o app reconecta. */
    void request(JSONObject q, Done done) {
        Link l = link;
        if (l != null && l.isOpen()) {
            send(l, q, done);
            return;
        }
        final Pending p = new Pending();
        p.query = q;
        p.done = done;
        synchronized (queue) {
            queue.add(p);
        }
        wanted = true;
        kick();
        main.postDelayed(() -> {
            boolean mine;
            synchronized (queue) {
                mine = queue.remove(p);
            }
            if (mine && done != null) done.on(Link.failure("Sem conexão com o PC."));
        }, QUEUE_MS);
    }

    private void send(Link l, JSONObject q, Done done) {
        l.ask(q, done == null ? null : reply -> main.post(() -> done.on(reply)));
    }

    static JSONObject query(String type, Object... kv) {
        JSONObject o = new JSONObject();
        try {
            o.put("t", type);
            for (int i = 0; i < kv.length; i += 2) o.put((String) kv[i], kv[i + 1]);
        } catch (JSONException e) {
            throw new IllegalStateException(e);
        }
        return o;
    }

    void refresh() {
        Paired p = current();
        Link l = link;
        if (p == null || l == null) return;
        l.ask(query("list"), reply -> {
            if (!reply.optBoolean("ok")) return;
            JSONArray a = reply.optJSONArray("items");
            if (a == null) return;
            items = parseItems(a, false);
            browseAllowed = reply.optBoolean("browse", true);
            JSONArray s = reply.optJSONArray("sounds");
            sounds = parseItems(s == null ? new JSONArray() : s, true);
            master = reply.optInt("master", 100);
            muted = reply.optBoolean("muted");
            playing = idSet(reply.optJSONArray("playing"));
            prefs.edit().putString("items_" + p.pid, a.toString()).putString("sounds_" + p.pid, s == null ? "[]" : s.toString()).apply();
            changed();
        });
    }

    private static List<Item> parseItems(JSONArray a, boolean sound) {
        List<Item> list = new ArrayList<>();
        for (int i = 0; i < a.length(); i++) {
            JSONObject o = a.optJSONObject(i);
            if (o == null) continue;
            Item it = new Item();
            it.id = o.optString("id");
            it.name = o.optString("name");
            it.path = o.optString("path");
            it.icon = o.optString("icon");
            it.dir = o.optBoolean("dir");
            it.missing = o.optBoolean("missing");
            it.sound = sound;
            it.volume = o.optInt("volume", 80);
            list.add(it);
        }
        return list;
    }

    private List<Item> loadCached(String prefix, String pid, boolean sound) {
        try {
            return parseItems(new JSONArray(prefs.getString(prefix + pid, "[]")), sound);
        } catch (JSONException e) {
            return new ArrayList<>();
        }
    }

    // ---------- icones ----------

    private File iconFile(String key) {
        File dir = new File(app.getFilesDir(), "icones");
        dir.mkdirs();
        return new File(dir, key + ".png");
    }

    private static boolean safeKey(String key) {
        return key != null && key.matches("[0-9a-f]{8,64}");
    }

    private int sampleSize() {
        int wanted = (int) (96 * app.getResources().getDisplayMetrics().density);
        int sample = 1;
        while (256 / (sample * 2) >= wanted) sample *= 2;
        return sample;
    }

    /** Devolve o icone se ja estiver pronto; senao busca no PC e avisa os ouvintes quando chegar. */
    Bitmap icon(Item it) {
        if (!safeKey(it.icon)) return null;
        Bitmap b = memory.get(it.icon);
        if (b != null) return b;
        synchronized (iconsInFlight) {
            if (iconsFailed.contains(it.icon) || !iconsInFlight.add(it.icon)) return null;
        }
        final String key = it.icon;
        final String id = it.id;
        iconWorker.execute(() -> {
            File f = iconFile(key);
            if (f.exists() && remember(key, BitmapFactory.decodeFile(f.getPath(), sampled()))) return;
            Link l = link;
            if (l == null) {
                release(key);
                return;
            }
            l.ask(query("icon", "id", id), reply -> iconWorker.execute(() -> {
                byte[] png = reply.optBoolean("ok") ? Base64.decode(reply.optString("png"), Base64.DEFAULT) : null;
                if (png != null) {
                    try (FileOutputStream out = new FileOutputStream(f)) {
                        out.write(png);
                    } catch (IOException ignored) {
                    }
                }
                if (png == null || !remember(key, BitmapFactory.decodeByteArray(png, 0, png.length, sampled()))) {
                    synchronized (iconsInFlight) {
                        iconsFailed.add(key);
                    }
                    release(key);
                }
            }));
        });
        return null;
    }

    /** Icone de um arquivo solto na navegacao de pastas; fica so na memoria. */
    Bitmap iconForPath(String path) {
        final String key = "p:" + path;
        Bitmap b = memory.get(key);
        if (b != null) return b;
        Link l = link;
        if (l == null) return null;
        synchronized (iconsInFlight) {
            if (iconsFailed.contains(key) || !iconsInFlight.add(key)) return null;
        }
        l.ask(query("icon", "path", path), reply -> iconWorker.execute(() -> {
            byte[] png = reply.optBoolean("ok") ? Base64.decode(reply.optString("png"), Base64.DEFAULT) : null;
            if (png == null || !remember(key, BitmapFactory.decodeByteArray(png, 0, png.length, sampled()))) {
                synchronized (iconsInFlight) {
                    iconsFailed.add(key);
                }
                release(key);
            }
        }));
        return null;
    }

    /** Versao em tamanho cheio para o atalho na tela inicial. */
    Bitmap fullIcon(Item it) {
        if (!safeKey(it.icon)) return null;
        File f = iconFile(it.icon);
        return f.exists() ? BitmapFactory.decodeFile(f.getPath()) : null;
    }

    private BitmapFactory.Options sampled() {
        BitmapFactory.Options o = new BitmapFactory.Options();
        o.inSampleSize = sampleSize();
        return o;
    }

    private boolean remember(String key, Bitmap b) {
        if (b == null) return false;
        memory.put(key, b);
        release(key);
        changed();
        return true;
    }

    private void release(String key) {
        synchronized (iconsInFlight) {
            iconsInFlight.remove(key);
        }
    }
}
