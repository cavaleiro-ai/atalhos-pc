package com.cavaleiro.atalhospc;

import android.content.Context;
import android.graphics.Canvas;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.RectF;
import android.graphics.Shader;
import android.os.SystemClock;
import android.view.MotionEvent;
import android.view.View;

/** Controles da mesa de som: fader com botao fisico e medidor de nivel. */
final class MixerViews {
    private MixerViews() {}

    interface OnFader {
        void onValue(int value, boolean finished);
    }

    static final class Fader extends View {
        private final Theme theme;
        private final boolean vertical;
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final RectF rect = new RectF();
        private OnFader listener;
        private int value = 80;
        private boolean tracking;
        private String caption = "";

        Fader(Context c, Theme theme, boolean vertical) {
            super(c);
            this.theme = theme;
            this.vertical = vertical;
            setFocusable(true);
        }

        void setListener(OnFader l) {
            listener = l;
        }

        void setCaption(String text) {
            caption = text;
            updateDescription();
        }

        void setValue(int v) {
            if (tracking || v == value) return;
            value = v;
            updateDescription();
            invalidate();
        }

        private void updateDescription() {
            setContentDescription(caption + ": volume " + value + "%");
        }

        @Override
        public boolean onTouchEvent(MotionEvent e) {
            if (!isEnabled()) return false;
            switch (e.getActionMasked()) {
                case MotionEvent.ACTION_DOWN:
                    tracking = true;
                    getParent().requestDisallowInterceptTouchEvent(true);
                    apply(e, false);
                    return true;
                case MotionEvent.ACTION_MOVE:
                    apply(e, false);
                    return true;
                case MotionEvent.ACTION_UP:
                case MotionEvent.ACTION_CANCEL:
                    apply(e, true);
                    tracking = false;
                    return true;
                default:
                    return false;
            }
        }

        private void apply(MotionEvent e, boolean finished) {
            float knob = knobLength() / 2f;
            float span = (vertical ? getHeight() : getWidth()) - knob * 2f;
            float at = vertical ? getHeight() - knob - e.getY() : e.getX() - knob;
            int v = Math.round(Math.max(0f, Math.min(1f, at / span)) * 100f);
            boolean changed = v != value;
            value = v;
            updateDescription();
            if (changed) invalidate();
            if (listener != null && (changed || finished)) listener.onValue(v, finished);
        }

        private float knobLength() {
            return Ui.dp(getContext(), 46);
        }

        @Override
        protected void onDraw(Canvas c) {
            float w = getWidth(), h = getHeight();
            float length = vertical ? h : w;
            float cross = vertical ? w : h;
            float knob = knobLength();
            float span = length - knob;
            float along = knob / 2f + span * (value / 100f);
            float slot = Ui.dp(getContext(), 7);
            float mid = cross / 2f;

            // marcas de escala dos dois lados da fenda
            paint.setShader(null);
            paint.setColor(Theme.alpha(theme.muted, 90));
            float markW = Ui.dp(getContext(), 1);
            for (int i = 0; i <= 10; i++) {
                float p = knob / 2f + span * i / 10f;
                float reach = cross * (i % 5 == 0 ? 0.40f : 0.30f);
                drawBar(c, p - markW / 2f, mid - reach, p + markW / 2f, mid - slot, 0);
                drawBar(c, p - markW / 2f, mid + slot, p + markW / 2f, mid + reach, 0);
            }

            // fenda
            paint.setColor(0xFF000000);
            drawBar(c, knob / 2f - slot / 2f, mid - slot / 2f, length - knob / 2f + slot / 2f, mid + slot / 2f, slot / 2f);
            paint.setColor(0x30FFFFFF);
            drawBar(c, knob / 2f, mid + slot / 2f, length - knob / 2f, mid + slot / 2f + Ui.dp(getContext(), 1), 0);

            // trecho aceso ate o botao
            setRect(knob / 2f, mid - slot / 4f, along, mid + slot / 4f);
            paint.setShader(new LinearGradient(vertical ? 0 : knob / 2f, vertical ? h - knob / 2f : 0, vertical ? 0 : along, vertical ? h - along : 0,
                    Theme.alpha(theme.accent, 120), theme.accent, Shader.TileMode.CLAMP));
            c.drawRoundRect(rect, slot / 4f, slot / 4f, paint);
            paint.setShader(null);

            drawKnob(c, along, mid, cross);
        }

        private void drawBar(Canvas c, float a0, float b0, float a1, float b1, float r) {
            setRect(a0, b0, a1, b1);
            c.drawRoundRect(rect, r, r, paint);
        }

        /** Converte "ao longo / atravessado" para a tela. Em vertical, o zero fica embaixo. */
        private void setRect(float a0, float b0, float a1, float b1) {
            if (vertical) rect.set(b0, getHeight() - a1, b1, getHeight() - a0);
            else rect.set(a0, b0, a1, b1);
        }

        private void drawKnob(Canvas c, float along, float mid, float cross) {
            float halfA = knobLength() / 2f;
            float halfB = Math.min(cross * 0.40f, Ui.dp(getContext(), 26));
            float r = Ui.dp(getContext(), 8);
            // sombra projetada no chassi
            paint.setShader(null);
            paint.setColor(0x60000000);
            setRect(along - halfA, mid - halfB, along + halfA, mid + halfB);
            rect.offset(0, Ui.dp(getContext(), 3));
            c.drawRoundRect(rect, r, r, paint);
            // corpo com luz de cima
            setRect(along - halfA, mid - halfB, along + halfA, mid + halfB);
            float top = rect.top, bottom = rect.bottom;
            paint.setShader(new LinearGradient(0, top, 0, bottom,
                    new int[] {0xFF565860, 0xFF2A2B30, 0xFF141518, 0xFF2C2D32},
                    new float[] {0f, 0.42f, 0.58f, 1f}, Shader.TileMode.CLAMP));
            c.drawRoundRect(rect, r, r, paint);
            paint.setShader(null);
            // fio de luz nas bordas
            Paint line = new Paint(Paint.ANTI_ALIAS_FLAG);
            line.setStyle(Paint.Style.STROKE);
            line.setStrokeWidth(Ui.dp(getContext(), 1));
            line.setColor(0x30FFFFFF);
            c.drawRoundRect(rect, r, r, line);
            // marca central na cor do tema
            paint.setColor(theme.accent);
            float t = Ui.dp(getContext(), 1.5f);
            setRect(along - t, mid - halfB * 0.72f, along + t, mid + halfB * 0.72f);
            c.drawRoundRect(rect, t, t, paint);
        }
    }

    /** Medidor da saida real do PC, em dB. Cai suavemente, com o relogio proprio da tela. */
    static final class Meter extends View {
        private static final int SEGMENTS = 22;
        private static final float RANGE_DB = 48f;
        private final Theme theme;
        private final boolean vertical;
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final RectF rect = new RectF();
        private float shown, peakHold;
        private long lastDraw, holdUntil;
        private float target;

        Meter(Context c, Theme theme, boolean vertical) {
            super(c);
            this.theme = theme;
            this.vertical = vertical;
            setContentDescription("Nível da saída de áudio do PC");
        }

        /** Pico de 0 a 1 vindo do PC. */
        void setPeak(float peak) {
            float db = peak <= 0.0001f ? -RANGE_DB : (float) (20.0 * Math.log10(peak));
            target = Math.max(0f, Math.min(1f, (db + RANGE_DB) / RANGE_DB));
            postInvalidateOnAnimation();
        }

        @Override
        protected void onDraw(Canvas c) {
            long now = SystemClock.uptimeMillis();
            float dt = lastDraw == 0 ? 0 : (now - lastDraw) / 1000f;
            lastDraw = now;
            shown = target >= shown ? target : Math.max(target, shown - dt * 1.1f);
            if (shown >= peakHold) {
                peakHold = shown;
                holdUntil = now + 700;
            } else if (now > holdUntil) {
                peakHold = Math.max(shown, peakHold - dt * 0.6f);
            }
            boolean moving = shown > target + 0.002f || peakHold > shown + 0.002f;

            float w = getWidth(), h = getHeight();
            float length = vertical ? h : w, cross = vertical ? w : h;
            float gap = Ui.dp(getContext(), 2);
            float seg = (length - gap * (SEGMENTS - 1)) / SEGMENTS;
            float r = Ui.dp(getContext(), 1);
            int lit = Math.round(shown * SEGMENTS);
            int hold = Math.round(peakHold * SEGMENTS) - 1;
            for (int i = 0; i < SEGMENTS; i++) {
                float a0 = i * (seg + gap), a1 = a0 + seg;
                if (vertical) rect.set(0, h - a1, cross, h - a0);
                else rect.set(a0, 0, a1, cross);
                int color = colorAt(i / (float) (SEGMENTS - 1));
                boolean on = i < lit || i == hold;
                paint.setColor(on ? color : Theme.alpha(color, 26));
                c.drawRoundRect(rect, r, r, paint);
            }
            if (moving) postInvalidateOnAnimation();
        }

        private static int colorAt(float t) {
            if (t < 0.62f) return Theme.mix(0xFF2BE36B, 0xFFB6EE3C, t / 0.62f);
            if (t < 0.86f) return Theme.mix(0xFFB6EE3C, 0xFFFFC233, (t - 0.62f) / 0.24f);
            return Theme.mix(0xFFFFC233, 0xFFFF4A3D, (t - 0.86f) / 0.14f);
        }
    }

    /** Fio de luz que separa a mesa dos pads, mais forte no meio e sumindo nas pontas. */
    static final class Divider extends View {
        private final Theme theme;
        private final boolean vertical;
        private final Paint paint = new Paint(Paint.ANTI_ALIAS_FLAG);

        Divider(Context c, Theme theme, boolean vertical) {
            super(c);
            this.theme = theme;
            this.vertical = vertical;
        }

        @Override
        protected void onDraw(Canvas c) {
            int on = Theme.alpha(theme.text, 110), off = Theme.alpha(theme.text, 0);
            int[] colors = {off, on, off};
            paint.setShader(vertical
                    ? new LinearGradient(0, 0, 0, getHeight(), colors, null, Shader.TileMode.CLAMP)
                    : new LinearGradient(0, 0, getWidth(), 0, colors, null, Shader.TileMode.CLAMP));
            c.drawRect(0, 0, getWidth(), getHeight(), paint);
        }
    }
}
