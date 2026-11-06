package com.cavaleiro.atalhospc;

import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.LinearGradient;
import android.graphics.Paint;
import android.graphics.Path;
import android.graphics.RadialGradient;
import android.graphics.Rect;
import android.graphics.RectF;
import android.graphics.Shader;
import android.os.SystemClock;
import android.text.TextPaint;
import android.text.TextUtils;
import android.util.LruCache;
import android.view.HapticFeedbackConstants;
import android.view.View;

/**
 * Uma tecla do painel, em dois estilos: tecla com tela (atalhos) ou pad de silicone iluminado por dentro (sons).
 * Afunda ao ser pressionada e acende com o resultado.
 */
final class KeyView extends View {
    private static final class Look {
        int average;
        boolean fills;
    }

    private static final int FLASH_MS = 800;
    private static final LruCache<Bitmap, Look> LOOKS = new LruCache<>(256);

    private final Paint fill = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final Paint image = new Paint(Paint.ANTI_ALIAS_FLAG | Paint.FILTER_BITMAP_FLAG);
    private final Paint stroke = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final TextPaint label = new TextPaint(Paint.ANTI_ALIAS_FLAG);
    private final Paint plus = new Paint(Paint.ANTI_ALIAS_FLAG);
    private final RectF socket = new RectF();
    private final RectF skirt = new RectF();
    private final RectF cap = new RectF();
    private final RectF screen = new RectF();
    private final RectF target = new RectF();
    private final Rect source = new Rect();
    private final Path screenClip = new Path();
    private LinearGradient capShade, gloss, labelShade, padBody, padEdge, padGloss;
    private RadialGradient padCore, padHot;
    private float radius, travel, screenRadius, size;

    private final Theme theme;
    private final boolean pad;
    private boolean showNames = true;
    private int padColor = 0xFF888888;
    private int padInk = Color.WHITE;
    private Hub.Item item;
    private Bitmap icon;
    private Look look;
    private boolean playing, selected;

    // Animacao por relogio proprio: ValueAnimator some em aparelhos com "escala de animacao" em 0x, comum nos otimizados.
    private float sink, sinkFrom, sinkTo;
    private long sinkStart, sinkMs;
    private int flashColor;
    private float flash;
    private long flashStart;

    KeyView(Context c, Theme theme, boolean pad) {
        super(c);
        this.theme = theme;
        this.pad = pad;
        label.setColor(theme.label);
        stroke.setStyle(Paint.Style.STROKE);
        plus.setStyle(Paint.Style.STROKE);
        plus.setStrokeCap(Paint.Cap.ROUND);
        setClickable(true);
        setLongClickable(true);
        setHapticFeedbackEnabled(true);
    }

    void setShowNames(boolean show) {
        if (show != showNames) {
            showNames = show;
            invalidate();
        }
    }

    void bind(Hub.Item it, Bitmap bmp, int color, boolean isPlaying, boolean isSelected) {
        item = it;
        icon = bmp;
        look = bmp == null ? null : lookOf(bmp);
        playing = isPlaying;
        selected = isSelected;
        if (pad && color != padColor) {
            padColor = color;
            padInk = Theme.luminance(color) > 0.62f ? 0xFF14161C : Color.WHITE;
            buildPadShaders();
        }
        setContentDescription(it == null ? "Tecla vazia: adicionar" : it.name);
        invalidate();
    }

    Hub.Item item() {
        return item;
    }

    /** Cor media do icone, para a tela da tecla; icone quadrado e opaco ocupa a tela inteira, como numa tecla LCD. */
    private static Look lookOf(Bitmap b) {
        Look l = LOOKS.get(b);
        if (l != null) return l;
        l = new Look();
        Bitmap tiny = Bitmap.createScaledBitmap(b, 8, 8, true);
        long r = 0, g = 0, bl = 0, weight = 0;
        for (int y = 0; y < 8; y++) {
            for (int x = 0; x < 8; x++) {
                int p = tiny.getPixel(x, y);
                int a = Color.alpha(p);
                r += Color.red(p) * a;
                g += Color.green(p) * a;
                bl += Color.blue(p) * a;
                weight += a;
            }
        }
        if (tiny != b) tiny.recycle();
        if (weight == 0) weight = 1;
        l.average = Color.rgb((int) (r / weight), (int) (g / weight), (int) (bl / weight));
        int w = b.getWidth(), h = b.getHeight();
        int inX = w / 10, inY = h / 10;
        l.fills = w == h
                && Color.alpha(b.getPixel(inX, inY)) > 240 && Color.alpha(b.getPixel(w - 1 - inX, inY)) > 240
                && Color.alpha(b.getPixel(inX, h - 1 - inY)) > 240 && Color.alpha(b.getPixel(w - 1 - inX, h - 1 - inY)) > 240;
        LOOKS.put(b, l);
        return l;
    }

    @Override
    protected void onSizeChanged(int w, int h, int oldW, int oldH) {
        size = Math.min(w, h);
        radius = size * (pad ? 0.16f : 0.13f);
        travel = size * 0.032f;
        float padIn = size * 0.035f;
        socket.set(0, 0, w, h);
        skirt.set(padIn, padIn, w - padIn, h - padIn);
        cap.set(padIn, padIn - travel, w - padIn, h - padIn - travel);
        float inset = size * 0.055f;
        screen.set(cap.left + inset, cap.top + inset, cap.right - inset, cap.bottom - inset);
        screenRadius = radius * 0.62f;
        screenClip.reset();
        screenClip.addRoundRect(screen, screenRadius, screenRadius, Path.Direction.CW);
        capShade = new LinearGradient(0, cap.top, 0, cap.bottom, theme.capTop, theme.capBottom, Shader.TileMode.CLAMP);
        gloss = new LinearGradient(0, screen.top, 0, screen.top + screen.height() * 0.45f, theme.gloss, Theme.alpha(theme.gloss, 0), Shader.TileMode.CLAMP);
        labelShade = new LinearGradient(0, screen.bottom - screen.height() * 0.36f, 0, screen.bottom, Theme.alpha(theme.labelShade, 0), theme.labelShade, Shader.TileMode.CLAMP);
        label.setTextSize(Math.max(Ui.dp(getContext(), 9), size * 0.085f));
        plus.setStrokeWidth(Math.max(1f, size * 0.018f));
        if (pad) buildPadShaders();
    }

    private void buildPadShaders() {
        if (size == 0) return;
        padBody = new LinearGradient(0, cap.top, 0, cap.bottom, Theme.mix(padColor, Color.WHITE, 0.16f), Theme.mix(padColor, Color.BLACK, 0.20f), Shader.TileMode.CLAMP);
        // miolo aceso: como um LED atras do silicone, mais forte no centro e sumindo antes da borda
        padCore = new RadialGradient(cap.centerX(), cap.centerY(), cap.width() * 0.62f,
                new int[] {Theme.alpha(Theme.mix(padColor, Color.WHITE, 0.72f), 215), Theme.alpha(Theme.mix(padColor, Color.WHITE, 0.30f), 95), Theme.alpha(padColor, 0)},
                new float[] {0f, 0.52f, 1f}, Shader.TileMode.CLAMP);
        padHot = new RadialGradient(cap.centerX(), cap.centerY(), cap.width() * 0.75f,
                new int[] {0xE6FFFFFF, Theme.alpha(Theme.mix(padColor, Color.WHITE, 0.5f), 150), Theme.alpha(padColor, 0)},
                new float[] {0f, 0.45f, 1f}, Shader.TileMode.CLAMP);
        padEdge = new LinearGradient(0, cap.top, 0, cap.bottom, 0xA6FFFFFF, 0x24FFFFFF, Shader.TileMode.CLAMP);
        padGloss = new LinearGradient(0, cap.top, 0, cap.top + cap.height() * 0.42f, 0x33FFFFFF, 0x00FFFFFF, Shader.TileMode.CLAMP);
    }

    private void advance() {
        long now = SystemClock.uptimeMillis();
        boolean moving = false;
        if (sink != sinkTo) {
            float t = sinkMs <= 0 ? 1f : Math.min(1f, (now - sinkStart) / (float) sinkMs);
            sink = t >= 1f ? sinkTo : sinkFrom + (sinkTo - sinkFrom) * t;
            moving = sink != sinkTo;
        }
        if (flashStart != 0) {
            float t = (now - flashStart) / (float) FLASH_MS;
            flash = t >= 1f ? 0f : 1f - t;
            if (t >= 1f) flashStart = 0;
            else moving = true;
        }
        if (moving || playing) postInvalidateOnAnimation();
    }

    @Override
    protected void onDraw(Canvas c) {
        advance();
        // cavidade no chassi: escura em cima, com a borda de baixo pegando luz
        fill.setShader(null);
        float line = Ui.dp(getContext(), 1);
        fill.setColor(theme.socketEdge);
        target.set(socket.left, socket.top + line, socket.right, socket.bottom);
        c.drawRoundRect(target, radius, radius, fill);
        fill.setColor(theme.socket);
        target.set(socket.left, socket.top, socket.right, socket.bottom - line);
        c.drawRoundRect(target, radius, radius, fill);

        if (pad) drawPad(c);
        else drawKey(c);

        if (selected) {
            stroke.setStrokeWidth(Ui.dp(getContext(), 2));
            stroke.setColor(theme.accent);
            target.set(socket.left + line, socket.top + line, socket.right - line, socket.bottom - line);
            c.drawRoundRect(target, radius, radius, stroke);
        }
    }

    // ---------- tecla com tela ----------

    private void drawKey(Canvas c) {
        float offset = travel * sink;
        fill.setColor(theme.skirt);
        c.drawRoundRect(skirt, radius * 0.9f, radius * 0.9f, fill);

        c.save();
        c.translate(0, offset);
        fill.setShader(capShade);
        c.drawRoundRect(cap, radius * 0.9f, radius * 0.9f, fill);
        fill.setShader(null);
        stroke.setStrokeWidth(Ui.dp(getContext(), 1));
        stroke.setColor(0x40FFFFFF);
        c.drawRoundRect(cap, radius * 0.9f, radius * 0.9f, stroke);
        drawScreen(c);
        c.restore();
    }

    private void drawScreen(Canvas c) {
        c.save();
        c.clipPath(screenClip);
        fill.setColor(look != null ? Theme.mix(theme.lcd, look.average, theme.lcdTint) : theme.lcd);
        c.drawRect(screen, fill);

        if (item == null) {
            drawPlus(c, theme.mark);
        } else {
            image.setAlpha(item.missing ? 90 : 255);
            if (icon != null) drawKeyIcon(c);
            else drawLetter(c, screen, theme.label);
            if (showNames) {
                fill.setShader(labelShade);
                c.drawRect(screen.left, screen.bottom - screen.height() * 0.36f, screen.right, screen.bottom, fill);
                fill.setShader(null);
                drawName(c, screen, theme.label);
            }
        }

        fill.setShader(gloss);
        c.drawRect(screen.left, screen.top, screen.right, screen.top + screen.height() * 0.45f, fill);
        fill.setShader(null);

        if (flash > 0f) {
            fill.setColor(flashColor);
            fill.setAlpha((int) (70 * flash));
            c.drawRect(screen, fill);
            fill.setAlpha(255);
        }
        c.restore();

        // aro da tela: esconde o serrilhado do recorte e, ao abrir, acende na cor do resultado
        stroke.setStrokeWidth(Ui.dp(getContext(), flash > 0f ? 2.5f : 1.5f));
        stroke.setColor(flash > 0f ? flashColor : Theme.alpha(Color.BLACK, Theme.luminance(theme.lcd) > 0.5f ? 40 : 255));
        c.drawRoundRect(screen, screenRadius, screenRadius, stroke);
    }

    private void drawKeyIcon(Canvas c) {
        source.set(0, 0, icon.getWidth(), icon.getHeight());
        if (look.fills) {
            c.drawBitmap(icon, source, screen, image);
            return;
        }
        // sem nome, o icone fica centrado e maior; com nome, reserva a faixa de baixo
        float area = showNames ? screen.height() * 0.70f : screen.height();
        float box = area * (showNames ? 0.86f : 0.74f);
        float scale = Math.min(box / icon.getWidth(), box / icon.getHeight());
        float w = icon.getWidth() * scale, h = icon.getHeight() * scale;
        float cx = screen.centerX(), cy = screen.top + area * (showNames ? 0.54f : 0.5f);
        target.set(cx - w / 2f, cy - h / 2f, cx + w / 2f, cy + h / 2f);
        c.drawBitmap(icon, source, target, image);
    }

    private void drawPlus(Canvas c, int color) {
        float cx = screen.centerX(), cy = screen.centerY(), arm = screen.width() * 0.09f;
        plus.setColor(color);
        c.drawLine(cx - arm, cy, cx + arm, cy, plus);
        c.drawLine(cx, cy - arm, cx, cy + arm, plus);
    }

    private void drawLetter(Canvas c, RectF box, int color) {
        String letter = item.name.isEmpty() ? "?" : item.name.substring(0, 1).toUpperCase();
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setColor(Theme.alpha(color, 128));
        p.setTextSize(box.height() * 0.42f);
        p.setTextAlign(Paint.Align.CENTER);
        c.drawText(pad ? "♪" : letter, box.centerX(), box.top + box.height() * (showNames ? 0.5f : 0.64f), p);
    }

    private void drawName(Canvas c, RectF box, int color) {
        label.setColor(color);
        CharSequence text = TextUtils.ellipsize(item.name, label, box.width() - Ui.dp(getContext(), 8), TextUtils.TruncateAt.END);
        float width = label.measureText(text, 0, text.length());
        c.drawText(text, 0, text.length(), box.centerX() - width / 2f, box.bottom - box.height() * (pad ? 0.135f : 0.075f), label);
    }

    // ---------- pad de som ----------

    private void drawPad(Canvas c) {
        boolean empty = item == null;
        float offset = travel * sink;
        float r = radius * 0.92f;

        // luz vazando para a cavidade em volta do pad: forte quando esta tocando
        if (!empty) {
            float pulse = playing ? 0.75f + 0.25f * (float) Math.sin(SystemClock.uptimeMillis() / 130.0) : 0f;
            int layers = 3;
            for (int i = layers; i >= 1; i--) {
                float grow = i * size * 0.008f;
                target.set(skirt.left - grow, skirt.top - grow, skirt.right + grow, skirt.bottom + grow);
                fill.setColor(padColor);
                fill.setAlpha((int) ((playing ? 70 * pulse + 24 : 20) / i));
                c.drawRoundRect(target, r + grow, r + grow, fill);
            }
            fill.setAlpha(255);
        }

        fill.setColor(empty ? theme.skirt : Theme.mix(padColor, Color.BLACK, 0.62f));
        c.drawRoundRect(skirt, r, r, fill);

        c.save();
        c.translate(0, offset);
        if (empty) {
            fill.setShader(capShade);
            c.drawRoundRect(cap, r, r, fill);
            fill.setShader(null);
            stroke.setStrokeWidth(Ui.dp(getContext(), 1));
            stroke.setColor(0x24FFFFFF);
            c.drawRoundRect(cap, r, r, stroke);
            drawPlus(c, theme.mark);
            c.restore();
            return;
        }

        int alphaAll = item.missing ? 110 : 255;
        fill.setAlpha(alphaAll);
        fill.setShader(padBody);
        c.drawRoundRect(cap, r, r, fill);
        fill.setShader(playing ? padHot : padCore);
        c.drawRoundRect(cap, r, r, fill);
        fill.setShader(padGloss);
        c.drawRoundRect(cap, r, r, fill);
        fill.setShader(null);
        fill.setAlpha(255);

        // borda clara do silicone: mais opaca em cima, quase some embaixo
        stroke.setShader(padEdge);
        stroke.setStrokeWidth(Math.max(1f, size * 0.012f));
        target.set(cap.left + 1, cap.top + 1, cap.right - 1, cap.bottom - 1);
        c.drawRoundRect(target, r, r, stroke);
        stroke.setShader(null);

        // tocando: aro branco pulsando, para ver de longe quais pads estao no ar
        if (playing) {
            float pulse = 0.65f + 0.35f * (float) Math.sin(SystemClock.uptimeMillis() / 130.0);
            stroke.setStrokeWidth(Math.max(2f, size * 0.022f));
            stroke.setColor(Theme.alpha(Color.WHITE, (int) (235 * pulse)));
            c.drawRoundRect(target, r, r, stroke);
        }

        // icone
        float top = cap.top + cap.height() * 0.09f;
        float area = cap.height() * (showNames ? 0.54f : 0.72f);
        target.set(cap.left, top, cap.right, top + area);
        if (icon != null) {
            source.set(0, 0, icon.getWidth(), icon.getHeight());
            float scale = Math.min(area / icon.getWidth(), area / icon.getHeight());
            float w = icon.getWidth() * scale, h = icon.getHeight() * scale;
            float cx = cap.centerX(), cy = top + area / 2f;
            target.set(cx - w / 2f, cy - h / 2f, cx + w / 2f, cy + h / 2f);
            image.setAlpha(alphaAll);
            c.drawBitmap(icon, source, target, image);
        } else {
            drawLetter(c, target, padInk);
        }

        if (showNames) drawName(c, cap, padInk);

        // barra do volume do proprio som
        float barHeight = Math.max(Ui.dp(getContext(), 2.5f), size * 0.022f);
        float barInset = cap.width() * 0.13f;
        float barBottom = cap.bottom - cap.height() * 0.06f;
        target.set(cap.left + barInset, barBottom - barHeight, cap.right - barInset, barBottom);
        fill.setColor(Theme.alpha(padInk == Color.WHITE ? Color.BLACK : 0xFF14161C, 56));
        c.drawRoundRect(target, barHeight, barHeight, fill);
        target.right = target.left + target.width() * Math.max(0.02f, item.volume / 100f);
        fill.setColor(Theme.alpha(padInk, 225));
        c.drawRoundRect(target, barHeight, barHeight, fill);

        if (flash > 0f) {
            stroke.setStrokeWidth(Ui.dp(getContext(), 2.5f));
            stroke.setColor(Theme.alpha(flashColor, (int) (255 * flash)));
            c.drawRoundRect(cap, r, r, stroke);
        }
        c.restore();
    }

    // ---------- toque ----------

    @Override
    public void setPressed(boolean pressed) {
        boolean was = isPressed();
        super.setPressed(pressed);
        if (was == pressed) return;
        sinkFrom = sink;
        sinkTo = pressed ? 1f : 0f;
        sinkStart = SystemClock.uptimeMillis();
        sinkMs = pressed ? 50 : 140;
        invalidate();
    }

    @Override
    public boolean performClick() {
        performHapticFeedback(HapticFeedbackConstants.VIRTUAL_KEY);
        return super.performClick();
    }

    /** Aviso do resultado: verde abriu no PC, vermelho deu erro. */
    void flash(int color) {
        flashColor = color;
        flash = 1f;
        flashStart = SystemClock.uptimeMillis();
        invalidate();
    }
}
