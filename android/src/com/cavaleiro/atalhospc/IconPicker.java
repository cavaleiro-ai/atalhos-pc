package com.cavaleiro.atalhospc;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.Intent;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.Paint;
import android.graphics.Rect;
import android.util.Base64;
import android.util.TypedValue;
import android.view.Gravity;
import android.view.View;
import android.view.ViewGroup;
import android.widget.BaseAdapter;
import android.widget.GridView;
import android.widget.TextView;
import android.widget.Toast;

import java.io.ByteArrayOutputStream;

/** Escolha do icone de um som: um emoji desenhado aqui mesmo ou uma imagem do PC. O resultado fica guardado no PC. */
final class IconPicker {
    private static final String[] EMOJI = {
        "🐶", "🐱", "🐔", "🐮", "🐷", "🐸", "🦆", "🐴", "🦁", "🐺", "🐵", "🐘", "🐦", "🐝", "🦉", "🐍",
        "🎵", "🎶", "🔔", "📯", "🥁", "🎺", "🎸", "🎤", "📣", "🔊", "💥", "⚡", "🔥", "💣", "🚗", "🚓",
        "🚨", "🚂", "✈️", "🚀", "👏", "🎉", "😂", "🤣", "😱", "😡", "🤡", "👻", "👽", "🤖", "💀", "⛈️",
        "🌧️", "🌊", "💧", "🍾", "🔫", "⚔️", "🏆", "💰", "⏰", "📱", "🚪", "💨", "⭐", "❌", "✅", "❤️",
    };

    private IconPicker() {}

    /** after roda depois de escolher ou cancelar; pode ser null. */
    static void show(Activity activity, Hub hub, String soundId, String soundName, Runnable after) {
        GridView grid = new GridView(activity);
        grid.setNumColumns(GridView.AUTO_FIT);
        grid.setColumnWidth(Ui.dp(activity, 54));
        grid.setStretchMode(GridView.STRETCH_COLUMN_WIDTH);
        int pad = Ui.dp(activity, 10);
        grid.setPadding(pad, pad, pad, pad);
        grid.setAdapter(new BaseAdapter() {
            @Override public int getCount() { return EMOJI.length; }
            @Override public Object getItem(int i) { return EMOJI[i]; }
            @Override public long getItemId(int i) { return i; }

            @Override
            public View getView(int i, View convert, ViewGroup parent) {
                TextView t = convert == null ? new TextView(activity) : (TextView) convert;
                t.setText(EMOJI[i]);
                t.setTextSize(TypedValue.COMPLEX_UNIT_SP, 30);
                t.setGravity(Gravity.CENTER);
                t.setMinHeight(Ui.dp(activity, 54));
                return t;
            }
        });

        AlertDialog dialog = Ui.dialog(activity)
                .setTitle("Ícone de " + soundName)
                .setView(grid)
                .setPositiveButton("Imagem do PC", (d, w) -> {
                    activity.startActivity(new Intent(activity, BrowseActivity.class)
                            .putExtra(BrowseActivity.EXTRA_MODE, BrowseActivity.MODE_IMAGE)
                            .putExtra(BrowseActivity.EXTRA_TARGET, soundId));
                    if (after != null) after.run();
                })
                .setNegativeButton("Agora não", (d, w) -> {
                    if (after != null) after.run();
                })
                .create();
        grid.setOnItemClickListener((p, v, i, id) -> {
            dialog.dismiss();
            hub.request(Hub.query("sound_seticon", "id", soundId, "png", emojiPng(EMOJI[i])), reply -> {
                if (!reply.optBoolean("ok")) Toast.makeText(activity.getApplicationContext(), reply.optString("err", "Não foi possível trocar o ícone."), Toast.LENGTH_LONG).show();
                if (after != null) after.run();
            });
        });
        dialog.show();
    }

    private static String emojiPng(String emoji) {
        int size = 256;
        Bitmap b = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888);
        Canvas c = new Canvas(b);
        Paint p = new Paint(Paint.ANTI_ALIAS_FLAG);
        p.setTextSize(size * 0.76f);
        Rect bounds = new Rect();
        p.getTextBounds(emoji, 0, emoji.length(), bounds);
        c.drawText(emoji, (size - bounds.width()) / 2f - bounds.left, (size - bounds.height()) / 2f - bounds.top, p);
        ByteArrayOutputStream out = new ByteArrayOutputStream();
        b.compress(Bitmap.CompressFormat.PNG, 100, out);
        b.recycle();
        return Base64.encodeToString(out.toByteArray(), Base64.NO_WRAP);
    }
}
