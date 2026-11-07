using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Atalhos PC")]
[assembly: AssemblyProduct("Atalhos PC")]
[assembly: AssemblyCompany(AtalhosPC.AppInfo.Publisher)]
[assembly: AssemblyVersion(AtalhosPC.AppInfo.Version + ".0.0")]
[assembly: AssemblyFileVersion(AtalhosPC.AppInfo.Version + ".0.0")]

namespace AtalhosPC
{
    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main(string[] args)
        {
            bool created;
            using (Mutex single = new Mutex(true, AppInfo.SingleMutex, out created))
            using (EventWaitHandle showSignal = new EventWaitHandle(false, EventResetMode.AutoReset, AppInfo.ShowEvent))
            {
                if (!created)
                {
                    showSignal.Set(); // ja esta rodando: so traz a janela para frente
                    return;
                }
                using (EventWaitHandle exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, AppInfo.ExitEvent))
                {
                    SetProcessDPIAware();
                    Application.EnableVisualStyles();
                    Application.SetCompatibleTextRenderingDefault(false);

                    string dir = Store.DefaultDir();
                    System.IO.Directory.CreateDirectory(dir);
                    Log.Init(dir);
                    Store store = new Store(dir);
                    if (args.Contains(AppInfo.GreetedArg) && !store.Greeted) store.Greeted = true;
                    Icons.Init(dir);
                    string exeDir = System.IO.Path.GetDirectoryName(Sys.ExePath);
                    try { store.SeedSounds(System.IO.Path.Combine(exeDir, "sons")); }
                    catch (System.IO.IOException e) { Log.Write("Sons de exemplo: " + e.Message); }
                    Server server = new Server(store);
                    server.Player = new MciPlayer(System.IO.Path.Combine(dir, "som-sem-etiqueta"));
                    server.Library = new Library(Library.ResolveRoot(store.LibraryDir, exeDir));
                    if (store.LibraryDir != null && server.Library.Root != store.LibraryDir) Log.Write("Pasta da biblioteca não encontrada: " + store.LibraryDir);
                    server.Start();

                    MainForm form = new MainForm(store, server, args.Contains("--bandeja"));
                    Thread watcher = new Thread(() =>
                    {
                        WaitHandle[] signals = { showSignal, exitSignal };
                        while (true)
                        {
                            int which = WaitHandle.WaitAny(signals);
                            if (!form.IsHandleCreated) continue;
                            if (which == 0) form.BeginInvoke((Action)form.ShowWindow);
                            else
                            {
                                // pedido do instalador: fecha como o "Sair" da bandeja, sem deixar icone fantasma
                                Log.Write("Fechado pelo instalador");
                                form.BeginInvoke((Action)form.ExitApp);
                                return;
                            }
                        }
                    });
                    watcher.IsBackground = true;
                    watcher.Start();

                    Application.Run(form);
                    GC.KeepAlive(single);
                }
            }
        }
    }
}
