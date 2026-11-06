// Ponto de entrada do instalador. Sem argumentos abre o assistente. Para scripts:
//   Instalar-AtalhosPC.exe /silencioso [/pasta="C:\..."] [/biblioteca="C:\..."] [/sem-atalhos] [/iniciar-com-windows] [/abrir]
//   Desinstalar.exe /desinstalar [/silencioso] [/pasta="C:\..."]
// Codigo de saida: 0 deu certo, 1 erro (detalhes em %TEMP%\AtalhosPC-instalador.log), 2 cancelado.
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

[assembly: AssemblyTitle("Instalador do Atalhos PC")]
[assembly: AssemblyProduct("Atalhos PC")]
[assembly: AssemblyCompany(AtalhosPC.AppInfo.Publisher)]
[assembly: AssemblyVersion(AtalhosPC.AppInfo.Version + ".0.0")]
[assembly: AssemblyFileVersion(AtalhosPC.AppInfo.Version + ".0.0")]

namespace AtalhosPC.Setup
{
    public static class SetupProgram
    {
        const string Caption = "Atalhos PC";

        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        public static Dictionary<string, string> ParseArgs(string[] args)
        {
            Dictionary<string, string> opt = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string raw in args)
            {
                string a = raw.TrimStart('/', '-');
                int eq = a.IndexOf('=');
                if (eq < 0) opt[a] = "";
                else opt[a.Substring(0, eq)] = a.Substring(eq + 1).Trim().Trim('"');
            }
            return opt;
        }

        [STAThread]
        public static int Main(string[] args)
        {
            Dictionary<string, string> opt = ParseArgs(args);
            bool silent = opt.ContainsKey("silencioso");
            bool uninstall = opt.ContainsKey("desinstalar") || string.Equals(Path.GetFileName(Installer.SelfPath), Installer.UninstallerName, StringComparison.OrdinalIgnoreCase);
            SetProcessDPIAware();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                return uninstall ? Uninstall(opt, silent) : Install(opt, silent);
            }
            catch (Exception e)
            {
                // ultima rede: qualquer falha vira mensagem e linha no log, nunca uma janela de erro do .NET
                Installer.Log("ERRO: " + e);
                if (!silent) MessageBox.Show(e.Message, Caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return 1;
            }
        }

        static int Install(Dictionary<string, string> opt, bool silent)
        {
            InstallPlan plan = new InstallPlan();
            string existing = Installer.InstalledFolder(plan);
            string folder;
            if (opt.TryGetValue("pasta", out folder) && folder.Length > 0) plan.Folder = folder;
            else if (!string.IsNullOrEmpty(existing)) plan.Folder = existing;
            plan.Autostart = Installer.AutostartEnabled(plan) || opt.ContainsKey("iniciar-com-windows");
            if (opt.ContainsKey("sem-atalhos")) plan.DesktopShortcut = plan.StartMenuShortcut = false;
            string library;
            opt.TryGetValue("biblioteca", out library);

            if (!silent)
            {
                using (SetupForm form = new SetupForm(plan, string.IsNullOrEmpty(library) ? Installer.GuessLibrary() : library))
                {
                    Application.Run(form);
                    return form.ExitCode;
                }
            }

            // Em modo silencioso a biblioteca so e copiada quando pedida, para o script saber exatamente o que acontece.
            if (!string.IsNullOrEmpty(library))
            {
                if (Installer.InspectLibrary(library).Files == 0) throw new DirectoryNotFoundException("Nenhum som encontrado em " + library + ". A pasta precisa ter uma subpasta por categoria.");
                plan.LibrarySource = library;
            }
            Installer.Install(plan, (p, s) => { });
            if (opt.ContainsKey("abrir")) Installer.Launch(plan.Folder);
            return 0;
        }

        static int Uninstall(Dictionary<string, string> opt, bool silent)
        {
            InstallPlan plan = new InstallPlan();
            string folder;
            if (!opt.TryGetValue("pasta", out folder) || folder.Length == 0)
            {
                folder = string.Equals(Path.GetFileName(Installer.SelfPath), Installer.UninstallerName, StringComparison.OrdinalIgnoreCase)
                    ? Path.GetDirectoryName(Installer.SelfPath)
                    : Installer.InstalledFolder(plan);
            }
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
            {
                Installer.Log("Desinstalar: nada instalado");
                if (!silent) MessageBox.Show("O Atalhos PC não está instalado neste computador.", Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 1;
            }
            plan.Folder = folder;
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtalhosPC");
            if (!silent)
            {
                string ask = "Remover o Atalhos PC deste computador?\n\n" +
                    "Seus atalhos, sons e aparelhos pareados continuam guardados em " + data + ", prontos para uma nova instalação.";
                if (MessageBox.Show(ask, "Desinstalar o Atalhos PC", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2) != DialogResult.Yes) return 2;
            }
            List<string> notes = new List<string>();
            Cursor.Current = Cursors.WaitCursor;
            bool selfLater = Installer.Uninstall(plan, notes);
            Cursor.Current = Cursors.Default;
            if (!silent)
            {
                string done = "O Atalhos PC foi removido.\n\n" + Greeting.Closing + " Siga " + Greeting.Handle + " no Instagram.";
                if (notes.Count > 0) done += "\n\nAvisos:\n" + string.Join("\n", notes);
                MessageBox.Show(done, Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            if (selfLater) Installer.ScheduleSelfDelete(folder);
            return 0;
        }
    }
}
