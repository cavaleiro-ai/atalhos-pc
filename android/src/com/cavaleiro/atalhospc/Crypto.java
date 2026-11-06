package com.cavaleiro.atalhospc;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.security.GeneralSecurityException;
import java.security.MessageDigest;
import java.security.SecureRandom;

import javax.crypto.Cipher;
import javax.crypto.Mac;
import javax.crypto.spec.IvParameterSpec;
import javax.crypto.spec.SecretKeySpec;

/** Espelho de windows/src/Proto.cs: qualquer mudanca aqui precisa ser feita la tambem. */
final class Crypto {
    static final int PAIR_ITERATIONS = 100000;
    private static final SecureRandom RANDOM = new SecureRandom();

    private Crypto() {}

    static byte[] random(int n) {
        byte[] b = new byte[n];
        RANDOM.nextBytes(b);
        return b;
    }

    static byte[] utf8(String s) {
        return s.getBytes(StandardCharsets.UTF_8);
    }

    static byte[] hmac(byte[] key, byte[]... parts) {
        try {
            Mac m = Mac.getInstance("HmacSHA256");
            m.init(new SecretKeySpec(key, "HmacSHA256"));
            for (byte[] p : parts) m.update(p);
            return m.doFinal();
        } catch (GeneralSecurityException e) {
            throw new IllegalStateException(e);
        }
    }

    /** PBKDF2-HMAC-SHA256 a mao: o provedor pronto so existe a partir do Android 8. */
    static byte[] pbkdf2(String code, byte[] salt, int iterations) {
        try {
            Mac m = Mac.getInstance("HmacSHA256");
            m.init(new SecretKeySpec(utf8(code), "HmacSHA256"));
            m.update(salt);
            m.update(new byte[] {0, 0, 0, 1});
            byte[] u = m.doFinal();
            byte[] t = u.clone();
            for (int i = 1; i < iterations; i++) {
                u = m.doFinal(u);
                for (int j = 0; j < t.length; j++) t[j] ^= u[j];
            }
            return t;
        } catch (GeneralSecurityException e) {
            throw new IllegalStateException(e);
        }
    }

    static boolean same(byte[] a, byte[] b) {
        return MessageDigest.isEqual(a, b);
    }

    private static byte[] seqBytes(long seq) {
        byte[] b = new byte[8];
        for (int i = 7; i >= 0; i--) {
            b[i] = (byte) seq;
            seq >>>= 8;
        }
        return b;
    }

    /** Formato: iv(16) | cifrado AES-256-CBC | HMAC-SHA256(seq | iv | cifrado). */
    static byte[] seal(byte[] enc, byte[] mac, long seq, byte[] plain) {
        try {
            byte[] iv = random(16);
            Cipher c = Cipher.getInstance("AES/CBC/PKCS5Padding");
            c.init(Cipher.ENCRYPT_MODE, new SecretKeySpec(enc, "AES"), new IvParameterSpec(iv));
            byte[] ct = c.doFinal(plain);
            byte[] tag = hmac(mac, seqBytes(seq), iv, ct);
            byte[] out = new byte[16 + ct.length + 32];
            System.arraycopy(iv, 0, out, 0, 16);
            System.arraycopy(ct, 0, out, 16, ct.length);
            System.arraycopy(tag, 0, out, 16 + ct.length, 32);
            return out;
        } catch (GeneralSecurityException e) {
            throw new IllegalStateException(e);
        }
    }

    static byte[] open(byte[] enc, byte[] mac, long seq, byte[] box) throws IOException {
        if (box.length < 16 + 16 + 32 || (box.length - 16 - 32) % 16 != 0) throw new IOException("frame invalido");
        byte[] iv = new byte[16];
        byte[] ct = new byte[box.length - 16 - 32];
        byte[] tag = new byte[32];
        System.arraycopy(box, 0, iv, 0, 16);
        System.arraycopy(box, 16, ct, 0, ct.length);
        System.arraycopy(box, 16 + ct.length, tag, 0, 32);
        if (!same(tag, hmac(mac, seqBytes(seq), iv, ct))) throw new IOException("assinatura invalida");
        try {
            Cipher c = Cipher.getInstance("AES/CBC/PKCS5Padding");
            c.init(Cipher.DECRYPT_MODE, new SecretKeySpec(enc, "AES"), new IvParameterSpec(iv));
            return c.doFinal(ct);
        } catch (GeneralSecurityException e) {
            throw new IOException("frame invalido");
        }
    }
}
