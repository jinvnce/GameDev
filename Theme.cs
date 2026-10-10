using System;
using System.Drawing;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

namespace Game
{
    public static class Theme
    {
        public static readonly Color Bg        = Color.FromArgb(14, 14, 16);
        public static readonly Color Panel     = Color.FromArgb(28, 28, 32);
        public static readonly Color PanelHi   = Color.FromArgb(44, 44, 50);
        public static readonly Color PanelDown = Color.FromArgb(20, 20, 23);
        public static readonly Color Border    = Color.FromArgb(70, 70, 78);
        public static readonly Color Text      = Color.FromArgb(235, 235, 240);
        public static readonly Color Muted     = Color.FromArgb(150, 150, 160);
        public static readonly Color Accent    = Color.FromArgb(245, 176, 65);

        private static string Pick(string want, string fallback)
        {
            using (var fonts = new InstalledFontCollection())
                return fonts.Families.Any(f => f.Name == want) ? want : fallback;
        }

        private static readonly string DisplayName = Pick("Bahnschrift", "Segoe UI");

        public static Font Display(float size, FontStyle style = FontStyle.Bold)
        {
            return new Font(DisplayName, size, style);
        }

        public static Font Body(float size, FontStyle style = FontStyle.Regular)
        {
            return new Font("Segoe UI", size, style);
        }
    }

    public class GameButton : Button
    {
        private bool hover, down, active;

        public bool Active
        {
            get { return active; }
            set { active = value; Invalidate(); }
        }

        public GameButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer, true);
            SetStyle(ControlStyles.Selectable, false);
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TabStop = false;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { down = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { down = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            Color back = down ? Theme.PanelDown : (hover || active) ? Theme.PanelHi : Theme.Panel;
            using (var b = new SolidBrush(back)) g.FillRectangle(b, ClientRectangle);

            Color edge = (hover || active) ? Theme.Accent : Theme.Border;
            using (var p = new Pen(edge, 1)) g.DrawRectangle(p, 0, 0, Width - 1, Height - 1);

            if (hover || active)
                using (var b = new SolidBrush(Theme.Accent))
                    g.FillRectangle(b, 0, 0, Math.Max(4, Height / 12), Height);

            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            using (var tb = new SolidBrush(Enabled ? Theme.Text : Theme.Muted))
                g.DrawString(Text, Font, tb, new RectangleF(0, 0, Width, Height), sf);
        }
    }
}