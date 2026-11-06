// Saudacao do Cavaleiro AI, nas cores da marca. E a primeira tela do instalador; no programa, aparece sozinha
// na primeira abertura e depois pelo quadro "Cavaleiro AI" da janela.
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace AtalhosPC
{
    public static class Greeting
    {
        public const string Title = "Saudações, cavaleiros e caveiras.";
        public const string Body = "Estou à disposição, meu objetivo é espalhar o conhecimento e a sabedoria de forma descentralizada e distribuída.";
        public const string Ask = "Só peço que siga o perfil no Instagram:";
        public const string Handle = "@cavaleiro.ai";
        public const string Closing = "Um grande abraço.";
        public const string Url = "https://www.instagram.com/cavaleiro.ai/";

        // Cores da marca: fundo preto, cartao cinza-escuro, destaque amarelo.
        public static readonly Color Back = Color.FromArgb(0x0A, 0x0A, 0x0B);
        public static readonly Color Card = Color.FromArgb(0x18, 0x18, 0x19);
        public static readonly Color Gold = Color.FromArgb(0xF2, 0xC2, 0x30);
        public static readonly Color Soft = Color.FromArgb(0xC9, 0xC9, 0xC9);

        public static int Px(int value)
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero)) return (int)Math.Round(value * g.DpiX / 96f);
        }

        public static void OpenInstagram()
        {
            try { Process.Start(Url); }
            catch (Win32Exception) { }
            catch (InvalidOperationException) { }
        }

        public static Button GoldButton(string text)
        {
            Button b = new Button { Text = text, FlatStyle = FlatStyle.Flat, BackColor = Gold, ForeColor = Back, AutoSize = true, Cursor = Cursors.Hand };
            b.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            b.FlatAppearance.BorderSize = 0;
            b.Padding = new Padding(Px(10), Px(4), Px(10), Px(4));
            return b;
        }

        public static Form CreateDialog()
        {
            Form f = new Form
            {
                Text = "Cavaleiro AI",
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MaximizeBox = false,
                MinimizeBox = false,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.CenterParent,
                BackColor = Back,
                Font = new Font("Segoe UI", 9.5f)
            };
            GreetingView view = new GreetingView { Dock = DockStyle.Fill };
            FlowLayoutPanel bar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = Px(58),
                BackColor = Card,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(Px(14), Px(12), Px(14), Px(10))
            };
            Button close = new Button { Text = "Fechar", DialogResult = DialogResult.OK, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, BackColor = Card, AutoSize = true, Margin = new Padding(Px(8), 0, 0, 0) };
            close.FlatAppearance.BorderColor = Soft;
            close.Padding = new Padding(Px(10), Px(4), Px(10), Px(4));
            Button follow = GoldButton("Seguir no Instagram");
            follow.Click += (a, e) => OpenInstagram();
            bar.Controls.Add(close);
            bar.Controls.Add(follow);
            f.Controls.Add(view);
            f.Controls.Add(bar);
            f.AcceptButton = close;
            f.CancelButton = close;
            int width = Px(560);
            f.ClientSize = new Size(width, view.PreferredHeight(width) + bar.Height);
            return f;
        }

        public static void Show(IWin32Window owner)
        {
            using (Form f = CreateDialog()) f.ShowDialog(owner);
        }
    }

    // Desenha a saudacao inteira e ajusta a quebra de linha a largura. So o @cavaleiro.ai e clicavel.
    public sealed class GreetingView : Control
    {
        const TextFormatFlags Wrap = TextFormatFlags.WordBreak | TextFormatFlags.NoPadding | TextFormatFlags.TextBoxControl;
        readonly Font kicker = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        readonly Font title = new Font("Segoe UI", 18f, FontStyle.Bold);
        readonly Font body = new Font("Segoe UI", 11.5f);
        readonly Font handle = new Font("Segoe UI", 14f, FontStyle.Bold);
        readonly Font handleHover = new Font("Segoe UI", 14f, FontStyle.Bold | FontStyle.Underline);
        readonly Font closing = new Font("Segoe UI", 11.5f, FontStyle.Italic);
        readonly Font sign = new Font("Segoe UI", 9.5f);
        Rectangle link;
        bool hover;

        public GreetingView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Greeting.Back;
        }

        public int PreferredHeight(int width)
        {
            using (Bitmap b = new Bitmap(1, 1))
            using (Graphics g = Graphics.FromImage(b)) return Flow(g, width, false);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            Flow(e.Graphics, ClientSize.Width, true);
        }

        int Flow(Graphics g, int width, bool draw)
        {
            int pad = Greeting.Px(30);
            int x = pad;
            int w = Math.Max(Greeting.Px(160), width - 2 * pad);
            int y = pad;
            y = Block(g, draw, "CAVALEIRO AI", kicker, Greeting.Soft, x, y, w) + Greeting.Px(8);
            y = Block(g, draw, Greeting.Title, title, Greeting.Gold, x, y, w) + Greeting.Px(12);
            if (draw)
            {
                using (SolidBrush rule = new SolidBrush(Greeting.Gold)) g.FillRectangle(rule, x, y, Greeting.Px(44), Greeting.Px(3));
            }
            y += Greeting.Px(18);
            y = Block(g, draw, Greeting.Body, body, Color.White, x, y, w) + Greeting.Px(14);
            y = Block(g, draw, Greeting.Ask, body, Color.White, x, y, w) + Greeting.Px(2);
            Size hs = TextRenderer.MeasureText(g, Greeting.Handle, handle, new Size(w, int.MaxValue), Wrap);
            link = new Rectangle(x, y, hs.Width, hs.Height);
            if (draw) TextRenderer.DrawText(g, Greeting.Handle, hover ? handleHover : handle, link, Greeting.Gold, Wrap);
            y += hs.Height + Greeting.Px(18);
            y = Block(g, draw, Greeting.Closing, closing, Color.White, x, y, w) + Greeting.Px(2);
            y = Block(g, draw, "— Cavaleiro AI", sign, Greeting.Soft, x, y, w);
            return y + pad;
        }

        static int Block(Graphics g, bool draw, string text, Font font, Color color, int x, int y, int w)
        {
            Size s = TextRenderer.MeasureText(g, text, font, new Size(w, int.MaxValue), Wrap);
            if (draw) TextRenderer.DrawText(g, text, font, new Rectangle(x, y, w, s.Height), color, Wrap);
            return y + s.Height;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            bool over = link.Contains(e.Location);
            if (over == hover) return;
            hover = over;
            Cursor = over ? Cursors.Hand : Cursors.Default;
            Invalidate(link);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (!hover) return;
            hover = false;
            Cursor = Cursors.Default;
            Invalidate(link);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            base.OnMouseClick(e);
            if (e.Button == MouseButtons.Left && link.Contains(e.Location)) Greeting.OpenInstagram();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Font f in new[] { kicker, title, body, handle, handleHover, closing, sign }) f.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
