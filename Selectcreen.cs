using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static Game.Theme;

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
        private readonly Dictionary<string, Players> info = new Dictionary<string, Players>();
        private float maxHpAll = 1f;

        private readonly Font titleFont  = Theme.Display(20);
        private readonly Font nameFont   = Theme.Display(20);
        private readonly Font badgeFont  = Theme.Display(16);
        private readonly Font promptFont = Theme.Display(12);
        private readonly Font smallFont  = Theme.Body(9, FontStyle.Bold);
        private readonly Font soonFont   = Theme.Body(7, FontStyle.Bold);
        private readonly Font stageFont  = Theme.Display(11);
        private readonly Font qFont      = Theme.Display(34);
        private readonly Font bigQFont   = Theme.Display(110);

        private static readonly Rectangle PanelRect = new Rectangle(302, 64, 196, 388);
        private static readonly Rectangle StripRect = new Rectangle(312, 372, 176, 70);
        private static readonly Rectangle BackRect = new Rectangle(12, 8, 80, 26);
        private readonly Font infoLabelFont = Theme.Body(8, FontStyle.Bold);
private readonly Font infoValueFont = Theme.Body(10, FontStyle.Bold);

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
            BackColor = Theme.Bg;
            SetStyle(ControlStyles.Selectable, false);

            foreach (string c in chars) LoadSprite(c);
            LoadInfo();

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

        private void LoadInfo()
        {
            foreach (string c in chars)
            {
                try { info[c] = new Special_List().CreatePlayer(c); }
                catch { }
            }
            if (info.Count > 0) maxHpAll = Math.Max(1f, info.Values.Max(p => (float)p.GetMaxHealth()));
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
            DrawInfo(g, 1);
            DrawInfo(g, 2);
            DrawTopBanner(g);
            DrawSteps(g);
            DrawPanel(g);
            DrawPrompt(g);
            DrawBottomBand(g);
        }

        private void DrawBackground(Graphics g)
        {
            if (bgImage != null)
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(bgImage, new Rectangle(0, 0, W, H));
                using (var dim = new SolidBrush(Color.FromArgb(130, 0, 0, 0)))
                    g.FillRectangle(dim, 0, 0, W, H);
                return;
            }
            using (var br = new LinearGradientBrush(new Rectangle(0, 0, W, H),
                       Color.FromArgb(38, 38, 44), Color.FromArgb(10, 10, 12), 90f))
                g.FillRectangle(br, 0, 0, W, H);
        }

        private void DrawTopBanner(Graphics g)
        {
            using (var bar = new SolidBrush(Theme.Panel)) g.FillRectangle(bar, 0, 0, W, 40);
            using (var line = new Pen(Theme.Accent, 2)) g.DrawLine(line, 0, 40, W, 40);

            var poly = new[] { new Point(250, 0), new Point(550, 0), new Point(515, 42), new Point(285, 42) };
            using (var fill = new SolidBrush(Theme.PanelHi)) g.FillPolygon(fill, poly);
            using (var pen = new Pen(Theme.Accent, 2)) g.DrawPolygon(pen, poly);
            DrawText(g, "SELECT", titleFont, Theme.Text, new Rectangle(250, 0, 300, 42), StringAlignment.Center);
        }

        private void DrawSteps(Graphics g)
        {
            string[] labels = { "1  P1", "2  " + p2Badge, "3  STAGE" };
            const int pw = 84, ph = 18, gap = 18, y = 45;
            int x0 = (W - (pw * 3 + gap * 2)) / 2;

            for (int i = 0; i < 3; i++)
            {
                int step = i + 1;
                bool active = phase == step, done = phase > step;
                var r = new Rectangle(x0 + i * (pw + gap), y, pw, ph);

                Color fill = active ? Theme.Accent : done ? Theme.PanelHi : Theme.PanelDown;
                using (var fb = new SolidBrush(fill)) g.FillRectangle(fb, r);
                using (var pen = new Pen(active || done ? Theme.Accent : Theme.Border, 1)) g.DrawRectangle(pen, r);

                Color tc = active ? Color.FromArgb(20, 20, 24) : done ? Theme.Text : Theme.Muted;
                DrawFlat(g, labels[i], smallFont, tc, r, StringAlignment.Center);

                if (i < 2)
                {
                    int ax = r.Right + 4;
                    var tri = new[] { new Point(ax, y + 4), new Point(ax, y + 14), new Point(ax + 8, y + 9) };
                    using (var tb = new SolidBrush(done ? Theme.Accent : Theme.Muted)) g.FillPolygon(tb, tri);
                }
            }
        }

        private void DrawPanel(Graphics g)
        {
            using (var back = new SolidBrush(Color.FromArgb(235, Theme.Panel))) g.FillRectangle(back, PanelRect);
            using (var pen = new Pen(Theme.Border, 2)) g.DrawRectangle(pen, PanelRect);

            int total = Rows() * 2;
            for (int slot = 0; slot < total; slot++)
            {
                Rectangle r = TileRect(slot);
                bool real = slot <= chars.Count;
                using (var fill = new SolidBrush(real ? Theme.PanelHi : Theme.PanelDown))
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
                    DrawText(g, "?", qFont, Theme.Muted, r, StringAlignment.Center);
                }
                else
                {
                    DrawLock(g, r, Color.FromArgb(150, 150, 155, 165));
                    DrawText(g, "COMING SOON", soonFont, Color.FromArgb(170, 170, 178), new Rectangle(r.X, r.Bottom - 20, r.Width, 16), StringAlignment.Center);
                }

                using (var pen = new Pen(Theme.Border, 1)) g.DrawRectangle(pen, r);
            }

            Rectangle c1 = TileRect(p1Idx);
            Rectangle c2 = TileRect(p2Idx);
            if (phase >= 2 && p1Idx == p2Idx) c2 = Rectangle.Inflate(c2, -5, -5);
            DrawBrackets(g, c1, Theme.Accent);
            if (phase >= 2) DrawBrackets(g, c2, Color.FromArgb(225, 50, 60));

            DrawText(g, "STAGE", stageFont, phase == 3 ? Theme.Text : Theme.Muted, new Rectangle(PanelRect.X, 346, PanelRect.Width, 24), StringAlignment.Center);
            DrawStage(g);
        }

        private static void DrawLock(Graphics g, Rectangle r, Color c)
        {
            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;

            int bw = r.Width / 3, bh = (int)(bw * 0.8f);
            int cx = r.X + r.Width / 2;
            int top = r.Y + r.Height / 2 - bh / 2 - 4;   
            int sw = bw * 2 / 3;                          

            using (var pen = new Pen(c, 3f))
                g.DrawArc(pen, cx - sw / 2, top - sw / 2, sw, sw, 180, 180);
            using (var fill = new SolidBrush(c))
                g.FillRectangle(fill, cx - bw / 2, top, bw, bh);
            using (var hole = new SolidBrush(Color.FromArgb(220, 20, 20, 24)))
                g.FillEllipse(hole, cx - 3, top + bh / 2 - 4, 6, 6);

            g.Restore(st);
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

                Color tint = player == 1 ? Color.FromArgb(55, 60, 80, 120) : Color.FromArgb(60, 120, 40, 50);
                using (var tb = new SolidBrush(tint)) g.FillRectangle(tb, 0, 40, 300, 500);

               float sc = Math.Min(205f / b.Height, 230f / b.Width);
                int sw = (int)(b.Width * sc), sh = (int)(b.Height * sc);
                g.DrawImage(bmp, new Rectangle(150 - sw / 2, 366 - sh, sw, sh), b, GraphicsUnit.Pixel);
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

        private void DrawInfo(Graphics g, int player)
{
    bool visible = player == 1 || phase >= 2;
    if (!visible) return;

    string name = SideName(player);
    var card = new Rectangle(player == 1 ? 12 : W - 12 - 284, 362, 284, 114);

    using (var back = new SolidBrush(Color.FromArgb(225, Theme.Panel))) g.FillRectangle(back, card);
    using (var pen = new Pen(Theme.Border, 2)) g.DrawRectangle(pen, card);

    Players d = null;
    if (name == null || !info.TryGetValue(name, out d))
    {
        DrawText(g, "RANDOM", promptFont, Theme.Text, new Rectangle(card.X, card.Y + 16, card.Width, 30), StringAlignment.Center);
        DrawText(g, "A surprise fighter is picked for you", infoValueFont, Color.FromArgb(210, 210, 218),
                 new Rectangle(card.X + 8, card.Y + 56, card.Width - 16, 30), StringAlignment.Center);
        return;
    }

    Color label = Color.FromArgb(170, 170, 182);
    Color value = Color.FromArgb(240, 240, 245);

    const int labelW = 56, dmgW = 52, costW = 44, rowH = 22, headH = 16;
    int x = card.X + 10;
    int right = card.Right - 10;
    int costX = right - costW;
    int dmgX = costX - dmgW;
    int nameX = x + labelW;
    int nameW = dmgX - nameX - 4;
    int y = card.Y + 4;

    DrawFlat(g, "DMG",  infoLabelFont, label, new Rectangle(dmgX,  y, dmgW,  headH), StringAlignment.Far);
    DrawFlat(g, "COST", infoLabelFont, label, new Rectangle(costX, y, costW, headH), StringAlignment.Far);
    y += headH;

    DrawFlat(g, "HP", infoLabelFont, label, new Rectangle(x, y, labelW, rowH), StringAlignment.Near);
    var bar = new Rectangle(nameX, y + 5, dmgX - nameX - 8, 12);
    using (var bb = new SolidBrush(Color.FromArgb(200, 20, 20, 20))) g.FillRectangle(bb, bar);
    float frac = Math.Max(0f, Math.Min(1f, d.GetMaxHealth() / maxHpAll));
    using (var fb = new SolidBrush(Color.Gold)) g.FillRectangle(fb, bar.X, bar.Y, (int)(bar.Width * frac), bar.Height);
    using (var pen = new Pen(Color.White, 1)) g.DrawRectangle(pen, bar);
    DrawFlat(g, d.GetMaxHealth().ToString(), infoValueFont, value,
             new Rectangle(dmgX, y, dmgW + costW, rowH), StringAlignment.Far);

    y += rowH;
    DrawFlat(g, "BASIC", infoLabelFont, label, new Rectangle(x, y, labelW, rowH), StringAlignment.Near);
    DrawFit(g, d.GetBasicName(), infoValueFont, value, new Rectangle(nameX, y, nameW, rowH));
    DrawFlat(g, d.GetBasicDamage().ToString(), infoValueFont, value, new Rectangle(dmgX, y, dmgW, rowH), StringAlignment.Far);
    DrawFlat(g, "-", infoValueFont, Theme.Muted, new Rectangle(costX, y, costW, rowH), StringAlignment.Far);

    y += rowH;
    DrawFlat(g, "SPECIAL", infoLabelFont, Color.Gold, new Rectangle(x, y, labelW, rowH), StringAlignment.Near);
    DrawFit(g, d.GetSpecialName(), infoValueFont, value, new Rectangle(nameX, y, nameW, rowH));
    DrawFlat(g, d.GetSpecialDamage().ToString(), infoValueFont, value, new Rectangle(dmgX, y, dmgW, rowH), StringAlignment.Far);
    DrawFlat(g, d.GetSpecialCost().ToString(), infoValueFont, Color.Gold, new Rectangle(costX, y, costW, rowH), StringAlignment.Far);

    y += rowH;
    bool heals = d.GetHealAmount() > 0;
    Color hc = heals ? Color.LimeGreen : Theme.Muted;
    DrawFlat(g, "HEAL", infoLabelFont, hc, new Rectangle(x, y, labelW, rowH), StringAlignment.Near);
    DrawFit(g, heals ? "+" + d.GetHealAmount() + " HP" : "None", infoValueFont, hc, new Rectangle(nameX, y, nameW, rowH));
    if (heals)
        DrawFlat(g, d.GetHealCost().ToString(), infoValueFont, hc, new Rectangle(costX, y, costW, rowH), StringAlignment.Far);
}

        private void DrawStage(Graphics g)
        {
            Rectangle r = StripRect;
            using (var fill = new SolidBrush(Theme.PanelDown)) g.FillRectangle(fill, r);

            if (maps.Length == 0)
            {
                DrawText(g, "No maps in assets\\map", smallFont, Theme.Text, r, StringAlignment.Center);
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
                    DrawText(g, "Can't read this image", smallFont, Theme.Text, r, StringAlignment.Center);
                }

                var cap = new Rectangle(r.X, r.Bottom - 18, r.Width, 18);
                using (var band = new SolidBrush(Color.FromArgb(170, 0, 0, 0))) g.FillRectangle(band, cap);
                DrawText(g, System.IO.Path.GetFileNameWithoutExtension(maps[mapIdx]), smallFont, Theme.Text, cap, StringAlignment.Center);
            }

            if (phase < 3)
                using (var dim = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                    g.FillRectangle(dim, r);

            using (var pen = new Pen(Theme.Border, 2)) g.DrawRectangle(pen, r);

            if (phase == 3)
            {
                DrawBrackets(g, Rectangle.Inflate(r, 4, 4), Theme.Accent);
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
                : "CHOOSE THE STAGE";
    DrawText(g, text, promptFont, Theme.Text, new Rectangle(0, 480, W, 30), StringAlignment.Center);

    string help = phase < 3
        ? "Arrows / WASD / Tab: Move     Enter: Select     Esc: Back"
        : "Left / Right / Tab: Change stage     Enter: Start     Esc: Back";

    using (var strip = new SolidBrush(Color.FromArgb(150, 0, 0, 0)))
        g.FillRectangle(strip, 0, 514, W, 24);
    DrawText(g, help, smallFont, Color.FromArgb(225, 225, 232), new Rectangle(0, 514, W, 24), StringAlignment.Center);

    using (var fill = new SolidBrush(hoverBack ? Theme.PanelHi : Theme.Panel))
        g.FillRectangle(fill, BackRect);
    using (var pen = new Pen(hoverBack ? Theme.Accent : Theme.Border, 2)) g.DrawRectangle(pen, BackRect);
    DrawText(g, "BACK", smallFont, Theme.Text, BackRect, StringAlignment.Center);
}

        private void DrawBottomBand(Graphics g)
        {
            using (var band = new SolidBrush(Theme.Panel)) g.FillRectangle(band, 0, 540, W, 60);
            using (var line = new Pen(Theme.Accent, 2)) g.DrawLine(line, 0, 541, W, 541);

            DrawBadge(g, new Rectangle(10, 546, 56, 48), "P1");
            DrawBadge(g, new Rectangle(734, 546, 56, 48), p2Badge);

            DrawText(g, SideLabel(1), nameFont, Theme.Text, new Rectangle(76, 546, 320, 48), StringAlignment.Near);
            DrawText(g, SideLabel(2), nameFont, Theme.Text, new Rectangle(404, 546, 320, 48), StringAlignment.Far);
        }

        private void DrawBadge(Graphics g, Rectangle r, string text)
        {
            using (var fill = new SolidBrush(Theme.PanelHi)) g.FillRectangle(fill, r);
            using (var pen = new Pen(Theme.Accent, 2)) g.DrawRectangle(pen, r);
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var br = new SolidBrush(Theme.Accent))
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

        private static void DrawFlat(Graphics g, string text, Font f, Color c, Rectangle r, StringAlignment align)
        {
            using (var sf = new StringFormat
            {
                Alignment = align,
                LineAlignment = StringAlignment.Center,
                FormatFlags = StringFormatFlags.NoWrap,
                Trimming = StringTrimming.EllipsisCharacter
            })
            using (var br = new SolidBrush(c))
                g.DrawString(text, f, br, r, sf);
        }
private static void DrawFit(Graphics g, string text, Font f, Color c, Rectangle r)
{
    float size = f.Size;
    Font use = f;
    bool own = false;
    while (size > 7f && g.MeasureString(text, use).Width > r.Width)
    {
        if (own) use.Dispose();
        size -= 0.5f;
        use = new Font(f.FontFamily, size, f.Style, f.Unit);
        own = true;
    }
    DrawFlat(g, text, use, c, r, StringAlignment.Near);
    if (own) use.Dispose();
}

        public bool HandleKey(Keys k, bool shift = false)
        {
            var p1 = MenuForm.Settings.P1Keys;
            var p2 = MenuForm.Settings.P2Keys;

            if (k == Keys.Tab) { Cycle(shift ? -1 : 1); return true; }
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

        private void Cycle(int d)
        {
            if (phase == 3) { ChangeMap(d); return; }

            int count = chars.Count + 1;                 
            int idx = phase == 1 ? p1Idx : p2Idx;
            SetIdx((idx + d + count) % count);           
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
                smallFont.Dispose(); soonFont.Dispose(); stageFont.Dispose(); qFont.Dispose(); bigQFont.Dispose();
                if (bgImage != null) bgImage.Dispose();
                infoLabelFont.Dispose(); infoValueFont.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}