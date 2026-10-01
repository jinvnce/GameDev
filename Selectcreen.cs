using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Game
{
    
    public class SelectScreen : Control
    {
        private const int W = 800, H = 600;

        private readonly List<string> chars;
        private readonly string[] maps;
        private readonly string p2Badge, p2Prompt;
        private readonly Random rng = new Random();

        private int phase;
        private int p1Idx, p2Idx, mapIdx;      
        private string p1Choice, p2Choice;
        private bool hoverBack;
        private readonly Dictionary<string, Bitmap> sprites = new Dictionary<string, Bitmap>();
        private readonly Dictionary<string, Rectangle> bounds = new Dictionary<string, Rectangle>();
        private readonly Dictionary<string, Bitmap> thumbs = new Dictionary<string, Bitmap>();
        private readonly Font titleFont = new Font("Segoe UI", 20, FontStyle.Bold);
        private readonly Font nameFont = new Font("Segoe UI", 20, FontStyle.Bold);
        private readonly Font badgeFont = new Font("Segoe UI", 16, FontStyle.Bold);
        private readonly Font promptFont = new Font("Segoe UI", 12, FontStyle.Bold);
        private readonly Font smallFont = new Font("Segoe UI", 9, FontStyle.Bold);
        private readonly Font stageFont = new Font("Segoe UI", 11, FontStyle.Bold);
        private readonly Font qFont = new Font("Segoe UI", 34, FontStyle.Bold);
        private readonly Font bigQFont = new Font("Segoe UI", 110, FontStyle.Bold);
        private static readonly Rectangle PanelRect = new Rectangle(302, 64, 196, 388);
        private static readonly Rectangle StripRect = new Rectangle(312, 372, 176, 70);
       private static readonly Rectangle BackRect = new Rectangle(350, 506, 100, 28);

        public event Action BackRequested;
        public event Action<string, string, string> StartRequested;  

      private readonly Bitmap bgImage;

public SelectScreen(List<string> characters, string[] mapFiles, string p2Badge, string p2Prompt,
                    int startPhase, string p1Name, string p2Name, string bgPath)
{
    bgImage = MenuForm.LoadImage(bgPath) as Bitmap;
            chars = characters;
            maps = mapFiles;
            this.p2Badge = p2Badge;
            this.p2Prompt = p2Prompt;

            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.Black;
            SetStyle(ControlStyles.Selectable, false);

            foreach (string c in chars) LoadSprite(c);

            phase = 1;
            if (startPhase == 3 && p1Name != null && p2Name != null)
            {
                phase = 3;
                p1Choice = p1Name;
                p2Choice = p2Name;
                p1Idx = Math.Max(0, chars.IndexOf(p1Name));
                p2Idx = Math.Max(0, chars.IndexOf(p2Name));
            }
        }


        private void LoadSprite(string name)
        {
            Bitmap bmp = MenuForm.LoadImage(Roster.SpritePath(name, "idle")) as Bitmap;
            sprites[name] = bmp;
            bounds[name] = bmp != null ? OpaqueBounds(bmp) : Rectangle.Empty;
        }

        private static Rectangle OpaqueBounds(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var data = bmp.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                int stride = data.Stride;
                byte[] buf = new byte[Math.Abs(stride) * h];
                Marshal.Copy(data.Scan0, buf, 0, buf.Length);

                int minX = w, minY = h, maxX = -1, maxY = -1;
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        if (buf[y * stride + x * 4 + 3] > 20)
                        {
                            if (x < minX) minX = x;
                            if (x > maxX) maxX = x;
                            if (y < minY) minY = y;
                            if (y > maxY) maxY = y;
                        }

                if (maxX < 0) return new Rectangle(0, 0, w, h);
                return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
            }
            finally { bmp.UnlockBits(data); }
        }

        private Bitmap GetThumb(string path)
        {
            Bitmap t;
            if (thumbs.TryGetValue(path, out t)) return t;

            Image src = MenuForm.LoadImage(path);
            if (src != null)
            {
                t = new Bitmap(352, 140);
                using (var g = Graphics.FromImage(t))
                {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.DrawImage(src, 0, 0, 352, 140);
                }
                src.Dispose();
            }
            thumbs[path] = t;  
            return t;
        }


        private int Rows() { return Math.Max(3, (chars.Count + 2) / 2); }

        private Rectangle TileRect(int slot)
        {
            int rows = Rows();
            int ts = Math.Min(84, (268 - 8 * (rows - 1)) / rows);
            int gridW = ts * 2 + 8;
            int gx = PanelRect.X + (PanelRect.Width - gridW) / 2;
            return new Rectangle(gx + (slot % 2) * (ts + 8), 74 + (slot / 2) * (ts + 8), ts, ts);
        }

        private float Scale() { return Math.Max(0.01f, Math.Min(Width / (float)W, Height / (float)H)); }

        private Point ToVirtual(Point p)
        {
            float s = Scale();
            float ox = (Width - W * s) / 2f, oy = (Height - H * s) / 2f;
            return new Point((int)((p.X - ox) / s), (int)((p.Y - oy) / s));
        }

        private string SlotName(int slot) { return slot < chars.Count ? chars[slot] : null; }   // null = random

        private string SideName(int player)
        {
            if (player == 1) return phase >= 2 ? p1Choice : SlotName(p1Idx);
            if (phase >= 3) return p2Choice;
            if (phase == 2) return SlotName(p2Idx);
            return null;
        }

        private string SideLabel(int player)
        {
            if (player == 2 && phase < 2) return "---";
            return SideName(player) ?? "Random";
        }


        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Color.Black);

            float s = Scale();
            g.TranslateTransform((Width - W * s) / 2f, (Height - H * s) / 2f);
            g.ScaleTransform(s, s);
            g.SetClip(new Rectangle(0, 0, W, H));
            g.PixelOffsetMode = PixelOffsetMode.Half;
            g.InterpolationMode = InterpolationMode.NearestNeighbor;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAliasGridFit;

            DrawBackground(g);
            DrawSide(g, 1);
            DrawSide(g, 2);
            DrawTopBanner(g);
            DrawPanel(g);
            DrawPrompt(g);
            DrawBottomBand(g);
        }

       private void DrawBackground(Graphics g)
{
    if (bgImage != null)
    {
        g.InterpolationMode = InterpolationMode.NearestNeighbor;   // keeps pixel art crisp
        g.DrawImage(bgImage, new Rectangle(0, 0, W, H));
        using (var dim = new SolidBrush(Color.FromArgb(70, 0, 0, 0)))
            g.FillRectangle(dim, 0, 0, W, H);
        return;
    }
    using (var br = new LinearGradientBrush(new Rectangle(0, 0, W, H),
               Color.FromArgb(25, 175, 165), Color.FromArgb(10, 80, 100), 90f))
        g.FillRectangle(br, 0, 0, W, H);
}

        private void DrawSide(Graphics g, int player)
        {
            var state = g.Save();
            if (player == 2) { g.TranslateTransform(W, 0); g.ScaleTransform(-1, 1); }
            g.SetClip(new Rectangle(0, 40, 300, 500));

            bool visible = player == 1 || phase >= 2;
            string name = visible ? SideName(player) : null;
            Bitmap bmp = null;
            if (name != null) sprites.TryGetValue(name, out bmp);

            if (bmp != null)
            {
                Rectangle b = bounds[name];
                Rectangle head = new Rectangle(b.X, b.Y, b.Width, Math.Min(b.Height, b.Width));
                int fw = 420;
                int fh = fw * head.Height / Math.Max(1, head.Width);
                g.DrawImage(bmp, new Rectangle(-60, 60, fw, fh), head, GraphicsUnit.Pixel);

                Color tint = player == 1 ? Color.FromArgb(50, 0, 70, 110) : Color.FromArgb(70, 160, 20, 30);
                using (var tb = new SolidBrush(tint)) g.FillRectangle(tb, 0, 40, 300, 500);

                float sc = Math.Min(300f / b.Height, 230f / b.Width);
                int sw = (int)(b.Width * sc), sh = (int)(b.Height * sc);
                g.DrawImage(bmp, new Rectangle(150 - sw / 2, 524 - sh, sw, sh), b, GraphicsUnit.Pixel);
            }
            else
            {
                using (var tb = new SolidBrush(Color.FromArgb(visible ? 40 : 90, 0, 0, 0)))
                    g.FillRectangle(tb, 0, 40, 300, 500);
                if (visible)  
                {
                    g.Restore(state);
                    state = g.Save();
                    g.SetClip(new Rectangle(player == 1 ? 0 : 500, 40, 300, 500));
                    var r = new Rectangle(player == 1 ? 0 : 500, 150, 300, 220);
                    DrawText(g, "?", bigQFont, Color.FromArgb(200, 255, 255, 255), r, StringAlignment.Center);
                }
            }
            g.Restore(state);
        }

        private void DrawTopBanner(Graphics g)
        {
            using (var orange = new SolidBrush(Color.FromArgb(235, 125, 65)))
                g.FillRectangle(orange, 0, 0, W, 40);
            var poly = new[] { new Point(250, 0), new Point(550, 0), new Point(515, 42), new Point(285, 42) };
            using (var teal = new SolidBrush(Color.FromArgb(30, 160, 150)))
                g.FillPolygon(teal, poly);
            using (var pen = new Pen(Color.FromArgb(15, 90, 90), 3))
                g.DrawPolygon(pen, poly);
            DrawText(g, "SELECT", titleFont, Color.White, new Rectangle(250, 0, 300, 42), StringAlignment.Center);
        }

        private void DrawPanel(Graphics g)
        {
            using (var back = new SolidBrush(Color.FromArgb(18, 52, 62))) g.FillRectangle(back, PanelRect);
            using (var pen = new Pen(Color.FromArgb(60, 150, 160), 3)) g.DrawRectangle(pen, PanelRect);

            int total = Rows() * 2;
            for (int slot = 0; slot < total; slot++)
            {
                Rectangle r = TileRect(slot);
                bool real = slot <= chars.Count;
                using (var fill = new SolidBrush(real ? Color.FromArgb(35, 95, 105) : Color.FromArgb(24, 66, 76)))
                    g.FillRectangle(fill, r);

                if (slot < chars.Count)
                {
                    Bitmap bmp = sprites[chars[slot]];
                    if (bmp != null)
                    {
                        Rectangle b = bounds[chars[slot]];
                        Rectangle head = new Rectangle(b.X, b.Y, b.Width, Math.Min(b.Height, b.Width));
                        g.DrawImage(bmp, Rectangle.Inflate(r, -3, -3), head, GraphicsUnit.Pixel);
                    }
                }
                else if (slot == chars.Count)
                {
                    DrawText(g, "?", qFont, Color.White, r, StringAlignment.Center);
                }

                using (var pen = new Pen(Color.FromArgb(12, 40, 50), 2)) g.DrawRectangle(pen, r);
            }

            Rectangle c1 = TileRect(p1Idx);
            Rectangle c2 = TileRect(p2Idx);
            if (phase >= 2 && p1Idx == p2Idx) c2 = Rectangle.Inflate(c2, -5, -5);
            DrawBrackets(g, c1, Color.FromArgb(255, 215, 0));
            if (phase >= 2) DrawBrackets(g, c2, Color.FromArgb(225, 50, 60));

            DrawText(g, "STAGE", stageFont, Color.FromArgb(200, 225, 225), new Rectangle(PanelRect.X, 346, PanelRect.Width, 24), StringAlignment.Center);
            DrawStage(g);
        }

        private void DrawStage(Graphics g)
        {
            Rectangle r = StripRect;
            using (var fill = new SolidBrush(Color.FromArgb(24, 66, 76))) g.FillRectangle(fill, r);

            if (maps.Length == 0)
            {
                DrawText(g, "No maps in assets\\map", smallFont, Color.White, r, StringAlignment.Center);
            }
            else
            {
                Bitmap t = GetThumb(maps[mapIdx]);
                if (t != null)
                {
                    g.InterpolationMode = InterpolationMode.Bilinear;
                    g.DrawImage(t, r);
                    g.InterpolationMode = InterpolationMode.NearestNeighbor;
                }
                else
                {
                    DrawText(g, "Can't read this image", smallFont, Color.White, r, StringAlignment.Center);
                }

                var cap = new Rectangle(r.X, r.Bottom - 18, r.Width, 18);
                using (var band = new SolidBrush(Color.FromArgb(170, 0, 0, 0))) g.FillRectangle(band, cap);
                DrawText(g, System.IO.Path.GetFileNameWithoutExtension(maps[mapIdx]), smallFont, Color.White, cap, StringAlignment.Center);
            }

            using (var pen = new Pen(Color.FromArgb(12, 40, 50), 2)) g.DrawRectangle(pen, r);

            if (phase == 3)
            {
                DrawBrackets(g, Rectangle.Inflate(r, 4, 4), Color.FromArgb(255, 215, 0));
                if (maps.Length > 1)
                {
                    int cy = r.Y + (r.Height - 18) / 2;
                    var left = new[] { new Point(r.Left + 6, cy), new Point(r.Left + 16, cy - 9), new Point(r.Left + 16, cy + 9) };
                    var right = new[] { new Point(r.Right - 6, cy), new Point(r.Right - 16, cy - 9), new Point(r.Right - 16, cy + 9) };
                    using (var white = new SolidBrush(Color.White))
                    using (var dark = new Pen(Color.Black, 1))
                    {
                        g.FillPolygon(white, left); g.DrawPolygon(dark, left);
                        g.FillPolygon(white, right); g.DrawPolygon(dark, right);
                    }
                }
            }
        }

       private void DrawPrompt(Graphics g)
{
    string text = phase == 1 ? "P1: CHOOSE YOUR FIGHTER"
                : phase == 2 ? p2Prompt
                : "CHOOSE THE STAGE  -  ENTER TO START";
    DrawText(g, text, promptFont, Color.White, new Rectangle(0, 450, W, 30), StringAlignment.Center);

    string help = phase < 3
        ? "Arrow keys / WASD: Move     Enter / Space: Select     Esc: Back     Mouse: Click a fighter"
        : "Left / Right (A / D): Change stage     Enter / Space: Start     Esc: Back";
    DrawText(g, help, smallFont, Color.FromArgb(230, 255, 255, 255), new Rectangle(0, 480, W, 22), StringAlignment.Center);

    using (var fill = new SolidBrush(hoverBack ? Color.FromArgb(60, 130, 140) : Color.FromArgb(20, 60, 70)))
        g.FillRectangle(fill, BackRect);
    using (var pen = new Pen(Color.White, 2)) g.DrawRectangle(pen, BackRect);
    DrawText(g, "BACK", smallFont, Color.White, BackRect, StringAlignment.Center);
}

        private void DrawBottomBand(Graphics g)
        {
            using (var band = new SolidBrush(Color.FromArgb(225, 120, 45))) g.FillRectangle(band, 0, 540, W, 60);
            using (var line = new Pen(Color.FromArgb(70, 35, 10), 3)) g.DrawLine(line, 0, 541, W, 541);

            DrawBadge(g, new Rectangle(10, 546, 56, 48), "P1");
            DrawBadge(g, new Rectangle(734, 546, 56, 48), p2Badge);

            DrawText(g, SideLabel(1), nameFont, Color.White, new Rectangle(76, 546, 320, 48), StringAlignment.Near);
            DrawText(g, SideLabel(2), nameFont, Color.White, new Rectangle(404, 546, 320, 48), StringAlignment.Far);
        }

        private void DrawBadge(Graphics g, Rectangle r, string text)
        {
            using (var fill = new SolidBrush(Color.FromArgb(250, 240, 215))) g.FillRectangle(fill, r);
            using (var pen = new Pen(Color.FromArgb(70, 35, 10), 3)) g.DrawRectangle(pen, r);
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var br = new SolidBrush(Color.FromArgb(60, 30, 20)))
                g.DrawString(text, badgeFont, br, r, sf);
        }

        private static void DrawBrackets(Graphics g, Rectangle r, Color c)
        {
            int L = Math.Max(8, r.Width / 5);
            using (var pen = new Pen(c, 3f))
            {
                g.DrawLine(pen, r.Left, r.Top, r.Left + L, r.Top);
                g.DrawLine(pen, r.Left, r.Top, r.Left, r.Top + L);
                g.DrawLine(pen, r.Right, r.Top, r.Right - L, r.Top);
                g.DrawLine(pen, r.Right, r.Top, r.Right, r.Top + L);
                g.DrawLine(pen, r.Left, r.Bottom, r.Left + L, r.Bottom);
                g.DrawLine(pen, r.Left, r.Bottom, r.Left, r.Bottom - L);
                g.DrawLine(pen, r.Right, r.Bottom, r.Right - L, r.Bottom);
                g.DrawLine(pen, r.Right, r.Bottom, r.Right, r.Bottom - L);
            }
        }

        private static void DrawText(Graphics g, string text, Font f, Color c, Rectangle r, StringAlignment align)
        {
            using (var sf = new StringFormat
            {
                Alignment = align,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            })
            {
                using (var shadow = new SolidBrush(Color.FromArgb(170, 0, 0, 0)))
                    g.DrawString(text, f, shadow, new Rectangle(r.X + 2, r.Y + 2, r.Width, r.Height), sf);
                using (var br = new SolidBrush(c))
                    g.DrawString(text, f, br, r, sf);
            }
        }


        public bool HandleKey(Keys k)
{
    var p1 = MenuForm.Settings.P1Keys;
    var p2 = MenuForm.Settings.P2Keys;

    if (k == Keys.Left  || k == Keys.A || k == p1["Left"]  || k == p2["Left"])  { Move(-1, 0); return true; }
    if (k == Keys.Right || k == Keys.D || k == p1["Right"] || k == p2["Right"]) { Move(1, 0);  return true; }
    if (k == Keys.Up    || k == Keys.W || k == p1["Jump"]  || k == p2["Jump"])  { Move(0, -1); return true; }
    if (k == Keys.Down  || k == Keys.S)                                         { Move(0, 1);  return true; }

    if (k == Keys.Enter || k == Keys.Space || k == p1["Attack"] || k == p2["Attack"]) { Confirm(); return true; }
    if (k == Keys.Escape || k == Keys.Back) { GoBack(); return true; }
    return false;
}

        private void Move(int dx, int dy)
        {
            if (phase == 3)
            {
                if (dx != 0) ChangeMap(dx);
                return;
            }

            int idx = phase == 1 ? p1Idx : p2Idx;
            int col = Math.Max(0, Math.Min(1, idx % 2 + dx));
            int row = Math.Max(0, Math.Min(Rows() - 1, idx / 2 + dy));
            int ni = row * 2 + col;
            if (ni > chars.Count) return;  
            SetIdx(ni);
        }

        private void SetIdx(int i)
        {
            if (phase == 1) p1Idx = i; else p2Idx = i;
            Invalidate();
        }

        private void ChangeMap(int d)
        {
            if (maps.Length == 0) return;
            mapIdx = (mapIdx + d + maps.Length) % maps.Length;
            Invalidate();
        }

        private string Resolve(int slot)
        {
            return slot < chars.Count ? chars[slot] : chars[rng.Next(chars.Count)];
        }

        private void Confirm()
        {
            if (phase == 1) { p1Choice = Resolve(p1Idx); phase = 2; }
            else if (phase == 2) { p2Choice = Resolve(p2Idx); phase = 3; }
            else
            {
                if (maps.Length == 0) return;
                var h = StartRequested;
                if (h != null) h(p1Choice, p2Choice, maps[mapIdx]);
                return;
            }
            Invalidate();
        }

        private void GoBack()
        {
            if (phase == 3) phase = 2;
            else if (phase == 2) phase = 1;
            else
            {
                var h = BackRequested;
                if (h != null) h();
                return;
            }
            Invalidate();
        }

        private int TileAt(Point v)
        {
            for (int slot = 0; slot <= chars.Count; slot++)
                if (TileRect(slot).Contains(v)) return slot;
            return -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Point v = ToVirtual(e.Location);

            bool back = BackRect.Contains(v);
            bool overStrip = phase == 3 && StripRect.Contains(v);
            int tile = phase < 3 ? TileAt(v) : -1;

            if (tile >= 0 && tile != (phase == 1 ? p1Idx : p2Idx)) SetIdx(tile);
            if (back != hoverBack) { hoverBack = back; Invalidate(); }
            Cursor = (back || overStrip || tile >= 0) ? Cursors.Hand : Cursors.Default;
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            if (hoverBack) { hoverBack = false; Invalidate(); }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button != MouseButtons.Left) return;
            Point v = ToVirtual(e.Location);

            if (BackRect.Contains(v)) { GoBack(); return; }

            if (phase < 3)
            {
                int tile = TileAt(v);
                if (tile >= 0) { SetIdx(tile); Confirm(); }
                return;
            }

            if (StripRect.Contains(v))
            {
                int third = StripRect.Width / 3;
                if (v.X < StripRect.Left + third) ChangeMap(-1);
                else if (v.X > StripRect.Right - third) ChangeMap(1);
                else Confirm();
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (var b in sprites.Values) if (b != null) b.Dispose();
                foreach (var b in thumbs.Values) if (b != null) b.Dispose();
                titleFont.Dispose(); nameFont.Dispose(); badgeFont.Dispose(); promptFont.Dispose();
                smallFont.Dispose(); stageFont.Dispose(); qFont.Dispose(); bigQFont.Dispose();
                if (bgImage != null) bgImage.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}