// Protocolo seguro compartilhado por servidor e testes. O espelho Java fica em android/src/.../Crypto.java e Channel.java.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Web.Script.Serialization;

namespace AtalhosPC
{
    public static class Crypto
    {
        public const int PairIterations = 100000;
        static readonly RandomNumberGenerator Rng = RandomNumberGenerator.Create();

        public static byte[] RandomBytes(int n)
        {
            byte[] b = new byte[n];
            lock (Rng) Rng.GetBytes(b);
            return b;
        }

        public static byte[] Utf8(string s) { return Encoding.UTF8.GetBytes(s); }

        public static byte[] Hmac(byte[] key, params byte[][] parts)
        {
            using (HMACSHA256 h = new HMACSHA256(key))
            {
                foreach (byte[] p in parts) h.TransformBlock(p, 0, p.Length, null, 0);
                h.TransformFinalBlock(new byte[0], 0, 0);
                return h.Hash;
            }
        }

        // PBKDF2-HMAC-SHA256 escrito a mao: a versao do framework so aceita SHA-256 a partir do .NET 4.7.2.
        public static byte[] Pbkdf2(string code, byte[] salt, int iterations)
        {
            using (HMACSHA256 h = new HMACSHA256(Utf8(code)))
            {
                byte[] block = new byte[salt.Length + 4];
                Buffer.BlockCopy(salt, 0, block, 0, salt.Length);
                block[block.Length - 1] = 1;
                byte[] u = h.ComputeHash(block);
                byte[] t = (byte[])u.Clone();
                for (int i = 1; i < iterations; i++)
                {
                    u = h.ComputeHash(u);
                    for (int j = 0; j < t.Length; j++) t[j] ^= u[j];
                }
                return t;
            }
        }

        public static bool Same(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }

        static byte[] SeqBytes(ulong seq)
        {
            byte[] b = new byte[8];
            for (int i = 7; i >= 0; i--) { b[i] = (byte)seq; seq >>= 8; }
            return b;
        }

        // Formato: iv(16) | cifrado AES-256-CBC | HMAC-SHA256(seq | iv | cifrado)
        public static byte[] Seal(byte[] enc, byte[] mac, ulong seq, byte[] plain)
        {
            byte[] iv = RandomBytes(16);
            byte[] ct;
            using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = enc;
                aes.IV = iv;
                using (ICryptoTransform t = aes.CreateEncryptor()) ct = t.TransformFinalBlock(plain, 0, plain.Length);
            }
            byte[] tag = Hmac(mac, SeqBytes(seq), iv, ct);
            byte[] o = new byte[16 + ct.Length + 32];
            Buffer.BlockCopy(iv, 0, o, 0, 16);
            Buffer.BlockCopy(ct, 0, o, 16, ct.Length);
            Buffer.BlockCopy(tag, 0, o, 16 + ct.Length, 32);
            return o;
        }

        public static byte[] Open(byte[] enc, byte[] mac, ulong seq, byte[] box)
        {
            if (box.Length < 16 + 16 + 32 || (box.Length - 16 - 32) % 16 != 0) throw new InvalidDataException("frame invalido");
            byte[] iv = new byte[16];
            byte[] ct = new byte[box.Length - 16 - 32];
            byte[] tag = new byte[32];
            Buffer.BlockCopy(box, 0, iv, 0, 16);
            Buffer.BlockCopy(box, 16, ct, 0, ct.Length);
            Buffer.BlockCopy(box, 16 + ct.Length, tag, 0, 32);
            if (!Same(tag, Hmac(mac, SeqBytes(seq), iv, ct))) throw new InvalidDataException("assinatura invalida");
            using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
            {
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                aes.Key = enc;
                aes.IV = iv;
                using (ICryptoTransform t = aes.CreateDecryptor()) return t.TransformFinalBlock(ct, 0, ct.Length);
            }
        }
    }

    public static class Json
    {
        static JavaScriptSerializer New()
        {
            JavaScriptSerializer s = new JavaScriptSerializer();
            s.MaxJsonLength = int.MaxValue;
            s.RecursionLimit = 16;
            return s;
        }

        public static Dictionary<string, object> Parse(string text)
        {
            Dictionary<string, object> d = New().DeserializeObject(text) as Dictionary<string, object>;
            if (d == null) throw new InvalidDataException("json invalido");
            return d;
        }

        public static string Write(object o) { return New().Serialize(o); }
        public static T Read<T>(string text) { return New().Deserialize<T>(text); }

        public static string Str(Dictionary<string, object> d, string key)
        {
            object v;
            if (!d.TryGetValue(key, out v) || v == null) return null;
            return Convert.ToString(v, CultureInfo.InvariantCulture);
        }

        public static int Int(Dictionary<string, object> d, string key, int fallback)
        {
            int n;
            string s = Str(d, key);
            return s != null && int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out n) ? n : fallback;
        }

        public static byte[] B64(Dictionary<string, object> d, string key)
        {
            string s = Str(d, key);
            if (s == null) throw new InvalidDataException("faltou " + key);
            return Convert.FromBase64String(s);
        }
    }

    // Frames: tamanho(4 bytes, big endian) | conteudo. Antes da autenticacao, so frames pequenos.
    public sealed class Channel
    {
        public const int PlainMax = 4096;
        public const int SecureMax = 32 * 1024 * 1024;

        readonly Stream stream;
        readonly object writeLock = new object();
        byte[] encOut, macOut, encIn, macIn;
        ulong seqOut, seqIn;
        int maxFrame = PlainMax;

        public Channel(Stream s) { stream = s; }

        public void AllowLargeFrames() { maxFrame = SecureMax; }

        void ReadFully(byte[] buf)
        {
            int got = 0;
            while (got < buf.Length)
            {
                int n = stream.Read(buf, got, buf.Length - got);
                if (n <= 0) throw new EndOfStreamException();
                got += n;
            }
        }

        byte[] ReadFrame()
        {
            byte[] head = new byte[4];
            ReadFully(head);
            long len = ((long)head[0] << 24) | ((long)head[1] << 16) | ((long)head[2] << 8) | head[3];
            if (len <= 0 || len > maxFrame) throw new InvalidDataException("frame fora do limite");
            byte[] body = new byte[len];
            ReadFully(body);
            return body;
        }

        void WriteFrame(byte[] body)
        {
            byte[] all = new byte[4 + body.Length];
            all[0] = (byte)(body.Length >> 24);
            all[1] = (byte)(body.Length >> 16);
            all[2] = (byte)(body.Length >> 8);
            all[3] = (byte)body.Length;
            Buffer.BlockCopy(body, 0, all, 4, body.Length);
            stream.Write(all, 0, all.Length);
            stream.Flush();
        }

        public string ReadPlain() { return Encoding.UTF8.GetString(ReadFrame()); }

        public void WritePlain(string text)
        {
            lock (writeLock) WriteFrame(Crypto.Utf8(text));
        }

        public void Secure(byte[] secret, byte[] clientNonce, byte[] serverNonce, bool isServer)
        {
            byte[] encC2S = Crypto.Hmac(secret, Crypto.Utf8("enc-c2s"), clientNonce, serverNonce);
            byte[] macC2S = Crypto.Hmac(secret, Crypto.Utf8("mac-c2s"), clientNonce, serverNonce);
            byte[] encS2C = Crypto.Hmac(secret, Crypto.Utf8("enc-s2c"), clientNonce, serverNonce);
            byte[] macS2C = Crypto.Hmac(secret, Crypto.Utf8("mac-s2c"), clientNonce, serverNonce);
            encOut = isServer ? encS2C : encC2S;
            macOut = isServer ? macS2C : macC2S;
            encIn = isServer ? encC2S : encS2C;
            macIn = isServer ? macC2S : macS2C;
        }

        public string Read()
        {
            byte[] box = ReadFrame();
            byte[] plain = Crypto.Open(encIn, macIn, seqIn, box);
            seqIn++;
            return Encoding.UTF8.GetString(plain);
        }

        public void Write(string text)
        {
            lock (writeLock)
            {
                WriteFrame(Crypto.Seal(encOut, macOut, seqOut, Crypto.Utf8(text)));
                seqOut++;
            }
        }
    }
}
