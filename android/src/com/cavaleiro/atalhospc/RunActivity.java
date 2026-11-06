package com.cavaleiro.atalhospc;

import android.app.Activity;
import android.os.Bundle;
import android.widget.Toast;

/** Aberta pelo atalho da tela inicial: manda abrir o arquivo ou tocar o som no PC e some, sem mostrar tela. */
public class RunActivity extends Activity {
    static final String EXTRA_PC = "pc";
    static final String EXTRA_ID = "id";
    static final String EXTRA_NAME = "name";
    static final String EXTRA_SOUND = "sound";

    private Hub hub;

    @Override
    protected void onCreate(Bundle saved) {
        super.onCreate(saved);
        hub = Hub.get(this);
        String pc = getIntent().getStringExtra(EXTRA_PC);
        String id = getIntent().getStringExtra(EXTRA_ID);
        String name = getIntent().getStringExtra(EXTRA_NAME);
        boolean sound = getIntent().getBooleanExtra(EXTRA_SOUND, false);
        if (pc == null || id == null || hub.paired(pc) == null) {
            Toast.makeText(this, "Este atalho é de um PC que não está mais pareado.", Toast.LENGTH_LONG).show();
            finish();
            return;
        }
        Hub.Paired current = hub.current();
        if (current == null || !current.pid.equals(pc)) hub.select(pc, null);
        hub.activityStarted();
        Toast.makeText(getApplicationContext(), (sound ? "Tocando " : "Abrindo ") + name, Toast.LENGTH_SHORT).show();
        final android.content.Context app = getApplicationContext();
        hub.request(Hub.query(sound ? "sound_play" : "run", "id", id), reply -> {
            if (!reply.optBoolean("ok")) Toast.makeText(app, reply.optString("err", "Não foi possível abrir."), Toast.LENGTH_LONG).show();
            hub.activityStopped();
        });
        finish();
    }
}
