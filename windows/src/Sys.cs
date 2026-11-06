// Integracao com o Windows: iniciar com o sistema, regra de firewall e USB por adb.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Win32;

namespace AtalhosPC
{
    public static class Sys
    {
        const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        const string RunName = "AtalhosPC";
        const string RuleName = "Atalhos PC";

        public static string ExePath { get { return Process.GetCurrentProcess().MainModule.FileName; } }

        public static bool Autostart
        {
            get
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(RunKey))
                    return k != null && k.GetValue(RunName) != null;
            }
            set
            {
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (value) k.SetValue(RunName, "\"" + ExePath + "\" --bandeja");
                    else k.DeleteValue(RunName, false);
                }
            }
        }

        static string Run(string exe, string args, int timeoutMs)
        {
            ProcessStartInfo psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using (Process p = Process.Start(psi))
            {
                // leitura assincrona: um processo com saida grande nao trava esperando alguem ler
                string err = "";
                Thread t = new Thread(() => { err = p.StandardError.ReadToEnd(); });
                t.IsBackground = true;
                t.Start();
                string output = p.StandardOutput.ReadToEnd();
                if (!p.WaitForExit(timeoutMs)) { try { p.Kill(); } catch (Win32Exception) { } }
                t.Join(1000);
                return output + err;
            }
        }

        public static bool FirewallRuleExists()
        {
            try
            {
                // vale qualquer regra deste programa, inclusive a criada pelo proprio aviso do Windows
                string text = Run("netsh", "advfirewall firewall show rule name=all dir=in verbose", 30000);
                foreach (string block in text.Replace("\r", "").Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries))
                {
                    if (block.IndexOf(ExePath, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (block.IndexOf("Allow", StringComparison.OrdinalIgnoreCase) >= 0 || block.IndexOf("Permitir", StringComparison.OrdinalIgnoreCase) >= 0) return true;
                }
                return false;
            }
            catch (Win32Exception) { return false; }
        }

        // Pede a confirmacao do UAC. A regra vale so para este programa e so para a sub-rede local.
        public static bool AddFirewallRule()
        {
            string exe = ExePath;
            string command = "/c netsh advfirewall firewall delete rule name=\"" + RuleName + "\" >nul 2>&1 & " +
                "netsh advfirewall firewall add rule name=\"" + RuleName + "\" dir=in action=allow enable=yes profile=any " +
                "remoteip=localsubnet program=\"" + exe + "\"";
            ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", command)
            {
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            };
            try
            {
                using (Process p = Process.Start(psi)) p.WaitForExit(20000);
            }
            catch (Win32Exception) { return false; } // o usuario recusou o UAC
            return FirewallRuleExists();
        }

        public static List<string> LocalAddresses()
        {
            List<string> found = new List<string>();
            foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus != OperationalStatus.Up) continue;
                if (ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (ni.Description.IndexOf("Hyper-V", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                if (ni.Description.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                foreach (UnicastIPAddressInformation a in ni.GetIPProperties().UnicastAddresses)
                {
                    if (a.Address.AddressFamily != AddressFamily.InterNetwork) continue;
                    bool usb = ni.Description.IndexOf("NDIS", StringComparison.OrdinalIgnoreCase) >= 0;
                    found.Add(a.Address + (usb ? "  (USB)" : ""));
                }
            }
            return found;
        }

        public static string FindAdb()
        {
            string beside = Path.GetDirectoryName(ExePath);
            List<string> places = new List<string> { beside, Path.Combine(beside, "platform-tools") };
            string dir = beside;
            for (int i = 0; i < 3 && dir != null; i++)
            {
                dir = Path.GetDirectoryName(dir);
                if (dir != null) places.Add(Path.Combine(dir, "platform-tools"));
            }
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            places.Add(Path.Combine(local, @"Android\Sdk\platform-tools"));
            string envPath = Environment.GetEnvironmentVariable("PATH") ?? "";
            places.AddRange(envPath.Split(';'));
            foreach (string p in places)
            {
                try
                {
                    string candidate = Path.Combine(p.Trim(), "adb.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException) { }
            }
            return null;
        }

        public static void RemoveAdbReverse(string adb, int port)
        {
            foreach (string raw in Run(adb, "devices", 8000).Split('\n'))
            {
                string[] parts = raw.Trim().Split('\t');
                if (parts.Length == 2 && parts[1] == "device") Run(adb, "-s " + parts[0] + " reverse --remove tcp:" + port, 8000);
            }
        }

        // Liga o tunel USB em cada aparelho autorizado. Retorna quantos estao com o tunel ativo.
        public static int ApplyAdbReverse(string adb, int port)
        {
            int ok = 0;
            string[] lines = Run(adb, "devices", 8000).Split('\n');
            foreach (string raw in lines)
            {
                string[] parts = raw.Trim().Split('\t');
                if (parts.Length != 2 || parts[1] != "device") continue; // ignora offline, unauthorized e recovery
                string answer = Run(adb, "-s " + parts[0] + " reverse tcp:" + port + " tcp:" + port, 8000);
                if (answer.IndexOf("error", StringComparison.OrdinalIgnoreCase) < 0) ok++;
            }
            return ok;
        }
    }
}
