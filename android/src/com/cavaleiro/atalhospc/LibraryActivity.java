package com.cavaleiro.atalhospc;

import android.app.Activity;
import android.content.Context;
import android.graphics.Bitmap;
import android.graphics.BitmapFactory;
import android.os.Bundle;
import android.os.Handler;
import android.os.Looper;
import android.text.Editable;
import android.text.TextUtils;
import android.text.TextWatcher;
import android.util.Base64;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.AbsListView;
import android.widget.BaseAdapter;
import android.widget.Button;
import android.widget.EditText;
import android.widget.ImageView;
import android.widget.LinearLayout;
import android.widget.ListView;
import android.widget.TextView;
import android.widget.Toast;

import org.json.JSONArray;
import org.json.JSONObject;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/** Biblioteca de sons do PC: categorias, busca e previa. A lista vem em paginas porque sao milhares de arquivos. */
public class LibraryActivity extends Activity implements Hub.Listener {
    private static final class Row {
        boolean category;
        String cat, name, detail, file;
        boolean added;
    }

    private static final int PAGE = 100;
    private static final long SEARCH_DELAY_MS = 300;

    private Hub hub;
    private final Handler main = new Handler(Looper.getMainLooper());
    private TextView title;
    private TextView notice;
    private EditText search;
    private ListView list;
    private final List<Row> rows = new ArrayList<>();
    private final RowAdapter adapter = new RowAdapter();
    private final Map<String, Bitmap> icons = new HashMap<>();
    private final Set<String> iconsAsked = new HashSet<>();
    private final List<Row> categories = new ArrayList<>();
    private String currentCat, currentTitle; // null = todas as categorias
    private boolean showingSounds;
    private boolean loading, loadedOnce;
    private int total;
    private int generation;
    private final Runnable searchNow = this::reload;

    @Override
    protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        hub = Hub.get(this);
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
        title = Ui.text(this, "Biblioteca de sons", 15, Ui.TEXT);
        title.setSingleLine(true);
        title.setEllipsize(TextUtils.TruncateAt.END);
        title.setPadding(pad, 0, pad, 0);
        bar.addView(title, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
        Button stop = Ui.button(this, "Parar", Ui.CARD_HIGH);
        stop.setOnClickListener(v -> hub.request(Hub.query("lib_stop"), null));
        bar.addView(stop);
        root.addView(bar);

        search = new EditText(this);
        search.setHint("Buscar som");
        search.setSingleLine(true);
        search.setTextColor(Ui.TEXT);
        search.setHintTextColor(Ui.MUTED);
        search.setBackground(Ui.inset(this));
        search.setPadding(pad, Ui.dp(this, 8), pad, Ui.dp(this, 8));
        LinearLayout.LayoutParams sp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        sp.topMargin = pad;
        root.addView(search, sp);
        search.addTextChangedListener(new TextWatcher() {
            @Override public void beforeTextChanged(CharSequence s, int a, int b, int c) {}
            @Override public void onTextChanged(CharSequence s, int a, int b, int c) {}
            @Override public void afterTextChanged(Editable s) {
                main.removeCallbacks(searchNow);
                main.postDelayed(searchNow, SEARCH_DELAY_MS);
            }
        });

        notice = Ui.text(this, "", 13, Ui.WARN);
        notice.setPadding(0, pad, 0, 0);
        notice.setVisibility(View.GONE);
        root.addView(notice);

        list = new ListView(this);
        list.setDivider(null);
        list.setDividerHeight(Ui.dp(this, 6));
        list.setSelector(android.R.color.transparent);
        list.setClipToPadding(false);
        list.setPadding(0, pad, 0, pad);
        list.setAdapter(adapter);
        list.setOnItemClickListener((p, v, pos, id) -> {
            Row r = rows.get(pos);
            if (r.category) openCategory(r);
            else preview(r);
        });
        list.setOnScrollListener(new AbsListView.OnScrollListener() {
            @Override public void onScrollStateChanged(AbsListView view, int state) {}
            @Override public void onScroll(AbsListView view, int first, int visible, int count) {
                if (showingSounds && !loading && rows.size() < total && first + visible >= count - 15) loadMore();
            }
        });
        root.addView(list, new LinearLayout.LayoutParams(LinearLayout.LayoutParams.MATCH_PARENT, 0, 1));
        setContentView(root);
        loadCategories();
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
        // sair da tela nao pode deixar um audio longo tocando no PC
        hub.request(Hub.query("lib_stop"), null);
        hub.removeListener(this);
        hub.activityStopped();
    }

    @Override
    protected void onDestroy() {
        super.onDestroy();
        main.removeCallbacks(searchNow);
    }

    @Override
    public void onHubChanged() {
        if (!loadedOnce && hub.state() == Hub.ONLINE) loadCategories();
    }

    @Override
    public void onBackPressed() {
        if (search.getText().length() > 0) {
            search.setText("");
            return;
        }
        if (showingSounds) {
            hub.request(Hub.query("lib_stop"), null);
            showCategories();
            return;
        }
        super.onBackPressed();
    }

    private void loadCategories() {
        hub.request(Hub.query("lib_categories"), reply -> {
            if (isDestroyed()) return;
            if (!reply.optBoolean("ok")) {
                show(reply.optString("err", "Sem conexão com o PC.") + " Atualize o programa do PC se ele for de uma versão antiga.");
                return;
            }
            loadedOnce = true;
            categories.clear();
            JSONArray a = reply.optJSONArray("categories");
            if (a != null) {
                for (int i = 0; i < a.length(); i++) {
                    JSONObject o = a.optJSONObject(i);
                    if (o == null) continue;
                    Row r = new Row();
                    r.category = true;
                    r.cat = o.optString("cat");
                    r.name = o.optString("name");
                    r.detail = o.optInt("count") + " sons";
                    categories.add(r);
                }
            }
            if (categories.isEmpty()) show("A biblioteca não está instalada neste PC. Veja o guia de uso do Atalhos PC.");
            if (!showingSounds && search.getText().length() == 0) showCategories();
        });
    }

    private void showCategories() {
        generation++;
        loading = false;
        showingSounds = false;
        currentCat = null;
        currentTitle = null;
        title.setText("Biblioteca de sons");
        rows.clear();
        rows.addAll(categories);
        adapter.notifyDataSetChanged();
        list.setSelection(0);
    }

    private void openCategory(Row r) {
        currentCat = r.cat;
        currentTitle = r.name;
        startSounds();
    }

    private void reload() {
        if (isDestroyed()) return;
        String q = search.getText().toString().trim();
        if (q.isEmpty() && currentCat == null) showCategories();
        else startSounds();
    }

    private void startSounds() {
        generation++;
        loading = false;
        showingSounds = true;
        rows.clear();
        total = 0;
        adapter.notifyDataSetChanged();
        list.setSelection(0);
        loadMore();
    }

    private void loadMore() {
        final int mine = generation;
        loading = true;
        String q = search.getText().toString().trim();
        hub.request(Hub.query("lib_list", "cat", currentCat == null ? "" : currentCat, "q", q, "from", rows.size(), "limit", PAGE), reply -> {
            if (isDestroyed() || mine != generation) return;
            loading = false;
            if (!reply.optBoolean("ok")) {
                show(reply.optString("err", "Não foi possível abrir a biblioteca."));
                return;
            }
            notice.setVisibility(View.GONE);
            total = reply.optInt("total");
            JSONArray a = reply.optJSONArray("items");
            if (a != null) {
                for (int i = 0; i < a.length(); i++) {
                    JSONObject o = a.optJSONObject(i);
                    if (o == null) continue;
                    Row r = new Row();
                    r.cat = o.optString("cat");
                    r.file = o.optString("file");
                    r.name = o.optString("name");
                    r.detail = o.optString("catName");
                    r.added = o.optBoolean("added");
                    rows.add(r);
                }
            }
            title.setText((currentTitle == null ? "Todas as categorias" : currentTitle) + ": " + total + " sons");
            if (total == 0) show("Nenhum som encontrado.");
            adapter.notifyDataSetChanged();
        });
    }

    private void preview(Row r) {
        hub.request(Hub.query("lib_preview", "cat", r.cat, "file", r.file), reply -> {
            if (isDestroyed()) return;
            if (!reply.optBoolean("ok")) Toast.makeText(this, reply.optString("err", "Não foi possível tocar."), Toast.LENGTH_LONG).show();
        });
    }

    private void add(Row r) {
        hub.request(Hub.query("lib_add", "cat", r.cat, "file", r.file), reply -> {
            if (isDestroyed()) return;
            if (!reply.optBoolean("ok")) {
                Toast.makeText(this, reply.optString("err", "Não foi possível adicionar."), Toast.LENGTH_LONG).show();
                return;
            }
            r.added = true;
            adapter.notifyDataSetChanged();
            Toast.makeText(this, "Adicionado aos seus sons: " + r.name, Toast.LENGTH_SHORT).show();
        });
    }

    private void show(String text) {
        notice.setText(text);
        notice.setVisibility(View.VISIBLE);
    }

    private Bitmap iconFor(String cat) {
        Bitmap b = icons.get(cat);
        if (b == null && iconsAsked.add(cat)) {
            hub.request(Hub.query("lib_icon", "cat", cat), reply -> {
                if (isDestroyed() || !reply.optBoolean("ok")) return;
                byte[] png = Base64.decode(reply.optString("png"), Base64.DEFAULT);
                Bitmap decoded = BitmapFactory.decodeByteArray(png, 0, png.length);
                if (decoded == null) return;
                icons.put(cat, decoded);
                adapter.notifyDataSetChanged();
            });
        }
        return b;
    }

    private final class RowAdapter extends BaseAdapter {
        @Override
        public int getCount() {
            return rows.size();
        }

        @Override
        public Object getItem(int position) {
            return rows.get(position);
        }

        @Override
        public long getItemId(int position) {
            return position;
        }

        @Override
        public View getView(int position, View convert, ViewGroup parent) {
            Context c = LibraryActivity.this;
            LinearLayout row;
            ImageView image;
            TextView name, detail;
            Button listen, addButton;
            if (convert == null) {
                row = new LinearLayout(c);
                row.setGravity(Gravity.CENTER_VERTICAL);
                row.setBackground(Ui.tileBackground(c, Ui.CARD, 10));
                int pad = Ui.dp(c, 8);
                row.setPadding(pad, pad, pad, pad);
                image = new ImageView(c);
                image.setScaleType(ImageView.ScaleType.FIT_CENTER);
                row.addView(image, new LinearLayout.LayoutParams(Ui.dp(c, 36), Ui.dp(c, 36)));
                LinearLayout texts = new LinearLayout(c);
                texts.setOrientation(LinearLayout.VERTICAL);
                texts.setPadding(Ui.dp(c, 12), 0, Ui.dp(c, 8), 0);
                name = Ui.text(c, "", 15, Ui.TEXT);
                name.setSingleLine(true);
                name.setEllipsize(TextUtils.TruncateAt.END);
                detail = Ui.text(c, "", 12, Ui.MUTED);
                detail.setSingleLine(true);
                texts.addView(name);
                texts.addView(detail);
                row.addView(texts, new LinearLayout.LayoutParams(0, LinearLayout.LayoutParams.WRAP_CONTENT, 1));
                listen = Ui.button(c, "Ouvir", Ui.CARD_HIGH);
                listen.setFocusable(false);
                row.addView(listen);
                addButton = Ui.button(c, "Adicionar", Ui.ACCENT);
                addButton.setFocusable(false);
                LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT);
                lp.leftMargin = Ui.dp(c, 6);
                row.addView(addButton, lp);
            } else {
                row = (LinearLayout) convert;
                image = (ImageView) row.getChildAt(0);
                LinearLayout texts = (LinearLayout) row.getChildAt(1);
                name = (TextView) texts.getChildAt(0);
                detail = (TextView) texts.getChildAt(1);
                listen = (Button) row.getChildAt(2);
                addButton = (Button) row.getChildAt(3);
            }
            Row r = rows.get(position);
            Bitmap icon = iconFor(r.cat);
            image.setImageBitmap(icon != null ? icon : Ui.letterIcon(c, r.category ? r.name : r.detail));
            name.setText(r.name);
            detail.setText(r.detail);
            listen.setVisibility(r.category ? View.GONE : View.VISIBLE);
            addButton.setVisibility(r.category ? View.GONE : View.VISIBLE);
            if (!r.category) {
                listen.setContentDescription("Ouvir " + r.name);
                listen.setOnClickListener(v -> preview(r));
                addButton.setText(r.added ? "Na lista" : "Adicionar");
                addButton.setEnabled(!r.added);
                addButton.setAlpha(r.added ? 0.55f : 1f);
                addButton.setContentDescription((r.added ? "Na lista: " : "Adicionar ") + r.name);
                addButton.setOnClickListener(v -> add(r));
            }
            return row;
        }
    }
}
