// Testa o servidor real em uma pasta temporaria e na interface local, incluindo os ataques que ele precisa recusar.
// Nao abre nada no PC: o Launcher e trocado por um registro.
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace AtalhosPC
{
    static class ProtocolTest
    {
        const int Port = 47899;
        static int failures;
        static int passes;

        static void Check(string name, bool ok)
        {
            Console.WriteLine((ok ? "  ok    " : "  FALHA ") + name);
            if (ok) passes++; else failures++;
        }

        sealed class Client : IDisposable
        {
            public readonly TcpClient Tcp;
            public readonly Channel Ch;
            int nextId = 1;

            public Client()
            {
                Tcp = new TcpClient("127.0.0.1", Port);
                Tcp.ReceiveTimeout = 8000;
                Ch = new Channel(Tcp.GetStream());
            }

            public void Dispose() { Tcp.Close(); }

            public Dictionary<string, object> Ask(Dictionary<string, object> q)
            {
                int id = nextId++;
                q["r"] = id;
                Ch.Write(Json.Write(q));
                while (true)
                {
                    Dictionary<string, object> a = Json.Parse(Ch.Read());
                    if (Json.Int(a, "r", -1) == id) return a;
                }
            }
        }

        static Dictionary<string, object> D(params object[] kv)
        {
            Dictionary<string, object> d = new Dictionary<string, object>();
            for (int i = 0; i < kv.Length; i += 2) d[(string)kv[i]] = kv[i + 1];
            return d;
        }

        static byte[] TryPair(string dev, string code, out string outcome)
        {
            outcome = null;
            using (Client c = new Client())
            {
                byte[] cn = Crypto.RandomBytes(16);
                c.Ch.WritePlain(Json.Write(D("t", "pair", "v", 1, "dev", dev, "name", "Teste", "cn", Convert.ToBase64String(cn))));
                Dictionary<string, object> ch = Json.Parse(c.Ch.ReadPlain());
                outcome = Json.Str(ch, "t");
                if (outcome != "pair_ch") return null;
                byte[] sn = Json.B64(ch, "sn");
                byte[] salt = new byte[32];
                Buffer.BlockCopy(cn, 0, salt, 0, 16);
                Buffer.BlockCopy(sn, 0, salt, 16, 16);
                byte[] key = Crypto.Pbkdf2(code, salt, Crypto.PairIterations);
                c.Ch.WritePlain(Json.Write(D("t", "pair_proof", "proof", Convert.ToBase64String(Crypto.Hmac(key, Crypto.Utf8("client"), cn, sn)))));
                Dictionary<string, object> r = Json.Parse(c.Ch.ReadPlain());
                outcome = Json.Str(r, "t");
                if (outcome != "pair_ok") return null;
                if (!Crypto.Same(Json.B64(r, "proof"), Crypto.Hmac(key, Crypto.Utf8("server"), cn, sn))) { outcome = "prova_do_servidor_errada"; return null; }
                return Crypto.Open(Crypto.Hmac(key, Crypto.Utf8("box-enc")), Crypto.Hmac(key, Crypto.Utf8("box-mac")), 0, Json.B64(r, "box"));
            }
        }

        static Client Login(string dev, byte[] secret)
        {
            Client c = new Client();
            byte[] cn = Crypto.RandomBytes(16);
            c.Ch.WritePlain(Json.Write(D("t", "hello", "v", 1, "dev", dev, "cn", Convert.ToBase64String(cn))));
            Dictionary<string, object> h = Json.Parse(c.Ch.ReadPlain());
            if (Json.Str(h, "known") != "True") { c.Dispose(); return null; }
            c.Ch.Secure(secret, cn, Json.B64(h, "sn"), false);
            c.Ch.Write(Json.Write(D("t", "auth")));
            if (Json.Str(Json.Parse(c.Ch.Read()), "t") != "ok") { c.Dispose(); return null; }
            c.Ch.AllowLargeFrames();
            return c;
        }

        static bool Closed(Action a)
        {
            try { a(); return false; }
            catch (IOException) { return true; }
            catch (SocketException) { return true; }
            catch (InvalidDataException) { return true; }
        }

        sealed class FakePlayer : ISoundPlayer
        {
            public readonly List<string> Log = new List<string>();
            public event Action PlayingChanged;
            public List<string> Now = new List<string>();
            public string Play(string id, string path, int volume)
            {
                lock (Log) Log.Add("play " + id + " " + volume);
                Now = new List<string> { id };
                Action h = PlayingChanged;
                if (h != null) h();
                return null;
            }
            public void SetVolume(string id, int volume) { lock (Log) Log.Add("vol " + id + " " + volume); }
            public void Stop(string id) { lock (Log) Log.Add("stop " + id); }
            public void StopAll() { lock (Log) Log.Add("stopall"); }
            public List<string> Playing() { return Now; }
        }

        static void SoundTests(string dir, Store store, FakePlayer player, byte[] secret)
        {
            Console.WriteLine("Sons");
            string wav = Path.Combine(dir, "latido teste.wav");
            File.WriteAllBytes(wav, new byte[64]);
            string txt = Path.Combine(dir, "nota.txt");
            File.WriteAllText(txt, "x");
            using (Client c = Login("dev-s", secret))
            {
                Check("recusa arquivo que nao e audio", Json.Str(c.Ask(D("t", "sound_add", "path", txt)), "ok") == "False");
                Dictionary<string, object> added = c.Ask(D("t", "sound_add", "path", wav));
                Dictionary<string, object> item = (Dictionary<string, object>)added["item"];
                string id = Json.Str(item, "id");
                Check("adiciona som com volume padrao", Json.Str(item, "name") == "latido teste" && Json.Int(item, "volume", 0) == 80);
                Check("som nao vira atalho", store.Shortcuts().Count == 0);
                Check("nao duplica o mesmo som", Json.Str((Dictionary<string, object>)c.Ask(D("t", "sound_add", "path", wav))["item"], "id") == id);

                Check("toca pelo id", Json.Str(c.Ask(D("t", "sound_play", "id", id)), "ok") == "True");
                lock (player.Log) Check("toca com o volume do som", player.Log.Contains("play " + id + " 80"));
                Check("id inexistente da erro", Json.Str(c.Ask(D("t", "sound_play", "id", "nada")), "ok") == "False");

                c.Ask(D("t", "sound_volume", "id", id, "volume", 40));
                Check("volume do arraste nao grava em disco ainda, mas vale", store.FindSound(id).volume == 40);
                c.Ask(D("t", "sound_master", "volume", 50, "muted", false, "final", true));
                c.Ask(D("t", "sound_play", "id", id));
                lock (player.Log) Check("volume geral multiplica o do som (40 x 50%)", player.Log.Contains("play " + id + " 20"));
                c.Ask(D("t", "sound_master", "volume", 50, "muted", true, "final", true));
                c.Ask(D("t", "sound_play", "id", id));
                lock (player.Log) Check("mudo zera o volume", player.Log.Contains("play " + id + " 0"));
                c.Ask(D("t", "sound_master", "volume", 100, "muted", false, "final", true));
                c.Ask(D("t", "sound_volume", "id", id, "volume", 900, "final", true));
                Check("volume fora da faixa e limitado", store.FindSound(id).volume == 100);

                Dictionary<string, object> list = c.Ask(D("t", "list"));
                Check("lista traz os sons e o estado", ((object[])list["sounds"]).Length == 1 && Json.Int(list, "master", 0) == 100);
                Check("som sem icone proprio responde sem icone", Json.Str(c.Ask(D("t", "icon", "id", id)), "ok") == "False");

                Check("imagem invalida e recusada", Json.Str(c.Ask(D("t", "sound_seticon", "id", id, "png", Convert.ToBase64String(new byte[] { 1, 2, 3, 4 }))), "ok") == "False");
                byte[] png;
                using (System.Drawing.Bitmap b = new System.Drawing.Bitmap(40, 40))
                using (MemoryStream ms = new MemoryStream())
                {
                    using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(b)) g.Clear(System.Drawing.Color.Red);
                    b.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                    png = ms.ToArray();
                }
                Check("aceita icone enviado", Json.Str(c.Ask(D("t", "sound_seticon", "id", id, "png", Convert.ToBase64String(png))), "ok") == "True");
                byte[] icon = Json.B64(c.Ask(D("t", "icon", "id", id)), "png");
                Check("icone do som volta como PNG", icon.Length > 100 && icon[0] == 0x89);
                Check("renomeia som", Json.Str(c.Ask(D("t", "rename", "id", id, "name", "Rex")), "ok") == "True" && store.FindSound(id).name == "Rex");
                Check("medidor liga sem erro", Json.Str(c.Ask(D("t", "meter", "on", true)), "ok") == "True");
                c.Ask(D("t", "meter", "on", false));

                store.AllowBrowse = false;
                Check("navegacao desligada: adicionar som recusado", Json.Str(c.Ask(D("t", "sound_add", "path", wav)), "ok") == "False");
                Check("navegacao desligada: icone por caminho recusado", Json.Str(c.Ask(D("t", "sound_seticon", "id", id, "path", txt)), "ok") == "False");
                Check("navegacao desligada: som da lista continua tocando", Json.Str(c.Ask(D("t", "sound_play", "id", id)), "ok") == "True");
                store.AllowBrowse = true;

                string iconFile = store.FindSound(id).icon;
                Check("remove som", Json.Str(c.Ask(D("t", "remove", "id", id)), "ok") == "True" && store.Sounds().Count == 0);
                Check("o audio do usuario continua no disco e o icone proprio sai", File.Exists(wav) && !File.Exists(iconFile));
            }

            string seed = Path.Combine(dir, "exemplos");
            Directory.CreateDirectory(seed);
            File.WriteAllBytes(Path.Combine(seed, "Gato.wav"), new byte[8]);
            File.WriteAllBytes(Path.Combine(seed, "Gato.png"), new byte[8]);
            File.WriteAllText(Path.Combine(seed, "leia.txt"), "x");
            store.SeedSounds(seed);
            Check("exemplos entram uma vez, com icone", store.Sounds().Count == 1 && store.Sounds()[0].icon != null);
            store.RemoveSound(store.Sounds()[0].id);
            store.SeedSounds(seed);
            Check("exemplo removido nao volta sozinho", store.Sounds().Count == 0);
        }

        static object[] Items(Dictionary<string, object> reply, string key)
        {
            return reply.ContainsKey(key) ? (object[])reply[key] : new object[0];
        }

        static void LibraryTests(string dir, Server server, Store store, FakePlayer player, byte[] secret)
        {
            Console.WriteLine("Biblioteca de sons");
            Check("etiqueta ID3v2: acha onde o audio comeca", MciPlayer.AudioStart(new byte[] { 73, 68, 51, 4, 0, 0, 0, 0, 2, 1 }) == 10 + 257);
            Check("etiqueta ID3v2 com rodape soma mais 10", MciPlayer.AudioStart(new byte[] { 73, 68, 51, 4, 0, 16, 0, 0, 0, 5 }) == 25);
            Check("sem etiqueta: comeca no inicio", MciPlayer.AudioStart(new byte[] { 255, 251, 0, 0, 0, 0, 0, 0, 0, 0 }) == 0);
            string root = Path.Combine(dir, "biblioteca");
            Directory.CreateDirectory(Path.Combine(root, "Games"));
            Directory.CreateDirectory(Path.Combine(root, "Memes & Cia"));
            Directory.CreateDirectory(Path.Combine(root, "Vazia"));
            File.WriteAllBytes(Path.Combine(root, "Games", "Água no chão.mp3"), new byte[8]);
            File.WriteAllBytes(Path.Combine(root, "Games", "Tiro.mp3"), new byte[8]);
            File.WriteAllBytes(Path.Combine(root, "Games", "capa.jpg"), new byte[8]);
            for (int i = 0; i < 250; i++) File.WriteAllBytes(Path.Combine(root, "Memes & Cia", "Meme " + i.ToString("000") + ".mp3"), new byte[8]);
            string outside = Path.Combine(dir, "segredo.mp3");
            File.WriteAllBytes(outside, new byte[8]);
            byte[] png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M/wHwAEBgIApD5fRAAAAABJRU5ErkJggg==");
            File.WriteAllBytes(Path.Combine(root, "Games.png"), png);

            using (Client c = Login("dev-s", secret))
            {
                Check("sem biblioteca: lista vazia, sem erro", Items(c.Ask(D("t", "lib_categories")), "categories").Length == 0);
                Check("sem biblioteca: adicionar e recusado", Json.Str(c.Ask(D("t", "lib_add", "cat", "Games", "file", "Tiro.mp3")), "ok") == "False");

                server.Library = new Library(root);
                Dictionary<string, object> cats = c.Ask(D("t", "lib_categories"));
                object[] list = Items(cats, "categories");
                Check("cada pasta com audio vira categoria; pasta vazia nao aparece", list.Length == 2 && Json.Int(cats, "total", 0) == 252);
                Check("arquivo que nao e audio fica de fora", Json.Int(c.Ask(D("t", "lib_list", "cat", "Games")), "total", 0) == 2);

                Dictionary<string, object> page = c.Ask(D("t", "lib_list", "cat", "Memes & Cia", "from", 100, "limit", 50));
                Check("lista em paginas", Json.Int(page, "total", 0) == 250 && Items(page, "items").Length == 50
                    && Json.Str((Dictionary<string, object>)Items(page, "items")[0], "name") == "Meme 100");
                Check("pagina tem limite", Items(c.Ask(D("t", "lib_list", "from", 0, "limit", 100000)), "items").Length == 200);
                Check("busca em todas as categorias, sem acento", Json.Int(c.Ask(D("t", "lib_list", "q", "agua")), "total", 0) == 1);
                Check("busca com palavras fora de ordem", Json.Int(c.Ask(D("t", "lib_list", "q", "chao AGUA")), "total", 0) == 1);
                Check("busca dentro da categoria nao acha as outras", Json.Int(c.Ask(D("t", "lib_list", "cat", "Games", "q", "meme")), "total", 0) == 0);
                Check("categoria inexistente da lista vazia", Json.Int(c.Ask(D("t", "lib_list", "cat", "..")), "total", 0) == 0);

                string[][] attacks =
                {
                    new[] { "Games", "..\\..\\segredo.mp3" }, new[] { "Games", "../../segredo.mp3" }, new[] { "..", "segredo.mp3" },
                    new[] { "Games", outside }, new[] { outside, "segredo.mp3" }, new[] { "Games", "capa.jpg" }
                };
                bool refused = true;
                foreach (string[] a in attacks)
                {
                    refused &= Json.Str(c.Ask(D("t", "lib_preview", "cat", a[0], "file", a[1])), "ok") == "False";
                    refused &= Json.Str(c.Ask(D("t", "lib_add", "cat", a[0], "file", a[1])), "ok") == "False";
                }
                lock (player.Log) refused &= !player.Log.Exists(l => l.Contains("segredo"));
                Check("nada fora da biblioteca pode ser tocado nem adicionado", refused && !store.Sounds().Exists(s => s.path == outside));

                Check("previa toca sem entrar nos sons", Json.Str(c.Ask(D("t", "lib_preview", "cat", "Games", "file", "Tiro.mp3")), "ok") == "True" && store.Sounds().Count == 0);
                store.SetMaster(50, false, false);
                c.Ask(D("t", "lib_preview", "cat", "Games", "file", "Tiro.mp3"));
                store.SetMaster(100, false, false);
                lock (player.Log) Check("previa respeita o volume geral (80 x 50%)", player.Log.Contains("play biblioteca-previa 40"));
                Check("parar a previa responde", Json.Str(c.Ask(D("t", "lib_stop")), "ok") == "True");

                store.AllowBrowse = false;
                Dictionary<string, object> first = c.Ask(D("t", "lib_add", "cat", "Games", "file", "Tiro.mp3"));
                Check("adiciona mesmo com a navegacao desligada", Json.Str(first, "ok") == "True" && store.Sounds().Count == 1);
                store.AllowBrowse = true;
                Dictionary<string, object> item = (Dictionary<string, object>)first["item"];
                SoundItem added = store.FindSound(Json.Str(item, "id"));
                Check("o som ganha uma copia do icone da categoria", added.icon != null && added.icon.StartsWith(store.SoundIconDir) && File.Exists(added.icon));
                Check("o audio continua na biblioteca, sem copia", added.path == Path.Combine(root, "Games", "Tiro.mp3"));
                Check("a lista mostra o que ja foi adicionado", ((object[])c.Ask(D("t", "lib_list", "cat", "Games", "q", "tiro"))["items"]).Length == 1
                    && Json.Str((Dictionary<string, object>)Items(c.Ask(D("t", "lib_list", "cat", "Games", "q", "tiro")), "items")[0], "added") == "True");
                Check("adicionar de novo nao duplica", Json.Str((Dictionary<string, object>)c.Ask(D("t", "lib_add", "cat", "Games", "file", "Tiro.mp3"))["item"], "id") == added.id && store.Sounds().Count == 1);
                Check("categoria sem PNG: som entra sem icone", Json.Str((Dictionary<string, object>)c.Ask(D("t", "lib_add", "cat", "Memes & Cia", "file", "Meme 001.mp3"))["item"], "icon") == "");
                Check("icone da categoria volta como PNG", Convert.FromBase64String(Json.Str(c.Ask(D("t", "lib_icon", "cat", "Games")), "png"))[0] == 0x89);
                Check("categoria sem PNG responde sem icone", Json.Str(c.Ask(D("t", "lib_icon", "cat", "Memes & Cia")), "ok") == "False");

                string icon = added.icon;
                Check("remover tira o som", Json.Str(c.Ask(D("t", "remove", "id", added.id)), "ok") == "True");
                Check("remover nao apaga o audio da biblioteca, so a copia do icone", File.Exists(Path.Combine(root, "Games", "Tiro.mp3")) && !File.Exists(icon) && File.Exists(Path.Combine(root, "Games.png")));
                foreach (SoundItem left in store.Sounds()) store.RemoveSound(left.id);
                File.WriteAllBytes(Path.Combine(root, "Games", "Novo.mp3"), new byte[8]);
                Dictionary<string, object> again = c.Ask(D("t", "lib_categories"));
                Check("arquivo novo aparece ao abrir a biblioteca de novo", Json.Int(again, "total", 0) == 253);
            }
            server.Library = null;
        }

        // Icone em cache chega antes de a janela aparecer: precisa aparecer mesmo assim. A janela nao chega a ser mostrada.
        static void WindowIconTest(string dir)
        {
            string file = Path.Combine(dir, "icone-teste.txt");
            File.WriteAllText(file, "x");
            Store store = new Store(Path.Combine(dir, "janela"));
            store.Add(file);
            Check("icone do arquivo entra no cache", Icons.GetPng(file) != null);

            MainForm form = new MainForm(store, new Server(store), true);
            Thread.Sleep(1500);
            for (int i = 0; i < 20; i++)
            {
                Application.DoEvents();
                Thread.Sleep(50);
            }
            ListView list = FindList(form);
            Check("janela aberta com o icone em cache mostra o icone", list != null && list.Items[0].ImageKey.Length > 0);

            // A bandeja nao e parte do Dispose da janela e deixaria um icone fantasma.
            ((NotifyIcon)typeof(MainForm).GetField("tray", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form)).Dispose();
            form.Dispose();
        }

        static ListView FindList(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                ListView v = c as ListView;
                if (v != null && v.Items.Count > 0) return v;
                v = FindList(c);
                if (v != null) return v;
            }
            return null;
        }

        static int Main()
        {
            string dir = Path.Combine(Path.GetTempPath(), "atalhospc-teste-" + Guid.NewGuid().ToString("N"));
            Store store = new Store(dir);
            store.SetPort(Port);
            Icons.Init(dir);
            Server server = new Server(store);
            List<string> launched = new List<string>();
            server.Launcher = p => { lock (launched) launched.Add(p); return null; };
            FakePlayer player = new FakePlayer();
            server.Player = player;
            server.Start();
            Thread.Sleep(400);

            Console.WriteLine("Estruturas nativas do Bluetooth");
            if (IntPtr.Size == 8)
            {
                Check("WSAQUERYSET tem 120 bytes em 64 bits", Marshal.SizeOf(typeof(Bth.WSAQUERYSET)) == 120);
                Check("CSADDR_INFO tem 40 bytes em 64 bits", Marshal.SizeOf(typeof(Bth.CSADDR_INFO)) == 40);
            }
            Check("SOCKADDR_BTH: canal fica no deslocamento 26", new Bth.EndPointBth(0x01020304).Serialize()[26] == 4);
            Thread.Sleep(600);
            Console.WriteLine("  info  estado do Bluetooth neste PC: " + server.BluetoothState);

            Console.WriteLine("Pareamento");
            string outcome;
            Check("recusa quando o pareamento nao foi aberto", TryPair("dev-a", "ABCDEF", out outcome) == null && outcome == "pair_closed");

            string code = server.BeginPair(120);
            Check("codigo errado e recusado", TryPair("dev-a", "ZZZZZZ", out outcome) == null && outcome == "pair_bad");
            byte[] secret = TryPair("dev-a", code, out outcome);
            Check("codigo certo entrega o segredo e o servidor prova que conhece o codigo", secret != null && secret.Length == 32);
            Check("codigo so vale uma vez", TryPair("dev-b", code, out outcome) == null && outcome == "pair_closed");

            server.BeginPair(120);
            for (int i = 0; i < 5; i++) TryPair("dev-x", "ERRADO", out outcome);
            Check("5 tentativas erradas fecham o pareamento", server.PairSecondsLeft == 0);
            TryPair("dev-x", "ERRADO", out outcome);
            Check("depois disso nem chega a pedir a prova", outcome == "pair_closed");

            Console.WriteLine("Autenticacao");
            using (Client c = new Client())
            {
                c.Ch.WritePlain(Json.Write(D("t", "hello", "v", 1, "dev", "desconhecido", "cn", Convert.ToBase64String(Crypto.RandomBytes(16)))));
                Check("aparelho desconhecido recebe known=false", Json.Str(Json.Parse(c.Ch.ReadPlain()), "known") == "False");
                Check("e a conexao e encerrada", Closed(() => c.Ch.ReadPlain()));
            }
            Check("segredo errado e derrubado sem resposta", Closed(() =>
            {
                Client bad = Login("dev-a", Crypto.RandomBytes(32));
                if (bad != null) bad.Dispose();
            }));
            using (Client c = new Client())
            {
                c.Ch.WritePlain(Json.Write(D("t", "list", "v", 1)));
                Check("comando sem autenticar nao e atendido", Closed(() => c.Ch.ReadPlain()));
            }
            using (Client c = new Client())
            {
                NetworkStream ns = c.Tcp.GetStream();
                ns.Write(new byte[] { 0x7F, 0xFF, 0xFF, 0xFF }, 0, 4);
                Check("frame gigante antes de autenticar derruba a conexao", Closed(() => c.Ch.ReadPlain()));
            }

            Console.WriteLine("Canal cifrado");
            byte[] cnReplay = Crypto.RandomBytes(16);
            using (Client c = new Client())
            {
                c.Ch.WritePlain(Json.Write(D("t", "hello", "v", 1, "dev", "dev-a", "cn", Convert.ToBase64String(cnReplay))));
                Dictionary<string, object> h = Json.Parse(c.Ch.ReadPlain());
                byte[] sn = Json.B64(h, "sn");
                byte[] enc = Crypto.Hmac(secret, Crypto.Utf8("enc-c2s"), cnReplay, sn);
                byte[] mac = Crypto.Hmac(secret, Crypto.Utf8("mac-c2s"), cnReplay, sn);
                byte[] box = Crypto.Seal(enc, mac, 0, Crypto.Utf8(Json.Write(D("t", "auth"))));
                box[20] ^= 1;
                Channel raw = new Channel(c.Tcp.GetStream());
                WriteRaw(c.Tcp.GetStream(), box);
                Check("frame adulterado derruba a conexao", Closed(() => raw.ReadPlain()));
            }
            using (Client c = new Client())
            {
                byte[] cn = Crypto.RandomBytes(16);
                c.Ch.WritePlain(Json.Write(D("t", "hello", "v", 1, "dev", "dev-a", "cn", Convert.ToBase64String(cn))));
                Dictionary<string, object> h = Json.Parse(c.Ch.ReadPlain());
                byte[] sn = Json.B64(h, "sn");
                byte[] enc = Crypto.Hmac(secret, Crypto.Utf8("enc-c2s"), cn, sn);
                byte[] mac = Crypto.Hmac(secret, Crypto.Utf8("mac-c2s"), cn, sn);
                byte[] auth = Crypto.Seal(enc, mac, 0, Crypto.Utf8(Json.Write(D("t", "auth"))));
                WriteRaw(c.Tcp.GetStream(), auth);
                Channel reader = new Channel(c.Tcp.GetStream());
                reader.ReadPlain();
                WriteRaw(c.Tcp.GetStream(), auth); // mesmo frame de novo
                Check("frame repetido (replay) derruba a conexao", Closed(() => reader.ReadPlain()));
            }

            Console.WriteLine("Comandos");
            string notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe");
            using (Client c = Login("dev-a", secret))
            {
                Check("login com o segredo certo", c != null);
                Dictionary<string, object> added = c.Ask(D("t", "add", "path", notepad));
                Dictionary<string, object> item = (Dictionary<string, object>)added["item"];
                string id = Json.Str(item, "id");
                Check("adiciona atalho", Json.Str(added, "ok") == "True" && Json.Str(item, "name") == "notepad");
                Dictionary<string, object> again = c.Ask(D("t", "add", "path", notepad));
                Check("nao duplica o mesmo arquivo", Json.Str((Dictionary<string, object>)again["item"], "id") == id);

                Dictionary<string, object> icon = c.Ask(D("t", "icon", "id", id));
                byte[] png = Json.B64(icon, "png");
                Check("icone e um PNG real (" + png.Length + " bytes)", png.Length > 500 && png[0] == 0x89 && png[1] == 0x50);
                Check("lista traz o atalho", ((object[])c.Ask(D("t", "list"))["items"]).Length == 1);

                Check("executa pelo id", Json.Str(c.Ask(D("t", "run", "id", id)), "ok") == "True");
                lock (launched) Check("abriu exatamente o arquivo do atalho", launched.Count == 1 && launched[0] == notepad);
                Check("id inexistente da erro", Json.Str(c.Ask(D("t", "run", "id", "nada")), "ok") == "False");

                Check("renomeia", Json.Str(c.Ask(D("t", "rename", "id", id, "name", "Bloco")), "ok") == "True" && store.Find(id).name == "Bloco");
                Check("navega em pasta", ((object[])c.Ask(D("t", "browse", "path", Environment.GetFolderPath(Environment.SpecialFolder.Windows)))["entries"]).Length > 0);
                Check("caminho relativo e recusado", Json.Str(c.Ask(D("t", "browse", "path", "..\\..")), "ok") == "False");

                store.AllowBrowse = false;
                Check("navegacao desligada: browse recusado", Json.Str(c.Ask(D("t", "browse", "path", "C:\\")), "ok") == "False");
                Check("navegacao desligada: abrir caminho avulso recusado", Json.Str(c.Ask(D("t", "open", "path", notepad)), "ok") == "False");
                Check("navegacao desligada: adicionar recusado", Json.Str(c.Ask(D("t", "add", "path", notepad)), "ok") == "False");
                Check("navegacao desligada: icone por caminho recusado", Json.Str(c.Ask(D("t", "icon", "path", notepad)), "ok") == "False");
                Check("navegacao desligada: atalho da lista continua abrindo", Json.Str(c.Ask(D("t", "run", "id", id)), "ok") == "True");
                store.AllowBrowse = true;

                Check("remove atalho", Json.Str(c.Ask(D("t", "remove", "id", id)), "ok") == "True" && store.Shortcuts().Count == 0);

                store.RemoveDevice("dev-a");
                Thread.Sleep(300);
                Client kicked = c;
                Check("remover o aparelho no PC derruba a sessao aberta", Closed(() => kicked.Ask(D("t", "ping"))));
            }
            Client after = Login("dev-a", secret);
            Check("segredo antigo nao entra mais depois de remover", after == null);

            byte[] soundSecret = Crypto.RandomBytes(32);
            store.AddDevice("dev-s", "Sons", soundSecret);
            SoundTests(dir, store, player, soundSecret);

            LibraryTests(dir, server, store, player, soundSecret);

            Console.WriteLine("Janela do PC");
            Thread ui = new Thread(() => WindowIconTest(dir));
            ui.SetApartmentState(ApartmentState.STA); // arrastar e soltar na lista exige STA
            ui.Start();
            ui.Join();

            server.Stop();
            Console.WriteLine();
            Console.WriteLine(passes + " ok, " + failures + " falha(s)");
            try { Directory.Delete(dir, true); } catch (IOException) { }
            return failures == 0 ? 0 : 1;
        }

        static void WriteRaw(Stream s, byte[] body)
        {
            byte[] all = new byte[4 + body.Length];
            all[2] = (byte)(body.Length >> 8);
            all[3] = (byte)body.Length;
            Buffer.BlockCopy(body, 0, all, 4, body.Length);
            s.Write(all, 0, all.Length);
            s.Flush();
        }
    }
}
