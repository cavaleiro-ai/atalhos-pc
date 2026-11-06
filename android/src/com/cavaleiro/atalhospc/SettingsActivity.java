package com.cavaleiro.atalhospc;

import android.app.Activity;
import android.content.Context;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.RadialGradient;
import android.graphics.RectF;
import android.graphics.Shader;
import android.os.Bundle;
import android.view.Gravity;
import android.view.View;
import android.widget.Button;
import android.widget.LinearLayout;
import android.widget.ScrollView;
import android.widget.Switch;
import android.widget.TextView;

/** Ajustes deste aparelho: tema, tamanho dos botoes e nomes. Valem na hora e ficam so neste aparelho. */
public class SettingsActivity extends Activity {
    private static final String[] SIZES = {"Grande", "Médio", "Pequeno", "Mínimo"};
    private Hub hub;

    @Override
    protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        hub = Hub.get(this);
        Ui.prepare(this);

        ScrollView scroll = new ScrollView(this);
        scroll.setBackground(Ui.deskBackground());
        LinearLayout col = new LinearLayout(this);
        col.setOrientation(LinearLayout.VERTICAL);
        int pad = Ui.dp(this, 18);
        col.setPadding(pad, pad, pad, pad * 2);
        scroll.addView(col);

        LinearLayout top = new LinearLayout(this);
        top.setGravity(Gravity.CENTER_VERTICAL);
        Button back = Ui.hardButton(this, "Voltar");
        back.setOnClickListener(v -> finish());
        top.addView(back, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, Ui.dp(this, 40)));
        TextView title = Ui.text(this, "Ajustes", 20, Ui.TEXT);
        title.getPaint().setFakeBoldText(true);
        title.setPadding(pad, 0, 0, 0);
        top.addView(title);
        col.addView(top);

        col.addView(Ui.heading(this, "Tema"));
        col.addView(themeGrid());

        col.addView(Ui.heading(this, "Atalhos"));
        col.addView(areaOptions(false));
        col.addView(Ui.heading(this, "Sons"));
        col.addView(areaOptions(true));

        setContentView(scroll);
    }

    private View themeGrid() {
        int columns = Ui.narrow(this) ? 2 : 5;
        LinearLayout grid = new LinearLayout(this);
        grid.setOrientation(LinearLayout.VERTICAL);
        LinearLayout row = null;
        int gap = Ui.dp(this, 10);
        for (int i = 0; i < Theme.ALL.length; i++) {
            if (i % columns == 0) {
                row = new LinearLayout(this);
                LinearLayout.LayoutParams rowLp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
                rowLp.topMargin = gap;
                grid.addView(row, rowLp);
            }
            final Theme t = Theme.ALL[i];
            LinearLayout card = new LinearLayout(this);
            card.setOrientation(LinearLayout.VERTICAL);
            card.setGravity(Gravity.CENTER_HORIZONTAL);
            card.setClickable(true);
            card.setContentDescription("Tema " + t.name);
            Swatch swatch = new Swatch(this, t, t.id == Ui.T.id);
            card.addView(swatch, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, Ui.dp(this, 100)));
            TextView name = Ui.text(this, t.name, 13, t.id == Ui.T.id ? Ui.ACCENT : Ui.TEXT);
            name.setGravity(Gravity.CENTER);
            name.setPadding(0, Ui.dp(this, 5), 0, 0);
            card.addView(name);
            TextView note = Ui.text(this, t.note, 11, Ui.MUTED);
            note.setGravity(Gravity.CENTER);
            card.addView(note);
            card.setOnClickListener(v -> {
                hub.setTheme(t.id);
                recreate();
            });
            LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1);
            lp.setMargins(i % columns == 0 ? 0 : gap / 2, 0, i % columns == columns - 1 ? 0 : gap / 2, 0);
            row.addView(card, lp);
        }
        return grid;
    }

    private View areaOptions(boolean sounds) {
        LinearLayout box = new LinearLayout(this);
        box.setOrientation(LinearLayout.VERTICAL);
        box.addView(Ui.text(this, "Tamanho dos botões", 13, Ui.MUTED));
        LinearLayout.LayoutParams seg = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, Ui.dp(this, 42));
        seg.topMargin = Ui.dp(this, 6);
        box.addView(Ui.segments(this, SIZES, hub.size(sounds), which -> {
            hub.setSize(sounds, which);
            recreate();
        }), seg);
        Switch names = new Switch(this);
        names.setText("Mostrar o nome no botão (desligado, fica só a logo centralizada)");
        names.setTextColor(Ui.TEXT);
        names.setChecked(hub.names(sounds));
        names.setPadding(0, Ui.dp(this, 12), 0, Ui.dp(this, 4));
        names.setOnCheckedChangeListener((b, on) -> hub.setNames(sounds, on));
        box.addView(names);
        return box;
    }

    /** Miniatura do aparelho em cada tema: chassi, luz da borda e pads. */
    private static final class Swatch extends View {
        private final Theme theme;
        private final boolean selected;
        private final Ui.ChassisDrawable chassis;
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final RectF r = new RectF();

        Swatch(Context c, Theme theme, boolean selected) {
            super(c);
            this.theme = theme;
            this.selected = selected;
            chassis = new Ui.ChassisDrawable(c, theme);
        }

        @Override
        protected void onDraw(Canvas c) {
            int w = getWidth(), h = getHeight();
            paint.setShader(new LinearGradient(0, 0, 0, h, theme.deskTop, theme.deskBottom, Shader.TileMode.CLAMP));
            r.set(0, 0, w, h);
            float round = Ui.dp(getContext(), 10);
            c.drawRoundRect(r, round, round, paint);
            paint.setShader(null);
            chassis.setBounds(0, 0, w, h);
            chassis.draw(c);

            int cols = 4, rows = 2;
            float inX = w * 0.17f, inY = h * 0.24f, gap = Ui.dp(getContext(), 4);
            float size = Math.min((w - inX * 2 - gap * (cols - 1)) / cols, (h - inY * 2 - gap * (rows - 1)) / rows);
            float startX = (w - size * cols - gap * (cols - 1)) / 2f;
            float startY = (h - size * rows - gap * (rows - 1)) / 2f;
            for (int i = 0; i < cols * rows; i++) {
                float x = startX + (i % cols) * (size + gap), y = startY + (i / cols) * (size + gap);
                int color = theme.pads[i % theme.pads.length];
                r.set(x, y, x + size, y + size);
                paint.setShader(new RadialGradient(x + size / 2f, y + size / 2f, size * 0.72f, Theme.mix(color, Color.WHITE, 0.55f), color, Shader.TileMode.CLAMP));
                c.drawRoundRect(r, size * 0.2f, size * 0.2f, paint);
            }
            paint.setShader(null);

            if (selected) {
                paint.setStyle(Paint.Style.STROKE);
                paint.setStrokeWidth(Ui.dp(getContext(), 2.5f));
                paint.setColor(Ui.ACCENT);
                r.set(1, 1, w - 1, h - 1);
                c.drawRoundRect(r, round, round, paint);
                paint.setStyle(Paint.Style.FILL);
            }
        }
    }
}
