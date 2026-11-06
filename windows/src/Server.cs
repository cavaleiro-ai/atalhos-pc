// Atende o tablet/celular por TCP (Wi-Fi e USB) e Bluetooth. Toda conexao passa pelo mesmo protocolo autenticado.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace AtalhosPC
{
    public sealed class Server
    {
        public const string DiscoveryAsk = "ATALHOSPC?1";
        const string CodeAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        const int MaxConnections = 32;
        const int MaxPairFailures = 5;
        const int MaxBrowseEntries = 3000;
        const int HandshakeTimeoutMs = 15000;
        const int SessionTimeoutMs = 45000;
        const int MaxCustomIcon = 8 * 1024 * 1024;
        const int MeterIntervalMs = 70;

        sealed class Conn
        {
            public Channel Channel;
            public string DeviceId;
            public string DeviceName;
            public string Via;
            public Action Close;
            public volatile bool WantsMeter;
        }

        readonly Store store;
        readonly object gate = new object();
        readonly List<Conn> sessions = new List<Conn>();
        int openConnections;
        volatile bool running;
        TcpListener tcp;
        UdpClient udp;
        Socket bluetooth;

        string pairCode;
        DateTime pairUntil;
        int pairFailures;

        public Func<string, string> Launcher = Launch.Open;
        public ISoundPlayer Player;
        public Library Library;
        public string TcpState = "Parado";
        public string BluetoothState = "Parado";
        public event Action StateChanged;
        public event Action<string> DevicePaired;

        public Server(Store s)
        {
            store = s;
            store.Changed += PushChanged;
            store.DeviceRemoved += DropDevice;
        }

        void NotifyState()
        {
            Action h = StateChanged;
            if (h != null) h();
        }

        public void Start()
        {
            running = true;
            if (Player != null) Player.PlayingChanged += PushPlaying;
            StartThread(MeterLoop);
            StartThread(TcpLoop);
            StartThread(UdpLoop);
            StartThread(BluetoothLoop);
        }

        public void Stop()
        {
            running = false;
            try { if (tcp != null) tcp.Stop(); } catch (SocketException) { }
            try { if (udp != null) udp.Close(); } catch (SocketException) { }
            try { if (bluetooth != null) bluetooth.Close(); } catch (SocketException) { }
            List<Conn> copy;
            lock (gate) copy = sessions.ToList();
            foreach (Conn c in copy) c.Close();
        }

        static void StartThread(ThreadStart body)
        {
            Thread t = new Thread(body);
            t.IsBackground = true;
            t.Start();
        }

        // ---------- pareamento ----------

        public string BeginPair(int seconds)
        {
            StringBuilder sb = new StringBuilder();
            while (sb.Length < 6)
            {
                byte b = Crypto.RandomBytes(1)[0];
                if (b < 248) sb.Append(CodeAlphabet[b % CodeAlphabet.Length]); // 248 = 8 x 31, sem vies
            }
            lock (gate)
            {
                pairCode = sb.ToString();
                pairUntil = DateTime.UtcNow.AddSeconds(seconds);
                pairFailures = 0;
                return pairCode;
            }
        }

        public void EndPair()
        {
            lock (gate) pairCode = null;
        }

        public int PairSecondsLeft
        {
            get
            {
                lock (gate)
                {
                    if (pairCode == null) return 0;
                    int left = (int)Math.Ceiling((pairUntil - DateTime.UtcNow).TotalSeconds);
                    if (left <= 0) pairCode = null;
                    return Math.Max(0, left);
                }
            }
        }

        string CurrentPairCode()
        {
            lock (gate) return PairSecondsLeft > 0 ? pairCode : null;
        }

        // ---------- transportes ----------

        void TcpLoop()
        {
            int port = store.Port;
            try
            {
                tcp = new TcpListener(IPAddress.Any, port);
                tcp.Start();
                TcpState = "Porta " + port + " ativa";
                NotifyState();
                while (running)
                {
                    TcpClient client = tcp.AcceptTcpClient();
                    try
                    {
                        client.NoDelay = true;
                        IPEndPoint peer = (IPEndPoint)client.Client.RemoteEndPoint;
                        string via = IPAddress.IsLoopback(peer.Address) ? "USB (adb)" : ViaOf((IPEndPoint)client.Client.LocalEndPoint);
                        Serve(client.GetStream(), client.Close, via, peer.ToString(), ms => { client.ReceiveTimeout = ms; });
                    }
                    catch (SocketException) { client.Close(); } // o aparelho desistiu antes de ser atendido
                    catch (InvalidOperationException) { client.Close(); }
                }
            }
            catch (SocketException e)
            {
                if (running)
                {
                    TcpState = "Erro na porta " + port + ": " + e.Message;
                    Log.Write(TcpState);
                    NotifyState();
                }
            }
            catch (ObjectDisposedException) { }
        }

        static string ViaOf(IPEndPoint local)
        {
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.Description.IndexOf("NDIS", StringComparison.OrdinalIgnoreCase) < 0) continue;
                foreach (UnicastIPAddressInformation a in ni.GetIPProperties().UnicastAddresses)
                {
                    if (a.Address.Equals(local.Address)) return "USB";
                }
            }
            return "Rede";
        }

        void UdpLoop()
        {
            try
            {
                udp = new UdpClient();
                // sem isto, responder a um aparelho que ja fechou a porta faz o proximo Receive falhar com WSAECONNRESET
                udp.Client.IOControl(unchecked((int)0x9800000C), new byte[] { 0, 0, 0, 0 }, null);
                udp.Client.Bind(new IPEndPoint(IPAddress.Any, store.Port));
                while (running)
                {
                    IPEndPoint from = new IPEndPoint(IPAddress.Any, 0);
                    byte[] data = udp.Receive(ref from);
                    if (Encoding.ASCII.GetString(data) != DiscoveryAsk) continue;
                    string reply = Json.Write(new Dictionary<string, object>
                    {
                        { "app", "AtalhosPC" }, { "pid", store.PcId }, { "pc", Environment.MachineName }, { "port", store.Port }
                    });
                    byte[] bytes = Crypto.Utf8(reply);
                    udp.Send(bytes, bytes.Length, from);
                }
            }
            catch (SocketException e) { if (running) Log.Write("Busca na rede parou: " + e.Message); }
            catch (ObjectDisposedException) { }
        }

        void BluetoothLoop()
        {
            string last = null;
            while (running)
            {
                try
                {
                    Socket s = new Socket((AddressFamily)Bth.AF_BTH, SocketType.Stream, (ProtocolType)Bth.BTHPROTO_RFCOMM);
                    bluetooth = s;
                    s.Bind(new Bth.EndPointBth(-1));
                    int channel = ((Bth.EndPointBth)s.LocalEndPoint).Channel;
                    Bth.Register(channel, "Atalhos PC");
                    s.Listen(4);
                    BluetoothState = "Ativo (canal " + channel + ")";
                    Log.Write("Bluetooth " + BluetoothState);
                    NotifyState();
                    while (running)
                    {
                        Socket peer = s.Accept();
                        NetworkStream ns = new NetworkStream(peer, true);
                        Serve(ns, ns.Close, "Bluetooth", "bluetooth", ms => { peer.ReceiveTimeout = ms; });
                    }
                }
                catch (SocketException e)
                {
                    if (!running) return;
                    string state = "Sem adaptador disponível (" + e.SocketErrorCode + ")";
                    if (state != last) Log.Write("Bluetooth: " + state);
                    last = state;
                    BluetoothState = state;
                    NotifyState();
                }
                catch (ObjectDisposedException) { return; }
                try { if (bluetooth != null) bluetooth.Close(); } catch (SocketException) { }
                // um adaptador USB pode ser ligado depois: tenta de novo sem precisar reabrir o programa
                for (int i = 0; i < 30 && running; i++) Thread.Sleep(1000);
            }
        }

        void Serve(Stream stream, Action close, string via, string remote, Action<int> setTimeout)
        {
            lock (gate)
            {
                if (openConnections >= MaxConnections)
                {
                    close();
                    return;
                }
                openConnections++;
            }
            StartThread(() =>
            {
                try
                {
                    setTimeout(HandshakeTimeoutMs);
                    Channel ch = new Channel(stream);
                    Dictionary<string, object> first = Json.Parse(ch.ReadPlain());
                    if (Json.Int(first, "v", 0) != 1) return;
                    string t = Json.Str(first, "t");
                    if (t == "hello") Session(ch, first, close, via, remote, setTimeout);
                    else if (t == "pair") Pair(ch, first, remote);
                }
                catch (IOException) { }
                catch (SocketException) { }
                catch (ObjectDisposedException) { }
                catch (InvalidDataException) { }
                catch (FormatException) { }
                catch (ArgumentException) { }
                catch (InvalidOperationException) { }
                finally
                {
                    try { close(); } catch (Exception) { }
                    lock (gate) openConnections--;
                }
            });
        }

        // ---------- pareamento e sessao ----------

        void Pair(Channel ch, Dictionary<string, object> first, string remote)
        {
            string dev = Json.Str(first, "dev");
            string name = Json.Str(first, "name") ?? "Aparelho";
            byte[] cn = Json.B64(first, "cn");
            if (string.IsNullOrEmpty(dev) || dev.Length > 64 || cn.Length != 16) return;
            if (name.Length > 80) name = name.Substring(0, 80);

            string code = CurrentPairCode();
            if (code == null)
            {
                ch.WritePlain(Json.Write(new Dictionary<string, object> { { "t", "pair_closed" } }));
                return;
            }
            byte[] sn = Crypto.RandomBytes(16);
            ch.WritePlain(Json.Write(new Dictionary<string, object>
            {
                { "t", "pair_ch" }, { "sn", Convert.ToBase64String(sn) }, { "pid", store.PcId }, { "pc", Environment.MachineName }, { "port", store.Port }
            }));

            Dictionary<string, object> answer = Json.Parse(ch.ReadPlain());
            if (Json.Str(answer, "t") != "pair_proof") return;
            byte[] key = Crypto.Pbkdf2(code, Concat(cn, sn), Crypto.PairIterations);
            bool right = Crypto.Same(Json.B64(answer, "proof"), Crypto.Hmac(key, Crypto.Utf8("client"), cn, sn));

            int left = 0;
            lock (gate)
            {
                if (pairCode != code || PairSecondsLeft == 0) right = false;
                if (!right)
                {
                    pairFailures++;
                    left = MaxPairFailures - pairFailures;
                    if (left <= 0) pairCode = null;
                }
                else pairCode = null; // o codigo vale para um unico aparelho
            }
            if (!right)
            {
                Log.Write("Pareamento recusado de " + remote + " (código errado)");
                ch.WritePlain(Json.Write(new Dictionary<string, object> { { "t", "pair_bad" }, { "left", Math.Max(0, left) } }));
                NotifyState();
                return;
            }

            byte[] secret = Crypto.RandomBytes(32);
            store.AddDevice(dev, name, secret);
            DropDevice(dev); // o segredo novo invalida as sessoes antigas deste aparelho
            byte[] box = Crypto.Seal(Crypto.Hmac(key, Crypto.Utf8("box-enc")), Crypto.Hmac(key, Crypto.Utf8("box-mac")), 0, secret);
            ch.WritePlain(Json.Write(new Dictionary<string, object>
            {
                { "t", "pair_ok" },
                { "proof", Convert.ToBase64String(Crypto.Hmac(key, Crypto.Utf8("server"), cn, sn)) },
                { "box", Convert.ToBase64String(box) }
            }));
            Log.Write("Pareado: " + name);
            Action<string> h = DevicePaired;
            if (h != null) h(name);
            NotifyState();
        }

        void Session(Channel ch, Dictionary<string, object> first, Action close, string via, string remote, Action<int> setTimeout)
        {
            string dev = Json.Str(first, "dev");
            byte[] cn = Json.B64(first, "cn");
            if (string.IsNullOrEmpty(dev) || cn.Length != 16) return;

            byte[] secret = store.DeviceSecret(dev);
            byte[] sn = Crypto.RandomBytes(16);
            ch.WritePlain(Json.Write(new Dictionary<string, object>
            {
                { "t", "hello" }, { "v", 1 }, { "pid", store.PcId }, { "pc", Environment.MachineName }, { "port", store.Port },
                { "known", secret != null }, { "sn", Convert.ToBase64String(sn) }
            }));
            if (secret == null) return;

            ch.Secure(secret, cn, sn, true);
            if (Json.Str(Json.Parse(ch.Read()), "t") != "auth") return; // assinatura errada ja derruba em Read()
            ch.Write(Json.Write(new Dictionary<string, object> { { "t", "ok" } }));
            ch.AllowLargeFrames();
            setTimeout(SessionTimeoutMs);

            string deviceName = store.Devices().Where(d => d.id == dev).Select(d => d.name).FirstOrDefault() ?? dev;
            Conn conn = new Conn { Channel = ch, DeviceId = dev, DeviceName = deviceName, Via = via, Close = close };
            lock (gate) sessions.Add(conn);
            Log.Write("Conectado: " + deviceName + " via " + via + " (" + remote + ")");
            NotifyState();
            try
            {
                while (running)
                {
                    Dictionary<string, object> q = Json.Parse(ch.Read());
                    ThreadPool.QueueUserWorkItem(_ => Answer(conn, q));
                }
            }
            finally
            {
                lock (gate) sessions.Remove(conn);
                Log.Write("Desconectado: " + deviceName);
                NotifyState();
            }
        }

        void Answer(Conn conn, Dictionary<string, object> q)
        {
            Dictionary<string, object> reply;
            try { reply = Dispatch(conn, q); }
            catch (Exception e) { reply = Fail(e.Message); }
            reply["r"] = Json.Int(q, "r", 0);
            try { conn.Channel.Write(Json.Write(reply)); }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (InvalidOperationException) { }
        }

        static Dictionary<string, object> Ok()
        {
            return new Dictionary<string, object> { { "ok", true } };
        }

        static Dictionary<string, object> Fail(string message)
        {
            return new Dictionary<string, object> { { "ok", false }, { "err", message } };
        }

        Dictionary<string, object> Describe(Shortcut s)
        {
            bool isDir = Directory.Exists(s.path);
            bool exists = isDir || File.Exists(s.path);
            return new Dictionary<string, object>
            {
                { "id", s.id }, { "name", s.name }, { "path", s.path }, { "dir", isDir }, { "missing", !exists }, { "icon", Icons.KeyFor(s.path) }
            };
        }

        Dictionary<string, object> Dispatch(Conn conn, Dictionary<string, object> q)
        {
            string t = Json.Str(q, "t");
            string id = Json.Str(q, "id");
            string path = Json.Str(q, "path");
            Dictionary<string, object> reply = Ok();
            Shortcut s;
            switch (t)
            {
                case "ping":
                    return reply;
                case "list":
                    reply["items"] = store.Shortcuts().Select(x => (object)Describe(x)).ToArray();
                    reply["browse"] = store.AllowBrowse;
                    reply["sounds"] = store.Sounds().Select(x => (object)Describe(x)).ToArray();
                    reply["master"] = store.MasterVolume;
                    reply["muted"] = store.MasterMuted;
                    reply["playing"] = Player == null ? new object[0] : Player.Playing().Cast<object>().ToArray();
                    return reply;
                case "run":
                    s = store.Find(id);
                    if (s == null) return Fail("Este atalho não existe mais no PC.");
                    return Launched(conn, s.path, s.name);
                case "remove":
                    if (store.FindSound(id) != null)
                    {
                        if (Player != null) Player.Stop(id);
                        store.RemoveSound(id);
                        return reply;
                    }
                    return store.Remove(id) ? reply : Fail("Este atalho não existe mais no PC.");
                case "rename":
                    return store.Rename(id, Json.Str(q, "name")) || store.RenameSound(id, Json.Str(q, "name")) ? reply : Fail("Nome inválido.");
                case "move":
                    return store.Move(id, Json.Int(q, "to", 0)) || store.MoveSound(id, Json.Int(q, "to", 0)) ? reply : Fail("Este atalho não existe mais no PC.");
                case "icon":
                    return Icon(q, id, path);
                case "sound_play":
                    return PlaySound(conn, id);
                case "sound_stop":
                    if (Player != null)
                    {
                        if (string.IsNullOrEmpty(id)) Player.StopAll();
                        else Player.Stop(id);
                    }
                    return reply;
                case "sound_volume":
                    return SoundVolume(id, Json.Int(q, "volume", 80), Json.Str(q, "final") == "True");
                case "sound_master":
                    return Master(Json.Int(q, "volume", store.MasterVolume), Json.Str(q, "muted") == "True", Json.Str(q, "final") == "True");
                case "sound_seticon":
                    if (Json.Str(q, "png") != null)
                    {
                        byte[] png = Json.B64(q, "png");
                        if (png.Length > MaxCustomIcon) return Fail("Imagem grande demais.");
                        return SetIcon(id, png);
                    }
                    break;
                case "meter":
                    conn.WantsMeter = Json.Str(q, "on") == "True";
                    return reply;
                // a biblioteca so alcanca a propria pasta: nao depende da navegacao pelos arquivos
                case "lib_categories":
                    return LibraryCategories();
                case "lib_list":
                    return LibraryList(q);
                case "lib_icon":
                    return LibraryIcon(Json.Str(q, "cat"));
                case "lib_preview":
                    return LibraryPreviewAsked(Json.Str(q, "cat"), Json.Str(q, "file"));
                case "lib_stop":
                    StopPreview();
                    return reply;
                case "lib_add":
                    return LibraryAdd(Json.Str(q, "cat"), Json.Str(q, "file"));
            }
            // daqui para baixo o aparelho enxerga e abre arquivos fora da lista: o dono pode desligar isso no PC
            if (t != "places" && t != "browse" && t != "add" && t != "open" && t != "sound_add" && t != "sound_seticon") return Fail("Comando desconhecido.");
            if (!store.AllowBrowse) return Fail("A navegação pelos arquivos está desligada no PC.");
            switch (t)
            {
                case "places":
                    reply["entries"] = Places();
                    return reply;
                case "browse":
                    return Browse(RequirePath(path));
                case "add":
                    path = RequirePath(path);
                    if (!File.Exists(path) && !Directory.Exists(path)) return Fail("Arquivo não encontrado no PC.");
                    reply["item"] = Describe(store.Add(path));
                    return reply;
                case "sound_add":
                    path = RequirePath(path);
                    if (!File.Exists(path)) return Fail("Arquivo não encontrado no PC.");
                    if (!Store.IsAudio(path)) return Fail("Use um arquivo de áudio: " + string.Join(", ", Store.AudioExtensions) + ".");
                    reply["item"] = Describe(store.AddSound(path, null));
                    return reply;
                case "sound_seticon":
                    path = RequirePath(path);
                    if (!File.Exists(path) || new FileInfo(path).Length > MaxCustomIcon) return Fail("Imagem não encontrada ou grande demais.");
                    return SetIcon(id, File.ReadAllBytes(path));
                default:
                    path = RequirePath(path);
                    return Launched(conn, path, Path.GetFileName(path));
            }
        }

        Dictionary<string, object> Describe(SoundItem s)
        {
            bool hasIcon = !string.IsNullOrEmpty(s.icon) && File.Exists(s.icon);
            return new Dictionary<string, object>
            {
                { "id", s.id }, { "name", s.name }, { "path", s.path }, { "dir", false }, { "missing", !File.Exists(s.path) },
                { "icon", hasIcon ? Icons.KeyFor(s.icon) : "" }, { "volume", s.volume }
            };
        }

        Dictionary<string, object> PlaySound(Conn conn, string id)
        {
            SoundItem s = store.FindSound(id);
            if (s == null) return Fail("Este som não existe mais no PC.");
            if (!File.Exists(s.path)) return Fail("Arquivo não encontrado no PC: " + s.path);
            if (Player == null) return Fail("Som indisponível neste PC.");
            string error = Player.Play(s.id, s.path, store.Effective(s));
            if (error != null) Log.Write("Som falhou: " + s.name + " - " + error);
            return error == null ? Ok() : Fail(error);
        }

        Dictionary<string, object> SoundVolume(string id, int volume, bool final)
        {
            if (!store.SetSoundVolume(id, volume, final)) return Fail("Este som não existe mais no PC.");
            SoundItem s = store.FindSound(id);
            if (Player != null) Player.SetVolume(id, store.Effective(s));
            return Ok();
        }

        Dictionary<string, object> Master(int volume, bool muted, bool final)
        {
            store.SetMaster(volume, muted, final);
            if (Player != null)
            {
                foreach (string id in Player.Playing())
                {
                    SoundItem s = store.FindSound(id);
                    if (s != null) Player.SetVolume(id, store.Effective(s));
                }
            }
            return Ok();
        }

        // ---------- biblioteca ----------

        const int MaxLibraryPage = 200;
        const string PreviewId = "biblioteca-previa";
        const int PreviewVolume = 80;

        Dictionary<string, object> LibraryCategories()
        {
            Dictionary<string, object> reply = Ok();
            List<LibraryCategory> found = Library == null ? new List<LibraryCategory>() : Library.Categories();
            reply["categories"] = found.Select(c => (object)new Dictionary<string, object>
            {
                { "cat", c.Folder }, { "name", c.Title }, { "count", c.Items.Count }, { "icon", c.Icon != null }
            }).ToArray();
            reply["total"] = found.Sum(c => c.Items.Count);
            return reply;
        }

        Dictionary<string, object> LibraryList(Dictionary<string, object> q)
        {
            if (Library == null) return Fail("A biblioteca não está instalada neste PC.");
            List<LibraryItem> found = Library.Search(Json.Str(q, "cat"), Json.Str(q, "q"));
            int from = Math.Max(0, Json.Int(q, "from", 0));
            int limit = Math.Max(1, Math.Min(MaxLibraryPage, Json.Int(q, "limit", 100)));
            HashSet<string> added = new HashSet<string>(store.Sounds().Select(s => s.path), StringComparer.OrdinalIgnoreCase);
            Dictionary<string, object> reply = Ok();
            reply["total"] = found.Count;
            reply["items"] = found.Skip(from).Take(limit).Select(i => (object)new Dictionary<string, object>
            {
                { "cat", i.Category }, { "file", i.File }, { "name", i.Name }, { "catName", Library.TitleOf(i.Category) }, { "added", added.Contains(i.Path) }
            }).ToArray();
            return reply;
        }

        Dictionary<string, object> LibraryIcon(string category)
        {
            LibraryCategory c = Library == null || category == null ? null : Library.FindCategory(category);
            if (c == null || c.Icon == null) return Fail("Sem ícone.");
            Dictionary<string, object> reply = Ok();
            reply["png"] = Convert.ToBase64String(File.ReadAllBytes(c.Icon));
            return reply;
        }

        Dictionary<string, object> LibraryPreviewAsked(string category, string file)
        {
            LibraryItem item = Library == null ? null : Library.Find(category, file);
            if (item == null) return Fail("Este som não está na biblioteca.");
            string error = PlayPreview(item);
            return error == null ? Ok() : Fail(error);
        }

        // Toca um por vez, sem entrar na lista de sons, com o volume geral e o mudo valendo.
        public string PlayPreview(LibraryItem item)
        {
            if (Player == null) return "Som indisponível neste PC.";
            if (!File.Exists(item.Path)) return "Arquivo não encontrado no PC: " + item.Path;
            Player.Stop(PreviewId);
            string error = Player.Play(PreviewId, item.Path, store.Effective(new SoundItem { volume = PreviewVolume }));
            if (error != null) Log.Write("Som falhou: " + item.Name + " - " + error);
            return error;
        }

        public void StopPreview()
        {
            if (Player != null) Player.Stop(PreviewId);
        }

        Dictionary<string, object> LibraryAdd(string category, string file)
        {
            LibraryItem item = Library == null ? null : Library.Find(category, file);
            if (item == null) return Fail("Este som não está na biblioteca.");
            Dictionary<string, object> reply = Ok();
            reply["item"] = Describe(Library.AddTo(store, item));
            return reply;
        }

        Dictionary<string, object> SetIcon(string id, byte[] image)
        {
            if (store.FindSound(id) == null) return Fail("Este som não existe mais no PC.");
            string target = Path.Combine(store.SoundIconDir, id + ".png");
            try { Icons.SaveCustom(image, target); }
            catch (ArgumentException) { return Fail("Este arquivo não é uma imagem válida."); }
            catch (OutOfMemoryException) { return Fail("Este arquivo não é uma imagem válida."); }
            store.SetSoundIcon(id, target);
            return Ok();
        }

        Dictionary<string, object> Launched(Conn conn, string path, string name)
        {
            string error = Launcher(path);
            Log.Write((error == null ? "Abriu: " : "Falhou: ") + name + " (pedido de " + conn.DeviceName + " via " + conn.Via + ")" + (error == null ? "" : " - " + error));
            return error == null ? Ok() : Fail(error);
        }

        Dictionary<string, object> Icon(Dictionary<string, object> q, string id, string path)
        {
            string target;
            if (!string.IsNullOrEmpty(id))
            {
                SoundItem sound = store.FindSound(id);
                if (sound != null)
                {
                    if (string.IsNullOrEmpty(sound.icon) || !File.Exists(sound.icon)) return Fail("Sem ícone.");
                    Dictionary<string, object> own = Ok();
                    own["key"] = Icons.KeyFor(sound.icon);
                    own["png"] = Convert.ToBase64String(File.ReadAllBytes(sound.icon));
                    return own;
                }
                Shortcut s = store.Find(id);
                if (s == null) return Fail("Este atalho não existe mais no PC.");
                target = s.path;
            }
            else
            {
                if (!store.AllowBrowse) return Fail("A navegação pelos arquivos está desligada no PC.");
                target = RequirePath(path);
            }
            byte[] png = Icons.GetPng(target);
            if (png == null) return Fail("Sem ícone.");
            Dictionary<string, object> reply = Ok();
            reply["key"] = Icons.KeyFor(target);
            reply["png"] = Convert.ToBase64String(png);
            return reply;
        }

        static string RequirePath(string path)
        {
            if (string.IsNullOrEmpty(path) || !Path.IsPathRooted(path)) throw new InvalidDataException("Caminho inválido.");
            return Path.GetFullPath(path);
        }

        static object[] Places()
        {
            List<object> list = new List<object>();
            Action<string, string> add = (name, path) =>
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    list.Add(new Dictionary<string, object> { { "name", name }, { "path", path }, { "dir", true } });
            };
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            add("Área de Trabalho", Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory));
            add("Programas do Menu Iniciar", Environment.GetFolderPath(Environment.SpecialFolder.Programs));
            add("Programas (todos os usuários)", Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms));
            add("Documentos", Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));
            add("Downloads", Path.Combine(home, "Downloads"));
            add("Imagens", Environment.GetFolderPath(Environment.SpecialFolder.MyPictures));
            add("Músicas", Environment.GetFolderPath(Environment.SpecialFolder.MyMusic));
            add("Vídeos", Environment.GetFolderPath(Environment.SpecialFolder.MyVideos));
            foreach (DriveInfo d in DriveInfo.GetDrives())
            {
                if (d.IsReady) add("Disco " + d.Name.TrimEnd('\\'), d.RootDirectory.FullName);
            }
            return list.ToArray();
        }

        static Dictionary<string, object> Browse(string path)
        {
            DirectoryInfo dir = new DirectoryInfo(path);
            if (!dir.Exists) return Fail("Pasta não encontrada no PC.");
            List<FileSystemInfo> found = new List<FileSystemInfo>();
            try
            {
                foreach (FileSystemInfo f in dir.EnumerateFileSystemInfos())
                {
                    if ((f.Attributes & (FileAttributes.Hidden | FileAttributes.System)) != 0) continue;
                    found.Add(f);
                }
            }
            catch (UnauthorizedAccessException) { return Fail("O Windows não deu permissão para abrir esta pasta."); }

            bool cut = found.Count > MaxBrowseEntries;
            object[] entries = found
                .OrderBy(f => f is DirectoryInfo ? 0 : 1)
                .ThenBy(f => f.Name, StringComparer.CurrentCultureIgnoreCase)
                .Take(MaxBrowseEntries)
                .Select(f => (object)new Dictionary<string, object> { { "name", f.Name }, { "path", f.FullName }, { "dir", f is DirectoryInfo } })
                .ToArray();
            Dictionary<string, object> reply = Ok();
            reply["entries"] = entries;
            reply["cut"] = cut;
            return reply;
        }

        // ---------- avisos e sessoes ativas ----------

        public List<string> ActiveSessions()
        {
            lock (gate) return sessions.Select(c => c.DeviceName + " via " + c.Via).ToList();
        }

        void PushPlaying()
        {
            string message = Json.Write(new Dictionary<string, object> { { "t", "sound_state" }, { "playing", Player.Playing().Cast<object>().ToArray() } });
            PushAll(message);
        }

        void PushAll(string message)
        {
            List<Conn> copy;
            lock (gate) copy = sessions.ToList();
            foreach (Conn c in copy)
            {
                Conn target = c;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { target.Channel.Write(message); }
                    catch (Exception) { }
                });
            }
        }

        // Le o nivel da saida so enquanto algum aparelho estiver olhando a area de sons.
        void MeterLoop()
        {
            float last = -2;
            int quiet = 0;
            while (running)
            {
                Thread.Sleep(MeterIntervalMs);
                List<Conn> watching;
                lock (gate) watching = sessions.Where(c => c.WantsMeter).ToList();
                if (watching.Count == 0) continue;
                float peak = OutputMeter.Peak();
                // em silencio nao ha por que ficar enviando: so um aviso de vez em quando
                if (Math.Abs(peak - last) < 0.004f && ++quiet < 25) continue;
                quiet = 0;
                last = peak;
                string message = Json.Write(new Dictionary<string, object> { { "t", "meter" }, { "p", Math.Round(peak, 3) } });
                foreach (Conn c in watching)
                {
                    try { c.Channel.Write(message); }
                    catch (Exception) { }
                }
            }
        }

        void PushChanged()
        {
            string message = Json.Write(new Dictionary<string, object> { { "t", "changed" } });
            List<Conn> copy;
            lock (gate) copy = sessions.ToList();
            foreach (Conn c in copy)
            {
                Conn target = c;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try { target.Channel.Write(message); }
                    catch (Exception) { }
                });
            }
        }

        void DropDevice(string id)
        {
            List<Conn> copy;
            lock (gate) copy = sessions.Where(c => c.DeviceId == id).ToList();
            foreach (Conn c in copy) c.Close();
        }

        static byte[] Concat(byte[] a, byte[] b)
        {
            byte[] o = new byte[a.Length + b.Length];
            Buffer.BlockCopy(a, 0, o, 0, a.Length);
            Buffer.BlockCopy(b, 0, o, a.Length, b.Length);
            return o;
        }
    }
}
