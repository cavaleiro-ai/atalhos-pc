package com.cavaleiro.atalhospc;

import java.io.BufferedOutputStream;
import java.io.DataInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;

/** Frames: tamanho(4 bytes, big endian) | conteudo. Antes da autenticacao, so frames pequenos. */
final class Channel {
    private static final int PLAIN_MAX = 4096;
    private static final int SECURE_MAX = 32 * 1024 * 1024;

    private final DataInputStream in;
    private final OutputStream out;
    private byte[] encOut, macOut, encIn, macIn;
    private long seqOut, seqIn;
    private volatile int maxFrame = PLAIN_MAX;

    Channel(InputStream in, OutputStream out) {
        this.in = new DataInputStream(in);
        this.out = new BufferedOutputStream(out, 16384);
    }

    void allowLargeFrames() {
        maxFrame = SECURE_MAX;
    }

    private byte[] readFrame() throws IOException {
        int len = in.readInt();
        if (len <= 0 || len > maxFrame) throw new IOException("frame fora do limite");
        byte[] body = new byte[len];
        in.readFully(body);
        return body;
    }

    private void writeFrame(byte[] body) throws IOException {
        out.write(body.length >>> 24);
        out.write(body.length >>> 16);
        out.write(body.length >>> 8);
        out.write(body.length);
        out.write(body);
        out.flush();
    }

    String readPlain() throws IOException {
        return new String(readFrame(), StandardCharsets.UTF_8);
    }

    synchronized void writePlain(String text) throws IOException {
        writeFrame(Crypto.utf8(text));
    }

    void secure(byte[] secret, byte[] clientNonce, byte[] serverNonce) {
        encOut = Crypto.hmac(secret, Crypto.utf8("enc-c2s"), clientNonce, serverNonce);
        macOut = Crypto.hmac(secret, Crypto.utf8("mac-c2s"), clientNonce, serverNonce);
        encIn = Crypto.hmac(secret, Crypto.utf8("enc-s2c"), clientNonce, serverNonce);
        macIn = Crypto.hmac(secret, Crypto.utf8("mac-s2c"), clientNonce, serverNonce);
    }

    String read() throws IOException {
        byte[] plain = Crypto.open(encIn, macIn, seqIn, readFrame());
        seqIn++;
        return new String(plain, StandardCharsets.UTF_8);
    }

    synchronized void write(String text) throws IOException {
        writeFrame(Crypto.seal(encOut, macOut, seqOut, Crypto.utf8(text)));
        seqOut++;
    }
}
