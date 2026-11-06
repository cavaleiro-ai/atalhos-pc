// Assistente de instalacao em quatro passos: saudacao, opcoes, copia e fim. A copia roda em outra thread.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace AtalhosPC.Setup
{
    public sealed class SetupForm : Form
    {
        static readonly Color Ink = Color.FromArgb(0x1F, 0x23, 0x30);
        static readonly Color Muted = Color.FromArgb(0x5F, 0x66, 0x78);
        static readonly Color Blue = Color.FromArgb(0x1F, 0x5F, 0xD0);
        static readonly Color Warn = Color.FromArgb(0xB4, 0x53, 0x09);

        readonly InstallPlan plan;
        readonly Sidebar sidebar = new Sidebar();
        readonly List<Panel> pages = new List<Panel>();
        readonly Button backButton = new Button();
        readonly Button nextButton = new Button();
        readonly Button cancelButton = new Button();
        readonly TextBox folderBox = new TextBox();
        readonly Label folderNote = new Label();
        readonly CheckBox desktopBox = new CheckBox();
        readonly CheckBox startMenuBox = new CheckBox();
        readonly CheckBox autostartBox = new CheckBox();
        readonly CheckBox libraryBox = new CheckBox();
        readonly TextBox libraryPath = new TextBox();
        readonly Button libraryButton = new Button();
        readonly Label libraryNote = new Label();
        readonly Label runningNote = new Label();
        readonly ProgressBar bar = new ProgressBar();
        readonly Label progressText = new Label();
        readonly ListBox logList = new ListBox();
        readonly Label doneTitle = new Label();
        readonly Label doneText = new Label();
        readonly Button openFolderButton = new Button();
        readonly CheckBox openBox = new CheckBox();
        LibraryInfo libraryInfo = new LibraryInfo();
        int page;
        volatile bool busy;
        bool installed;

        public int ExitCode = 2; // 0 instalado, 1 erro, 2 cancelado

        static int Px(int v) { return Greeting.Px(v); }

        public SetupForm(InstallPlan p, string libraryGuess)
        {
            plan = p;
            Text = "Instalar o Atalhos PC " + AppInfo.Version;
            Font = new Font("Segoe UI", 9.5f);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            ClientSize = new Size(Px(800), Px(540));
            try { Icon = Icon.ExtractAssociatedIcon(Installer.SelfPath); } catch (ArgumentException) { }

            sidebar.Dock = DockStyle.Left;
            sidebar.Width = Px(220);
            using (Stream s = Installer.OpenResource("logo.png"))
            {
                if (s != null) using (Image img = Image.FromStream(s)) sidebar.Logo = new Bitmap(img);
            }

            Panel content = new Panel { Dock = DockStyle.Fill, Padding = new Padding(Px(28), Px(24), Px(28), Px(16)) };
            pages.Add(BuildWelcome());
            pages.Add(BuildOptions(libraryGuess));
            pages.Add(BuildProgress());
            pages.Add(BuildDone());
            foreach (Panel pg in pages)
            {
                pg.Dock = DockStyle.Fill;
                pg.Visible = false;
                content.Controls.Add(pg);
            }

            Controls.Add(content);
            Controls.Add(BuildButtons());
            Controls.Add(sidebar);
            AcceptButton = nextButton;
            CancelButton = cancelButton;
            ShowPage(0);
        }

        // ---------- montagem ----------

        static Label Title(string text)
        {
            return new Label { Text = text, Dock = DockStyle.Top, Height = Px(40), Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Ink };
        }

        static Label Note(string text, int height)
        {
            return new Label { Text = text, ForeColor = Muted, AutoSize = false, Height = height };
        }

        Panel BuildWelcome()
        {
            Panel pg = new Panel();
            GreetingView view = new GreetingView { Dock = DockStyle.Fill };
            Label intro = new Label
            {
                Dock = DockStyle.Bottom,
                Height = Px(52),
                ForeColor = Muted,
                TextAlign = ContentAlignment.BottomLeft,
                Text = "Este assistente instala o Atalhos PC " + AppInfo.Version + " neste computador, só para o seu usuário e sem pedir senha de administrador."
            };
            pg.Controls.Add(view);
            pg.Controls.Add(intro);
            return pg;
        }

        Panel BuildOptions(string libraryGuess)
        {
            Panel pg = new Panel();
            int w = Px(800 - 220 - 56);
            int y = Px(46);

            pg.Controls.Add(new Label { Text = "Onde e como instalar", Left = 0, Top = 0, Width = w, Height = Px(36), Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Ink });
            pg.Controls.Add(new Label { Text = "Pasta do programa", Left = 0, Top = y, Width = w, Height = Px(20), ForeColor = Ink });
            y += Px(22);
            folderBox.SetBounds(0, y, w - Px(112), Px(26));
            folderBox.Text = plan.Folder;
            folderBox.Leave += (a, e) => RefreshFolderNote();
            Button browse = new Button { Text = "Escolher…" };
            browse.SetBounds(w - Px(104), y - 1, Px(104), Px(28));
            browse.Click += (a, e) => ChooseFolder();
            pg.Controls.Add(folderBox);
            pg.Controls.Add(browse);
            y += Px(32);
            folderNote.SetBounds(0, y, w, Px(38));
            folderNote.ForeColor = Muted;
            pg.Controls.Add(folderNote);
            y += Px(46);

            desktopBox.Text = "Criar atalho na área de trabalho";
            desktopBox.Checked = plan.DesktopShortcut;
            startMenuBox.Text = "Criar atalho no menu Iniciar";
            startMenuBox.Checked = plan.StartMenuShortcut;
            autostartBox.Text = "Iniciar junto com o Windows (fica perto do relógio, pronto para o tablet)";
            autostartBox.Checked = plan.Autostart;
            foreach (CheckBox c in new[] { desktopBox, startMenuBox, autostartBox })
            {
                c.SetBounds(0, y, w, Px(26));
                pg.Controls.Add(c);
                y += Px(28);
            }
            y += Px(12);

            libraryBox.Text = "Copiar a biblioteca de sons para a pasta do programa";
            libraryBox.SetBounds(0, y, w, Px(26));
            libraryBox.CheckedChanged += (a, e) => RefreshLibrary(false);
            pg.Controls.Add(libraryBox);
            y += Px(28);
            libraryPath.SetBounds(Px(20), y, w - Px(132), Px(26));
            libraryPath.Text = libraryGuess ?? "";
            libraryPath.Leave += (a, e) => RefreshLibrary(true);
            libraryButton.Text = "Escolher…";
            libraryButton.SetBounds(w - Px(104), y - 1, Px(104), Px(28));
            libraryButton.Click += (a, e) => ChooseLibrary();
            pg.Controls.Add(libraryPath);
            pg.Controls.Add(libraryButton);
            y += Px(32);
            libraryNote.SetBounds(Px(20), y, w - Px(20), Px(38));
            libraryNote.ForeColor = Muted;
            pg.Controls.Add(libraryNote);
            y += Px(44);

            runningNote.SetBounds(0, y, w, Px(40));
            runningNote.ForeColor = Warn;
            pg.Controls.Add(runningNote);

            libraryInfo = Installer.InspectLibrary(libraryPath.Text);
            libraryBox.Checked = libraryInfo.Files > 0;
            RefreshFolderNote();
            RefreshLibrary(false);
            return pg;
        }

        Panel BuildProgress()
        {
            Panel pg = new Panel();
            int w = Px(800 - 220 - 56);
            pg.Controls.Add(new Label { Text = "Instalando…", Left = 0, Top = 0, Width = w, Height = Px(36), Font = new Font("Segoe UI", 15f, FontStyle.Bold), ForeColor = Ink });
            bar.SetBounds(0, Px(52), w, Px(22));
            bar.Maximum = 100;
            progressText.SetBounds(0, Px(80), w, Px(24));
            progressText.ForeColor = Muted;
            logList.SetBounds(0, Px(112), w, Px(300));
            logList.IntegralHeight = false;
            logList.BorderStyle = BorderStyle.FixedSingle;
            pg.Controls.Add(bar);
            pg.Controls.Add(progressText);
            pg.Controls.Add(logList);
            return pg;
        }

        Panel BuildDone()
        {
            Panel pg = new Panel();
            int w = Px(800 - 220 - 56);
            doneTitle.SetBounds(0, 0, w, Px(36));
            doneTitle.Font = new Font("Segoe UI", 15f, FontStyle.Bold);
            doneTitle.ForeColor = Ink;
            doneText.SetBounds(0, Px(46), w, Px(210));
            doneText.ForeColor = Ink;
            openFolderButton.Text = "Abrir a pasta do programa";
            openFolderButton.AutoSize = true;
            openFolderButton.Location = new Point(0, Px(262));
            openFolderButton.Click += (a, e) =>
            {
                try { Process.Start(Path.GetFullPath(plan.Folder)); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Atalhos PC", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            openBox.Text = "Abrir o Atalhos PC agora";
            openBox.Checked = true;
            openBox.SetBounds(0, Px(308), w, Px(26));

            // faixa da marca no fim
            Panel brand = new Panel { BackColor = Greeting.Back, Cursor = Cursors.Hand };
            brand.SetBounds(0, Px(352), w, Px(56));
            Label line1 = new Label { Text = Greeting.Closing, ForeColor = Greeting.Gold, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Cursor = Cursors.Hand };
            line1.SetBounds(Px(14), Px(8), w - Px(28), Px(22));
            Label line2 = new Label { Text = "Cavaleiro AI · siga " + Greeting.Handle + " no Instagram", ForeColor = Greeting.Soft, Cursor = Cursors.Hand };
            line2.SetBounds(Px(14), Px(30), w - Px(28), Px(20));
            EventHandler follow = (a, e) => Greeting.OpenInstagram();
            brand.Click += follow;
            line1.Click += follow;
            line2.Click += follow;
            brand.Controls.Add(line1);
            brand.Controls.Add(line2);

            pg.Controls.Add(doneTitle);
            pg.Controls.Add(doneText);
            pg.Controls.Add(openFolderButton);
            pg.Controls.Add(openBox);
            pg.Controls.Add(brand);
            return pg;
        }

        Control BuildButtons()
        {
            Panel strip = new Panel { Dock = DockStyle.Bottom, Height = Px(60), BackColor = Color.FromArgb(0xF3, 0xF4, 0xF7) };
            strip.Paint += (a, e) =>
            {
                using (Pen line = new Pen(Color.FromArgb(0xDD, 0xE0, 0xE6))) e.Graphics.DrawLine(line, 0, 0, strip.Width, 0);
            };
            int right = Px(800 - 220) - Px(20);
            int top = Px(14);
            cancelButton.Text = "Cancelar";
            cancelButton.SetBounds(right - Px(104), top, Px(104), Px(32));
            cancelButton.Click += (a, e) => Close();
            nextButton.SetBounds(right - Px(104 + 10 + 120), top, Px(120), Px(32));
            nextButton.FlatStyle = FlatStyle.Flat;
            nextButton.FlatAppearance.BorderSize = 0;
            nextButton.BackColor = Blue;
            nextButton.ForeColor = Color.White;
            nextButton.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            nextButton.Click += (a, e) => Next();
            backButton.Text = "< Voltar";
            backButton.SetBounds(right - Px(104 + 10 + 120 + 10 + 104), top, Px(104), Px(32));
            backButton.Click += (a, e) => ShowPage(page - 1);
            // a barra lateral entra por ultimo e ocupa a altura toda; esta faixa fica so embaixo do conteudo
            strip.Controls.Add(backButton);
            strip.Controls.Add(nextButton);
            strip.Controls.Add(cancelButton);
            return strip;
        }

        // ---------- navegacao ----------

        void ShowPage(int index)
        {
            page = Math.Max(0, Math.Min(pages.Count - 1, index));
            for (int i = 0; i < pages.Count; i++) pages[i].Visible = i == page;
            sidebar.Step = page;
            sidebar.Invalidate();
            backButton.Visible = page == 1;
            cancelButton.Visible = page < 3;
            cancelButton.Enabled = page != 2;
            nextButton.Enabled = page != 2;
            nextButton.Text = page == 0 ? "Avançar >" : page == 1 ? "Instalar" : "Concluir";
            if (page == 1)
            {
                List<System.Diagnostics.Process> open = Installer.Running(null);
                runningNote.Text = open.Count > 0 ? "O Atalhos PC está aberto e será fechado durante a instalação. Ele volta a abrir no fim, se você quiser." : "";
                foreach (System.Diagnostics.Process pr in open) pr.Dispose();
            }
            nextButton.Focus();
        }

        void Next()
        {
            if (page == 0) ShowPage(1);
            else if (page == 1) StartInstall();
            else if (page == 3)
            {
                if (installed && openBox.Checked)
                {
                    try { Installer.Launch(plan.Folder); }
                    catch (Exception e) { MessageBox.Show(this, "Não consegui abrir o programa: " + e.Message, "Atalhos PC", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                }
                Close();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (busy)
            {
                e.Cancel = true; // parar no meio deixaria a pasta pela metade
                return;
            }
            if (page < 2 && e.CloseReason == CloseReason.UserClosing)
            {
                if (MessageBox.Show(this, "Cancelar a instalação?", "Atalhos PC", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
            }
            base.OnFormClosing(e);
        }

        // ---------- opcoes ----------

        void ChooseFolder()
        {
            string start = "";
            try { start = Path.GetDirectoryName(folderBox.Text.Trim().TrimEnd('\\')) ?? ""; }
            catch (ArgumentException) { }
            using (FolderBrowserDialog d = new FolderBrowserDialog { Description = "Escolha onde instalar. O programa vai para uma subpasta AtalhosPC.", SelectedPath = start })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string chosen = d.SelectedPath;
                if (!string.Equals(Path.GetFileName(chosen.TrimEnd('\\')), "AtalhosPC", StringComparison.OrdinalIgnoreCase)) chosen = Path.Combine(chosen, "AtalhosPC");
                folderBox.Text = chosen;
                RefreshFolderNote();
            }
        }

        void RefreshFolderNote()
        {
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AtalhosPC");
            string version = null;
            string registered = Installer.InstalledFolder(plan);
            try
            {
                if (registered != null && string.Equals(Path.GetFullPath(registered).TrimEnd('\\'), Path.GetFullPath(folderBox.Text.Trim()).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                    version = Installer.InstalledVersion(plan);
            }
            catch (ArgumentException) { }
            catch (NotSupportedException) { }
            string head = version != null ? "Já instalado aqui (versão " + version + "): será atualizado. " : "";
            folderNote.Text = head + "Seus atalhos, sons e aparelhos pareados ficam em " + data + " e o instalador nunca os apaga.";
        }

        void ChooseLibrary()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog { Description = "Escolha a pasta da biblioteca: uma subpasta por categoria, com os sons dentro.", SelectedPath = libraryPath.Text })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                libraryPath.Text = d.SelectedPath;
                RefreshLibrary(true);
                if (libraryInfo.Files > 0) libraryBox.Checked = true;
            }
        }

        void RefreshLibrary(bool rescan)
        {
            if (rescan) libraryInfo = Installer.InspectLibrary(libraryPath.Text);
            libraryPath.Enabled = libraryBox.Checked;
            libraryButton.Enabled = libraryBox.Checked;
            if (string.IsNullOrWhiteSpace(libraryPath.Text))
                libraryNote.Text = "Opcional. Escolha a pasta com uma subpasta por categoria. Sem ela, o programa funciona igual, só sem a biblioteca.";
            else if (libraryInfo.Files == 0)
                libraryNote.Text = "Nenhum som nessa pasta. Ela precisa ter uma subpasta por categoria, com os arquivos de áudio dentro.";
            else
                libraryNote.Text = libraryInfo.Files.ToString("N0") + " sons em " + libraryInfo.Categories + " categorias, " + Installer.SizeText(libraryInfo.Bytes) + ". Arquivos que já estão na pasta do programa não são copiados de novo.";
        }

        bool ReadOptions()
        {
            string folder = folderBox.Text.Trim();
            bool valid;
            try { valid = folder.Length > 3 && Path.IsPathRooted(folder) && Path.GetFullPath(folder).Length > 3; }
            catch (ArgumentException) { valid = false; }
            catch (NotSupportedException) { valid = false; }
            if (!valid)
            {
                MessageBox.Show(this, "Escolha uma pasta completa, por exemplo " + Installer.DefaultFolder(), "Atalhos PC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            plan.Folder = folder;
            plan.DesktopShortcut = desktopBox.Checked;
            plan.StartMenuShortcut = startMenuBox.Checked;
            plan.Autostart = autostartBox.Checked;
            plan.LibrarySource = null;
            if (libraryBox.Checked)
            {
                libraryInfo = Installer.InspectLibrary(libraryPath.Text);
                RefreshLibrary(false);
                if (libraryInfo.Files == 0)
                {
                    MessageBox.Show(this, "A pasta da biblioteca não tem sons. Escolha outra ou desmarque a biblioteca.", "Atalhos PC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                plan.LibrarySource = libraryPath.Text.Trim();
            }
            return true;
        }

        // ---------- instalacao ----------

        void StartInstall()
        {
            if (!ReadOptions()) return;
            busy = true;
            ShowPage(2);
            int lastPercent = -1;
            string lastText = null;
            Thread worker = new Thread(() =>
            {
                string error = null;
                List<string> notes = null;
                try
                {
                    notes = Installer.Install(plan, (percent, text) =>
                    {
                        if (percent == lastPercent && text == lastText) return;
                        lastPercent = percent;
                        lastText = text;
                        BeginInvoke((Action)(() =>
                        {
                            bar.Value = Math.Max(0, Math.Min(100, percent));
                            progressText.Text = text;
                            if (logList.Items.Count == 0 || !text.StartsWith("Biblioteca de sons:") || !((string)logList.Items[logList.Items.Count - 1]).StartsWith("Biblioteca de sons:"))
                                logList.Items.Add(text);
                            else logList.Items[logList.Items.Count - 1] = text;
                            logList.TopIndex = logList.Items.Count - 1;
                        }));
                    });
                }
                catch (Exception e)
                {
                    // a mensagem vai para a tela de fim; o detalhe completo, para o log
                    error = e.Message;
                    Installer.Log("ERRO: " + e);
                }
                BeginInvoke((Action)(() => Finish(error, notes)));
            });
            worker.SetApartmentState(ApartmentState.STA); // o WScript.Shell dos atalhos prefere STA
            worker.IsBackground = true;
            worker.Start();
        }

        void Finish(string error, List<string> notes)
        {
            busy = false;
            installed = error == null;
            ExitCode = installed ? 0 : 1;
            string folder = Path.GetFullPath(plan.Folder);
            if (installed)
            {
                doneTitle.Text = "Pronto!";
                string text = "O Atalhos PC " + AppInfo.Version + " foi instalado em:\n" + folder + "\n\n" +
                    "No tablet ou celular: instale o " + Installer.ApkName + ", que está nessa pasta. Depois clique em Parear novo aparelho no PC e digite o código no app.\n\n" +
                    "Na primeira vez, se o Windows perguntar sobre a rede, clique em Permitir acesso.";
                if (notes != null && notes.Count > 0) text += "\n\nAvisos: " + string.Join(" ", notes);
                doneText.Text = text;
                openBox.Visible = true;
                openFolderButton.Visible = true;
            }
            else
            {
                doneTitle.Text = "A instalação não terminou";
                doneText.Text = error + "\n\nNada dos seus atalhos e sons foi apagado. Corrija e rode o instalador de novo.\nDetalhes em " + Installer.LogFile;
                openBox.Visible = false;
                openFolderButton.Visible = Directory.Exists(folder);
            }
            ShowPage(3);
        }

        // Barra lateral azul-escura, no tom do icone do app, com os passos.
        sealed class Sidebar : Control
        {
            static readonly string[] Steps = { "Saudação", "Opções", "Instalação", "Pronto" };
            public int Step;
            public Image Logo;

            public Sidebar()
            {
                SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                Rectangle all = ClientRectangle;
                using (LinearGradientBrush back = new LinearGradientBrush(all, Color.FromArgb(0x1B, 0x2A, 0x5C), Color.FromArgb(0x0E, 0x14, 0x30), 90f))
                    g.FillRectangle(back, all);
                int size = Px(84);
                if (Logo != null) g.DrawImage(Logo, (Width - size) / 2, Px(34), size, size);
                TextFormatFlags center = TextFormatFlags.HorizontalCenter | TextFormatFlags.NoPadding;
                using (Font name = new Font("Segoe UI", 15f, FontStyle.Bold))
                    TextRenderer.DrawText(g, "Atalhos PC", name, new Rectangle(0, Px(130), Width, Px(30)), Color.White, center);
                using (Font small = new Font("Segoe UI", 9f))
                    TextRenderer.DrawText(g, "versão " + AppInfo.Version, small, new Rectangle(0, Px(162), Width, Px(20)), Color.FromArgb(0xB8, 0xC0, 0xDA), center);

                int y = Px(212);
                using (Font stepFont = new Font("Segoe UI", 10.5f))
                using (Font current = new Font("Segoe UI", 10.5f, FontStyle.Bold))
                {
                    for (int i = 0; i < Steps.Length; i++)
                    {
                        Color c = i == Step ? Greeting.Gold : i < Step ? Color.White : Color.FromArgb(0x8A, 0x93, 0xB5);
                        if (i == Step)
                        {
                            using (SolidBrush mark = new SolidBrush(Greeting.Gold)) g.FillRectangle(mark, 0, y + Px(2), Px(4), Px(24));
                        }
                        string label = (i < Step ? "✓  " : (i + 1) + "   ") + Steps[i];
                        TextRenderer.DrawText(g, label, i == Step ? current : stepFont, new Point(Px(30), y + Px(4)), c, TextFormatFlags.NoPadding);
                        y += Px(36);
                    }
                }
                using (Font brand = new Font("Segoe UI", 8.5f, FontStyle.Bold))
                    TextRenderer.DrawText(g, "CAVALEIRO AI", brand, new Rectangle(0, Height - Px(52), Width, Px(18)), Greeting.Gold, center);
                using (Font handle = new Font("Segoe UI", 9f))
                    TextRenderer.DrawText(g, Greeting.Handle, handle, new Rectangle(0, Height - Px(32), Width, Px(18)), Color.FromArgb(0xB8, 0xC0, 0xDA), center);
            }
        }
    }
}
