// Janela da biblioteca: categorias a esquerda, sons a direita, com busca. A lista e virtual porque sao milhares de arquivos.
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace AtalhosPC
{
    public sealed class LibraryForm : Form
    {
        readonly Store store;
        readonly Server server;
        Library library;
        readonly ListView categories = new ListView();
        readonly ImageList categoryImages = new ImageList();
        readonly ListView sounds = new ListView();
        readonly TextBox search = new TextBox();
        readonly Label info = new Label();
        readonly Timer searchDelay = new Timer();
        List<LibraryItem> shown = new List<LibraryItem>();
        HashSet<string> added;

        public LibraryForm(Store s, Server srv)
        {
            store = s;
            server = srv;
            library = srv.Library;
            Text = "Biblioteca de sons";
            StartPosition = FormStartPosition.CenterParent;
            Font = new Font("Segoe UI", 9.5f);
            MinimumSize = new Size(760, 480);
            ClientSize = new Size(940, 620);
            ShowInTaskbar = false;
            BuildLayout();
            FillCategories();
        }

        void BuildLayout()
        {
            TableLayoutPanel root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(10) };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 270));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            Controls.Add(root);

            categoryImages.ColorDepth = ColorDepth.Depth32Bit;
            categoryImages.ImageSize = new Size(32, 32);
            categories.Dock = DockStyle.Fill;
            categories.View = View.Details;
            categories.HeaderStyle = ColumnHeaderStyle.None;
            categories.FullRowSelect = true;
            categories.MultiSelect = false;
            categories.HideSelection = false;
            categories.SmallImageList = categoryImages;
            categories.Margin = new Padding(0, 0, 10, 0);
            categories.Columns.Add("Categoria", 240);
            categories.SelectedIndexChanged += (a, e) => { if (categories.SelectedItems.Count > 0) Fill(); };
            root.Controls.Add(categories, 0, 0);

            Panel right = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };

            TableLayoutPanel top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 34, ColumnCount = 2 };
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 56));
            top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            top.Controls.Add(new Label { Text = "Buscar", Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft }, 0, 0);
            search.Dock = DockStyle.Fill;
            searchDelay.Interval = 250;
            searchDelay.Tick += (a, e) => { searchDelay.Stop(); Fill(); };
            search.TextChanged += (a, e) => { searchDelay.Stop(); searchDelay.Start(); };
            top.Controls.Add(search, 1, 0);

            sounds.Dock = DockStyle.Fill;
            sounds.View = View.Details;
            sounds.FullRowSelect = true;
            sounds.HideSelection = false;
            sounds.VirtualMode = true;
            sounds.Columns.Add("Som", 390);
            sounds.Columns.Add("Categoria", 150);
            sounds.Columns.Add("", 90);
            sounds.RetrieveVirtualItem += (a, e) =>
            {
                LibraryItem i = shown[e.ItemIndex];
                e.Item = new ListViewItem(new[] { i.Name, library.TitleOf(i.Category), added.Contains(i.Path) ? "✓ na lista" : "" });
            };
            sounds.DoubleClick += (a, e) => Preview();
            sounds.KeyDown += (a, e) => { if (e.KeyCode == Keys.Enter) { e.Handled = true; AddSelected(); } };

            FlowLayoutPanel bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(0, 6, 0, 0) };
            bar.Controls.Add(MakeButton("Ouvir", Preview));
            bar.Controls.Add(MakeButton("Parar", server.StopPreview));
            bar.Controls.Add(MakeButton("Adicionar aos meus sons", AddSelected));
            bar.Controls.Add(MakeButton("Escolher pasta…", ChooseFolder));
            bar.Controls.Add(MakeButton("Fechar", Close));

            info.Dock = DockStyle.Bottom;
            info.Height = 26;
            info.TextAlign = ContentAlignment.MiddleLeft;
            info.ForeColor = SystemColors.GrayText;
            info.AutoEllipsis = true;

            right.Controls.Add(sounds);
            right.Controls.Add(top);
            right.Controls.Add(info);
            right.Controls.Add(bar);
            root.Controls.Add(right, 1, 0);
        }

        static Button MakeButton(string text, Action onClick)
        {
            Button b = new Button { Text = text, AutoSize = true, Height = 32, Margin = new Padding(0, 0, 6, 0) };
            b.Click += (a, e) => onClick();
            return b;
        }

        void FillCategories()
        {
            List<LibraryCategory> found = library == null ? new List<LibraryCategory>() : library.Categories();
            categories.BeginUpdate();
            categories.Items.Clear();
            categoryImages.Images.Clear();
            shown = new List<LibraryItem>();
            sounds.VirtualListSize = 0;
            categories.Items.Add(new ListViewItem("Todas (" + found.Sum(c => c.Items.Count).ToString("N0") + ")") { Tag = "" });
            foreach (LibraryCategory c in found)
            {
                ListViewItem li = new ListViewItem(c.Title + " (" + c.Items.Count.ToString("N0") + ")") { Tag = c.Folder };
                if (c.Icon != null)
                {
                    using (MemoryStream ms = new MemoryStream(File.ReadAllBytes(c.Icon))) categoryImages.Images.Add(c.Folder, new Bitmap(Image.FromStream(ms)));
                    li.ImageKey = c.Folder;
                }
                categories.Items.Add(li);
            }
            categories.EndUpdate();
            if (found.Count == 0)
            {
                search.Enabled = false;
                info.ForeColor = Color.FromArgb(0xB0, 0x40, 0x30);
                info.Text = "Biblioteca vazia. Clique em Escolher pasta… e indique a pasta com uma subpasta por categoria. Procurei em: " + (library == null ? "biblioteca" : library.Root);
                return;
            }
            search.Enabled = true;
            categories.Items[0].Selected = true;
        }

        // A pasta vale para o PC e para o aparelho na hora, e fica guardada na configuracao.
        void ChooseFolder()
        {
            string start = library != null && Directory.Exists(library.Root) ? library.Root : Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
            string path;
            using (FolderBrowserDialog d = new FolderBrowserDialog { Description = "Escolha a pasta da biblioteca: uma subpasta por categoria, com os sons dentro.", SelectedPath = start, ShowNewFolderButton = false })
            {
                if (d.ShowDialog(this) != DialogResult.OK) return;
                path = d.SelectedPath;
            }
            Library chosen = new Library(path);
            List<LibraryCategory> found = chosen.Categories();
            if (found.Count == 0)
            {
                bool loose = Directory.GetFiles(path).Any(Store.IsAudio);
                MessageBox.Show(this, loose
                    ? "Essa pasta tem sons soltos, sem subpastas. Escolha a pasta de cima: a biblioteca precisa de uma subpasta por categoria, com os sons dentro."
                    : "Essa pasta não tem nenhuma subpasta com sons. A biblioteca precisa de uma subpasta por categoria, com os sons dentro.",
                    "Atalhos PC", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            server.StopPreview();
            store.LibraryDir = path;
            server.Library = chosen;
            library = chosen;
            Log.Write("Pasta da biblioteca: " + path);
            search.Text = "";
            FillCategories();
            info.ForeColor = SystemColors.GrayText;
            info.Text = found.Sum(c => c.Items.Count).ToString("N0") + " sons em " + found.Count + " categorias. O aparelho já vê esta pasta.";
        }

        void Fill()
        {
            if (library == null || categories.SelectedItems.Count == 0) return;
            string category = (string)categories.SelectedItems[0].Tag;
            shown = library.Search(category, search.Text);
            added = new HashSet<string>(store.Sounds().Select(s => s.path), StringComparer.OrdinalIgnoreCase);
            sounds.SelectedIndices.Clear();
            sounds.VirtualListSize = shown.Count;
            sounds.Invalidate();
            ShowCount();
        }

        void ShowCount()
        {
            info.ForeColor = SystemColors.GrayText;
            info.Text = shown.Count.ToString("N0") + " sons. Clique duas vezes para ouvir no PC; o som entra nos seus sons, com o ícone da categoria, ao adicionar.";
        }

        List<LibraryItem> Selected()
        {
            return sounds.SelectedIndices.Cast<int>().Where(i => i < shown.Count).Select(i => shown[i]).ToList();
        }

        void Preview()
        {
            List<LibraryItem> items = Selected();
            if (items.Count == 0) return;
            string error = server.PlayPreview(items[0]);
            info.ForeColor = error == null ? SystemColors.GrayText : Color.FromArgb(0xB0, 0x40, 0x30);
            info.Text = error ?? "Tocando no PC: " + items[0].Name;
        }

        void AddSelected()
        {
            List<LibraryItem> items = Selected();
            if (items.Count == 0) return;
            foreach (LibraryItem i in items)
            {
                library.AddTo(store, i);
                added.Add(i.Path);
            }
            sounds.Invalidate();
            info.ForeColor = SystemColors.GrayText;
            info.Text = items.Count == 1 ? "Adicionado aos seus sons: " + items[0].Name : items.Count + " sons adicionados aos seus sons.";
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            server.StopPreview();
            searchDelay.Dispose();
            base.OnFormClosed(e);
        }
    }
}
