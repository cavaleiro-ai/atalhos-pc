package com.cavaleiro.atalhospc;

import android.util.Base64;

import org.json.JSONException;
import org.json.JSONObject;

import java.io.IOException;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.RejectedExecutionException;

/** Uma sessao autenticada com o PC: handshake, pedidos com resposta e avisos do PC. */
final class Link {
    interface Reply {
        void on(JSONObject reply);
    }

    interface Events {
        void onPush(JSONObject message);
    }

    /** O PC nao conhece este aparelho: precisa parear de novo. */
    static final class NeedPair extends IOException {
        final String pid;
        final String pc;

        NeedPair(String pid, String pc) {
            super("Pareamento necessário");
            this.pid = pid;
            this.pc = pc;
        }
    }

    /** Resultado de um pareamento feito com sucesso. */
    static final class Pairing {
        String pid;
        String pc;
        int port;
        byte[] secret;
    }

    static final class WrongCode extends IOException {
        final int left;

        WrongCode(int left) {
            super("Código errado");
            this.left = left;
        }
    }

    static final class PairClosed extends IOException {
        PairClosed() {
            super("Pareamento fechado no PC");
        }
    }

    private static final int PING_MS = 15000;
    private static final int DEAD_MS = 45000;
    private static final int HANDSHAKE_MS = 15000;

    final String via;
    final String target;
    final String pid;
    final String pc;
    private final Transport transport;
    private final Channel channel;
    private final Events events;
    private final ExecutorService writer = Executors.newSingleThreadExecutor();
    private final Map<Integer, Reply> waiting = new HashMap<>();
    private final Object closedSignal = new Object();
    private int nextId = 1;
    private volatile boolean closed;
    private volatile long lastReceived;

    private Link(Transport t, Channel c, String pid, String pc, Events events) {
        transport = t;
        channel = c;
        via = t.label;
        target = t.target;
        this.pid = pid;
        this.pc = pc;
        this.events = events;
    }

    private static String b64(byte[] b) {
        return Base64.encodeToString(b, Base64.NO_WRAP);
    }

    private static byte[] unb64(JSONObject o, String key) throws JSONException {
        return Base64.decode(o.getString(key), Base64.DEFAULT);
    }

    /** Fecha o transporte se a etapa inicial demorar: o socket Bluetooth nao tem tempo limite de leitura. */
    private static final class Guard extends Thread {
        private final Transport t;
        private volatile boolean done;

        Guard(Transport t) {
            this.t = t;
            setDaemon(true);
            start();
        }

        @Override
        public void run() {
            try {
                Thread.sleep(HANDSHAKE_MS);
            } catch (InterruptedException e) {
                return;
            }
            if (!done) t.close();
        }

        void finish() {
            done = true;
            interrupt();
        }
    }

    /** Conversa inicial so para saber quem esta do outro lado, sem autenticar. */
    static JSONObject probe(String target, String device) throws IOException {
        try (Transport t = Transport.open(target)) {
            Guard guard = new Guard(t);
            try {
                Channel ch = new Channel(t.in, t.out);
                ch.writePlain(new JSONObject().put("t", "hello").put("v", 1).put("dev", device).put("cn", b64(Crypto.random(16))).toString());
                JSONObject h = new JSONObject(ch.readPlain());
                if (!"hello".equals(h.optString("t"))) throw new IOException("Isto não é um Atalhos PC.");
                return h;
            } catch (JSONException e) {
                throw new IOException("Resposta inválida do PC.");
            } finally {
                guard.finish();
            }
        }
    }

    static Link connect(String target, String device, String expectedPid, byte[] secret, Events events) throws IOException {
        Transport t = Transport.open(target);
        Guard guard = new Guard(t);
        try {
            Channel ch = new Channel(t.in, t.out);
            byte[] cn = Crypto.random(16);
            ch.writePlain(new JSONObject().put("t", "hello").put("v", 1).put("dev", device).put("cn", b64(cn)).toString());
            JSONObject h = new JSONObject(ch.readPlain());
            if (!"hello".equals(h.optString("t"))) throw new IOException("Isto não é um Atalhos PC.");
            String pid = h.getString("pid");
            if (!pid.equals(expectedPid)) throw new IOException("É outro PC.");
            if (!h.optBoolean("known")) throw new NeedPair(pid, h.optString("pc"));
            ch.secure(secret, cn, unb64(h, "sn"));
            ch.write(new JSONObject().put("t", "auth").toString());
            JSONObject ok = new JSONObject(ch.read());
            if (!"ok".equals(ok.optString("t"))) throw new IOException("O PC recusou a conexão.");
            ch.allowLargeFrames();
            guard.finish();
            t.setReadTimeout(DEAD_MS);
            Link link = new Link(t, ch, pid, h.optString("pc", "PC"), events);
            link.start();
            return link;
        } catch (JSONException e) {
            t.close();
            guard.finish();
            throw new IOException("Resposta inválida do PC.");
        } catch (IOException e) {
            t.close();
            guard.finish();
            throw e;
        }
    }

    /** Pareia com o codigo mostrado no PC. Os dois lados provam que conhecem o codigo sem envia-lo pela rede. */
    static Pairing pair(String target, String device, String deviceName, String code) throws IOException {
        Transport t = Transport.open(target);
        try {
            t.setReadTimeout(20000);
            Channel ch = new Channel(t.in, t.out);
            byte[] cn = Crypto.random(16);
            ch.writePlain(new JSONObject().put("t", "pair").put("v", 1).put("dev", device).put("name", deviceName).put("cn", b64(cn)).toString());
            JSONObject challenge = new JSONObject(ch.readPlain());
            if ("pair_closed".equals(challenge.optString("t"))) throw new PairClosed();
            if (!"pair_ch".equals(challenge.optString("t"))) throw new IOException("Resposta inválida do PC.");
            byte[] sn = unb64(challenge, "sn");
            byte[] salt = new byte[cn.length + sn.length];
            System.arraycopy(cn, 0, salt, 0, cn.length);
            System.arraycopy(sn, 0, salt, cn.length, sn.length);
            byte[] key = Crypto.pbkdf2(code, salt, Crypto.PAIR_ITERATIONS);
            ch.writePlain(new JSONObject().put("t", "pair_proof").put("proof", b64(Crypto.hmac(key, Crypto.utf8("client"), cn, sn))).toString());
            JSONObject r = new JSONObject(ch.readPlain());
            String type = r.optString("t");
            if ("pair_bad".equals(type)) throw new WrongCode(r.optInt("left"));
            if (!"pair_ok".equals(type)) throw new IOException("Resposta inválida do PC.");
            if (!Crypto.same(unb64(r, "proof"), Crypto.hmac(key, Crypto.utf8("server"), cn, sn))) throw new IOException("O PC não comprovou o código.");
            Pairing p = new Pairing();
            p.pid = challenge.getString("pid");
            p.pc = challenge.optString("pc", "PC");
            p.port = challenge.optInt("port", Transport.DEFAULT_PORT);
            p.secret = Crypto.open(Crypto.hmac(key, Crypto.utf8("box-enc")), Crypto.hmac(key, Crypto.utf8("box-mac")), 0, unb64(r, "box"));
            return p;
        } catch (JSONException e) {
            throw new IOException("Resposta inválida do PC.");
        } finally {
            t.close();
        }
    }

    private void start() {
        lastReceived = System.currentTimeMillis();
        Thread reader = new Thread(this::readLoop, "atalhos-leitor");
        reader.setDaemon(true);
        reader.start();
        Thread pinger = new Thread(this::pingLoop, "atalhos-ping");
        pinger.setDaemon(true);
        pinger.start();
    }

    private void readLoop() {
        try {
            while (!closed) {
                JSONObject o = new JSONObject(channel.read());
                lastReceived = System.currentTimeMillis();
                int id = o.optInt("r", 0);
                if (id != 0) {
                    Reply cb;
                    synchronized (waiting) {
                        cb = waiting.remove(id);
                    }
                    if (cb != null) cb.on(o);
                } else if (events != null) {
                    events.onPush(o);
                }
            }
        } catch (IOException | JSONException ignored) {
        } finally {
            close();
        }
    }

    private void pingLoop() {
        while (!closed) {
            try {
                Thread.sleep(PING_MS);
            } catch (InterruptedException e) {
                return;
            }
            if (closed) return;
            if (System.currentTimeMillis() - lastReceived > DEAD_MS) {
                close();
                return;
            }
            try {
                ask(new JSONObject().put("t", "ping"), null);
            } catch (JSONException ignored) {
            }
        }
    }

    /** Envia sem bloquear; o aviso de resposta chega em uma thread de rede. */
    void ask(JSONObject q, Reply cb) {
        final int id;
        synchronized (waiting) {
            id = nextId++;
            if (cb != null) waiting.put(id, cb);
        }
        try {
            q.put("r", id);
            final String text = q.toString();
            writer.execute(() -> {
                try {
                    channel.write(text);
                } catch (IOException e) {
                    close();
                }
            });
        } catch (JSONException | RejectedExecutionException e) {
            failOne(id);
        }
    }

    private void failOne(int id) {
        Reply cb;
        synchronized (waiting) {
            cb = waiting.remove(id);
        }
        if (cb != null) cb.on(failure("Conexão perdida com o PC."));
    }

    static JSONObject failure(String message) {
        JSONObject o = new JSONObject();
        try {
            o.put("ok", false).put("err", message);
        } catch (JSONException ignored) {
        }
        return o;
    }

    boolean isOpen() {
        return !closed;
    }

    void close() {
        synchronized (closedSignal) {
            if (closed) return;
            closed = true;
            closedSignal.notifyAll();
        }
        transport.close();
        writer.shutdown();
        Map<Integer, Reply> copy;
        synchronized (waiting) {
            copy = new HashMap<>(waiting);
            waiting.clear();
        }
        for (Reply cb : copy.values()) cb.on(failure("Conexão perdida com o PC."));
    }

    void awaitClosed() {
        synchronized (closedSignal) {
            while (!closed) {
                try {
                    closedSignal.wait();
                } catch (InterruptedException e) {
                    return;
                }
            }
        }
    }
}
