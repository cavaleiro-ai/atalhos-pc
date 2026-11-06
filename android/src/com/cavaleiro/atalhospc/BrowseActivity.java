package com.cavaleiro.atalhospc;

import android.app.Activity;
import android.content.Context;
import android.graphics.Bitmap;
import android.os.Bundle;
import android.text.TextUtils;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.Button;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/** Navega nos arquivos do PC para abrir na hora ou virar atalho. */
public class BrowseActivity extends Activity implements Hub.Listener {
    private static final class Entry {
        String name, path;
        boolean dir;
    }

    static final String EXTRA_MODE = "mode";
    static final String EXTRA_TARGET = "target";
    static final int MODE_SHORTCUT = 0, MODE_SOUND = 1, MODE_IMAGE = 2;
    private static final String[] AUDIO = {".mp3", ".wav", ".wma", ".m4a", ".aac"};
    private static final String[] IMAGES = {".png", ".jpg", ".jpeg", ".bmp", ".gif"};

    private int mode;
    private String targetId;
    private Hub hub;
    private TextView pathText;
    private TextView notice;
    private Button addHere;
    private final List<Entry> entries = new ArrayList<>();
    private final RowAdapter adapter = new RowAdapter();
    private String currentPath; // null = lista de locais
    private final ArrayList<String> history = new ArrayList<>();
    private boolean loadedOnce;
    private int generation;

    @Override
    protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        hub = Hub.get(this);
        mode = getIntent().getIntExtra(EXTRA_MODE, MODE_SHORTCUT);
        targetId = getIntent().getStringExtra(EXTRA_TARGET);
        Ui.prepare(this);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setBackgroundColor(Ui.BG);
        int pad = Ui.dp(this, 12);
        root.setPadding(pad, pad, pad, pad);

        LinearLayout bar = new LinearLayout(this);
        bar.setGravity(Gravity.CENTER_VERTICAL);
        Button back = Ui.button(this, "Voltar", Ui.CARD_HIGH);
        back.setOnClickListener(v -> onBackPressed());
        bar.addView(back);
        pathText = Ui.text(this, "", 14, Ui.TEXT);
        pathText.setSingleLine(true);
        pathText.setEllipsize(TextUtils.TruncateAt.START);
        pathText.setPadding(pad, 0, pad, 0);
        bar.addView(pathText, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
        addHere = Ui.button(this, Ui.narrow(this) ? "Adicionar" : "Adicionar esta pasta", Ui.CARD_HIGH);
        addHere.setOnClickListener(v -> add(currentPath));
        bar.addView(addHere);
        root.addView(bar);

        notice = Ui.text(this, "", 13, Ui.WARN);
        notice.setPadding(0, pad, 0, 0);
        notice.setVisibility(View.GONE);
        root.addView(notice);

        ListView list = new ListView(this);
        list.setDivider(null);
        list.setDividerHeight(Ui.dp(this, 6));
        list.setSelector(android.R.color.transparent);
        list.setClipToPadding(false);
        list.setPadding(0, pad, 0, pad);
        list.setAdapter(adapter);
        list.setOnItemClickListener((p, v, pos, id) -> {
            Entry e = entries.get(pos);
            if (e.dir) load(e.path, true);
            else if (mode == MODE_SOUND) addSound(e);
            else if (mode == MODE_IMAGE) useImage(e);
            else askFile(e);
        });
        list.setOnItemLongClickListener((p, v, pos, id) -> {
            if (mode != MODE_SHORTCUT) return false;
            askFile(entries.get(pos));
            return true;
        });
        root.addView(list, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
        setContentView(root);
        load(null, false);
    }

    @Override
    protected void onStart() {
        super.onStart();
        hub.addListener(this);
        hub.activityStarted();
    }

    @Override
    protected void onStop() {
        super.onStop();
        hub.removeListener(this);
        hub.activityStopped();
    }

    @Override
    public void onHubChanged() {
        if (!loadedOnce && hub.state() == Hub.ONLINE) load(currentPath, false);
        adapter.notifyDataSetChanged();
    }

    @Override
    public void onBackPressed() {
        if (history.isEmpty()) super.onBackPressed();
        else load(history.remove(history.size() - 1), false);
    }

    private void load(String path, boolean remember) {
        final int mine = ++generation;
        notice.setVisibility(View.GONE);
        hub.request(path == null ? Hub.query("places") : Hub.query("browse", "path", path), reply -> {
            if (isDestroyed() || mine != generation) return;
            if (!reply.optBoolean("ok")) {
                show(reply.optString("err", "Não foi possível abrir."));
                return;
            }
            loadedOnce = true;
            if (remember) history.add(currentPath);
            currentPath = path;
            entries.clear();
            JSONArray a = reply.optJSONArray("entries");
            if (a != null) {
                for (int i = 0; i < a.length(); i++) {
                    JSONObject o = a.optJSONObject(i);
                    if (o == null) continue;
                    Entry e = new Entry();
                    e.name = o.optString("name");
                    e.path = o.optString("path");
                    e.dir = o.optBoolean("dir");
                    if (e.dir || accepts(e.name)) entries.add(e);
                }
            }
            pathText.setText(path == null ? (mode == MODE_SOUND ? "Escolha um áudio" : mode == MODE_IMAGE ? "Escolha uma imagem" : "Locais do PC") : path);
            addHere.setVisibility(path == null || mode != MODE_SHORTCUT ? View.GONE : View.VISIBLE);
            adapter.notifyDataSetChanged();
            if (reply.optBoolean("cut")) show("Esta pasta tem muitos itens: mostrando os 3.000 primeiros.");
            else if (entries.isEmpty()) show("Pasta vazia.");
        });
    }

    private boolean accepts(String name) {
        if (mode == MODE_SHORTCUT) return true;
        String lower = name.toLowerCase();
        for (String ext : mode == MODE_SOUND ? AUDIO : IMAGES) if (lower.endsWith(ext)) return true;
        return false;
    }

    private void addSound(Entry e) {
        hub.request(Hub.query("sound_add", "path", e.path), reply -> {
            if (isDestroyed()) return;
            if (!reply.optBoolean("ok")) {
                Toast.makeText(this, reply.optString("err", "Não foi possível adicionar."), Toast.LENGTH_LONG).show();
                return;
            }
            JSONObject item = reply.optJSONObject("item");
            if (item == null) return;
            hub.setSelectedSound(item.optString("id"));
            // cada som ganha a sua logo logo depois de entrar
            IconPicker.show(this, hub, item.optString("id"), item.optString("name"), this::finish);
        });
    }

    private void useImage(Entry e) {
        hub.request(Hub.query("sound_seticon", "id", targetId, "path", e.path), reply -> {
            if (isDestroyed()) return;
            if (reply.optBoolean("ok")) finish();
            else Toast.makeText(this, reply.optString("err", "Não foi possível usar esta imagem."), Toast.LENGTH_LONG).show();
        });
    }

    private void show(String text) {
        notice.setText(text);
        notice.setVisibility(View.VISIBLE);
    }

    private void askFile(Entry e) {
        String[] options = e.dir ? new String[] {"Abrir no PC", "Adicionar aos atalhos", "Entrar na pasta"} : new String[] {"Abrir no PC", "Adicionar aos atalhos"};
        Ui.dialog(this)
                .setTitle(e.name)
                .setItems(options, (d, which) -> {
                    if (which == 0) open(e);
                    else if (which == 1) add(e.path);
                    else load(e.path, true);
                })
                .show();
    }

    private void open(Entry e) {
        hub.request(Hub.query("open", "path", e.path), reply -> {
            if (isDestroyed()) return;
            Toast.makeText(this, reply.optBoolean("ok") ? "Abrindo " + e.name : reply.optString("err", "Não foi possível abrir."), Toast.LENGTH_LONG).show();
        });
    }

    private void add(String path) {
        hub.request(Hub.query("add", "path", path), reply -> {
            if (isDestroyed()) return;
            Toast.makeText(this, reply.optBoolean("ok") ? "Adicionado aos atalhos" : reply.optString("err", "Não foi possível adicionar."), Toast.LENGTH_LONG).show();
        });
    }

    private final class RowAdapter extends BaseAdapter {
        @Override
        public int getCount() {
            return entries.size();
        }

        @Override
        public Object getItem(int position) {
            return entries.get(position);
        }

        @Override
        public long getItemId(int position) {
            return position;
        }

        @Override
        public View getView(int position, View convert, ViewGroup parent) {
            Context c = BrowseActivity.this;
            LinearLayout row;
            ImageView image;
            TextView name;
            if (convert == null) {
                row = new LinearLayout(c);
                row.setGravity(Gravity.CENTER_VERTICAL);
                row.setBackground(Ui.tileBackground(c, Ui.CARD, 10));
                int pad = Ui.dp(c, 8);
                row.setPadding(pad, pad, pad, pad);
                image = new ImageView(c);
                image.setScaleType(ImageView.ScaleType.FIT_CENTER);
                row.addView(image, new LinearLayout.LayoutParams(Ui.dp(c, 36), Ui.dp(c, 36)));
                name = Ui.text(c, "", 15, Ui.TEXT);
                name.setSingleLine(true);
                name.setEllipsize(TextUtils.TruncateAt.MIDDLE);
                name.setPadding(Ui.dp(c, 12), 0, 0, 0);
                row.addView(name, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
            } else {
                row = (LinearLayout) convert;
                image = (ImageView) row.getChildAt(0);
                name = (TextView) row.getChildAt(1);
            }
            Entry e = entries.get(position);
            Bitmap icon = hub.iconForPath(e.path);
            if (icon != null) image.setImageBitmap(icon);
            else image.setImageBitmap(Ui.letterIcon(c, e.name));
            name.setText(e.name);
            return row;
        }
    }
}
