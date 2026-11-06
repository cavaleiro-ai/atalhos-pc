package com.cavaleiro.atalhospc;

import android.Manifest;
import android.app.Activity;
import android.bluetooth.BluetoothAdapter;
import android.bluetooth.BluetoothClass;
import android.bluetooth.BluetoothDevice;
import android.content.ActivityNotFoundException;
import android.content.ComponentName;
import android.content.Intent;
import android.os.Build;
import android.os.Bundle;
import android.provider.Settings;
import android.text.InputFilter;
import android.text.InputType;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.RadioButton;
import android.widget.RadioGroup;
import android.widget.ScrollView;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONObject;

import java.io.IOException;
import java.util.ArrayList;
import java.util.List;

/** Escolha do PC e do meio de conexao: USB, Wi-Fi ou Bluetooth. */
public class ConnectActivity extends Activity implements Hub.Listener {
    private static final int REQUEST_BT = 7;

    private Hub hub;
    private TextView stateText;
    private LinearLayout pairedBox;
    private LinearLayout foundBox;
    private LinearLayout btBox;
    private Button searchButton;
    private EditText ipField;
    private volatile boolean destroyed;

    @Override
    protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        hub = Hub.get(this);
        Ui.prepare(this);

        ScrollView scroll = new ScrollView(this);
        scroll.setBackgroundColor(Ui.BG);
        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);
        int pad = Ui.dp(this, 16);
        col.setPadding(pad, pad, pad, pad * 2);
        scroll.addView(col);

        TextView title = Ui.text(this, "Conectar ao PC", 20, Ui.TEXT);
        title.getPaint().setFakeBoldText(true);
        col.addView(title);
        stateText = Ui.text(this, "", 13, Ui.MUTED);
        col.addView(stateText);

        col.addView(Ui.heading(this, "PCs pareados"));
        pairedBox = new LinearLayout(this);
        pairedBox.setOrientation(LinearLayout.VERTICAL);
        col.addView(pairedBox);

        col.addView(Ui.heading(this, "Conectar por"));
        col.addView(modeSelector());

        col.addView(Ui.heading(this, "PCs encontrados na rede e no USB"));
        col.addView(Ui.text(this, "Abra o AtalhosPC.exe no computador. Para parear, clique em \"Parear novo aparelho\" na janela dele.", 13, Ui.MUTED));
        foundBox = new LinearLayout(this);
        foundBox.setOrientation(LinearLayout.VERTICAL);
        col.addView(foundBox);
        searchButton = Ui.button(this, "Procurar de novo", Ui.CARD_HIGH);
        searchButton.setOnClickListener(v -> search());
        col.addView(searchButton, spaced());

        col.addView(Ui.heading(this, "USB"));
        col.addView(Ui.text(this, "Ligue o cabo e ative a Ancoragem USB neste aparelho. O PC aparece na lista acima. Quem usa depuração USB pode marcar \"USB por adb\" no programa do PC.", 13, Ui.MUTED));
        Button tether = Ui.button(this, "Abrir Ancoragem USB", Ui.CARD_HIGH);
        tether.setOnClickListener(v -> openTethering());
        col.addView(tether, spaced());

        col.addView(Ui.heading(this, "Digitar o IP do PC"));
        col.addView(Ui.text(this, "Use se o PC não aparecer na lista. O IP está na janela do programa no PC.", 13, Ui.MUTED));
        ipField = new EditText(this);
        ipField.setHint("192.168.0.10");
        ipField.setSingleLine(true);
        ipField.setTextColor(Ui.TEXT);
        ipField.setHintTextColor(Ui.MUTED);
        ipField.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);
        col.addView(ipField);
        Button ipGo = Ui.button(this, "Conectar neste IP", Ui.CARD_HIGH);
        ipGo.setOnClickListener(v -> connectToIp());
        col.addView(ipGo, spaced());

        col.addView(Ui.heading(this, "Bluetooth"));
        col.addView(Ui.text(this, "Primeiro pareie este aparelho com o PC nas configurações de Bluetooth. O PC precisa ter adaptador Bluetooth.", 13, Ui.MUTED));
        btBox = new LinearLayout(this);
        btBox.setOrientation(LinearLayout.VERTICAL);
        col.addView(btBox);
        Button btList = Ui.button(this, "Mostrar PCs pareados por Bluetooth", Ui.CARD_HIGH);
        btList.setOnClickListener(v -> showBluetooth());
        col.addView(btList, spaced());
        Button btSettings = Ui.button(this, "Abrir configurações de Bluetooth", Ui.CARD_HIGH);
        btSettings.setOnClickListener(v -> startActivity(new Intent(Settings.ACTION_BLUETOOTH_SETTINGS)));
        col.addView(btSettings, spaced());

        setContentView(scroll);
        search();
    }

    private LinearLayout.LayoutParams spaced() {
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.topMargin = Ui.dp(this, 8);
        return lp;
    }

    private View modeSelector() {
        String[] labels = {"Automático", "USB", "Wi-Fi", "Bluetooth"};
        int[] modes = {Hub.MODE_AUTO, Hub.MODE_USB, Hub.MODE_WIFI, Hub.MODE_BT};
        RadioGroup group = new RadioGroup(this);
        group.setOrientation(Ui.narrow(this) ? RadioGroup.VERTICAL : RadioGroup.HORIZONTAL);
        for (int i = 0; i < labels.length; i++) {
            RadioButton b = new RadioButton(this);
            b.setText(labels[i]);
            b.setTextColor(Ui.TEXT);
            b.setId(100 + i);
            b.setPadding(0, Ui.dp(this, 10), Ui.dp(this, 14), Ui.dp(this, 10));
            group.addView(b);
            if (modes[i] == hub.mode()) b.setChecked(true);
        }
        group.setOnCheckedChangeListener((g, id) -> {
            int mode = modes[id - 100];
            if (mode == hub.mode()) return;
            hub.setMode(mode);
            if (mode == Hub.MODE_BT) ensureBluetoothPermission();
        });
        return group;
    }

    @Override
    protected void onStart() {
        super.onStart();
        hub.addListener(this);
        hub.activityStarted();
        onHubChanged();
    }

    @Override
    protected void onStop() {
        super.onStop();
        hub.removeListener(this);
        hub.activityStopped();
    }

    @Override
    protected void onDestroy() {
        destroyed = true;
        super.onDestroy();
    }

    @Override
    public void onHubChanged() {
        Hub.Paired current = hub.current();
        String text;
        switch (hub.state()) {
            case Hub.ONLINE:
                text = "Conectado a " + (current == null ? "PC" : current.pc) + " por " + hub.detail();
                break;
            case Hub.CONNECTING:
                text = "Conectando…";
                break;
            case Hub.UNPAIRED:
                text = "O PC não reconhece mais este aparelho. Pareie de novo.";
                break;
            case Hub.OFFLINE:
                text = "Sem conexão. Veja se o AtalhosPC.exe está aberto no PC.";
                break;
            default:
                text = "Nenhum PC pareado ainda.";
                break;
        }
        stateText.setText(text);
        fillPaired();
    }

    private void fillPaired() {
        pairedBox.removeAllViews();
        Hub.Paired current = hub.current();
        List<Hub.Paired> list = hub.pairedList();
        if (list.isEmpty()) pairedBox.addView(Ui.text(this, "Nenhum.", 13, Ui.MUTED));
        for (Hub.Paired p : list) {
            boolean isCurrent = current != null && current.pid.equals(p.pid);
            LinearLayout row = Ui.row(this, p.pc + (isCurrent ? "  (atual)" : ""), "Toque para usar. Segure para esquecer este PC.");
            row.setOnClickListener(v -> {
                hub.select(p.pid, null);
                finish();
            });
            row.setOnLongClickListener(v -> {
                confirmForget(p);
                return true;
            });
            pairedBox.addView(row);
        }
    }

    private void confirmForget(Hub.Paired p) {
        Ui.dialog(this)
                .setTitle("Esquecer " + p.pc + "?")
                .setMessage("Este aparelho deixa de abrir arquivos nesse PC. Para voltar, será preciso parear de novo.")
                .setPositiveButton("Esquecer", (d, w) -> hub.forget(p.pid))
                .setNegativeButton("Cancelar", null)
                .show();
    }

    // ---------- busca na rede ----------

    private void search() {
        searchButton.setEnabled(false);
        searchButton.setText("Procurando…");
        new Thread(() -> {
            List<Transport.Found> found = Transport.discover(1500);
            addAdbTunnel(found);
            runOnUiThread(() -> {
                if (destroyed) return;
                searchButton.setEnabled(true);
                searchButton.setText("Procurar de novo");
                fillFound(found);
            });
        }).start();
    }

    /** O tunel do adb so leva TCP, entao a busca por broadcast nao o enxerga: pergunta direto. */
    private void addAdbTunnel(List<Transport.Found> found) {
        String target = "tcp:127.0.0.1:" + Transport.DEFAULT_PORT;
        try {
            JSONObject h = Link.probe(target, hub.deviceId());
            Transport.Found f = new Transport.Found();
            f.pid = h.getString("pid");
            f.pc = h.optString("pc", "PC");
            f.target = target;
            f.label = "USB (adb)";
            found.add(0, f);
        } catch (IOException | org.json.JSONException ignored) {
            // sem tunel ativo: normal para quem usa Wi-Fi ou Ancoragem USB
        }
    }

    private void fillFound(List<Transport.Found> found) {
        foundBox.removeAllViews();
        if (found.isEmpty()) {
            foundBox.addView(Ui.text(this, "Nada encontrado. Confira se o programa está aberto no PC, se o firewall foi liberado e se os dois estão na mesma rede ou com a Ancoragem USB ligada.", 13, Ui.WARN));
        }
        for (Transport.Found f : found) {
            String address = f.target.substring(4);
            LinearLayout row = Ui.row(this, f.pc, f.label + " · " + address);
            row.setOnClickListener(v -> tryTarget(f.target));
            foundBox.addView(row);
        }
    }

    private void connectToIp() {
        String text = ipField.getText().toString().trim();
        if (text.isEmpty()) return;
        tryTarget("tcp:" + (text.contains(":") ? text : text + ":" + Transport.DEFAULT_PORT));
    }

    /** Se este aparelho ja e conhecido pelo PC, conecta. Se nao, pede o codigo de pareamento. */
    private void tryTarget(String target) {
        Toast.makeText(this, "Conectando…", Toast.LENGTH_SHORT).show();
        new Thread(() -> {
            try {
                JSONObject h = Link.probe(target, hub.deviceId());
                String pid = h.getString("pid");
                String pc = h.optString("pc", "PC");
                boolean known = h.optBoolean("known") && hub.paired(pid) != null;
                runOnUiThread(() -> {
                    if (destroyed) return;
                    if (known) {
                        hub.select(pid, target);
                        finish();
                    } else {
                        askCode(target, pc);
                    }
                });
            } catch (Exception e) {
                runOnUiThread(() -> {
                    if (!destroyed) Toast.makeText(this, "Não conectou: " + e.getMessage(), Toast.LENGTH_LONG).show();
                });
            }
        }).start();
    }

    private void askCode(String target, String pc) {
        EditText input = new EditText(this);
        input.setHint("ABC-DEF");
        input.setSingleLine(true);
        input.setInputType(InputType.TYPE_CLASS_TEXT | InputType.TYPE_TEXT_FLAG_CAP_CHARACTERS | InputType.TYPE_TEXT_FLAG_NO_SUGGESTIONS);
        input.setFilters(new InputFilter[] {new InputFilter.AllCaps(), new InputFilter.LengthFilter(7)});
        Ui.showWithInput(this, Ui.dialog(this)
                .setTitle("Parear com " + pc)
                .setMessage("No PC, clique em \"Parear novo aparelho\" e digite aqui o código que aparecer.")
                .setPositiveButton("Parear", (d, w) -> pair(target, pc, input.getText().toString()))
                .setNegativeButton("Cancelar", null), input);
    }

    private void pair(String target, String pc, String typed) {
        String code = typed.toUpperCase().replaceAll("[^0-9A-Z]", "");
        Toast.makeText(this, "Pareando…", Toast.LENGTH_SHORT).show();
        new Thread(() -> {
            try {
                Link.Pairing r = Link.pair(target, hub.deviceId(), hub.deviceName(), code);
                runOnUiThread(() -> {
                    hub.addPairing(r, target);
                    Toast.makeText(getApplicationContext(), "Pareado com " + r.pc, Toast.LENGTH_LONG).show();
                    if (!destroyed) finish();
                });
            } catch (Link.WrongCode e) {
                message("Código errado" + (e.left > 0 ? ". Restam " + e.left + " tentativas." : ". Gere um novo código no PC."));
            } catch (Link.PairClosed e) {
                message("O pareamento não está aberto. Clique em \"Parear novo aparelho\" no PC.");
            } catch (IOException e) {
                message("Não pareou: " + e.getMessage());
            }
        }).start();
    }

    private void message(String text) {
        runOnUiThread(() -> {
            if (!destroyed) Toast.makeText(this, text, Toast.LENGTH_LONG).show();
        });
    }

    // ---------- USB ----------

    private void openTethering() {
        Intent direct = new Intent().setComponent(new ComponentName("com.android.settings", "com.android.settings.TetherSettings"));
        try {
            startActivity(direct);
        } catch (ActivityNotFoundException | SecurityException e) {
            startActivity(new Intent(Settings.ACTION_WIRELESS_SETTINGS));
        }
    }

    // ---------- Bluetooth ----------

    private boolean ensureBluetoothPermission() {
        if (hub.bluetoothAllowed()) return true;
        if (Build.VERSION.SDK_INT >= 31) requestPermissions(new String[] {Manifest.permission.BLUETOOTH_CONNECT}, REQUEST_BT);
        return false;
    }

    @Override
    public void onRequestPermissionsResult(int code, String[] permissions, int[] results) {
        if (code == REQUEST_BT && hub.bluetoothAllowed()) {
            showBluetooth();
            hub.kick();
        } else if (code == REQUEST_BT) {
            Toast.makeText(this, "Sem a permissão, o Bluetooth não pode ser usado. USB e Wi-Fi continuam funcionando.", Toast.LENGTH_LONG).show();
        }
    }

    private void showBluetooth() {
        if (!ensureBluetoothPermission()) return;
        btBox.removeAllViews();
        BluetoothAdapter adapter = BluetoothAdapter.getDefaultAdapter();
        if (adapter == null) {
            btBox.addView(Ui.text(this, "Este aparelho não tem Bluetooth.", 13, Ui.WARN));
            return;
        }
        if (!adapter.isEnabled()) {
            btBox.addView(Ui.text(this, "O Bluetooth está desligado neste aparelho.", 13, Ui.WARN));
            return;
        }
        List<BluetoothDevice> computers = new ArrayList<>();
        List<BluetoothDevice> others = new ArrayList<>();
        for (BluetoothDevice d : adapter.getBondedDevices()) {
            BluetoothClass k = d.getBluetoothClass();
            if (k != null && k.getMajorDeviceClass() == BluetoothClass.Device.Major.COMPUTER) computers.add(d);
            else others.add(d);
        }
        computers.addAll(others);
        if (computers.isEmpty()) {
            btBox.addView(Ui.text(this, "Nenhum aparelho pareado. Pareie com o PC nas configurações de Bluetooth.", 13, Ui.WARN));
            return;
        }
        for (BluetoothDevice d : computers) {
            LinearLayout row = Ui.row(this, d.getName() == null ? d.getAddress() : d.getName(), d.getAddress());
            row.setOnClickListener(v -> tryTarget("bt:" + d.getAddress()));
            btBox.addView(row);
        }
    }
}
