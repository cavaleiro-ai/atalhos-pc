// Janela do PC: lista de atalhos, pareamento e estado das conexoes. Fechar a janela so esconde na bandeja.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace AtalhosPC
{
    public sealed class MainForm : Form
    {
        const int PairSeconds = 180;

        readonly Store store;
        readonly Server server;
        readonly bool startHidden;
        readonly ListView soundList = new ListView();
        readonly ImageList soundImages = new ImageList();
        readonly TrackBar soundVolume = new TrackBar();
        readonly TrackBar masterVolume = new TrackBar();
        readonly Label soundVolumeLabel = new Label();
        readonly ListView list = new ListView();
        readonly ImageList images = new ImageList();
        readonly NotifyIcon tray = new NotifyIcon();
        readonly Label pairCode = new Label();
        readonly Label pairHint = new Label();
        readonly Button pairButton = new Button();
        readonly Label status = new Label();
        readonly ListBox devices = new ListBox();
        readonly CheckBox autoStart = new CheckBox();
        readonly CheckBox allowBrowse = new CheckBox();
        readonly CheckBox adbReverse = new CheckBox();
        readonly Button firewallButton = new Button();
        readonly System.Windows.Forms.Timer ticker = new System.Windows.Forms.Timer();
        volatile bool firewallOk;
        volatile string adbState = "";
        bool exiting;
        bool shownOnce;

        public MainForm(Store s, Server srv, bool hidden)
        {
            store = s;
            server = srv;
            startHidden = hidden;
            Text = "Atalhos PC";
            StartPosition = FormStartPosition.CenterScreen;
            Font = new Font("Segoe UI", 9.5f);
            MinimumSize = new Size(900, 620);
            ClientSize = new Size(1040, 750);
            try { Icon = Icon.ExtractAssociatedIcon(Sys.ExePath); } catch (ArgumentException) { }

            BuildLayout();
            BuildTray();
            // OnUi descarta o que chega sem handle, e os icones em cache chegam antes da janela aparecer.
            // Tambem vale para quem inicia escondido na bandeja, cuja janela so apareceria muito depois.
            CreateHandle();

            store.Changed += () => OnUi(ReloadList);
            store.Changed += () => OnUi(ReloadSounds);
            server.StateChanged += () => OnUi(RefreshStatus);
            server.DevicePaired += name => OnUi(() =>
            {
                pairCode.Text = "";
                pairHint.Text = "Pareado com " + name + ".";
                pairButton.Text = "Parear novo aparelho";
                ReloadDevices();
            });

            ticker.Interval = 1000;
            ticker.Tick += (a, b) => Tick();
            ticker.Start();

            ReloadList();
            ReloadSounds();
            ReloadDevices();
            CheckFirewallAsync();
            RefreshStatus();
        }

        protected override void SetVisibleCore(bool value)
        {
            if (startHidden && !shownOnce)
            {
                shownOnce = true;
                base.SetVisibleCore(false);
                return;
            }
            shownOnce = true;
            base.SetVisibleCore(value);
        }

        void OnUi(Action a)
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(a); }
            catch (InvalidOperationException) { }
        }

        void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(10) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 340));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            TabControl tabs = new TabControl { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 10, 0) };
            TabPage shortcutsTab = new TabPage("Atalhos");
            shortcutsTab.Controls.Add(BuildLeft());
            TabPage soundsTab = new TabPage("Sons");
            soundsTab.Controls.Add(BuildSounds());
            tabs.TabPages.Add(shortcutsTab);
            tabs.TabPages.Add(soundsTab);
            root.Controls.Add(tabs, 0, 0);
            root.Controls.Add(BuildRight(), 1, 0);
        }

        Control BuildLeft()
        {
            Panel left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };

            FlowLayoutPanel bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
            bar.Controls.Add(MakeButton("Adicionar arquivos…", AddFiles));
            bar.Controls.Add(MakeButton("Adicionar pasta…", AddFolder));
            bar.Controls.Add(MakeButton("Abrir", OpenSelected));
            bar.Controls.Add(MakeButton("Renomear", RenameSelected));
            bar.Controls.Add(MakeButton("Remover", RemoveSelected));
            bar.Controls.Add(MakeButton("◀ Mover", () => MoveSelected(-1)));
            bar.Controls.Add(MakeButton("Mover ▶", () => MoveSelected(1)));

            images.ColorDepth = ColorDepth.Depth32Bit;
            images.ImageSize = new Size(64, 64);
            list.Dock = DockStyle.Fill;
            list.View = View.LargeIcon;
            list.LargeImageList = images;
            list.MultiSelect = true;
            list.AllowDrop = true;
            list.HideSelection = false;
            list.LabelEdit = false;
            list.DragEnter += (a, e) => { e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; };
            list.DragDrop += (a, e) =>
            {
                string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null) AddPaths(paths);
            };
            list.DoubleClick += (a, e) => OpenSelected();
            list.KeyDown += (a, e) => { if (e.KeyCode == Keys.Delete) RemoveSelected(); };

            Label hint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = SystemColors.GrayText,
                Text = "Arraste para cá qualquer arquivo, pasta ou atalho. Ele aparece no tablet com o mesmo ícone."
            };

            left.Controls.Add(list);
            left.Controls.Add(hint);
            left.Controls.Add(bar);
            return left;
        }

        Control BuildRight()
        {
            TableLayoutPanel right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoScroll = true };
            right.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

            GroupBox pair = MakeGroup("Parear tablet ou celular");
            pairButton.Text = "Parear novo aparelho";
            pairButton.Height = 34;
            pairButton.Dock = DockStyle.Top;
            pairButton.Click += (a, e) => TogglePair();
            pairCode.Dock = DockStyle.Top;
            pairCode.Height = 54;
            pairCode.Font = new Font("Consolas", 26f, FontStyle.Bold);
            pairCode.ForeColor = Color.FromArgb(0x1F, 0x5F, 0xD0);
            pairCode.TextAlign = ContentAlignment.MiddleCenter;
            pairHint.Dock = DockStyle.Top;
            pairHint.Height = 38;
            pairHint.ForeColor = SystemColors.GrayText;
            pairHint.Text = "Gere um código e digite-o no app. Ele vale para um aparelho.";
            pair.Controls.Add(pairHint);
            pair.Controls.Add(pairCode);
            pair.Controls.Add(pairButton);
            pair.Height = 160;

            GroupBox conn = MakeGroup("Conexões");
            status.Dock = DockStyle.Fill;
            status.AutoEllipsis = true;
            conn.Controls.Add(status);
            conn.Height = 170;

            GroupBox dev = MakeGroup("Aparelhos pareados");
            devices.Dock = DockStyle.Fill;
            devices.IntegralHeight = false;
            Button drop = new Button { Text = "Remover aparelho selecionado", Dock = DockStyle.Bottom, Height = 30 };
            drop.Click += (a, e) => RemoveDevice();
            dev.Controls.Add(devices);
            dev.Controls.Add(drop);
            dev.Height = 120;

            GroupBox opt = MakeGroup("Opções");
            autoStart.Text = "Iniciar junto com o Windows";
            autoStart.Dock = DockStyle.Top;
            autoStart.Checked = Sys.Autostart;
            autoStart.CheckedChanged += (a, e) => { Sys.Autostart = autoStart.Checked; };
            allowBrowse.Text = "Permitir navegar nos arquivos pelo aparelho";
            allowBrowse.Dock = DockStyle.Top;
            allowBrowse.Checked = store.AllowBrowse;
            allowBrowse.CheckedChanged += (a, e) => { store.AllowBrowse = allowBrowse.Checked; };
            adbReverse.Text = "USB por adb (depuração USB)";
            adbReverse.Dock = DockStyle.Top;
            adbReverse.Checked = store.AdbReverse;
            adbReverse.CheckedChanged += (a, e) => { store.AdbReverse = adbReverse.Checked; ApplyAdbAsync(); };
            firewallButton.Text = "Liberar no firewall do Windows";
            firewallButton.Dock = DockStyle.Top;
            firewallButton.Height = 30;
            firewallButton.Click += (a, e) => AddFirewall();
            opt.Controls.Add(firewallButton);
            opt.Controls.Add(adbReverse);
            opt.Controls.Add(allowBrowse);
            opt.Controls.Add(autoStart);
            opt.Height = 150;

            right.Controls.Add(pair);
            right.Controls.Add(conn);
            right.Controls.Add(dev);
            right.Controls.Add(opt);
            right.Controls.Add(BuildBrand());
            return right;
        }

        // Quadro do Cavaleiro AI nas cores da marca: um clique abre a saudacao completa.
        Control BuildBrand()
        {
            Panel box = new Panel { Dock = DockStyle.Top, Height = 62, BackColor = Greeting.Back, Cursor = Cursors.Hand, Padding = new Padding(12, 9, 12, 6), Margin = new Padding(0, 0, 0, 6) };
            Label head = new Label { Text = Greeting.Title, Dock = DockStyle.Top, Height = 24, ForeColor = Greeting.Gold, Font = new Font("Segoe UI", 10.5f, FontStyle.Bold), Cursor = Cursors.Hand };
            Label sub = new Label { Text = "Cavaleiro AI · siga " + Greeting.Handle + " · clique para ler", Dock = DockStyle.Top, Height = 20, ForeColor = Greeting.Soft, Cursor = Cursors.Hand };
            EventHandler open = (a, e) => Greeting.Show(this);
            box.Click += open;
            head.Click += open;
            sub.Click += open;
            box.Controls.Add(sub);
            box.Controls.Add(head);
            return box;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            // Primeira abertura neste PC: a saudacao aparece uma vez. Quem instalou pelo instalador ja a viu la.
            if (store.Greeted) return;
            store.Greeted = true;
            BeginInvoke((Action)(() => Greeting.Show(this)));
        }

        static GroupBox MakeGroup(string title)
        {
            return new GroupBox { Text = title, Dock = DockStyle.Top, Padding = new Padding(8), Margin = new Padding(0, 0, 0, 6) };
        }

        static Button MakeButton(string text, Action onClick)
        {
            Button b = new Button { Text = text, AutoSize = true, Height = 32, Margin = new Padding(0, 0, 6, 6) };
            b.Click += (a, e) => onClick();
            return b;
        }

        void BuildTray()
        {
            tray.Icon = Icon ?? SystemIcons.Application;
            tray.Text = "Atalhos PC";
            tray.Visible = true;
            ContextMenuStrip menu = new ContextMenuStrip();
            menu.Items.Add("Abrir Atalhos PC", null, (a, e) => ShowWindow());
            menu.Items.Add("Sair", null, (a, e) => ExitApp());
            tray.ContextMenuStrip = menu;
            tray.DoubleClick += (a, e) => ShowWindow();
        }

        public void ShowWindow()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
        }

        public void ExitApp()
        {
            exiting = true;
            tray.Visible = false;
            server.Stop();
            Application.Exit();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!exiting && e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                Hide();
                tray.ShowBalloonTip(2500, "Atalhos PC", "Continua rodando aqui na bandeja. Para fechar de vez, clique com o botão direito no ícone.", ToolTipIcon.Info);
                return;
            }
            tray.Visible = false;
            base.OnFormClosing(e);
        }

        // ---------- atalhos ----------

        void ReloadList()
        {
            HashSet<string> selected = new HashSet<string>(list.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag));
            List<Shortcut> items = store.Shortcuts();
            list.BeginUpdate();
            list.Items.Clear();
            foreach (Shortcut s in items)
            {
                ListViewItem li = new ListViewItem(s.name) { Tag = s.id, ToolTipText = s.path, Selected = selected.Contains(s.id) };
                string key = Icons.KeyFor(s.path);
                if (images.Images.ContainsKey(key)) li.ImageKey = key;
                list.Items.Add(li);
            }
            list.EndUpdate();
            LoadMissingIconsAsync(items);
        }

        void LoadMissingIconsAsync(List<Shortcut> items)
        {
            List<Shortcut> missing = items.Where(s => !images.Images.ContainsKey(Icons.KeyFor(s.path))).ToList();
            if (missing.Count == 0) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                foreach (Shortcut s in missing)
                {
                    byte[] png = Icons.GetPng(s.path);
                    if (png == null) continue;
                    string key = Icons.KeyFor(s.path);
                    Image img;
                    using (MemoryStream ms = new MemoryStream(png)) img = new Bitmap(Image.FromStream(ms));
                    OnUi(() =>
                    {
                        if (!images.Images.ContainsKey(key)) images.Images.Add(key, img);
                        foreach (ListViewItem li in list.Items)
                        {
                            if ((string)li.Tag == s.id) li.ImageKey = key;
                        }
                    });
                }
            });
        }

        void AddPaths(IEnumerable<string> paths)
        {
            foreach (string p in paths)
            {
                if (File.Exists(p) || Directory.Exists(p)) store.Add(p);
            }
        }

        void AddFiles()
        {
            using (OpenFileDialog d = new OpenFileDialog { Multiselect = true, Title = "Escolha os arquivos", DereferenceLinks = false, Filter = "Todos os arquivos (*.*)|*.*" })
            {
                if (d.ShowDialog(this) == DialogResult.OK) AddPaths(d.FileNames);
            }
        }

        void AddFolder()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog { Description = "Escolha a pasta" })
            {
                if (d.ShowDialog(this) == DialogResult.OK) store.Add(d.SelectedPath);
            }
        }

        IEnumerable<string> SelectedIds()
        {
            return list.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag).ToList();
        }

        void OpenSelected()
        {
            foreach (string id in SelectedIds())
            {
                Shortcut s = store.Find(id);
                if (s == null) continue;
                string error = Launch.Open(s.path);
                if (error != null) MessageBox.Show(this, error, "Atalhos PC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        void RemoveSelected()
        {
            foreach (string id in SelectedIds()) store.Remove(id);
        }

        void RenameSelected()
        {
            string id = SelectedIds().FirstOrDefault();
            Shortcut s = id == null ? null : store.Find(id);
            if (s == null) return;
            string name = Prompt("Novo nome:", s.name);
            if (!string.IsNullOrWhiteSpace(name)) store.Rename(id, name);
        }

        void MoveSelected(int delta)
        {
            string id = SelectedIds().FirstOrDefault();
            if (id == null) return;
            List<Shortcut> items = store.Shortcuts();
            int at = items.FindIndex(x => x.id == id);
            if (at >= 0) store.Move(id, at + delta);
        }

        string Prompt(string label, string value)
        {
            using (Form f = new Form { Text = "Atalhos PC", ClientSize = new Size(380, 110), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false })
            {
                Label l = new Label { Text = label, Left = 12, Top = 12, Width = 356 };
                TextBox t = new TextBox { Text = value, Left = 12, Top = 36, Width = 356 };
                Button ok = new Button { Text = "OK", DialogResult = DialogResult.OK, Left = 212, Top = 72, Width = 75 };
                Button no = new Button { Text = "Cancelar", DialogResult = DialogResult.Cancel, Left = 293, Top = 72, Width = 75 };
                f.Controls.AddRange(new Control[] { l, t, ok, no });
                f.AcceptButton = ok;
                f.CancelButton = no;
                return f.ShowDialog(this) == DialogResult.OK ? t.Text : null;
            }
        }

        // ---------- sons ----------

        Control BuildSounds()
        {
            Panel page = new Panel { Dock = DockStyle.Fill, Padding = new Padding(6) };

            FlowLayoutPanel bar = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true };
            bar.Controls.Add(MakeButton("Biblioteca…", OpenLibrary));
            bar.Controls.Add(MakeButton("Adicionar sons…", AddSoundFiles));
            bar.Controls.Add(MakeButton("Tocar", PlaySelectedSound));
            bar.Controls.Add(MakeButton("Parar tudo", () => { if (server.Player != null) server.Player.StopAll(); }));
            bar.Controls.Add(MakeButton("Ícone…", ChooseSoundIcon));
            bar.Controls.Add(MakeButton("Renomear", RenameSelectedSound));
            bar.Controls.Add(MakeButton("Remover", RemoveSelectedSounds));
            bar.Controls.Add(MakeButton("◀ Mover", () => MoveSelectedSound(-1)));
            bar.Controls.Add(MakeButton("Mover ▶", () => MoveSelectedSound(1)));

            soundImages.ColorDepth = ColorDepth.Depth32Bit;
            soundImages.ImageSize = new Size(64, 64);
            soundList.Dock = DockStyle.Fill;
            soundList.View = View.LargeIcon;
            soundList.LargeImageList = soundImages;
            soundList.AllowDrop = true;
            soundList.HideSelection = false;
            soundList.DragEnter += (a, e) => { e.Effect = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; };
            soundList.DragDrop += (a, e) =>
            {
                string[] paths = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (paths != null) AddSoundPaths(paths);
            };
            soundList.DoubleClick += (a, e) => PlaySelectedSound();
            soundList.KeyDown += (a, e) => { if (e.KeyCode == Keys.Delete) RemoveSelectedSounds(); };
            soundList.SelectedIndexChanged += (a, e) => ShowSelectedVolume();

            TableLayoutPanel sliders = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 96, ColumnCount = 2, RowCount = 2 };
            sliders.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 190));
            sliders.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            soundVolumeLabel.Text = "Volume do som";
            soundVolumeLabel.Dock = DockStyle.Fill;
            soundVolumeLabel.TextAlign = ContentAlignment.MiddleLeft;
            soundVolumeLabel.AutoEllipsis = true;
            soundVolume.Dock = DockStyle.Fill;
            soundVolume.Maximum = 100;
            soundVolume.TickFrequency = 10;
            soundVolume.Enabled = false;
            soundVolume.Scroll += (a, e) => ApplySoundVolume(false);
            soundVolume.MouseUp += (a, e) => ApplySoundVolume(true);
            masterVolume.Dock = DockStyle.Fill;
            masterVolume.Maximum = 100;
            masterVolume.TickFrequency = 10;
            masterVolume.Value = store.MasterVolume;
            masterVolume.Scroll += (a, e) => ApplyMaster(false);
            masterVolume.MouseUp += (a, e) => ApplyMaster(true);
            sliders.Controls.Add(soundVolumeLabel, 0, 0);
            sliders.Controls.Add(soundVolume, 1, 0);
            sliders.Controls.Add(new Label { Text = "Volume geral dos sons", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 1);
            sliders.Controls.Add(masterVolume, 1, 1);

            Label hint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = SystemColors.GrayText,
                Text = "Arraste arquivos de áudio para cá (" + string.Join(", ", Store.AudioExtensions) + "). O som sai neste PC."
            };

            page.Controls.Add(soundList);
            page.Controls.Add(hint);
            page.Controls.Add(sliders);
            page.Controls.Add(bar);
            return page;
        }

        void OpenLibrary()
        {
            using (LibraryForm f = new LibraryForm(store, server)) f.ShowDialog(this);
        }

        void ReloadSounds()
        {
            HashSet<string> selected = new HashSet<string>(soundList.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag));
            soundList.BeginUpdate();
            soundList.Items.Clear();
            foreach (SoundItem s in store.Sounds())
            {
                ListViewItem li = new ListViewItem(s.name) { Tag = s.id, ToolTipText = s.path, Selected = selected.Contains(s.id) };
                if (!string.IsNullOrEmpty(s.icon) && File.Exists(s.icon))
                {
                    string key = Icons.KeyFor(s.icon);
                    if (!soundImages.Images.ContainsKey(key))
                    {
                        byte[] bytes = File.ReadAllBytes(s.icon);
                        using (MemoryStream ms = new MemoryStream(bytes)) soundImages.Images.Add(key, new Bitmap(Image.FromStream(ms)));
                    }
                    li.ImageKey = key;
                }
                soundList.Items.Add(li);
            }
            soundList.EndUpdate();
            if (!masterVolume.Capture) masterVolume.Value = store.MasterVolume;
            ShowSelectedVolume();
        }

        SoundItem SelectedSound()
        {
            if (soundList.SelectedItems.Count == 0) return null;
            return store.FindSound((string)soundList.SelectedItems[0].Tag);
        }

        void ShowSelectedVolume()
        {
            SoundItem s = SelectedSound();
            soundVolume.Enabled = s != null;
            soundVolumeLabel.Text = s == null ? "Volume do som" : "Volume: " + s.name;
            if (s != null && !soundVolume.Capture) soundVolume.Value = s.volume;
        }

        void ApplySoundVolume(bool final)
        {
            SoundItem s = SelectedSound();
            if (s == null) return;
            store.SetSoundVolume(s.id, soundVolume.Value, final);
            if (server.Player != null) server.Player.SetVolume(s.id, store.Effective(store.FindSound(s.id)));
        }

        void ApplyMaster(bool final)
        {
            store.SetMaster(masterVolume.Value, store.MasterMuted, final);
            if (server.Player == null) return;
            foreach (string id in server.Player.Playing())
            {
                SoundItem s = store.FindSound(id);
                if (s != null) server.Player.SetVolume(id, store.Effective(s));
            }
        }

        void AddSoundFiles()
        {
            string filter = "Áudio|" + string.Join(";", Store.AudioExtensions.Select(x => "*" + x));
            using (OpenFileDialog d = new OpenFileDialog { Multiselect = true, Title = "Escolha os sons", Filter = filter })
            {
                if (d.ShowDialog(this) == DialogResult.OK) AddSoundPaths(d.FileNames);
            }
        }

        void AddSoundPaths(IEnumerable<string> paths)
        {
            foreach (string p in paths)
            {
                if (File.Exists(p) && Store.IsAudio(p)) store.AddSound(p, null);
            }
        }

        void PlaySelectedSound()
        {
            SoundItem s = SelectedSound();
            if (s == null || server.Player == null) return;
            string error = server.Player.Play(s.id, s.path, store.Effective(s));
            if (error != null) MessageBox.Show(this, error, "Atalhos PC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        void ChooseSoundIcon()
        {
            SoundItem s = SelectedSound();
            if (s == null) return;
            using (OpenFileDialog d = new OpenFileDialog { Title = "Escolha a imagem do som", Filter = "Imagens|*.png;*.jpg;*.jpeg;*.bmp;*.gif" })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                string target = Path.Combine(store.SoundIconDir, s.id + ".png");
                try
                {
                    Icons.SaveCustom(File.ReadAllBytes(d.FileName), target);
                    store.SetSoundIcon(s.id, target);
                }
                catch (ArgumentException)
                {
                    MessageBox.Show(this, "Este arquivo não é uma imagem válida.", "Atalhos PC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
        }

        void RenameSelectedSound()
        {
            SoundItem s = SelectedSound();
            if (s == null) return;
            string name = Prompt("Novo nome:", s.name);
            if (!string.IsNullOrWhiteSpace(name)) store.RenameSound(s.id, name);
        }

        void RemoveSelectedSounds()
        {
            foreach (string id in soundList.SelectedItems.Cast<ListViewItem>().Select(i => (string)i.Tag).ToList())
            {
                if (server.Player != null) server.Player.Stop(id);
                store.RemoveSound(id);
            }
        }

        void MoveSelectedSound(int delta)
        {
            SoundItem s = SelectedSound();
            if (s == null) return;
            int at = store.Sounds().FindIndex(x => x.id == s.id);
            if (at >= 0) store.MoveSound(s.id, at + delta);
        }

        // ---------- pareamento, aparelhos e estado ----------

        void TogglePair()
        {
            if (server.PairSecondsLeft > 0)
            {
                server.EndPair();
                pairCode.Text = "";
                pairHint.Text = "Pareamento cancelado.";
                pairButton.Text = "Parear novo aparelho";
                return;
            }
            string code = server.BeginPair(PairSeconds);
            pairCode.Text = code.Substring(0, 3) + "-" + code.Substring(3);
            pairButton.Text = "Cancelar";
        }

        void ReloadDevices()
        {
            devices.Items.Clear();
            foreach (Device d in store.Devices()) devices.Items.Add(new DeviceRow(d));
        }

        sealed class DeviceRow
        {
            public readonly Device Device;
            public DeviceRow(Device d) { Device = d; }
            public override string ToString() { return Device.name + "  (" + Device.added + ")"; }
        }

        void RemoveDevice()
        {
            DeviceRow row = devices.SelectedItem as DeviceRow;
            if (row == null) return;
            store.RemoveDevice(row.Device.id);
            ReloadDevices();
        }

        void CheckFirewallAsync()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                firewallOk = Sys.FirewallRuleExists();
                OnUi(RefreshStatus);
            });
        }

        void AddFirewall()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                firewallOk = Sys.AddFirewallRule();
                OnUi(RefreshStatus);
            });
        }

        void ApplyAdbAsync()
        {
            if (!store.AdbReverse)
            {
                adbState = "";
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    string found = Sys.FindAdb();
                    try { if (found != null) Sys.RemoveAdbReverse(found, store.Port); }
                    catch (Exception e) { Log.Write("Tunel adb: " + e.Message); }
                });
                return;
            }
            ThreadPool.QueueUserWorkItem(_ =>
            {
                string adb = Sys.FindAdb();
                if (adb == null) adbState = "adb.exe não encontrado";
                else
                {
                    try
                    {
                        int n = Sys.ApplyAdbReverse(adb, store.Port);
                        adbState = n == 0 ? "nenhum aparelho autorizado" : n + " aparelho(s) ligado(s)";
                    }
                    catch (Exception e) { adbState = "erro: " + e.Message; }
                }
                OnUi(RefreshStatus);
            });
        }

        int tickCount;

        void Tick()
        {
            int left = server.PairSecondsLeft;
            if (pairCode.Text.Length > 0)
            {
                if (left > 0) pairHint.Text = "Digite este código no app. Expira em " + left + " s.";
                else
                {
                    pairCode.Text = "";
                    pairHint.Text = "O código expirou ou teve tentativas erradas demais.";
                    pairButton.Text = "Parear novo aparelho";
                }
            }
            tickCount++;
            if (tickCount % 10 == 0 && store.AdbReverse) ApplyAdbAsync();
            if (tickCount % 3 == 0) RefreshStatus();
        }

        void RefreshStatus()
        {
            List<string> addresses = Sys.LocalAddresses();
            List<string> active = server.ActiveSessions();
            List<string> lines = new List<string>();
            lines.Add("Rede/USB: " + server.TcpState);
            if (addresses.Count > 0) lines.Add("IP: " + string.Join("  |  ", addresses));
            lines.Add("Bluetooth: " + server.BluetoothState);
            lines.Add("Firewall: " + (firewallOk ? "liberado" : "falta liberar (botão abaixo)"));
            if (store.AdbReverse) lines.Add("USB por adb: " + adbState);
            lines.Add(active.Count == 0 ? "Nenhum aparelho conectado agora." : "Conectado: " + string.Join(", ", active));
            status.Text = string.Join(Environment.NewLine, lines);
            firewallButton.Enabled = !firewallOk;
            firewallButton.Text = firewallOk ? "Firewall do Windows liberado" : "Liberar no firewall do Windows";
            tray.Text = active.Count == 0 ? "Atalhos PC" : "Atalhos PC: " + active.Count + " conectado(s)";
        }
    }
}
