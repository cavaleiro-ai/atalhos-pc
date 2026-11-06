// Instala, atualiza e remove o Atalhos PC so para o usuario atual, sem pedir administrador.
// Nunca toca nos dados do usuario (%APPDATA%\AtalhosPC): atalhos, sons e pareamentos sobrevivem a reinstalar e desinstalar.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace AtalhosPC.Setup
{
    public sealed class InstallPlan
    {
        public string Folder = Installer.DefaultFolder();
        public bool DesktopShortcut = true;
        public bool StartMenuShortcut = true;
        public bool Autostart;
        public string LibrarySource; // pasta com uma subpasta por categoria; null nao copia
        public bool CloseRunning = true; // os testes desligam para nao fechar o programa do usuario

        // Lugares do Windows. Os testes trocam por pastas e chaves proprias.
        public string DesktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        public string StartMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
        public string UninstallKey = Installer.UninstallKeyPath;
        public string RunKey = Installer.RunKeyPath;
        public string RunValue = "AtalhosPC"; // o mesmo nome da opcao "Iniciar junto com o Windows" do programa
    }

    public sealed class LibraryInfo
    {
        public int Categories;
        public int Files;
        public long Bytes;
    }

    public static class Installer
    {
        public const string UninstallKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\AtalhosPC";
        public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string ExeName = "AtalhosPC.exe";
        public const string ApkName = "AtalhosPC.apk";
        public const string UninstallerName = "Desinstalar.exe";
        public const string ListName = "arquivos-instalados.txt";
        public const string LinkName = "Atalhos PC.lnk";
        public const string LibraryFolder = "biblioteca";
        const string PayloadPrefix = "payload/";
        const string IconPrefix = "icones-biblioteca/";
        // A mesma lista do programa (Store.AudioExtensions)
        static readonly string[] AudioExtensions = { ".mp3", ".wav", ".wma", ".m4a", ".aac" };

        static Assembly Me { get { return typeof(Installer).Assembly; } }

        public static string SelfPath { get { return Me.Location; } }

        public static string LogFile { get { return Path.Combine(Path.GetTempPath(), "AtalhosPC-instalador.log"); } }

        public static string DefaultFolder()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "AtalhosPC");
        }

        public static void Log(string text)
        {
            try { File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + text + Environment.NewLine, Encoding.UTF8); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        static string Quote(string path) { return "\"" + path + "\""; }

        public static string SizeText(long bytes)
        {
            if (bytes >= 1L << 30) return (bytes / (double)(1L << 30)).ToString("0.0") + " GB";
            return Math.Max(1, (long)Math.Round(bytes / (double)(1L << 20))) + " MB";
        }

        static bool SamePath(string a, string b)
        {
            if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
            try { return string.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
            catch (ArgumentException) { return false; }
            catch (NotSupportedException) { return false; }
        }

        static bool Inside(string folder, string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            string root = Path.GetFullPath(folder).TrimEnd('\\') + "\\";
            return Path.GetFullPath(path).StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        // Caminho da lista dentro da pasta, ou null se tentar sair dela.
        public static string SafePath(string folder, string relative)
        {
            if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative)) return null;
            if (relative.Split('\\', '/').Any(part => part.Trim() == "..")) return null;
            try
            {
                string full = Path.GetFullPath(Path.Combine(folder, relative));
                return Inside(folder, full) ? full : null;
            }
            catch (ArgumentException) { return null; }
            catch (NotSupportedException) { return null; }
            catch (PathTooLongException) { return null; }
        }

        // ---------- o que ja existe neste PC ----------

        public static string InstalledFolder(InstallPlan plan) { return ReadUninstall(plan, "InstallLocation"); }

        public static string InstalledVersion(InstallPlan plan) { return ReadUninstall(plan, "DisplayVersion"); }

        static string ReadUninstall(InstallPlan plan, string name)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(plan.UninstallKey))
                return k == null ? null : k.GetValue(name) as string;
        }

        public static bool AutostartEnabled(InstallPlan plan)
        {
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(plan.RunKey))
                return k != null && k.GetValue(plan.RunValue) != null;
        }

        // A biblioteca ao lado do instalador, como na pasta dist do projeto.
        public static string GuessLibrary()
        {
            string beside = Path.Combine(Path.GetDirectoryName(SelfPath), LibraryFolder);
            return InspectLibrary(beside).Files > 0 ? beside : null;
        }

        static string ProcessPath(Process p)
        {
            try { return p.MainModule.FileName; }
            catch (Win32Exception) { return null; }       // processo de outro usuario
            catch (InvalidOperationException) { return null; } // ja saiu
        }

        // Copias abertas deste usuario. folder null: qualquer pasta.
        public static List<Process> Running(string folder)
        {
            List<Process> found = new List<Process>();
            foreach (Process p in Process.GetProcessesByName("AtalhosPC"))
            {
                string path = ProcessPath(p);
                if (path != null && (folder == null || Inside(folder, path))) found.Add(p);
                else p.Dispose();
            }
            return found;
        }

        // Pede ao programa para sair pelo sinal, como o "Sair" da bandeja. Versao antiga, sem o sinal, e encerrada a forca.
        public static bool StopRunning(string folder)
        {
            List<Process> running = Running(folder);
            if (running.Count == 0) return true;
            EventWaitHandle exit = null;
            try { exit = EventWaitHandle.OpenExisting(AppInfo.ExitEvent); }
            catch (WaitHandleCannotBeOpenedException) { }
            catch (UnauthorizedAccessException) { }
            try
            {
                if (exit != null) exit.Set();
                foreach (Process p in running)
                {
                    using (p)
                    {
                        if (exit != null && p.WaitForExit(10000)) { Log("Programa aberto fechou pelo sinal (processo " + p.Id + ")"); continue; }
                        try
                        {
                            p.Kill();
                            p.WaitForExit(5000);
                            Log("Programa aberto encerrado a forca (processo " + p.Id + ")");
                        }
                        catch (Win32Exception e) { Log("Nao consegui fechar o processo " + p.Id + ": " + e.Message); }
                        catch (InvalidOperationException) { }
                    }
                }
            }
            finally
            {
                // Sinal que ninguem consumiu fecharia a copia nova logo ao abrir.
                if (exit != null)
                {
                    exit.Reset();
                    exit.Dispose();
                }
            }
            List<Process> left = Running(folder);
            foreach (Process p in left) p.Dispose();
            return left.Count == 0;
        }

        // ---------- conteudo do instalador ----------

        static List<string> Resources(string prefix)
        {
            return Me.GetManifestResourceNames().Where(n => n.StartsWith(prefix, StringComparison.Ordinal))
                .Select(n => n.Substring(prefix.Length)).OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToList();
        }

        // Caminhos relativos, com barra invertida, de tudo que vai para a pasta do programa.
        public static List<string> PayloadFiles()
        {
            return Resources(PayloadPrefix).Select(n => n.Replace('/', '\\')).ToList();
        }

        public static Stream OpenPayload(string relative)
        {
            return Me.GetManifestResourceStream(PayloadPrefix + relative.Replace('\\', '/'));
        }

        public static Stream OpenResource(string name)
        {
            return Me.GetManifestResourceStream(name);
        }

        static void WriteFile(string target, Stream source)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            string tmp = target + ".novo";
            using (FileStream f = File.Create(tmp)) source.CopyTo(f);
            if (File.Exists(target)) File.Delete(target);
            File.Move(tmp, target);
        }

        // ---------- lista do que foi instalado ----------

        public static HashSet<string> ReadList(string folder)
        {
            HashSet<string> list = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string file = Path.Combine(folder, ListName);
            if (!File.Exists(file)) return list;
            foreach (string line in File.ReadAllLines(file, Encoding.UTF8))
            {
                string t = line.Trim();
                if (t.Length > 0 && !t.StartsWith("#")) list.Add(t);
            }
            return list;
        }

        static void WriteList(string folder, HashSet<string> list)
        {
            List<string> lines = new List<string> { "# Arquivos que o instalador colocou aqui. O desinstalador apaga so estes; o resto da pasta fica." };
            lines.AddRange(list.OrderBy(x => x, StringComparer.OrdinalIgnoreCase));
            File.WriteAllLines(Path.Combine(folder, ListName), lines, new UTF8Encoding(false));
        }

        // ---------- biblioteca de sons ----------

        static bool IsAudio(string name)
        {
            return AudioExtensions.Contains(Path.GetExtension(name).ToLowerInvariant());
        }

        // A mesma regra do programa: uma subpasta por categoria, com os audios direto dentro dela.
        public static LibraryInfo InspectLibrary(string source)
        {
            LibraryInfo info = new LibraryInfo();
            if (string.IsNullOrWhiteSpace(source) || !Directory.Exists(source)) return info;
            foreach (DirectoryInfo dir in new DirectoryInfo(source).GetDirectories())
            {
                int before = info.Files;
                foreach (FileInfo f in dir.GetFiles())
                {
                    if (!IsAudio(f.Name)) continue;
                    info.Files++;
                    info.Bytes += f.Length;
                }
                if (info.Files > before) info.Categories++;
            }
            return info;
        }

        static void CheckSpace(string folder, long need)
        {
            DriveInfo drive;
            try { drive = new DriveInfo(Path.GetPathRoot(Path.GetFullPath(folder))); }
            catch (ArgumentException) { return; } // pasta de rede: o Windows nao informa o espaco
            long margin = 50L << 20;
            if (drive.IsReady && drive.AvailableFreeSpace < need + margin)
                throw new IOException("Falta espaço no disco " + drive.Name + ": a biblioteca precisa de " + SizeText(need) + " e há " + SizeText(drive.AvailableFreeSpace) + " livres.");
        }

        static void CopyLibrary(string source, string folder, HashSet<string> list, Action<int, string> progress, int from, int to)
        {
            string target = Path.Combine(folder, LibraryFolder);
            if (SamePath(source, target)) return;
            List<KeyValuePair<FileInfo, string>> todo = new List<KeyValuePair<FileInfo, string>>();
            foreach (DirectoryInfo dir in new DirectoryInfo(source).GetDirectories())
            {
                foreach (FileInfo f in dir.GetFiles())
                {
                    if (IsAudio(f.Name)) todo.Add(new KeyValuePair<FileInfo, string>(f, Path.Combine(LibraryFolder, dir.Name, f.Name)));
                }
            }
            long need = 0;
            foreach (KeyValuePair<FileInfo, string> t in todo)
            {
                FileInfo there = new FileInfo(Path.Combine(folder, t.Value));
                if (!there.Exists || there.Length != t.Key.Length) need += t.Key.Length;
            }
            CheckSpace(folder, need);
            Log("Biblioteca: " + todo.Count + " sons de " + source + ", " + SizeText(need) + " a copiar");

            int done = 0;
            foreach (KeyValuePair<FileInfo, string> t in todo)
            {
                string dst = Path.Combine(folder, t.Value);
                FileInfo there = new FileInfo(dst);
                if (!there.Exists || there.Length != t.Key.Length)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(dst));
                    File.Copy(t.Key.FullName, dst, true);
                }
                list.Add(t.Value);
                done++;
                if (done % 100 == 0 || done == todo.Count)
                    progress(from + (to - from) * done / todo.Count, "Biblioteca de sons: " + done.ToString("N0") + " de " + todo.Count.ToString("N0"));
            }

            // Icones das categorias: os da pasta de origem e, para o que faltar, os que vem no instalador.
            Directory.CreateDirectory(target);
            foreach (FileInfo png in new DirectoryInfo(source).GetFiles("*.png"))
            {
                png.CopyTo(Path.Combine(target, png.Name), true);
                list.Add(Path.Combine(LibraryFolder, png.Name));
            }
            foreach (string name in Resources(IconPrefix))
            {
                string dst = Path.Combine(target, name);
                if (File.Exists(dst)) continue;
                using (Stream s = OpenResource(IconPrefix + name)) WriteFile(dst, s);
                list.Add(Path.Combine(LibraryFolder, name));
            }
        }

        // ---------- atalhos do Windows (.lnk) pelo WScript.Shell, que existe em todo Windows ----------

        static object Call(object target, string member, BindingFlags kind, params object[] args)
        {
            return target.GetType().InvokeMember(member, kind, null, target, args);
        }

        static object OpenLink(out object shell, string lnk)
        {
            Type type = Type.GetTypeFromProgID("WScript.Shell");
            if (type == null) throw new InvalidOperationException("O Windows Script Host está desligado neste PC.");
            shell = Activator.CreateInstance(type);
            return Call(shell, "CreateShortcut", BindingFlags.InvokeMethod, lnk);
        }

        public static void CreateShortcut(string lnk, string target, string description)
        {
            object shell;
            object link = OpenLink(out shell, lnk);
            try
            {
                Call(link, "TargetPath", BindingFlags.SetProperty, target);
                Call(link, "WorkingDirectory", BindingFlags.SetProperty, Path.GetDirectoryName(target));
                Call(link, "IconLocation", BindingFlags.SetProperty, target + ",0");
                Call(link, "Description", BindingFlags.SetProperty, description);
                Call(link, "Save", BindingFlags.InvokeMethod);
            }
            finally
            {
                Marshal.ReleaseComObject(link);
                Marshal.ReleaseComObject(shell);
            }
        }

        public static string ShortcutTarget(string lnk)
        {
            object shell;
            object link = OpenLink(out shell, lnk);
            try { return Call(link, "TargetPath", BindingFlags.GetProperty) as string; }
            finally
            {
                Marshal.ReleaseComObject(link);
                Marshal.ReleaseComObject(shell);
            }
        }

        // So apaga o atalho que abre esta copia do programa: um "Atalhos PC.lnk" do usuario para outro lugar fica.
        static void RemoveShortcutIfOurs(string lnk, string exe, List<string> notes)
        {
            if (!File.Exists(lnk)) return;
            try
            {
                if (SamePath(ShortcutTarget(lnk), exe)) File.Delete(lnk);
            }
            catch (Exception e)
            {
                if (!(e is IOException || e is UnauthorizedAccessException || e is COMException || e is TargetInvocationException || e is InvalidOperationException)) throw;
                notes.Add("Não consegui apagar o atalho " + lnk + ": " + e.Message);
            }
        }

        static void MakeShortcut(bool wanted, string dir, string exe, List<string> notes)
        {
            string lnk = Path.Combine(dir, LinkName);
            if (!wanted)
            {
                RemoveShortcutIfOurs(lnk, exe, notes);
                return;
            }
            try
            {
                Directory.CreateDirectory(dir);
                CreateShortcut(lnk, exe, "Abra arquivos e toque sons do PC pelo tablet ou celular");
            }
            catch (Exception e)
            {
                if (!(e is IOException || e is UnauthorizedAccessException || e is COMException || e is TargetInvocationException || e is InvalidOperationException)) throw;
                notes.Add("Não consegui criar o atalho " + lnk + ": " + e.Message);
            }
        }

        // ---------- instalar ----------

        // Retorna avisos que nao impedem o uso (por exemplo, atalho que nao pode ser criado).
        public static List<string> Install(InstallPlan plan, Action<int, string> progress)
        {
            List<string> notes = new List<string>();
            List<string> files = PayloadFiles();
            if (!files.Contains(ExeName, StringComparer.OrdinalIgnoreCase)) throw new InvalidOperationException("Este instalador está incompleto: o programa não está dentro dele.");
            string folder = Path.GetFullPath(plan.Folder.Trim());
            Log("Instalando a versao " + AppInfo.Version + " em " + folder);

            progress(2, "Preparando a pasta");
            try
            {
                Directory.CreateDirectory(folder);
                string probe = Path.Combine(folder, ".teste-" + Guid.NewGuid().ToString("N"));
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
            }
            catch (UnauthorizedAccessException)
            {
                throw new UnauthorizedAccessException("Sem permissão para gravar em " + folder + ". Use a pasta sugerida, que é sua, ou abra o instalador como administrador.");
            }

            if (plan.CloseRunning)
            {
                progress(5, "Fechando o Atalhos PC aberto");
                if (!StopRunning(null)) throw new InvalidOperationException("Não consegui fechar o Atalhos PC aberto. Feche pelo ícone perto do relógio (botão direito, Sair) e tente de novo.");
            }

            HashSet<string> list = ReadList(folder);
            for (int i = 0; i < files.Count; i++)
            {
                progress(8 + 22 * i / files.Count, "Copiando " + files[i]);
                using (Stream s = OpenPayload(files[i])) WriteFile(Path.Combine(folder, files[i]), s);
                list.Add(files[i]);
            }

            progress(30, "Criando o desinstalador");
            string uninstaller = Path.Combine(folder, UninstallerName);
            if (!SamePath(SelfPath, uninstaller)) File.Copy(SelfPath, uninstaller, true);
            list.Add(UninstallerName);
            list.Add(ListName);

            if (!string.IsNullOrEmpty(plan.LibrarySource))
            {
                progress(32, "Conferindo a biblioteca de sons");
                CopyLibrary(plan.LibrarySource, folder, list, progress, 32, 92);
            }

            progress(93, "Criando os atalhos");
            string exe = Path.Combine(folder, ExeName);
            MakeShortcut(plan.DesktopShortcut, plan.DesktopDir, exe, notes);
            MakeShortcut(plan.StartMenuShortcut, plan.StartMenuDir, exe, notes);

            progress(96, "Registrando no Windows");
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(plan.RunKey))
            {
                if (plan.Autostart) k.SetValue(plan.RunValue, Quote(exe) + " --bandeja");
                else k.DeleteValue(plan.RunValue, false);
            }
            WriteList(folder, list);
            long bytes = 0;
            foreach (string rel in list)
            {
                FileInfo f = new FileInfo(Path.Combine(folder, rel));
                if (f.Exists) bytes += f.Length;
            }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(plan.UninstallKey))
            {
                k.SetValue("DisplayName", AppInfo.Name);
                k.SetValue("DisplayVersion", AppInfo.Version);
                k.SetValue("Publisher", AppInfo.Publisher);
                k.SetValue("DisplayIcon", exe + ",0");
                k.SetValue("InstallLocation", folder);
                k.SetValue("UninstallString", Quote(uninstaller) + " /desinstalar");
                k.SetValue("QuietUninstallString", Quote(uninstaller) + " /desinstalar /silencioso");
                k.SetValue("URLInfoAbout", Greeting.Url);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"));
                k.SetValue("EstimatedSize", (int)Math.Min(int.MaxValue, bytes / 1024), RegistryValueKind.DWord);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
            foreach (string n in notes) Log("Aviso: " + n);
            Log("Instalado: " + list.Count + " arquivos, " + SizeText(bytes));
            progress(100, "Pronto");
            return notes;
        }

        public static void Launch(string folder)
        {
            string exe = Path.Combine(Path.GetFullPath(folder), ExeName);
            Process.Start(new ProcessStartInfo(exe, AppInfo.GreetedArg) { WorkingDirectory = Path.GetDirectoryName(exe), UseShellExecute = true });
        }

        // ---------- desinstalar ----------

        // Retorna true quando o proprio desinstalador esta dentro da pasta e so pode ser apagado depois de fechar.
        public static bool Uninstall(InstallPlan plan, List<string> notes)
        {
            string folder = Path.GetFullPath(plan.Folder.Trim());
            string exe = Path.Combine(folder, ExeName);
            Log("Desinstalando de " + folder);
            if (!StopRunning(folder)) throw new InvalidOperationException("Não consegui fechar o Atalhos PC aberto. Feche pelo ícone perto do relógio (botão direito, Sair) e tente de novo.");

            RemoveShortcutIfOurs(Path.Combine(plan.DesktopDir, LinkName), exe, notes);
            RemoveShortcutIfOurs(Path.Combine(plan.StartMenuDir, LinkName), exe, notes);
            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(plan.RunKey, true))
            {
                string value = k == null ? null : k.GetValue(plan.RunValue) as string;
                if (value != null && value.IndexOf(exe, StringComparison.OrdinalIgnoreCase) >= 0) k.DeleteValue(plan.RunValue, false);
            }

            HashSet<string> list = ReadList(folder);
            if (list.Count == 0)
            {
                // Pasta sem lista (copiada a mao): so os arquivos que este instalador conhece.
                foreach (string f in PayloadFiles()) list.Add(f);
                list.Add(UninstallerName);
            }
            bool selfLater = false;
            HashSet<string> dirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rel in list)
            {
                string full = SafePath(folder, rel);
                if (full == null)
                {
                    Log("Ignorado, fora da pasta do programa: " + rel);
                    continue;
                }
                for (string d = Path.GetDirectoryName(full); Inside(folder, d); d = Path.GetDirectoryName(d)) dirs.Add(d);
                if (SamePath(full, SelfPath))
                {
                    selfLater = true;
                    continue;
                }
                try { if (File.Exists(full)) File.Delete(full); }
                catch (IOException e) { notes.Add("Não consegui apagar " + full + ": " + e.Message); }
                catch (UnauthorizedAccessException e) { notes.Add("Não consegui apagar " + full + ": " + e.Message); }
            }
            foreach (string d in dirs.OrderByDescending(x => x.Length)) RemoveIfEmpty(d);

            // A entrada em "Aplicativos" so sai se for desta pasta.
            string registered = InstalledFolder(plan);
            if (registered == null || SamePath(registered, folder)) Registry.CurrentUser.DeleteSubKeyTree(plan.UninstallKey, false);
            if (!selfLater) RemoveIfEmpty(folder);
            foreach (string n in notes) Log("Aviso: " + n);
            Log("Desinstalado" + (selfLater ? "; o desinstalador sai em seguida" : ""));
            return selfLater;
        }

        static void RemoveIfEmpty(string dir)
        {
            try
            {
                if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any()) Directory.Delete(dir);
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        // O Windows nao deixa apagar um exe aberto: um cmd escondido apaga o desinstalador depois que ele fecha.
        // rmdir sem /s so remove a pasta se ela estiver vazia.
        public static void ScheduleSelfDelete(string folder)
        {
            string command = "/c ping -n 4 127.0.0.1 >nul & del /f /q " + Quote(SelfPath) + " & rmdir " + Quote(Path.GetFullPath(folder).TrimEnd('\\'));
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", command)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Path.GetTempPath()
            };
            using (Process.Start(psi)) { }
        }
    }
}
