package com.cavaleiro.atalhospc;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Context;
import android.content.res.ColorStateList;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.ColorFilter;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.PixelFormat;
import android.graphics.RadialGradient;
import android.graphics.Rect;
import android.graphics.RectF;
import android.graphics.Shader;
import android.graphics.SweepGradient;
import android.graphics.drawable.ColorDrawable;
import android.graphics.drawable.Drawable;
import android.graphics.drawable.GradientDrawable;
import android.graphics.drawable.RippleDrawable;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.FrameLayout;
import android.widget.LinearLayout;
import android.widget.TextView;

/** A interface e toda montada em codigo: o APK fica pequeno e o build nao depende de bibliotecas. */
final class Ui {
    // Cores do tema atual: mudam em apply(); as telas leem ao serem montadas.
    static Theme T = Theme.ALL[0];
    static int BG = T.bg, CARD = T.card, CARD_HIGH = T.cardHigh, TEXT = T.text, MUTED = T.muted, ACCENT = T.accent;
    static final int OK = 0xFF3DD68C;
    static final int WARN = 0xFFFFB454;
    static final int BAD = 0xFFFF6B6B;

    private Ui() {}

    static int dp(Context c, float v) {
        return (int) TypedValue.applyDimension(TypedValue.COMPLEX_UNIT_DIP, v, c.getResources().getDisplayMetrics());
    }

    static void apply(Theme theme) {
        T = theme;
        BG = theme.bg;
        CARD = theme.card;
        CARD_HIGH = theme.cardHigh;
        TEXT = theme.text;
        MUTED = theme.muted;
        ACCENT = theme.accent;
    }

    static void prepare(Activity a) {
        apply(Theme.byId(Hub.get(a).themeId()));
        a.getWindow().setStatusBarColor(BG);
        a.getWindow().setNavigationBarColor(BG);
        a.getWindow().setBackgroundDrawable(new ColorDrawable(BG));
    }

    // ---------- aparencia do aparelho fisico ----------

    /** A mesa onde o aparelho esta: escuro, com um pouco de luz vindo de cima. */
    static Drawable deskBackground() {
        return new GradientDrawable(GradientDrawable.Orientation.TOP_BOTTOM, new int[] {T.deskTop, T.deskBottom});
    }

    /** Corpo do aparelho: luz da borda com halo suave, metal em degrade, brilho de canto e fio de luz. */
    static final class ChassisDrawable extends Drawable {
        private final Theme theme;
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final RectF rect = new RectF();
        private final float radius, inset, unit;
        private Shader rimShader, bodyShader, sheenShader, topShader;

        ChassisDrawable(Context c, Theme theme) {
            this.theme = theme;
            radius = dp(c, 18);
            inset = dp(c, 9);
            unit = dp(c, 1);
        }

        @Override
        protected void onBoundsChange(Rect b) {
            float top = b.top + inset, bottom = b.bottom - inset;
            rimShader = new SweepGradient(b.exactCenterX(), b.exactCenterY(), theme.rim, null);
            bodyShader = new LinearGradient(0, top, 0, bottom, theme.chassisTop, theme.chassisBottom, Shader.TileMode.CLAMP);
            float reach = Math.max(b.width(), b.height()) * 0.6f;
            sheenShader = new RadialGradient(b.left + b.width() * 0.22f, top, Math.max(1f, reach), Theme.alpha(0xFFFFFFFF, 26), Theme.alpha(0xFFFFFFFF, 0), Shader.TileMode.CLAMP);
            topShader = new LinearGradient(0, top, 0, top + (bottom - top) * 0.5f, theme.chassisEdge, Theme.alpha(theme.chassisEdge, 0), Shader.TileMode.CLAMP);
        }

        @Override
        public void draw(Canvas c) {
            Rect b = getBounds();
            rect.set(b.left + inset, b.top + inset, b.right - inset, b.bottom - inset);

            // halo: traços cada vez mais largos e mais transparentes, para a luz sumir sem degrau
            paint.setShader(rimShader);
            paint.setStyle(Paint.Style.STROKE);
            int[] alphas = {16, 22, 30, 42, 60};
            for (int i = 0; i < alphas.length; i++) {
                paint.setAlpha((int) (alphas[i] * theme.glow));
                paint.setStrokeWidth(unit * (15 - i * 3));
                c.drawRoundRect(rect, radius, radius, paint);
            }
            paint.setAlpha(255);

            // corpo
            paint.setStyle(Paint.Style.FILL);
            paint.setShader(bodyShader);
            c.drawRoundRect(rect, radius, radius, paint);
            paint.setShader(sheenShader);
            c.drawRoundRect(rect, radius, radius, paint);

            // tira de luz em volta e fio claro em cima, que some antes da metade
            paint.setStyle(Paint.Style.STROKE);
            paint.setShader(rimShader);
            paint.setAlpha((int) (235 * Math.min(1f, 0.35f + theme.glow)));
            paint.setStrokeWidth(unit * 2f);
            c.drawRoundRect(rect, radius, radius, paint);
            paint.setAlpha(255);
            rect.inset(unit * 2f, unit * 2f);
            paint.setShader(topShader);
            paint.setStrokeWidth(unit);
            c.drawRoundRect(rect, radius - unit * 2f, radius - unit * 2f, paint);
            paint.setShader(null);
            paint.setStyle(Paint.Style.FILL);
        }

        @Override public void setAlpha(int alpha) {}
        @Override public void setColorFilter(ColorFilter filter) {}
        @Override public int getOpacity() { return PixelFormat.TRANSLUCENT; }
    }

    /** A dobradica do suporte, logo abaixo do corpo. */
    static Drawable hinge(Context c) {
        GradientDrawable d = new GradientDrawable(GradientDrawable.Orientation.TOP_BOTTOM, new int[] {Theme.mix(T.chassisBottom, 0xFF000000, 0.6f), Theme.mix(T.chassisTop, 0xFF000000, 0.1f), Theme.mix(T.chassisBottom, 0xFF000000, 0.35f)});
        float r = dp(c, 3);
        d.setCornerRadii(new float[] {0, 0, 0, 0, r, r, r, r});
        return d;
    }

    /** Area rebaixada no chassi: visor de estado e campo de busca. */
    static Drawable inset(Context c) {
        GradientDrawable d = new GradientDrawable(GradientDrawable.Orientation.TOP_BOTTOM, new int[] {T.insetTop, T.insetBottom});
        d.setCornerRadius(dp(c, 10));
        d.setStroke(dp(c, 1), T.insetEdge);
        return d;
    }

    /** Botao pequeno do proprio aparelho. */
    static Button hardButton(Context c, String s) {
        Button b = new Button(c);
        b.setText(s);
        b.setAllCaps(false);
        b.setTextColor(TEXT);
        b.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
        GradientDrawable base = new GradientDrawable(GradientDrawable.Orientation.TOP_BOTTOM, new int[] {T.buttonTop, T.buttonBottom});
        base.setCornerRadius(dp(c, 10));
        base.setStroke(dp(c, 1), T.buttonEdge);
        b.setBackground(new RippleDrawable(ColorStateList.valueOf(0x40FFFFFF), base, null));
        b.setPadding(dp(c, 14), 0, dp(c, 14), 0);
        b.setMinWidth(dp(c, 48));
        b.setMinimumWidth(dp(c, 48));
        b.setMinHeight(0);
        b.setMinimumHeight(0);
        b.setStateListAnimator(null);
        return b;
    }

    static GradientDrawable round(Context c, int color, float radiusDp) {
        GradientDrawable d = new GradientDrawable();
        d.setColor(color);
        d.setCornerRadius(dp(c, radiusDp));
        return d;
    }

    static Drawable tileBackground(Context c, int color, float radiusDp) {
        GradientDrawable base = round(c, color, radiusDp);
        return new RippleDrawable(ColorStateList.valueOf(0x40FFFFFF), base, round(c, Color.WHITE, radiusDp));
    }

    static TextView text(Context c, String s, float sp, int color) {
        TextView t = new TextView(c);
        t.setText(s);
        t.setTextSize(TypedValue.COMPLEX_UNIT_SP, sp);
        t.setTextColor(color);
        return t;
    }

    static TextView heading(Context c, String s) {
        TextView t = text(c, s, 15, TEXT);
        t.setPadding(0, dp(c, 18), 0, dp(c, 6));
        t.getPaint().setFakeBoldText(true);
        return t;
    }

    /** Tela estreita de celular: os botoes usam textos curtos para caber. */
    static boolean narrow(Context c) {
        return c.getResources().getConfiguration().screenWidthDp < 520;
    }

    interface OnSegment {
        void onChosen(int which);
    }

    /** Seletor de opcoes em peca unica, com a opcao atual acesa na cor do tema. */
    static View segments(Context c, String[] labels, int current, OnSegment listener) {
        LinearLayout box = new LinearLayout(c);
        box.setBackground(inset(c));
        int in = dp(c, 3);
        box.setPadding(in, in, in, in);
        for (int i = 0; i < labels.length; i++) {
            final int which = i;
            TextView t = text(c, labels[i], 13, i == current ? (Theme.luminance(ACCENT) > 0.6f ? 0xFF14161C : Color.WHITE) : MUTED);
            t.setGravity(Gravity.CENTER);
            t.setClickable(true);
            t.setContentDescription(labels[i] + (i == current ? ", selecionado" : ""));
            if (i == current) {
                GradientDrawable on = new GradientDrawable(GradientDrawable.Orientation.TOP_BOTTOM, new int[] {Theme.mix(ACCENT, Color.WHITE, 0.14f), Theme.mix(ACCENT, Color.BLACK, 0.14f)});
                on.setCornerRadius(dp(c, 8));
                t.setBackground(on);
                t.getPaint().setFakeBoldText(true);
            }
            t.setOnClickListener(v -> listener.onChosen(which));
            box.addView(t, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.MATCH_PARENT, 1));
        }
        return box;
    }

    /** Botao vermelho da mesa de som: apagado, ou aceso quando a funcao esta ativa. */
    static Button alertButton(Context c, String s, boolean lit) {
        Button b = new Button(c);
        b.setText(s);
        b.setAllCaps(false);
        b.setTextSize(TypedValue.COMPLEX_UNIT_SP, 13);
        b.setPadding(0, 0, 0, 0);
        b.setMinHeight(0);
        b.setMinimumHeight(0);
        b.setMinWidth(0);
        b.setMinimumWidth(0);
        b.setStateListAnimator(null);
        styleAlert(c, b, lit);
        return b;
    }

    static void styleAlert(Context c, Button b, boolean lit) {
        GradientDrawable base = new GradientDrawable(GradientDrawable.Orientation.TOP_BOTTOM,
                lit ? new int[] {0xFFFFA39E, 0xFFFF5F58, 0xFFD93B35} : new int[] {0xFFF07A74, 0xFFD6524C, 0xFFAC3530});
        base.setCornerRadius(dp(c, 10));
        base.setStroke(dp(c, lit ? 2 : 1), lit ? 0xFFFFFFFF : 0x66FFFFFF);
        b.setBackground(new RippleDrawable(ColorStateList.valueOf(0x50FFFFFF), base, null));
        b.setTextColor(Color.WHITE);
    }

    static Button button(Context c, String s, int color) {
        Button b = new Button(c);
        b.setText(s);
        b.setAllCaps(false);
        b.setTextColor(Theme.luminance(color) > 0.6f ? 0xFF14161C : Color.WHITE);
        b.setTextSize(TypedValue.COMPLEX_UNIT_SP, 14);
        b.setBackground(tileBackground(c, color, 10));
        b.setPadding(dp(c, 14), dp(c, 8), dp(c, 14), dp(c, 8));
        b.setMinimumHeight(dp(c, 44));
        b.setMinHeight(dp(c, 44));
        b.setStateListAnimator(null);
        return b;
    }

    /** Linha clicavel usada nas listas de PCs. */
    static LinearLayout row(Context c, String title, String subtitle) {
        LinearLayout l = new LinearLayout(c);
        l.setOrientation(LinearLayout.VERTICAL);
        l.setGravity(Gravity.CENTER_VERTICAL);
        l.setBackground(tileBackground(c, CARD, 10));
        l.setPadding(dp(c, 14), dp(c, 10), dp(c, 14), dp(c, 10));
        l.setMinimumHeight(dp(c, 52));
        l.addView(text(c, title, 15, TEXT));
        if (subtitle != null && !subtitle.isEmpty()) l.addView(text(c, subtitle, 12, MUTED));
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.topMargin = dp(c, 6);
        l.setLayoutParams(lp);
        l.setClickable(true);
        return l;
    }

    static AlertDialog.Builder dialog(Context c) {
        return new AlertDialog.Builder(c, android.R.style.Theme_Material_Dialog_Alert);
    }

    /** Mostra um dialogo com campo de texto: com margem e com o teclado ja aberto. */
    static void showWithInput(Context c, AlertDialog.Builder builder, EditText input) {
        FrameLayout box = new FrameLayout(c);
        int side = dp(c, 24);
        box.setPadding(side, dp(c, 8), side, 0);
        box.addView(input);
        builder.setView(box);
        AlertDialog d = builder.create();
        d.getWindow().setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_STATE_ALWAYS_VISIBLE);
        d.show();
        input.requestFocus();
    }

    /** Enquanto o icone real nao chega: a primeira letra do nome, na cor de destaque. */
    static Bitmap letterIcon(Context c, String name) {
        int size = dp(c, 64);
        Bitmap b = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888);
        Canvas canvas = new Canvas(b);
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setColor(CARD_HIGH);
        canvas.drawCircle(size / 2f, size / 2f, size / 2f, p);
        p.setColor(TEXT);
        p.setTextSize(size * 0.45f);
        p.setTextAlign(Paint.Align.CENTER);
        String letter = name == null || name.isEmpty() ? "?" : name.substring(0, 1).toUpperCase();
        canvas.drawText(letter, size / 2f, size / 2f - (p.descent() + p.ascent()) / 2f, p);
        return b;
    }
}
