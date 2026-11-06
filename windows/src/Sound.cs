// Toca os sons no PC e le o nivel real da saida. Usa so o que ja vem no Windows: MCI (winmm) e Core Audio.
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AtalhosPC
{
    public interface ISoundPlayer
    {
        // volume de 0 a 100, ja com o volume geral aplicado. Retorna null quando deu certo, ou a mensagem de erro.
        string Play(string id, string path, int volume);
        void SetVolume(string id, int volume);
        void Stop(string id);
        void StopAll();
        List<string> Playing();
        event Action PlayingChanged;
    }

    public sealed class MciPlayer : ISoundPlayer
    {
        const int MaxVoices = 32;

        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        static extern int mciSendString(string command, StringBuilder result, int resultLength, IntPtr callback);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        static extern bool mciGetErrorString(int error, StringBuilder text, int length);

        sealed class Voice
        {
            public string Id;
            public string Alias;
            public string Path;
            public DateTime Started;
        }

        // MCI liga o dispositivo a thread que o abriu: tudo passa por esta fila.
        readonly BlockingCollection<Action> queue = new BlockingCollection<Action>();
        readonly List<Voice> voices = new List<Voice>();
        readonly object snapshotGate = new object();
        List<string> snapshot = new List<string>();
        int nextAlias;
        readonly string cacheDir;

        public event Action PlayingChanged;

        public MciPlayer(string cacheFolder)
        {
            cacheDir = cacheFolder;
            Thread t = new Thread(Loop);
            t.IsBackground = true;
            t.SetApartmentState(ApartmentState.STA);
            t.Start();
        }

        void Loop()
        {
            while (true)
            {
                Action a;
                if (queue.TryTake(out a, 80)) a();
                Sweep();
            }
        }

        string OnThread(Func<string> f)
        {
            string value = null;
            using (ManualResetEventSlim done = new ManualResetEventSlim(false))
            {
                queue.Add(() =>
                {
                    try { value = f(); }
                    finally { done.Set(); }
                });
                if (!done.Wait(8000)) return "O Windows demorou para responder ao tocar o som.";
            }
            return value;
        }

        static string Send(string command, out int code)
        {
            StringBuilder sb = new StringBuilder(128);
            code = mciSendString(command, sb, sb.Capacity, IntPtr.Zero);
            return sb.ToString();
        }

        static string ErrorText(int code)
        {
            StringBuilder sb = new StringBuilder(256);
            return mciGetErrorString(code, sb, sb.Capacity) ? sb.ToString() : "erro " + code;
        }

        // Fecha as vozes que terminaram e avisa quem acompanha.
        void Sweep()
        {
            bool changed = false;
            foreach (Voice v in voices.ToList())
            {
                int code;
                string mode = Send("status " + v.Alias + " mode", out code);
                if (code != 0 || mode == "stopped" || mode == "not ready")
                {
                    // logo apos o play alguns formatos ainda dizem "stopped"
                    if (code == 0 && (DateTime.UtcNow - v.Started).TotalMilliseconds < 300) continue;
                    Send("close " + v.Alias, out code);
                    voices.Remove(v);
                    changed = true;
                }
            }
            if (changed) Publish();
        }

        void Publish()
        {
            lock (snapshotGate) snapshot = voices.Select(v => v.Id).Distinct().ToList();
            Action h = PlayingChanged;
            if (h != null) h();
        }

        public string Play(string id, string path, int volume)
        {
            return OnThread(() =>
            {
                int code;
                Voice v = voices.FirstOrDefault(x => x.Id == id && x.Path == path);
                if (v != null)
                {
                    // apertar de novo recomeca do inicio, sem abrir outro dispositivo
                    Send("seek " + v.Alias + " to start", out code);
                }
                else
                {
                    if (voices.Count >= MaxVoices)
                    {
                        Voice oldest = voices[0];
                        Send("close " + oldest.Alias, out code);
                        voices.RemoveAt(0);
                    }
                    v = new Voice { Id = id, Path = path, Alias = "som" + (++nextAlias) };
                    Send("open \"" + path + "\" type mpegvideo alias " + v.Alias, out code);
                    if (code != 0)
                    {
                        string clean = CopyWithoutTag(path);
                        int retry = -1;
                        if (clean != null) Send("open \"" + clean + "\" type mpegvideo alias " + v.Alias, out retry);
                        if (retry != 0) return "O Windows não conseguiu abrir este som: " + ErrorText(code);
                    }
                    voices.Add(v);
                }
                Send("setaudio " + v.Alias + " volume to " + ToMci(volume), out code);
                Send("play " + v.Alias, out code);
                if (code != 0)
                {
                    string error = ErrorText(code);
                    Send("close " + v.Alias, out code);
                    voices.Remove(v);
                    Publish();
                    return "O Windows não conseguiu tocar este som: " + error;
                }
                v.Started = DateTime.UtcNow;
                Publish();
                return null;
            });
        }

        // Onde o audio comeca depois da etiqueta ID3v2, ou 0 se nao houver etiqueta.
        public static int AudioStart(byte[] head)
        {
            if (head.Length < 10 || head[0] != 'I' || head[1] != 'D' || head[2] != '3') return 0;
            int size = ((head[6] & 127) << 21) | ((head[7] & 127) << 14) | ((head[8] & 127) << 7) | (head[9] & 127);
            return 10 + size + ((head[5] & 16) != 0 ? 10 : 0);
        }

        // O MCI recusa MP3 valido com certas etiquetas ID3v2 (erro 277). A copia sem etiqueta abre, e o arquivo do usuario nao muda.
        string CopyWithoutTag(string path)
        {
            try
            {
                string copy = System.IO.Path.Combine(cacheDir, Icons.KeyFor(path) + ".mp3");
                if (File.Exists(copy)) return copy;
                byte[] all = File.ReadAllBytes(path);
                int start = AudioStart(all);
                if (start == 0 || start >= all.Length) return null;
                Directory.CreateDirectory(cacheDir);
                using (FileStream f = new FileStream(copy, FileMode.Create, FileAccess.Write)) f.Write(all, start, all.Length - start);
                return copy;
            }
            catch (IOException e) { Log.Write("Som sem etiqueta: " + e.Message); }
            catch (UnauthorizedAccessException e) { Log.Write("Som sem etiqueta: " + e.Message); }
            return null;
        }

        // O MCI usa 0 a 1000. A curva ao quadrado acompanha melhor o ouvido do que a reta.
        static int ToMci(int volume)
        {
            double v = Math.Max(0, Math.Min(100, volume)) / 100.0;
            return (int)Math.Round(v * v * 1000);
        }

        public void SetVolume(string id, int volume)
        {
            queue.Add(() =>
            {
                int code;
                foreach (Voice v in voices.Where(x => x.Id == id)) Send("setaudio " + v.Alias + " volume to " + ToMci(volume), out code);
            });
        }

        public void Stop(string id)
        {
            queue.Add(() =>
            {
                int code;
                foreach (Voice v in voices.Where(x => x.Id == id).ToList())
                {
                    Send("close " + v.Alias, out code);
                    voices.Remove(v);
                }
                Publish();
            });
        }

        public void StopAll()
        {
            queue.Add(() =>
            {
                int code;
                foreach (Voice v in voices) Send("close " + v.Alias, out code);
                voices.Clear();
                Publish();
            });
        }

        public List<string> Playing()
        {
            lock (snapshotGate) return snapshot.ToList();
        }
    }

    // Nivel de pico da saida padrao do Windows: o medidor do app mostra o som de verdade, nao uma animacao.
    public static class OutputMeter
    {
        [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
        class MMDeviceEnumerator { }

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDeviceEnumerator
        {
            int NotImpl1();
            [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object service);
        }

        [ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        interface IAudioMeterInformation
        {
            [PreserveSig] int GetPeakValue(out float peak);
        }

        [ThreadStatic] static IAudioMeterInformation meter;
        [ThreadStatic] static DateTime nextTry;

        // Retorna de 0 a 1, ou -1 se nao houver saida de audio.
        public static float Peak()
        {
            try
            {
                if (meter == null)
                {
                    if (DateTime.UtcNow < nextTry) return -1;
                    nextTry = DateTime.UtcNow.AddSeconds(5);
                    IMMDeviceEnumerator e = (IMMDeviceEnumerator)new MMDeviceEnumerator();
                    IMMDevice device;
                    if (e.GetDefaultAudioEndpoint(0, 1, out device) != 0) return -1;
                    Guid iid = typeof(IAudioMeterInformation).GUID;
                    object service;
                    if (device.Activate(ref iid, 23, IntPtr.Zero, out service) != 0) return -1;
                    meter = (IAudioMeterInformation)service;
                }
                float peak;
                if (meter.GetPeakValue(out peak) != 0)
                {
                    meter = null; // a saida padrao mudou: pega de novo na proxima
                    return -1;
                }
                return peak;
            }
            catch (COMException)
            {
                meter = null;
                return -1;
            }
        }
    }
}
