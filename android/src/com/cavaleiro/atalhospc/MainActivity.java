package com.cavaleiro.atalhospc;

import android.app.Activity;
import android.content.Context;
import android.content.Intent;
import android.content.pm.ShortcutInfo;
import android.content.pm.ShortcutManager;
import android.content.res.Configuration;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.Icon;
import android.os.Build;
import android.os.Bundle;
import android.os.SystemClock;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.view.Gravity;
import android.view.View;
import android.view.inputmethod.InputMethodManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONObject;

import java.util.ArrayList;
import java.util.List;

/** Tela principal: o aparelho. Duas areas no mesmo chassi: Atalhos (teclas) e Sons (pads e mesa de som). */
public class MainActivity extends Activity implements Hub.Listener, Hub.MeterListener, DeckView.Host {
    private static final int SEARCH_FROM = 13;
    private static final int FADER_SEND_MS = 70;

    private Hub hub;
    private Theme theme;
    private int builtRevision = -1;
    private boolean soundsArea;
    private View led;
    private TextView statusText;
    private EditText search;
    private DeckView deck;
    private Dots dots;
    private TextView hint;
    private LinearLayout setupCard;
    private Button addButton;
    private Button searchButton;
    private Button muteButton;
    private MixerViews.Fader masterFader;
    private MixerViews.Fader soundFader;
    private MixerViews.Meter meter;
    private TextView soundCaption;
    private boolean searching;
    private boolean started;
    private long lastFaderSend;

    @Override
    protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        hub = Hub.get(this);
        buildUi();
    }

    private void buildUi() {
        Ui.prepare(this);
        theme = Ui.T;
        builtRevision = hub.settingsRevision();
        soundsArea = hub.soundsArea();
        searching = false;

        LinearLayout desk = new LinearLayout(this);
        desk.setOrientation(LinearLayout.VERTICAL);
        desk.setBackground(Ui.deskBackground());
        int margin = Ui.dp(this, 5);
        desk.setPadding(margin, margin, margin, Ui.dp(this, 2));

        LinearLayout body = new LinearLayout(this);
        body.setOrientation(LinearLayout.VERTICAL);
        body.setBackground(new Ui.ChassisDrawable(this, theme));
        int pad = Ui.dp(this, 26);
        body.setPadding(pad, Ui.dp(this, 20), pad, Ui.dp(this, 18));

        body.addView(band());

        search = new EditText(this);
        search.setHint(soundsArea ? "Buscar som" : "Buscar atalho");
        search.setSingleLine(true);
        search.setTextColor(Ui.TEXT);
        search.setHintTextColor(Ui.MUTED);
        search.setBackground(Ui.inset(this));
        search.setPadding(pad, Ui.dp(this, 8), pad, Ui.dp(this, 8));
        search.setVisibility(View.GONE);
        search.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int a, int b, int c) {}
            @Override public void onTextChanged(CharSequence s, int a, int b, int c) {}
            @Override public void afterTextChanged(Editable s) { applyFilter(); }
        });
        LinearLayout.LayoutParams searchLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        searchLp.topMargin = Ui.dp(this, 10);
        body.addView(search, searchLp);

        deck = new DeckView(this, theme, soundsArea, this);
        deck.configure(hub.size(soundsArea), hub.names(soundsArea));
        LinearLayout.LayoutParams stageLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1);
        stageLp.topMargin = Ui.dp(this, 12);
        body.addView(soundsArea ? mixerStage() : plainStage(), stageLp);

        hint = Ui.text(this, soundsArea
                ? "Toque em um pad vazio para escolher um áudio, ou arraste arquivos para a aba Sons no PC."
                : "Toque em uma tecla vazia para escolher um arquivo, ou arraste arquivos para a janela do programa no PC.", 13, Ui.MUTED);
        hint.setGravity(Gravity.CENTER);
        hint.setPadding(0, Ui.dp(this, 8), 0, 0);
        body.addView(hint);

        dots = new Dots(this, theme);
        LinearLayout.LayoutParams dotsLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, Ui.dp(this, 14));
        dotsLp.topMargin = Ui.dp(this, 6);
        body.addView(dots, dotsLp);

        desk.addView(body, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));

        View hinge = new View(this);
        hinge.setBackground(Ui.hinge(this));
        LinearLayout.LayoutParams hingeLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, Ui.dp(this, 9));
        int side = Ui.dp(this, 54);
        hingeLp.setMargins(side, 0, side, 0);
        desk.addView(hinge, hingeLp);

        setContentView(desk);
        if (started) {
            hub.setMeterWanted(soundsArea);
            onHubChanged();
        }
    }

    private View plainStage() {
        FrameLayout stage = new FrameLayout(this);
        stage.addView(deck, new FrameLayout.LayoutParams(FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT));
        stage.addView(setupCard(), new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT, Gravity.CENTER));
        return stage;
    }

    // ---------- faixa superior ----------

    private View band() {
        boolean narrow = Ui.narrow(this);

        LinearLayout chip = new LinearLayout(this);
        chip.setOrientation(LinearLayout.HORIZONTAL);
        chip.setGravity(Gravity.CENTER_VERTICAL);
        chip.setBackground(Ui.inset(this));
        chip.setPadding(Ui.dp(this, 12), 0, Ui.dp(this, 14), 0);
        led = new View(this);
        chip.addView(led, new LinearLayout.LayoutParams(Ui.dp(this, 10), Ui.dp(this, 10)));
        statusText = Ui.text(this, "", 13, Ui.TEXT);
        statusText.setSingleLine(true);
        statusText.setEllipsize(TextUtils.TruncateAt.END);
        statusText.setMaxWidth(Ui.dp(this, narrow ? 96 : 170));
        statusText.setPadding(Ui.dp(this, 9), 0, 0, 0);
        chip.addView(statusText);
        chip.setClickable(true);
        chip.setOnClickListener(v -> startActivity(new Intent(this, ConnectActivity.class)));

        LinearLayout right = new LinearLayout(this);
        right.setOrientation(LinearLayout.HORIZONTAL);
        searchButton = Ui.hardButton(this, narrow ? "Busca" : "Buscar");
        searchButton.setOnClickListener(v -> toggleSearch());
        right.addView(searchButton, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, Ui.dp(this, 40)));
        addButton = Ui.hardButton(this, narrow ? "+" : "+  Adicionar");
        addButton.setOnClickListener(v -> onEmptyKey());
        right.addView(addButton, marginLeft(8));
        Button settings = Ui.hardButton(this, narrow ? "⚙" : "⚙  Ajustes");
        settings.setContentDescription("Ajustes");
        settings.setOnClickListener(v -> startActivity(new Intent(this, SettingsActivity.class)));
        right.addView(settings, marginLeft(8));

        if (narrow) {
            LinearLayout col = new LinearLayout(this);
            col.setOrientation(LinearLayout.VERTICAL);
            LinearLayout top = new LinearLayout(this);
            top.setGravity(Gravity.CENTER_VERTICAL);
            top.addView(chip, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, Ui.dp(this, 40)));
            top.addView(new View(this), new LinearLayout.LayoutParams(0, 1, 1));
            top.addView(right);
            col.addView(top);
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, Ui.dp(this, 40));
            lp.topMargin = Ui.dp(this, 8);
            col.addView(areaSwitch(), lp);
            return col;
        }

        FrameLayout band = new FrameLayout(this);
        LinearLayout left = new LinearLayout(this);
        left.setGravity(Gravity.CENTER_VERTICAL);
        ImageView logo = new ImageView(this);
        logo.setImageDrawable(getApplicationInfo().loadIcon(getPackageManager()));
        logo.setContentDescription("Atalhos PC");
        left.addView(logo, new LinearLayout.LayoutParams(Ui.dp(this, 34), Ui.dp(this, 34)));
        LinearLayout.LayoutParams chipLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, Ui.dp(this, 40));
        chipLp.leftMargin = Ui.dp(this, 10);
        left.addView(chip, chipLp);
        band.addView(left, new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT, Gravity.START | Gravity.CENTER_VERTICAL));
        band.addView(areaSwitch(), new FrameLayout.LayoutParams(Ui.dp(this, 230), Ui.dp(this, 40), Gravity.CENTER));
        band.addView(right, new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WRAP_CONTENT, Ui.dp(this, 40), Gravity.END | Gravity.CENTER_VERTICAL));
        return band;
    }

    private LinearLayout.LayoutParams marginLeft(int dp) {
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, Ui.dp(this, 40));
        lp.leftMargin = Ui.dp(this, dp);
        return lp;
    }

    private View areaSwitch() {
        return Ui.segments(this, new String[] {"Atalhos", "Sons"}, soundsArea ? 1 : 0, which -> {
            if ((which == 1) == soundsArea) return;
            hub.setSoundsArea(which == 1);
            buildUi();
        });
    }

    // ---------- mesa de som ----------

    private View mixerStage() {
        boolean wide = getResources().getConfiguration().screenWidthDp >= getResources().getConfiguration().screenHeightDp;
        LinearLayout stage = new LinearLayout(this);
        stage.setOrientation(wide ? LinearLayout.HORIZONTAL : LinearLayout.VERTICAL);

        masterFader = new MixerViews.Fader(this, theme, wide);
        masterFader.setCaption("Volume geral");
        masterFader.setListener(this::onMasterFader);
        soundFader = new MixerViews.Fader(this, theme, wide);
        soundFader.setCaption("Volume do som");
        soundFader.setListener(this::onSoundFader);
        meter = new MixerViews.Meter(this, theme, wide);
        soundCaption = Ui.text(this, "", 11, Ui.MUTED);
        soundCaption.setGravity(Gravity.CENTER);
        soundCaption.setSingleLine(true);
        soundCaption.setEllipsize(TextUtils.TruncateAt.END);
        TextView masterCaption = Ui.text(this, "GERAL", 11, Ui.MUTED);
        masterCaption.setGravity(Gravity.CENTER);
        muteButton = Ui.alertButton(this, "Mudo", false);
        muteButton.setOnClickListener(v -> hub.setMaster(hub.master(), !hub.muted(), true));
        Button stop = Ui.alertButton(this, "Parar", false);
        stop.setOnClickListener(v -> hub.request(Hub.query("sound_stop"), this::toastError));

        View mixer;
        if (wide) {
            LinearLayout col = new LinearLayout(this);
            col.setOrientation(LinearLayout.VERTICAL);
            LinearLayout faders = new LinearLayout(this);
            faders.addView(masterFader, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
            LinearLayout.LayoutParams meterLp = new LinearLayout.LayoutParams(Ui.dp(this, 12), LinearLayout.LayoutParams.MATCH_PARENT);
            meterLp.setMargins(Ui.dp(this, 4), 0, Ui.dp(this, 4), 0);
            faders.addView(meter, meterLp);
            faders.addView(soundFader, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
            col.addView(faders, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
            LinearLayout captions = new LinearLayout(this);
            captions.setPadding(0, Ui.dp(this, 6), 0, Ui.dp(this, 8));
            captions.addView(masterCaption, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
            captions.addView(soundCaption, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
            col.addView(captions);
            LinearLayout buttons = new LinearLayout(this);
            LinearLayout.LayoutParams a = new LinearLayout.LayoutParams(0, Ui.dp(this, 48), 1);
            a.rightMargin = Ui.dp(this, 5);
            LinearLayout.LayoutParams b = new LinearLayout.LayoutParams(0, Ui.dp(this, 48), 1);
            b.leftMargin = Ui.dp(this, 5);
            buttons.addView(muteButton, a);
            buttons.addView(stop, b);
            col.addView(buttons);
            mixer = col;
            stage.addView(mixer, new LinearLayout.LayoutParams(Ui.dp(this, 176), LinearLayout.LayoutParams.MATCH_PARENT));
            LinearLayout.LayoutParams div = new LinearLayout.LayoutParams(Ui.dp(this, 1), LinearLayout.LayoutParams.MATCH_PARENT);
            div.setMargins(Ui.dp(this, 14), 0, Ui.dp(this, 14), 0);
            stage.addView(new MixerViews.Divider(this, theme, true), div);
        } else {
            LinearLayout col = new LinearLayout(this);
            col.setOrientation(LinearLayout.VERTICAL);
            col.addView(faderRow(masterCaption, masterFader));
            LinearLayout.LayoutParams meterLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, Ui.dp(this, 7));
            meterLp.setMargins(0, Ui.dp(this, 4), 0, Ui.dp(this, 4));
            col.addView(meter, meterLp);
            col.addView(faderRow(soundCaption, soundFader));
            LinearLayout buttons = new LinearLayout(this);
            buttons.setPadding(0, Ui.dp(this, 6), 0, 0);
            LinearLayout.LayoutParams a = new LinearLayout.LayoutParams(0, Ui.dp(this, 40), 1);
            a.rightMargin = Ui.dp(this, 5);
            LinearLayout.LayoutParams b = new LinearLayout.LayoutParams(0, Ui.dp(this, 40), 1);
            b.leftMargin = Ui.dp(this, 5);
            buttons.addView(muteButton, a);
            buttons.addView(stop, b);
            col.addView(buttons);
            stage.addView(col, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT));
            LinearLayout.LayoutParams div = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, Ui.dp(this, 1));
            div.setMargins(0, Ui.dp(this, 10), 0, Ui.dp(this, 10));
            stage.addView(new MixerViews.Divider(this, theme, false), div);
        }

        FrameLayout padArea = new FrameLayout(this);
        padArea.addView(deck, new FrameLayout.LayoutParams(FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT));
        padArea.addView(setupCard(), new FrameLayout.LayoutParams(FrameLayout.LayoutParams.WRAP_CONTENT, FrameLayout.LayoutParams.WRAP_CONTENT, Gravity.CENTER));
        stage.addView(padArea, wide
                ? new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1)
                : new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
        return stage;
    }

    private View faderRow(TextView caption, MixerViews.Fader fader) {
        LinearLayout row = new LinearLayout(this);
        row.setGravity(Gravity.CENTER_VERTICAL);
        row.addView(caption, new LinearLayout.LayoutParams(Ui.dp(this, 62), LinearLayout.LayoutParams.WRAP_CONTENT));
        row.addView(fader, new LinearLayout.LayoutParams(0, Ui.dp(this, 40), 1));
        return row;
    }

    private void onMasterFader(int value, boolean finished) {
        long now = SystemClock.uptimeMillis();
        if (!finished && now - lastFaderSend < FADER_SEND_MS) return;
        lastFaderSend = now;
        hub.setMaster(value, hub.muted(), finished);
    }

    private void onSoundFader(int value, boolean finished) {
        Hub.Item it = hub.selectedSound();
        if (it == null) return;
        it.volume = value;
        applyFilter();
        long now = SystemClock.uptimeMillis();
        if (!finished && now - lastFaderSend < FADER_SEND_MS) return;
        lastFaderSend = now;
        hub.setSoundVolume(it, value, finished);
    }

    private void refreshMixer() {
        if (masterFader == null || !soundsArea) return;
        masterFader.setValue(hub.master());
        Hub.Item it = hub.selectedSound();
        soundFader.setEnabled(it != null);
        if (it != null) soundFader.setValue(it.volume);
        soundCaption.setText(it == null ? "SOM" : it.name);
        soundFader.setCaption(it == null ? "Volume do som" : "Volume de " + it.name);
        Ui.styleAlert(this, muteButton, hub.muted());
        muteButton.setText(hub.muted() ? "Sem som" : "Mudo");
    }

    @Override
    public void onMeter(float peak) {
        if (meter != null && soundsArea) meter.setPeak(Math.max(0f, peak));
    }

    // ---------- ciclo de vida ----------

    private View setupCard() {
        setupCard = new LinearLayout(this);
        setupCard.setOrientation(LinearLayout.VERTICAL);
        setupCard.setGravity(Gravity.CENTER);
        setupCard.setBackground(Ui.round(this, Theme.alpha(theme.card, 0xF0), 16));
        int pad = Ui.dp(this, 24);
        setupCard.setPadding(pad, pad, pad, pad);
        TextView message = Ui.text(this, "Abra o AtalhosPC.exe no computador\ne conecte este aparelho a ele.", 16, Ui.TEXT);
        message.setGravity(Gravity.CENTER);
        setupCard.addView(message);
        Button connect = Ui.button(this, "Conectar ao PC", Ui.ACCENT);
        connect.setOnClickListener(v -> startActivity(new Intent(this, ConnectActivity.class)));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.topMargin = Ui.dp(this, 16);
        setupCard.addView(connect, lp);
        return setupCard;
    }

    @Override
    public void onConfigurationChanged(Configuration config) {
        super.onConfigurationChanged(config);
        buildUi();
    }

    /** Sem barras do sistema, a tela inteira vira o aparelho. Deslizar da borda traz as barras de volta. */
    @Override
    public void onWindowFocusChanged(boolean hasFocus) {
        super.onWindowFocusChanged(hasFocus);
        if (!hasFocus) return;
        getWindow().getDecorView().setSystemUiVisibility(View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                | View.SYSTEM_UI_FLAG_FULLSCREEN | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_STABLE | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION);
    }

    @Override
    protected void onStart() {
        super.onStart();
        started = true;
        if (builtRevision != hub.settingsRevision()) buildUi();
        hub.addListener(this);
        hub.setMeterListener(this);
        hub.activityStarted();
        hub.setMeterWanted(soundsArea);
        onHubChanged();
    }

    @Override
    protected void onStop() {
        super.onStop();
        started = false;
        hub.setMeterWanted(false);
        hub.setMeterListener(null);
        hub.removeListener(this);
        hub.activityStopped();
    }

    @Override
    public void onHubChanged() {
        int color;
        String label;
        Hub.Paired p = hub.current();
        String pc = p == null ? "" : p.pc;
        switch (hub.state()) {
            case Hub.ONLINE:
                color = Ui.OK;
                label = Ui.narrow(this) ? hub.detail() : hub.detail() + " · " + pc;
                break;
            case Hub.CONNECTING:
                color = Ui.WARN;
                label = "Conectando…";
                break;
            case Hub.UNPAIRED:
                color = Ui.BAD;
                label = "Parear de novo";
                break;
            case Hub.OFFLINE:
                color = Ui.BAD;
                label = "Sem conexão";
                break;
            default:
                color = Ui.MUTED;
                label = "Não pareado";
                break;
        }
        GradientDrawable dot = new GradientDrawable();
        dot.setShape(GradientDrawable.OVAL);
        dot.setColor(color);
        dot.setStroke(Ui.dp(this, 2), (color & 0x00FFFFFF) | 0x40000000);
        led.setBackground(dot);
        statusText.setText(label);

        addButton.setVisibility(p != null && (soundsArea || hub.browseAllowed()) ? View.VISIBLE : View.GONE);
        int count = current().size();
        searchButton.setVisibility(count >= SEARCH_FROM || searching ? View.VISIBLE : View.GONE);
        setupCard.setVisibility(p == null ? View.VISIBLE : View.GONE);
        hint.setVisibility(p != null && count == 0 ? View.VISIBLE : View.GONE);
        refreshMixer();
        applyFilter();
    }

    private List<Hub.Item> current() {
        return soundsArea ? hub.sounds() : hub.items();
    }

    private void toggleSearch() {
        searching = !searching;
        search.setVisibility(searching ? View.VISIBLE : View.GONE);
        InputMethodManager keyboard = (InputMethodManager) getSystemService(Context.INPUT_METHOD_SERVICE);
        if (searching) {
            search.requestFocus();
            keyboard.showSoftInput(search, InputMethodManager.SHOW_IMPLICIT);
        } else {
            search.setText("");
            keyboard.hideSoftInputFromWindow(search.getWindowToken(), 0);
        }
        applyFilter();
    }

    @Override
    public void onBackPressed() {
        if (searching) toggleSearch();
        else super.onBackPressed();
    }

    private void applyFilter() {
        String term = searching ? search.getText().toString().trim().toLowerCase() : "";
        List<Hub.Item> shown = new ArrayList<>();
        for (Hub.Item it : current()) {
            if (term.isEmpty() || it.name.toLowerCase().contains(term)) shown.add(it);
        }
        deck.setItems(shown);
    }

    // ---------- DeckView.Host ----------

    @Override
    public Bitmap iconFor(Hub.Item item) {
        return hub.icon(item);
    }

    @Override
    public int colorFor(Hub.Item item) {
        List<Hub.Item> all = hub.sounds();
        int at = all.indexOf(item);
        return theme.pads[Math.max(0, at) % theme.pads.length];
    }

    @Override
    public boolean isPlaying(Hub.Item item) {
        return item.sound && hub.isPlaying(item.id);
    }

    @Override
    public boolean isSelected(Hub.Item item) {
        Hub.Item selected = soundsArea ? hub.selectedSound() : null;
        return selected != null && selected.id.equals(item.id);
    }

    @Override
    public void onPageChanged(int page, int pages) {
        dots.show(page, pages);
    }

    @Override
    public void onEmptyKey() {
        if (hub.current() == null) {
            startActivity(new Intent(this, ConnectActivity.class));
        } else if (soundsArea) {
            chooseSoundSource();
        } else if (hub.browseAllowed()) {
            startActivity(new Intent(this, BrowseActivity.class).putExtra(BrowseActivity.EXTRA_MODE, BrowseActivity.MODE_SHORTCUT));
        } else {
            Toast.makeText(this, "A navegação pelos arquivos está desligada no PC.", Toast.LENGTH_LONG).show();
        }
    }

    // A biblioteca nao depende da navegacao pelos arquivos: com ela desligada, vai direto.
    private void chooseSoundSource() {
        if (!hub.browseAllowed()) {
            startActivity(new Intent(this, LibraryActivity.class));
            return;
        }
        Ui.dialog(this)
                .setTitle("Adicionar som")
                .setItems(new String[] {"Biblioteca de sons", "Arquivo do PC"}, (d, which) -> {
                    if (which == 0) startActivity(new Intent(this, LibraryActivity.class));
                    else startActivity(new Intent(this, BrowseActivity.class).putExtra(BrowseActivity.EXTRA_MODE, BrowseActivity.MODE_SOUND));
                })
                .show();
    }

    @Override
    public void onKey(Hub.Item it, KeyView key) {
        if (it.sound) hub.setSelectedSound(it.id);
        hub.request(Hub.query(it.sound ? "sound_play" : "run", "id", it.id), reply -> {
            if (isDestroyed()) return;
            if (reply.optBoolean("ok")) {
                if (!it.sound) key.flash(Ui.OK);
            } else {
                key.flash(Ui.BAD);
                Toast.makeText(this, reply.optString("err", "Não foi possível abrir."), Toast.LENGTH_LONG).show();
            }
        });
        if (it.sound) onHubChanged();
    }

    @Override
    public void onKeyMenu(Hub.Item it) {
        List<String> options = new ArrayList<>();
        if (it.sound) {
            options.add("Ajustar volume");
            options.add("Trocar ícone");
        }
        options.add("Renomear");
        options.add("Mover para a esquerda");
        options.add("Mover para a direita");
        options.add("Mover para o início");
        boolean canPin = Build.VERSION.SDK_INT >= 26 && Pin.supported(this);
        if (canPin) options.add("Criar atalho na tela inicial");
        options.add("Remover da lista");
        Ui.dialog(this)
                .setTitle(it.name)
                .setItems(options.toArray(new String[0]), (d, which) -> {
                    String choice = options.get(which);
                    if (choice.startsWith("Ajustar")) {
                        // seleciona sem tocar: o fader SOM passa a controlar este pad
                        hub.setSelectedSound(it.id);
                        onHubChanged();
                    } else if (choice.startsWith("Trocar")) IconPicker.show(this, hub, it.id, it.name, null);
                    else if (choice.startsWith("Renomear")) rename(it);
                    else if (choice.endsWith("esquerda")) move(it, -1);
                    else if (choice.endsWith("direita")) move(it, 1);
                    else if (choice.startsWith("Mover")) hub.request(Hub.query("move", "id", it.id, "to", 0), this::toastError);
                    else if (choice.startsWith("Criar")) pin(it);
                    else confirmRemove(it);
                })
                .show();
    }

    private void move(Hub.Item it, int delta) {
        List<Hub.Item> all = current();
        for (int i = 0; i < all.size(); i++) {
            if (all.get(i).id.equals(it.id)) {
                hub.request(Hub.query("move", "id", it.id, "to", Math.max(0, i + delta)), this::toastError);
                return;
            }
        }
    }

    private void toastError(JSONObject reply) {
        if (!reply.optBoolean("ok")) Toast.makeText(this, reply.optString("err", "Não deu certo."), Toast.LENGTH_LONG).show();
    }

    private void rename(Hub.Item it) {
        EditText input = new EditText(this);
        input.setText(it.name);
        input.setSingleLine(true);
        input.setSelectAllOnFocus(true);
        Ui.showWithInput(this, Ui.dialog(this)
                .setTitle("Novo nome")
                .setPositiveButton("Salvar", (d, w) -> hub.request(Hub.query("rename", "id", it.id, "name", input.getText().toString()), this::toastError))
                .setNegativeButton("Cancelar", null), input);
    }

    private void confirmRemove(Hub.Item it) {
        Ui.dialog(this)
                .setTitle("Remover da lista?")
                .setMessage(it.name + "\n\nO arquivo continua no PC. " + (it.sound ? "Só o pad sai daqui." : "Só o atalho sai daqui."))
                .setPositiveButton("Remover", (d, w) -> hub.request(Hub.query("remove", "id", it.id), this::toastError))
                .setNegativeButton("Cancelar", null)
                .show();
    }

    private void pin(Hub.Item it) {
        Hub.Paired p = hub.current();
        Bitmap icon = hub.fullIcon(it);
        if (p == null) return;
        if (icon == null) icon = Ui.letterIcon(this, it.name);
        Pin.request(this, p.pid, it, icon);
    }

    /** Isolado: estas classes so existem a partir do Android 8. */
    private static final class Pin {
        static boolean supported(Context c) {
            ShortcutManager sm = c.getSystemService(ShortcutManager.class);
            return sm != null && sm.isRequestPinShortcutSupported();
        }

        static void request(Context c, String pid, Hub.Item it, Bitmap icon) {
            Intent run = new Intent(c, RunActivity.class)
                    .setAction(Intent.ACTION_VIEW)
                    .putExtra(RunActivity.EXTRA_PC, pid)
                    .putExtra(RunActivity.EXTRA_ID, it.id)
                    .putExtra(RunActivity.EXTRA_NAME, it.name)
                    .putExtra(RunActivity.EXTRA_SOUND, it.sound);
            ShortcutInfo info = new ShortcutInfo.Builder(c, "a_" + it.id)
                    .setShortLabel(it.name.length() > 24 ? it.name.substring(0, 24) : it.name)
                    .setLongLabel(it.name)
                    .setIcon(Icon.createWithBitmap(icon))
                    .setIntent(run)
                    .build();
            c.getSystemService(ShortcutManager.class).requestPinShortcut(info, null);
        }
    }

    /** Pontos de pagina, como pequenos LEDs no chassi. */
    private static final class Dots extends View {
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Theme theme;
        private int page, pages = 1;

        Dots(Context c, Theme theme) {
            super(c);
            this.theme = theme;
        }

        void show(int page, int pages) {
            this.page = page;
            this.pages = pages;
            invalidate();
        }

        @Override
        protected void onDraw(Canvas c) {
            if (pages <= 1) return;
            float r = Ui.dp(getContext(), 3.5f), step = Ui.dp(getContext(), 16);
            float x = (getWidth() - step * (pages - 1)) / 2f, y = getHeight() / 2f;
            for (int i = 0; i < pages; i++) {
                paint.setColor(i == page ? theme.accent : theme.socket);
                c.drawCircle(x + i * step, y, r, paint);
            }
        }
    }
}
