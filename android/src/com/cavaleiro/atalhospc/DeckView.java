package com.cavaleiro.atalhospc;

import android.content.Context;
import android.view.MotionEvent;
import android.view.VelocityTracker;
import android.view.View;
import android.view.ViewConfiguration;
import android.view.ViewGroup;

import java.util.ArrayList;
import java.util.List;

/** A grade de teclas ou pads: sempre quadradas, em quatro tamanhos, com paginas deslizando para o lado. */
final class DeckView extends ViewGroup {
    /** Colunas no lado comprido e no curto, do maior botao ao menor. */
    private static final int[] KEY_LONG = {5, 6, 8, 10};
    private static final int[] PAD_LONG = {4, 5, 7, 9};
    private static final int[] SHORT = {3, 4, 5, 6};
    private static final int[] GAP_DP = {12, 10, 8, 6};

    interface Host {
        void onKey(Hub.Item item, KeyView key);
        void onKeyMenu(Hub.Item item);
        void onEmptyKey();
        android.graphics.Bitmap iconFor(Hub.Item item);
        int colorFor(Hub.Item item);
        boolean isPlaying(Hub.Item item);
        boolean isSelected(Hub.Item item);
        void onPageChanged(int page, int pages);
    }

    private final Host host;
    private final Theme theme;
    private final boolean pad;
    private final int slop;
    private final int minFling;
    private final List<KeyView> keys = new ArrayList<>();
    private List<Hub.Item> items = new ArrayList<>();
    private int sizeIndex;
    private boolean showNames = true;
    private int cols = 5, rows = 3, keySize, gap;
    private int page;
    private float downX, downY;
    private boolean dragging;
    private VelocityTracker tracker;

    DeckView(Context c, Theme theme, boolean pad, Host host) {
        super(c);
        this.host = host;
        this.theme = theme;
        this.pad = pad;
        ViewConfiguration vc = ViewConfiguration.get(c);
        slop = vc.getScaledTouchSlop();
        minFling = vc.getScaledMinimumFlingVelocity() * 4;
    }

    void configure(int size, boolean names) {
        sizeIndex = Math.max(0, Math.min(3, size));
        showNames = names;
        gap = Ui.dp(getContext(), GAP_DP[sizeIndex]);
        for (KeyView k : keys) k.setShowNames(names);
        requestLayout();
    }

    private KeyView newKey() {
        KeyView k = new KeyView(getContext(), theme, pad);
        k.setShowNames(showNames);
        k.setOnClickListener(v -> {
            KeyView key = (KeyView) v;
            if (key.item() == null) host.onEmptyKey();
            else host.onKey(key.item(), key);
        });
        k.setOnLongClickListener(v -> {
            KeyView key = (KeyView) v;
            if (key.item() == null) return false;
            host.onKeyMenu(key.item());
            return true;
        });
        return k;
    }

    private int perPage() {
        return Math.max(1, cols * rows);
    }

    private int pages() {
        return Math.max(1, (items.size() + perPage() - 1) / perPage());
    }

    void setItems(List<Hub.Item> list) {
        items = list;
        bindPage();
    }

    private void bindPage() {
        int pages = pages();
        if (page >= pages) page = pages - 1;
        int start = page * perPage();
        for (int i = 0; i < keys.size(); i++) {
            KeyView k = keys.get(i);
            k.setVisibility(i < perPage() ? VISIBLE : GONE);
            int at = start + i;
            Hub.Item it = i < perPage() && at < items.size() ? items.get(at) : null;
            if (it == null) k.bind(null, null, 0xFF888888, false, false);
            else k.bind(it, host.iconFor(it), host.colorFor(it), host.isPlaying(it), host.isSelected(it));
        }
        host.onPageChanged(page, pages);
    }

    @Override
    protected void onMeasure(int widthSpec, int heightSpec) {
        int w = MeasureSpec.getSize(widthSpec), h = MeasureSpec.getSize(heightSpec);
        boolean wide = w >= h;
        int oldPerPage = perPage();
        cols = wide ? (pad ? PAD_LONG : KEY_LONG)[sizeIndex] : SHORT[sizeIndex];
        float keyW = (w - gap * (cols - 1f)) / cols;
        rows = Math.max(1, Math.round((h + gap) / (keyW + gap)));
        keySize = Math.max(0, Math.min((int) keyW, (h - gap * (rows - 1)) / rows));

        // As teclas nascem aqui, depois de setItems: tecla nova sempre precisa ser preenchida,
        // mesmo quando a grade tem o mesmo tamanho de antes.
        boolean created = false;
        while (keys.size() < perPage()) {
            KeyView k = newKey();
            keys.add(k);
            addViewInLayout(k, -1, generateDefaultLayoutParams(), true);
            created = true;
        }
        int spec = MeasureSpec.makeMeasureSpec(keySize, MeasureSpec.EXACTLY);
        for (KeyView k : keys) k.measure(spec, spec);
        setMeasuredDimension(w, h);
        if (created || oldPerPage != perPage()) post(this::bindPage);
    }

    @Override
    protected LayoutParams generateDefaultLayoutParams() {
        return new LayoutParams(LayoutParams.WRAP_CONTENT, LayoutParams.WRAP_CONTENT);
    }

    @Override
    protected void onLayout(boolean changed, int l, int t, int r, int b) {
        int gridW = keySize * cols + gap * (cols - 1);
        int gridH = keySize * rows + gap * (rows - 1);
        int left = (r - l - gridW) / 2, top = (b - t - gridH) / 2;
        for (int i = 0; i < keys.size(); i++) {
            int x = left + (i % cols) * (keySize + gap);
            int y = top + (i / cols) * (keySize + gap);
            keys.get(i).layout(x, y, x + keySize, y + keySize);
        }
    }

    // ---------- deslizar entre paginas ----------

    @Override
    public boolean onInterceptTouchEvent(MotionEvent e) {
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_DOWN:
                downX = e.getX();
                downY = e.getY();
                dragging = false;
                tracker = VelocityTracker.obtain();
                tracker.addMovement(e);
                return false;
            case MotionEvent.ACTION_MOVE:
                if (tracker != null) tracker.addMovement(e);
                float dx = e.getX() - downX;
                float dy = e.getY() - downY;
                if (pages() > 1 && Math.abs(dx) > slop && Math.abs(dx) > Math.abs(dy) * 1.5f) {
                    dragging = true;
                    return true;
                }
                return false;
            default:
                recycleTracker();
                return false;
        }
    }

    @Override
    public boolean onTouchEvent(MotionEvent e) {
        if (!dragging) return e.getActionMasked() == MotionEvent.ACTION_DOWN;
        if (tracker != null) tracker.addMovement(e);
        float raw = e.getX() + getTranslationX() - downX;
        switch (e.getActionMasked()) {
            case MotionEvent.ACTION_MOVE:
                boolean atEdge = (raw > 0 && page == 0) || (raw < 0 && page == pages() - 1);
                setTranslationX(atEdge ? raw * 0.25f : raw);
                return true;
            case MotionEvent.ACTION_UP:
                float speed = 0;
                if (tracker != null) {
                    tracker.computeCurrentVelocity(1000);
                    speed = tracker.getXVelocity();
                }
                finishDrag(raw, speed);
                recycleTracker();
                return true;
            case MotionEvent.ACTION_CANCEL:
                finishDrag(0, 0);
                recycleTracker();
                return true;
            default:
                return true;
        }
    }

    private void finishDrag(float raw, float speed) {
        dragging = false;
        boolean far = Math.abs(raw) > getWidth() * 0.18f || Math.abs(speed) > minFling;
        int next = !far ? page : (raw < 0 ? page + 1 : page - 1);
        if (next < 0 || next >= pages() || next == page) {
            animate().translationX(0).setDuration(160).start();
            return;
        }
        final int direction = next > page ? -1 : 1;
        final int target = next;
        animate().translationX(direction * getWidth()).alpha(0.2f).setDuration(130).withEndAction(() -> {
            page = target;
            bindPage();
            setTranslationX(-direction * getWidth() * 0.5f);
            animate().translationX(0).alpha(1f).setDuration(150).start();
        }).start();
    }

    private void recycleTracker() {
        if (tracker != null) {
            tracker.recycle();
            tracker = null;
        }
    }
}
