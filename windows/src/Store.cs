// Configuracao em disco: atalhos, aparelhos pareados e preferencias.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace AtalhosPC
{
    public sealed class Shortcut
    {
        public string id;
        public string name;
        public string path;
    }

    public sealed class Device
    {
        public string id;
        public string name;
        public string secret; // protegido com DPAPI: so este usuario do Windows consegue abrir
        public string added;
    }

    public sealed class SoundItem
    {
        public string id;
        public string name;
        public string path;
        public string icon; // PNG proprio do som, ou null
        public int volume = 80;
    }

    public sealed class Config
    {
        public string pcId;
        public int port = 47810;
        public bool allowBrowse = true;
        public bool adbReverse = false;
        public List<Shortcut> shortcuts = new List<Shortcut>();
        public List<Device> devices = new List<Device>();
        public List<SoundItem> sounds = new List<SoundItem>();
        public int masterVolume = 100;
        public bool masterMuted = false;
        public bool soundsSeeded = false;
        public bool greeted = false; // a saudacao do Cavaleiro AI ja apareceu neste PC
    }

    public static class Log
    {
        static readonly object Gate = new object();
        static readonly Queue<string> Recent = new Queue<string>();
        static string file;

        public static void Init(string dir) { file = Path.Combine(dir, "atalhos.log"); }

        public static void Write(string text)
        {
            string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text;
            lock (Gate)
            {
                Recent.Enqueue(line);
                while (Recent.Count > 200) Recent.Dequeue();
                if (file == null) return;
                try
                {
                    FileInfo fi = new FileInfo(file);
                    if (fi.Exists && fi.Length > 512 * 1024) File.Copy(file, file + ".1", true);
                    if (fi.Exists && fi.Length > 512 * 1024) File.Delete(file);
                    File.AppendAllText(file, line + Environment.NewLine, Encoding.UTF8);
                }
                catch (IOException) { }
            }
        }

        public static string[] Tail()
        {
            lock (Gate) return Recent.ToArray();
        }
    }

    public sealed class Store
    {
        readonly object gate = new object();
        readonly string file;
        Config cfg;

        public readonly string Dir;
        public event Action Changed;
        public event Action<string> DeviceRemoved;

        public Store(string dir)
        {
            Dir = dir;
            Directory.CreateDirectory(dir);
            file = Path.Combine(dir, "config.json");
            Load();
        }

        public static string DefaultDir()
        {
            string over = Environment.GetEnvironmentVariable("ATALHOSPC_DATA");
            if (!string.IsNullOrEmpty(over)) return over;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtalhosPC");
        }

        void Load()
        {
            try
            {
                if (File.Exists(file)) cfg = Json.Read<Config>(File.ReadAllText(file, Encoding.UTF8));
            }
            catch (Exception e)
            {
                string bad = file + ".invalido-" + DateTime.Now.ToString("yyyyMMddHHmmss");
                try { File.Move(file, bad); } catch (IOException) { }
                Log.Write("Configuracao ilegivel, guardada em " + bad + ": " + e.Message);
            }
            if (cfg == null) cfg = new Config();
            if (cfg.shortcuts == null) cfg.shortcuts = new List<Shortcut>();
            if (cfg.devices == null) cfg.devices = new List<Device>();
            if (cfg.sounds == null) cfg.sounds = new List<SoundItem>();
            if (string.IsNullOrEmpty(cfg.pcId))
            {
                cfg.pcId = Guid.NewGuid().ToString("N");
                Save();
            }
        }

        void Save()
        {
            string tmp = file + ".tmp";
            File.WriteAllText(tmp, Json.Write(cfg), new UTF8Encoding(false));
            if (File.Exists(file)) File.Replace(tmp, file, null);
            else File.Move(tmp, file);
        }

        void Commit()
        {
            Save();
            Action h = Changed;
            if (h != null) h();
        }

        public string PcId { get { lock (gate) return cfg.pcId; } }
        public int Port { get { lock (gate) return cfg.port; } }

        public void SetPort(int port)
        {
            lock (gate) { cfg.port = port; Save(); }
        }

        public bool AllowBrowse
        {
            get { lock (gate) return cfg.allowBrowse; }
            set { lock (gate) { cfg.allowBrowse = value; Save(); } }
        }

        public bool Greeted
        {
            get { lock (gate) return cfg.greeted; }
            set { lock (gate) { cfg.greeted = value; Save(); } }
        }

        public bool AdbReverse
        {
            get { lock (gate) return cfg.adbReverse; }
            set { lock (gate) { cfg.adbReverse = value; Save(); } }
        }

        public List<Shortcut> Shortcuts()
        {
            lock (gate) return cfg.shortcuts.Select(s => new Shortcut { id = s.id, name = s.name, path = s.path }).ToList();
        }

        public Shortcut Find(string id)
        {
            lock (gate)
            {
                Shortcut s = cfg.shortcuts.FirstOrDefault(x => x.id == id);
                return s == null ? null : new Shortcut { id = s.id, name = s.name, path = s.path };
            }
        }

        public static string DisplayName(string path)
        {
            string trimmed = path.TrimEnd('\\', '/');
            if (trimmed.Length == 2 && trimmed[1] == ':') return "Disco " + trimmed.ToUpperInvariant();
            string ext = Path.GetExtension(trimmed).ToLowerInvariant();
            bool hide = ext == ".lnk" || ext == ".url" || ext == ".exe" || ext == ".bat" || ext == ".cmd" || ext == ".appref-ms";
            return hide ? Path.GetFileNameWithoutExtension(trimmed) : Path.GetFileName(trimmed);
        }

        public Shortcut Add(string path)
        {
            lock (gate)
            {
                string full = Path.GetFullPath(path);
                Shortcut old = cfg.shortcuts.FirstOrDefault(x => string.Equals(x.path, full, StringComparison.OrdinalIgnoreCase));
                if (old != null) return old;
                Shortcut s = new Shortcut { id = Guid.NewGuid().ToString("N"), name = DisplayName(full), path = full };
                cfg.shortcuts.Add(s);
                Commit();
                return s;
            }
        }

        public bool Remove(string id)
        {
            lock (gate)
            {
                if (cfg.shortcuts.RemoveAll(x => x.id == id) == 0) return false;
                Commit();
                return true;
            }
        }

        public bool Rename(string id, string name)
        {
            lock (gate)
            {
                Shortcut s = cfg.shortcuts.FirstOrDefault(x => x.id == id);
                if (s == null || string.IsNullOrWhiteSpace(name)) return false;
                s.name = name.Trim();
                Commit();
                return true;
            }
        }

        public bool Move(string id, int index)
        {
            lock (gate)
            {
                Shortcut s = cfg.shortcuts.FirstOrDefault(x => x.id == id);
                if (s == null) return false;
                cfg.shortcuts.Remove(s);
                cfg.shortcuts.Insert(Math.Max(0, Math.Min(index, cfg.shortcuts.Count)), s);
                Commit();
                return true;
            }
        }

        // ---------- sons ----------

        public static readonly string[] AudioExtensions = { ".mp3", ".wav", ".wma", ".m4a", ".aac" };

        public static bool IsAudio(string path)
        {
            return AudioExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
        }

        static SoundItem Copy(SoundItem s)
        {
            return new SoundItem { id = s.id, name = s.name, path = s.path, icon = s.icon, volume = s.volume };
        }

        public List<SoundItem> Sounds()
        {
            lock (gate) return cfg.sounds.Select(Copy).ToList();
        }

        public SoundItem FindSound(string id)
        {
            lock (gate)
            {
                SoundItem s = cfg.sounds.FirstOrDefault(x => x.id == id);
                return s == null ? null : Copy(s);
            }
        }

        public SoundItem AddSound(string path, string icon)
        {
            lock (gate)
            {
                string full = Path.GetFullPath(path);
                SoundItem old = cfg.sounds.FirstOrDefault(x => string.Equals(x.path, full, StringComparison.OrdinalIgnoreCase));
                if (old != null) return Copy(old);
                SoundItem s = new SoundItem { id = Guid.NewGuid().ToString("N"), name = Path.GetFileNameWithoutExtension(full), path = full, icon = icon };
                cfg.sounds.Add(s);
                Commit();
                return Copy(s);
            }
        }

        public bool RemoveSound(string id)
        {
            string icon;
            lock (gate)
            {
                SoundItem s = cfg.sounds.FirstOrDefault(x => x.id == id);
                if (s == null) return false;
                icon = s.icon;
                cfg.sounds.Remove(s);
                Commit();
            }
            // o icone proprio so existe para este som; o audio do usuario continua onde estava
            try { if (!string.IsNullOrEmpty(icon) && icon.StartsWith(SoundIconDir, StringComparison.OrdinalIgnoreCase)) File.Delete(icon); }
            catch (IOException) { }
            return true;
        }

        public bool RenameSound(string id, string name)
        {
            lock (gate)
            {
                SoundItem s = cfg.sounds.FirstOrDefault(x => x.id == id);
                if (s == null || string.IsNullOrWhiteSpace(name)) return false;
                s.name = name.Trim();
                Commit();
                return true;
            }
        }

        public bool MoveSound(string id, int index)
        {
            lock (gate)
            {
                SoundItem s = cfg.sounds.FirstOrDefault(x => x.id == id);
                if (s == null) return false;
                cfg.sounds.Remove(s);
                cfg.sounds.Insert(Math.Max(0, Math.Min(index, cfg.sounds.Count)), s);
                Commit();
                return true;
            }
        }

        // Durante o arraste do fader so muda na memoria; "final" grava e avisa os outros aparelhos.
        public bool SetSoundVolume(string id, int volume, bool final)
        {
            lock (gate)
            {
                SoundItem s = cfg.sounds.FirstOrDefault(x => x.id == id);
                if (s == null) return false;
                s.volume = Math.Max(0, Math.Min(100, volume));
                if (final) Commit();
                return true;
            }
        }

        public string SoundIconDir { get { return Path.Combine(Dir, "icones-sons"); } }

        public bool SetSoundIcon(string id, string iconPath)
        {
            lock (gate)
            {
                SoundItem s = cfg.sounds.FirstOrDefault(x => x.id == id);
                if (s == null) return false;
                s.icon = iconPath;
                Commit();
                return true;
            }
        }

        public int MasterVolume { get { lock (gate) return cfg.masterVolume; } }
        public bool MasterMuted { get { lock (gate) return cfg.masterMuted; } }

        public void SetMaster(int volume, bool muted, bool final)
        {
            lock (gate)
            {
                cfg.masterVolume = Math.Max(0, Math.Min(100, volume));
                cfg.masterMuted = muted;
                if (final) Commit();
            }
        }

        public int Effective(SoundItem s)
        {
            lock (gate) return cfg.masterMuted ? 0 : s.volume * cfg.masterVolume / 100;
        }

        // Na primeira vez, copia os sons de exemplo da pasta "sons" ao lado do programa. Cada audio pode ter um PNG de mesmo nome.
        public void SeedSounds(string folder)
        {
            lock (gate)
            {
                if (cfg.soundsSeeded || !Directory.Exists(folder)) return;
                string target = Path.Combine(Dir, "sons");
                Directory.CreateDirectory(target);
                Directory.CreateDirectory(SoundIconDir);
                foreach (string f in Directory.GetFiles(folder).Where(IsAudio).OrderBy(x => x, StringComparer.CurrentCultureIgnoreCase))
                {
                    string copy = Path.Combine(target, Path.GetFileName(f));
                    File.Copy(f, copy, true);
                    string id = Guid.NewGuid().ToString("N");
                    string png = Path.ChangeExtension(f, ".png");
                    string icon = null;
                    if (File.Exists(png))
                    {
                        icon = Path.Combine(SoundIconDir, id + ".png");
                        File.Copy(png, icon, true);
                    }
                    cfg.sounds.Add(new SoundItem { id = id, name = Path.GetFileNameWithoutExtension(f), path = copy, icon = icon });
                }
                cfg.soundsSeeded = true;
                Commit();
            }
        }

        public List<Device> Devices()
        {
            lock (gate) return cfg.devices.Select(d => new Device { id = d.id, name = d.name, added = d.added }).ToList();
        }

        public bool HasDevice(string id)
        {
            lock (gate) return cfg.devices.Any(d => d.id == id);
        }

        public byte[] DeviceSecret(string id)
        {
            lock (gate)
            {
                Device d = cfg.devices.FirstOrDefault(x => x.id == id);
                if (d == null) return null;
                return ProtectedData.Unprotect(Convert.FromBase64String(d.secret), null, DataProtectionScope.CurrentUser);
            }
        }

        public void AddDevice(string id, string name, byte[] secret)
        {
            lock (gate)
            {
                cfg.devices.RemoveAll(d => d.id == id);
                cfg.devices.Add(new Device
                {
                    id = id,
                    name = name,
                    secret = Convert.ToBase64String(ProtectedData.Protect(secret, null, DataProtectionScope.CurrentUser)),
                    added = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
                });
                Commit();
            }
        }

        public void RemoveDevice(string id)
        {
            lock (gate)
            {
                if (cfg.devices.RemoveAll(d => d.id == id) == 0) return;
                Commit();
            }
            Action<string> h = DeviceRemoved;
            if (h != null) h(id);
        }
    }
}
