// Testa o instalador de verdade em pastas temporarias e com chaves de registro proprias: nao cria atalho na area de
// trabalho do usuario, nao mexe na entrada real de "Aplicativos" e nao fecha o programa que estiver aberto.
// Uso: InstaladorTest.exe [pasta dist] [pasta para capturas de tela]
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Windows.Forms;
using AtalhosPC.Setup;
using Microsoft.Win32;

namespace AtalhosPC
{
    static class InstallerTest
    {
        static int passes;
        static int failures;

        static void Check(string name, bool ok)
        {
            Console.WriteLine((ok ? "  ok    " : "  FALHA ") + name);
            if (ok) passes++; else failures++;
        }

        static string Hash(Stream s)
        {
            using (SHA256 sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(s));
        }

        static string Hash(string file)
        {
            using (FileStream f = File.OpenRead(file)) return Hash(f);
        }

        static string PayloadHash(string rel)
        {
            using (Stream s = Installer.OpenPayload(rel)) return Hash(s);
        }

        static object Reg(string key, string name)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(key)) return k == null ? null : k.GetValue(name);
        }

        static void SetReg(string key, string name, string value)
        {
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(key)) k.SetValue(name, value);
        }

        static InstallPlan Plan(string tmp, string keyBase, string name)
        {
            return new InstallPlan
            {
                Folder = Path.Combine(tmp, name, "AtalhosPC"),
                DesktopDir = Path.Combine(tmp, "AreaDeTrabalho"),
                StartMenuDir = Path.Combine(tmp, "MenuIniciar"),
                UninstallKey = keyBase + @"\Uninstall\AtalhosPC",
                RunKey = keyBase + @"\Run",
                CloseRunning = false
            };
        }

        static void Write(string path, int bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, Enumerable.Repeat((byte)7, bytes).ToArray());
        }

        static int Main(string[] args)
        {
            string dist = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\dist"));
            string shots = args.Length > 1 ? args[1] : null;
            string tmp = Path.Combine(Path.GetTempPath(), "atalhospc-instalador-" + Guid.NewGuid().ToString("N"));
            string keyBase = @"Software\AtalhosPC-Teste-" + Guid.NewGuid().ToString("N");
            string realDesktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Installer.LinkName);
            bool realLinkBefore = File.Exists(realDesktopLink);
            bool realKeyBefore = Reg(Installer.UninstallKeyPath, "DisplayName") != null;
            try
            {
                Run(dist, tmp, keyBase);
                Thread ui = new Thread(() => Wizard(tmp, keyBase, shots));
                ui.SetApartmentState(ApartmentState.STA);
                ui.Start();
                ui.Join();
                Console.WriteLine("Nada fora das pastas de teste");
                Check("area de trabalho real nao ganhou atalho", File.Exists(realDesktopLink) == realLinkBefore);
                Check("entrada real de Aplicativos nao foi criada", (Reg(Installer.UninstallKeyPath, "DisplayName") != null) == realKeyBefore);
            }
            finally
            {
                Registry.CurrentUser.DeleteSubKeyTree(keyBase, false);
                try { Directory.Delete(tmp, true); } catch (IOException) { }
            }
            Console.WriteLine();
            Console.WriteLine(passes + " ok, " + failures + " falha(s)");
            return failures == 0 ? 0 : 1;
        }

        static void Run(string dist, string tmp, string keyBase)
        {
            Console.WriteLine("Conteudo do instalador");
            List<string> files = Installer.PayloadFiles();
            Check("versao " + AppInfo.Version, AppInfo.Version == "2.3");
            Check("traz o programa igual ao de dist", files.Contains(Installer.ExeName) && PayloadHash(Installer.ExeName) == Hash(Path.Combine(dist, "AtalhosPC.exe")));
            Check("traz o app igual ao de dist", files.Contains(Installer.ApkName) && PayloadHash(Installer.ApkName) == Hash(Path.Combine(dist, "AtalhosPC.apk")));
            int sons = Directory.GetFiles(Path.Combine(dist, "sons")).Length;
            Check("traz os " + sons + " arquivos de sons de exemplo", files.Count(f => f.StartsWith(@"sons\")) == sons);
            Check("nao traz nada da biblioteca de terceiros", !files.Any(f => f.StartsWith(Installer.LibraryFolder, StringComparison.OrdinalIgnoreCase)));
            Check("logo para a barra lateral", Installer.OpenResource("logo.png") != null);

            Console.WriteLine("Biblioteca de exemplo");
            string lib = Path.Combine(tmp, "biblioteca-origem");
            Write(Path.Combine(lib, "Memes", "a.mp3"), 1000);
            Write(Path.Combine(lib, "Memes", "b.wav"), 2000);
            Write(Path.Combine(lib, "Memes", "nota.txt"), 10);
            Write(Path.Combine(lib, "Memes", "sub", "fundo.mp3"), 10);
            Write(Path.Combine(lib, "Viral", "c.mp3"), 3000);
            Write(Path.Combine(lib, "Vazia", "x.txt"), 10);
            LibraryInfo info = Installer.InspectLibrary(lib);
            Check("conta 3 sons em 2 categorias, como o programa le", info.Files == 3 && info.Categories == 2 && info.Bytes == 6000);
            Check("pasta inexistente da zero", Installer.InspectLibrary(Path.Combine(tmp, "nada")).Files == 0);

            Console.WriteLine("Primeira instalacao");
            InstallPlan plan = Plan(tmp, keyBase, "Programas");
            plan.Autostart = true;
            plan.LibrarySource = lib;
            List<int> percents = new List<int>();
            List<string> notes = Installer.Install(plan, (p, s) => percents.Add(p));
            string folder = Path.GetFullPath(plan.Folder);
            string exe = Path.Combine(folder, Installer.ExeName);
            Check("sem avisos", notes.Count == 0);
            Check("progresso vai ate 100 sem voltar", percents.Count > 5 && percents.Last() == 100 && percents.Zip(percents.Skip(1), (a, b) => b >= a).All(x => x));
            Check("programa copiado igual", Hash(exe) == Hash(Path.Combine(dist, "AtalhosPC.exe")));
            Check("app copiado igual", Hash(Path.Combine(folder, Installer.ApkName)) == Hash(Path.Combine(dist, "AtalhosPC.apk")));
            Check("sons de exemplo copiados", Directory.GetFiles(Path.Combine(folder, "sons")).Length == Directory.GetFiles(Path.Combine(dist, "sons")).Length);
            Check("desinstalador e uma copia do instalador", Hash(Path.Combine(folder, Installer.UninstallerName)) == Hash(Installer.SelfPath));
            string bib = Path.Combine(folder, Installer.LibraryFolder);
            Check("biblioteca: os 3 sons copiados", File.Exists(Path.Combine(bib, "Memes", "a.mp3")) && File.Exists(Path.Combine(bib, "Memes", "b.wav")) && File.Exists(Path.Combine(bib, "Viral", "c.mp3")));
            Check("biblioteca: texto e subpasta ficam de fora", !File.Exists(Path.Combine(bib, "Memes", "nota.txt")) && !Directory.Exists(Path.Combine(bib, "Memes", "sub")) && !Directory.Exists(Path.Combine(bib, "Vazia")));
            Check("biblioteca: 14 icones de categoria", Directory.GetFiles(bib, "*.png").Length == 14);
            HashSet<string> list = Installer.ReadList(folder);
            Check("lista tem programa, desinstalador e biblioteca", list.Contains(Installer.ExeName) && list.Contains(Installer.UninstallerName) && list.Contains(@"biblioteca\Memes\a.mp3") && list.Contains(Installer.ListName));
            Check("atalho da area de trabalho abre o programa", Installer.ShortcutTarget(Path.Combine(plan.DesktopDir, Installer.LinkName)) == exe);
            Check("atalho do menu Iniciar abre o programa", Installer.ShortcutTarget(Path.Combine(plan.StartMenuDir, Installer.LinkName)) == exe);
            Check("Aplicativos: nome, versao e editor", (string)Reg(plan.UninstallKey, "DisplayName") == "Atalhos PC" && (string)Reg(plan.UninstallKey, "DisplayVersion") == "2.3" && (string)Reg(plan.UninstallKey, "Publisher") == "Cavaleiro AI");
            Check("Aplicativos: pasta e desinstalador", (string)Reg(plan.UninstallKey, "InstallLocation") == folder && (string)Reg(plan.UninstallKey, "UninstallString") == "\"" + Path.Combine(folder, Installer.UninstallerName) + "\" /desinstalar");
            Check("Aplicativos: tamanho estimado", (int)Reg(plan.UninstallKey, "EstimatedSize") > 100);
            Check("iniciar com o Windows aponta para a copia instalada, na bandeja", (string)Reg(plan.RunKey, plan.RunValue) == "\"" + exe + "\" --bandeja");
            Check("instalador acha a pasta instalada", Installer.InstalledFolder(plan) == folder && Installer.InstalledVersion(plan) == "2.3");

            Console.WriteLine("Atualizar por cima");
            File.WriteAllText(Path.Combine(folder, "sons", "Aplausos.mp3"), "estragado");
            plan.Autostart = false;
            plan.DesktopShortcut = false;
            plan.LibrarySource = null;
            Installer.Install(plan, (p, s) => { });
            Check("arquivo estragado volta ao original", Hash(Path.Combine(folder, "sons", "Aplausos.mp3")) == PayloadHash(@"sons\Aplausos.mp3"));
            Check("desmarcar iniciar com o Windows remove a entrada", Reg(plan.RunKey, plan.RunValue) == null);
            Check("desmarcar a area de trabalho remove o atalho", !File.Exists(Path.Combine(plan.DesktopDir, Installer.LinkName)));
            Check("menu Iniciar continua", File.Exists(Path.Combine(plan.StartMenuDir, Installer.LinkName)));
            Check("lista continua com a biblioteca da instalacao anterior", Installer.ReadList(folder).Contains(@"biblioteca\Viral\c.mp3"));

            Console.WriteLine("Caminhos perigosos");
            Check("recusa ..", Installer.SafePath(folder, @"..\fora.txt") == null && Installer.SafePath(folder, @"sons\..\..\fora.txt") == null);
            Check("recusa caminho absoluto", Installer.SafePath(folder, @"C:\Windows\notepad.exe") == null && Installer.SafePath(folder, @"\Windows\notepad.exe") == null);
            Check("aceita caminho de dentro", Installer.SafePath(folder, @"sons\Aplausos.mp3") == Path.Combine(folder, "sons", "Aplausos.mp3"));

            Console.WriteLine("Desinstalar");
            File.WriteAllText(Path.Combine(folder, "meu-arquivo.txt"), "do usuario");
            File.WriteAllText(Path.Combine(folder, "sons", "meu-som.mp3"), "do usuario");
            File.WriteAllText(Path.Combine(bib, "Memes", "meu.mp3"), "do usuario");
            string outside = Path.Combine(tmp, "Programas", "fora.txt");
            File.WriteAllText(outside, "fora");
            File.AppendAllLines(Path.Combine(folder, Installer.ListName), new[] { @"..\fora.txt", outside, @"sons\..\..\fora.txt" });
            string notepad = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "notepad.exe");
            Installer.CreateShortcut(Path.Combine(plan.DesktopDir, Installer.LinkName), notepad, "do usuario");
            SetReg(plan.RunKey, plan.RunValue, "\"C:\\outra\\AtalhosPC.exe\" --bandeja");
            List<string> uninstallNotes = new List<string>();
            bool selfLater = Installer.Uninstall(plan, uninstallNotes);
            Check("sem avisos ao desinstalar", uninstallNotes.Count == 0);
            Check("desinstalador fora da pasta nao fica pendente", !selfLater);
            Check("programa, app, desinstalador e lista apagados", !File.Exists(exe) && !File.Exists(Path.Combine(folder, Installer.ApkName)) && !File.Exists(Path.Combine(folder, Installer.UninstallerName)) && !File.Exists(Path.Combine(folder, Installer.ListName)));
            Check("sons de exemplo apagados", !File.Exists(Path.Combine(folder, "sons", "Aplausos.mp3")));
            Check("arquivos do usuario na pasta ficam", File.Exists(Path.Combine(folder, "meu-arquivo.txt")) && File.Exists(Path.Combine(folder, "sons", "meu-som.mp3")) && File.Exists(Path.Combine(bib, "Memes", "meu.mp3")));
            Check("arquivo fora da pasta, posto na lista, fica", File.Exists(outside));
            Check("pasta de categoria vazia sai, a com arquivo do usuario fica", !Directory.Exists(Path.Combine(bib, "Viral")) && Directory.Exists(Path.Combine(bib, "Memes")));
            Check("atalho do usuario com o mesmo nome, para outro programa, fica", File.Exists(Path.Combine(plan.DesktopDir, Installer.LinkName)));
            Check("atalho do menu Iniciar sai", !File.Exists(Path.Combine(plan.StartMenuDir, Installer.LinkName)));
            Check("iniciar com o Windows de outra copia fica", Reg(plan.RunKey, plan.RunValue) != null);
            Check("entrada de Aplicativos sai", Reg(plan.UninstallKey, "DisplayName") == null);

            Console.WriteLine("Desinstalar pasta limpa");
            InstallPlan clean = Plan(tmp, keyBase, "Limpa");
            Installer.Install(clean, (p, s) => { });
            SetReg(clean.RunKey, clean.RunValue, "x");
            Installer.Uninstall(clean, new List<string>());
            Check("a pasta inteira some quando so tinha o que foi instalado", !Directory.Exists(clean.Folder));
        }

        static void Pump(Func<bool> done, int ms)
        {
            Stopwatch sw = Stopwatch.StartNew();
            while (!done() && sw.ElapsedMilliseconds < ms)
            {
                Application.DoEvents();
                Thread.Sleep(20);
            }
            Application.DoEvents();
        }

        static void Shot(Control c, string shots, string name)
        {
            if (shots == null) return;
            Directory.CreateDirectory(shots);
            using (Bitmap b = new Bitmap(c.Width, c.Height))
            {
                c.DrawToBitmap(b, new Rectangle(0, 0, c.Width, c.Height));
                b.Save(Path.Combine(shots, name), ImageFormat.Png);
            }
        }

        static T Field<T>(object o, string name)
        {
            return (T)o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(o);
        }

        static void Wizard(string tmp, string keyBase, string shots)
        {
            Console.WriteLine("Assistente");
            string lib = Path.Combine(tmp, "biblioteca-origem");
            InstallPlan plan = Plan(tmp, keyBase, "Assistente");
            plan.Autostart = false;
            using (SetupForm form = new SetupForm(plan, lib))
            {
                form.Show();
                Pump(() => false, 400);
                Button next = Field<Button>(form, "nextButton");
                Check("comeca na saudacao, com o botao Avancar", Field<int>(form, "page") == 0 && next.Text.StartsWith("Avançar"));
                Shot(form, shots, "1-saudacao.png");
                next.PerformClick();
                Pump(() => false, 200);
                Check("segunda tela: opcoes, com o botao Instalar", Field<int>(form, "page") == 1 && next.Text == "Instalar");
                Check("pasta sugerida e a do plano", Field<TextBox>(form, "folderBox").Text == plan.Folder);
                Check("biblioteca encontrada vem marcada, com a contagem", Field<CheckBox>(form, "libraryBox").Checked && Field<Label>(form, "libraryNote").Text.StartsWith("3 sons em 2 categorias"));
                Shot(form, shots, "2-opcoes.png");
                next.PerformClick();
                Pump(() => Field<int>(form, "page") == 3, 60000);
                Check("instala e chega ao fim", Field<int>(form, "page") == 3 && form.ExitCode == 0 && Field<Label>(form, "doneTitle").Text == "Pronto!");
                Check("programa e biblioteca na pasta escolhida", File.Exists(Path.Combine(plan.Folder, Installer.ExeName)) && File.Exists(Path.Combine(plan.Folder, Installer.LibraryFolder, "Viral", "c.mp3")));
                Shot(form, shots, "3-pronto.png");
                Field<CheckBox>(form, "openBox").Checked = false; // nao abre o programa de verdade no teste
                next.PerformClick();
                Pump(() => !form.Visible, 2000);
                Check("Concluir fecha o assistente", !form.Visible);
            }
            Installer.Uninstall(plan, new List<string>());
            Check("instalacao do assistente removida", !Directory.Exists(plan.Folder));

            Console.WriteLine("Saudacao do programa");
            using (Form dialog = Greeting.CreateDialog())
            {
                dialog.StartPosition = FormStartPosition.CenterScreen;
                dialog.Show();
                Pump(() => false, 300);
                GreetingView view = dialog.Controls.OfType<GreetingView>().FirstOrDefault();
                Check("janela da saudacao mostra o texto inteiro", view != null && view.Height >= view.PreferredHeight(view.Width) - 2);
                Shot(dialog, shots, "4-saudacao-programa.png");
                dialog.Close();
            }
        }
    }
}
