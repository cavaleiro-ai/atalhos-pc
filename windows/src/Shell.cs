// Tudo que fala com o shell do Windows: icone real do arquivo e abertura do arquivo.
using System;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace AtalhosPC
{
    public static class Launch
    {
        [DllImport("user32.dll")]
        static extern bool AllowSetForegroundWindow(int processId);

        // Retorna null quando deu certo, ou a mensagem de erro para mostrar no tablet.
        public static string Open(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return "Arquivo não encontrado no PC: " + path;
            string error = null;
            Thread t = new Thread(() =>
            {
                try
                {
                    AllowSetForegroundWindow(-1);
                    ProcessStartInfo psi = new ProcessStartInfo(path) { UseShellExecute = true };
                    string dir = File.Exists(path) ? Path.GetDirectoryName(path) : null;
                    if (!string.IsNullOrEmpty(dir)) psi.WorkingDirectory = dir;
                    using (Process.Start(psi)) { }
                }
                catch (Win32Exception e) { error = e.NativeErrorCode == 1223 ? "Cancelado na tela do PC." : e.Message; }
                catch (Exception e) { error = e.Message; }
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
            if (!t.Join(15000)) return "O Windows ainda não respondeu. Veja se há uma confirmação na tela do PC.";
            return error;
        }
    }

    public static class Icons
    {
        const int Canvas = 256;

        [StructLayout(LayoutKind.Sequential)]
        struct SIZE { public int cx, cy; }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IShellItemImageFactory
        {
            [PreserveSig]
            int GetImage(SIZE size, int flags, out IntPtr bitmap);
        }

        [DllImport("shell32.dll", CharSet = CharSet.Unicode, PreserveSig = false)]
        static extern void SHCreateItemFromParsingName(string path, IntPtr ctx,
            [MarshalAs(UnmanagedType.LPStruct)] Guid riid,
            [MarshalAs(UnmanagedType.Interface)] out IShellItemImageFactory item);

        [StructLayout(LayoutKind.Sequential)]
        struct BITMAPINFOHEADER
        {
            public int biSize, biWidth, biHeight;
            public short biPlanes, biBitCount;
            public int biCompression, biSizeImage, biXPelsPerMeter, biYPelsPerMeter, biClrUsed, biClrImportant;
        }

        [DllImport("gdi32.dll")]
        static extern int GetDIBits(IntPtr hdc, IntPtr bitmap, int start, int lines, [Out] byte[] bits, ref BITMAPINFOHEADER info, int usage);
        [DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr obj);
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

        static readonly BlockingCollection<Action> Queue = new BlockingCollection<Action>();
        static string cacheDir;

        public static void Init(string dataDir)
        {
            cacheDir = Path.Combine(dataDir, "icones");
            Directory.CreateDirectory(cacheDir);
            // O shell exige apartamento STA; uma fila unica tambem evita extrair o mesmo icone em paralelo.
            Thread t = new Thread(() =>
            {
                foreach (Action a in Queue.GetConsumingEnumerable()) a();
            });
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        public static string KeyFor(string path)
        {
            string stamp = "";
            try
            {
                if (File.Exists(path)) stamp = File.GetLastWriteTimeUtc(path).Ticks.ToString();
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            using (SHA1 sha = SHA1.Create())
            {
                byte[] hash = sha.ComputeHash(Crypto.Utf8(path.ToLowerInvariant() + "|" + stamp));
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < 12; i++) sb.Append(hash[i].ToString("x2"));
                return sb.ToString();
            }
        }

        // PNG com o mesmo icone que o Explorer mostra, ou null se o arquivo sumiu.
        public static byte[] GetPng(string path)
        {
            string cached = Path.Combine(cacheDir, KeyFor(path) + ".png");
            try
            {
                if (File.Exists(cached)) return File.ReadAllBytes(cached);
            }
            catch (IOException) { }

            byte[] png = null;
            using (ManualResetEventSlim done = new ManualResetEventSlim(false))
            {
                Queue.Add(() =>
                {
                    try { png = Render(path); }
                    catch (Exception e) { Log.Write("Icone falhou (" + Path.GetFileName(path) + "): " + e.Message); }
                    finally { done.Set(); }
                });
                if (!done.Wait(20000)) return null;
            }
            if (png != null)
            {
                try { File.WriteAllBytes(cached, png); }
                catch (IOException) { }
            }
            return png;
        }

        static byte[] Render(string path)
        {
            IShellItemImageFactory factory;
            SHCreateItemFromParsingName(path, IntPtr.Zero, typeof(IShellItemImageFactory).GUID, out factory);
            try
            {
                IntPtr hbm;
                SIZE size = new SIZE { cx = Canvas, cy = Canvas };
                int hr = factory.GetImage(size, 0, out hbm);
                if (hr != 0) throw new Win32Exception(hr);
                try
                {
                    using (Bitmap raw = ToArgb(hbm))
                    using (Bitmap fitted = Fit(raw))
                    using (MemoryStream ms = new MemoryStream())
                    {
                        fitted.Save(ms, ImageFormat.Png);
                        return ms.ToArray();
                    }
                }
                finally { DeleteObject(hbm); }
            }
            finally { Marshal.ReleaseComObject(factory); }
        }

        // Valida a imagem enviada ou escolhida e grava como PNG quadrado, igual aos outros icones.
        public static void SaveCustom(byte[] data, string target)
        {
            using (MemoryStream input = new MemoryStream(data))
            using (Image raw = Image.FromStream(input, false, true))
            using (Bitmap argb = new Bitmap(raw.Width, raw.Height, PixelFormat.Format32bppArgb))
            {
                using (Graphics g = Graphics.FromImage(argb)) g.DrawImage(raw, 0, 0, raw.Width, raw.Height);
                using (Bitmap fitted = Fit(argb))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    string tmp = target + ".tmp";
                    fitted.Save(tmp, ImageFormat.Png);
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(tmp, target);
                }
            }
        }

        static Bitmap ToArgb(IntPtr hbm)
        {
            BITMAPINFOHEADER info = new BITMAPINFOHEADER();
            info.biSize = Marshal.SizeOf(typeof(BITMAPINFOHEADER));
            IntPtr hdc = GetDC(IntPtr.Zero);
            try
            {
                if (GetDIBits(hdc, hbm, 0, 0, null, ref info, 0) == 0) throw new InvalidDataException("GetDIBits falhou");
                int w = info.biWidth;
                int h = Math.Abs(info.biHeight);
                info.biHeight = -h; // linhas de cima para baixo
                info.biBitCount = 32;
                info.biCompression = 0;
                info.biSizeImage = 0;
                byte[] px = new byte[w * h * 4];
                if (GetDIBits(hdc, hbm, 0, h, px, ref info, 0) == 0) throw new InvalidDataException("GetDIBits falhou");

                bool hasAlpha = false;
                for (int i = 3; i < px.Length; i += 4) if (px[i] != 0) { hasAlpha = true; break; }
                for (int i = 0; i < px.Length; i += 4)
                {
                    byte a = px[i + 3];
                    if (!hasAlpha) { px[i + 3] = 255; continue; }
                    if (a == 0 || a == 255) continue;
                    // o shell entrega alfa pre-multiplicado; o PNG espera alfa direto
                    px[i] = (byte)Math.Min(255, px[i] * 255 / a);
                    px[i + 1] = (byte)Math.Min(255, px[i + 1] * 255 / a);
                    px[i + 2] = (byte)Math.Min(255, px[i + 2] * 255 / a);
                }

                Bitmap bmp = new Bitmap(w, h, PixelFormat.Format32bppArgb);
                BitmapData data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                for (int y = 0; y < h; y++) Marshal.Copy(px, y * w * 4, IntPtr.Add(data.Scan0, y * data.Stride), w * 4);
                bmp.UnlockBits(data);
                return bmp;
            }
            finally { ReleaseDC(IntPtr.Zero, hdc); }
        }

        // Icone antigo de 32 px vem pequeno no meio de uma tela de 256: corta a sobra e amplia para o azulejo ficar cheio.
        static Bitmap Fit(Bitmap src)
        {
            Rectangle box = OpaqueBounds(src);
            float scale = Math.Min((float)Canvas / box.Width, (float)Canvas / box.Height);
            int w = Math.Max(1, (int)(box.Width * scale));
            int h = Math.Max(1, (int)(box.Height * scale));
            Bitmap dst = new Bitmap(Canvas, Canvas, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(dst))
            {
                g.CompositingMode = CompositingMode.SourceCopy;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                using (ImageAttributes attrs = new ImageAttributes())
                {
                    attrs.SetWrapMode(WrapMode.TileFlipXY); // evita borda escura ao ampliar
                    g.DrawImage(src, new Rectangle((Canvas - w) / 2, (Canvas - h) / 2, w, h),
                        box.X, box.Y, box.Width, box.Height, GraphicsUnit.Pixel, attrs);
                }
            }
            return dst;
        }

        static Rectangle OpaqueBounds(Bitmap bmp)
        {
            BitmapData data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] px = new byte[data.Stride * bmp.Height];
                Marshal.Copy(data.Scan0, px, 0, px.Length);
                int left = bmp.Width, top = bmp.Height, right = -1, bottom = -1;
                for (int y = 0; y < bmp.Height; y++)
                {
                    for (int x = 0; x < bmp.Width; x++)
                    {
                        if (px[y * data.Stride + x * 4 + 3] <= 8) continue;
                        if (x < left) left = x;
                        if (x > right) right = x;
                        if (y < top) top = y;
                        if (y > bottom) bottom = y;
                    }
                }
                if (right < 0) return new Rectangle(0, 0, bmp.Width, bmp.Height);
                return new Rectangle(left, top, right - left + 1, bottom - top + 1);
            }
            finally { bmp.UnlockBits(data); }
        }
    }
}
