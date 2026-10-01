using System;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
using static Game.MenuForm;

namespace Game
{
    public class MainGUI : Form
    {
        private PlayerModel p1;
        private PlayerModel p2;
        private readonly bool vsComputer;
        private readonly bool practice;
        private readonly HashSet<Keys> heldKeys = new HashSet<Keys>();
        private readonly Random rng = new Random();
        private long aiNextDecision = 5;
        private const int AiMoveSpeed = 5;
        private int AiThinkMin { get { return Settings.AiLevel == 0 ? 500 : Settings.AiLevel == 1 ? 250 : 120; } }
        private int AiThinkMax { get { return Settings.AiLevel == 0 ? 900 : Settings.AiLevel == 1 ? 500 : 250; } }
        private const int AiBasicRange = 85;
        private readonly float Gravity = 1.2f;
        private readonly float JumpVelocity = -23f;
        public long now { get; private set; }
        private System.Windows.Forms.Timer gameLoop;
        private bool gameOver;
        private bool paused;
        private long pausedAt;
        private Panel pausePanel;
        public string Result { get; private set; } = "Quit";
        private long roundEndsAt;
        private float lag1 = 1f, lag2 = 1f;
        private readonly Dictionary<PlayerModel, int> jumpStartX = new Dictionary<PlayerModel, int>();
        private readonly HashSet<PlayerModel> airMoved = new HashSet<PlayerModel>();
        private readonly Font hudFont = new Font("Segoe UI", 9, FontStyle.Bold);
        private readonly Font nameFont = new Font("Segoe UI", 11, FontStyle.Bold);
        private readonly Font timerFont = new Font("Segoe UI", 28, FontStyle.Bold);
        private readonly Font smallFont = new Font("Segoe UI", 7, FontStyle.Bold);
        private readonly Font tagFont = new Font("Segoe UI", 9, FontStyle.Bold);
        private readonly Font roundFont = new Font("Segoe UI", 9, FontStyle.Bold);
        private readonly Font bannerFont = new Font("Segoe UI", 30, FontStyle.Bold);
        private int p1Wins, p2Wins;
        private int currentRound = 1;
        private readonly int winsNeeded;      
        private bool intermission;            
        private long intermissionEnds;
        private string banner;               
        private const int GameW = 800, GameH = 600;
        private Image mapImage;
        private Panel endPanel;
        public FormWindowState EndState { get; private set; } = FormWindowState.Normal;
        public Size EndClientSize { get; private set; } = new Size(800, 600);

        public MainGUI(string p1Name, string p2Name, string mapPath, bool vsComputer, bool practice = false)
        {
            this.vsComputer = vsComputer;
            this.practice = practice;
            winsNeeded = Settings.Rounds / 2 + 1;
            now = Environment.TickCount64;
            roundEndsAt = now + Settings.RoundSeconds * 1000;
            p1 = new PlayerModel(p1Name, Color.Blue, "Right", "P1");
            p2 = new PlayerModel(p2Name, Color.Red, "Left", "P2");

            Text = "Pinoy Brawler";
            ClientSize = new Size(GameW, GameH);
            MinimumSize = SizeFromClientSize(new Size(400, 300));
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.Black;   
            CenterToScreen();
            int form_width = GameW;

            mapImage = LoadImage(mapPath);  

            p1.Location = new Point(100, GameH / 2);
            p1.SetGroundY(GameH / 2);

            p2.Location = new Point(form_width - 200, GameH / 2);
            p2.SetGroundY(GameH / 2);

            p1.LoadSprites(Roster.SpritePath(p1Name, "idle"), Roster.SpritePath(p1Name, "walk"),
            Roster.SpritePath(p1Name, "basic"), Roster.SpecialPath(p1Name), Roster.JumpPath(p1Name));
            p2.LoadSprites(Roster.SpritePath(p2Name, "idle"), Roster.SpritePath(p2Name, "walk"),
            Roster.SpritePath(p2Name, "basic"), Roster.SpecialPath(p2Name), Roster.JumpPath(p2Name));

            KeyPreview = true;
            KeyDown += (s, e) => heldKeys.Add(e.KeyCode);
            KeyUp += (s, e) => heldKeys.Remove(e.KeyCode);
            KeyDown += PlayerActions;
            gameLoop = new System.Windows.Forms.Timer { Interval = 15 };
            gameLoop.Tick += gameLoop_Tick;
            gameLoop.Start();
            FormClosed += (s, e) => gameLoop.Stop();
            FormClosing += (s, e) =>
            {
                EndState = WindowState == FormWindowState.Minimized ? FormWindowState.Normal : WindowState;
                EndClientSize = ClientSize;
            };
            FormClosed += (s, e) => { mapImage?.Dispose(); mapImage = null; };
        }

        private void gameLoop_Tick(object sender, EventArgs e)
        {
            if (gameOver || paused) return;
            now = Environment.TickCount64;

            if (intermission)
            {
                Invalidate();
                if (now >= intermissionEnds) BeginNextRound();
                return;
            }

            p1.checkDefense(now);
            p2.checkDefense(now);
            Invalidate();
            int movement = 6;

            p1.SetMoving(false);
            p2.SetMoving(false);

            var k1 = Settings.P1Keys;
            var k2 = Settings.P2Keys;

            if (heldKeys.Contains(k1["Left"])) PlayerMovement(p1, p2, -movement, "Left");
            if (heldKeys.Contains(k1["Right"])) PlayerMovement(p1, p2, movement, "Right");

            if (vsComputer)
            {
                ComputerTurn();
            }
            else if (!practice)  
            {
                if (heldKeys.Contains(k2["Left"])) PlayerMovement(p2, p1, -movement, "Left");
                if (heldKeys.Contains(k2["Right"])) PlayerMovement(p2, p1, movement, "Right");
            }

            p1.UpdateAnimation();
            p2.UpdateAnimation();

            ApplyGravity(p1, p2);
            ApplyGravity(p2, p1);

            if (practice)
            {
                p1.data.Restore();
                if (p2.data.GetHealth() < p2.data.GetMaxHealth() * 0.3f) p2.data.Restore();
            }

            lag1 = EaseLag(lag1, HealthFraction(p1));
            lag2 = EaseLag(lag2, HealthFraction(p2));

            if (!practice && (!p1.data.IsAlive() || !p2.data.IsAlive() || now >= roundEndsAt)) EndRound();
        }

        private void PlayerActions(object sender, KeyEventArgs e)
        {
            Keys k = e.KeyCode;
            if (k == Keys.Escape) { TogglePause(); return; }
            if (gameOver || paused || intermission) return;

            var k1 = Settings.P1Keys;
            var k2 = Settings.P2Keys;

            if (k == k1["Jump"]) PlayerJump(p1);
            else if (k == k1["Attack"]) p1.CreateHurtBox(p2, "Basic", now);
            else if (k == k1["Special"]) p1.CreateHurtBox(p2, "Special", now);
            else if (k == k1["Defend"]) p1.activateDefense(now);
            else if (k == k1["Heal"]) p1.HealSelf(now);

            if (vsComputer || practice) return;

            if (k == k2["Jump"]) PlayerJump(p2);
            else if (k == k2["Attack"]) p2.CreateHurtBox(p1, "Basic", now);
            else if (k == k2["Special"]) p2.CreateHurtBox(p1, "Special", now);
            else if (k == k2["Defend"]) p2.activateDefense(now);
            else if (k == k2["Heal"]) p2.HealSelf(now);
        }

        private int RoundWinner()
        {
            bool a1 = p1.data.IsAlive();
            bool a2 = p2.data.IsAlive();
            if (a1 && !a2) return 1;
            if (!a1 && a2) return 2;
            if (!a1 && !a2) return 0;

            float h1 = HealthFraction(p1), h2 = HealthFraction(p2);
            if (Math.Abs(h1 - h2) < 0.001f) return 0;
            return h1 > h2 ? 1 : 2;
        }

        private void EndRound()
        {
            int winner = RoundWinner();
            if (winner == 1) p1Wins++;
            else if (winner == 2) p2Wins++;

            heldKeys.Clear();
            p1.SetMoving(false);
            p2.SetMoving(false);

            string p2Tag = vsComputer ? "CPU" : "P2";

            if (p1Wins >= winsNeeded || p2Wins >= winsNeeded)
            {
                string text = p1Wins > p2Wins
                    ? "PLAYER 1 WINS!"
                    : (vsComputer ? "COMPUTER WINS!" : "PLAYER 2 WINS!");
                if (Settings.Rounds > 1) text += "\n" + p1Wins + " - " + p2Wins;
                EndGame(text);
                return;
            }

            banner = winner == 0 ? "DRAW - REPLAY"
                   : winner == 1 ? "P1 WINS THE ROUND"
                   : p2Tag + " WINS THE ROUND";
            if (winner != 0) currentRound++;

            intermission = true;
            intermissionEnds = now + 2000;
        }

        private void BeginNextRound()
        {
            banner = null;
            intermission = false;

            heldKeys.Clear();
            jumpStartX.Clear();
            airMoved.Clear();

            p1.data.Restore();
            p2.data.Restore();

            p1.Location = new Point(100, GameH / 2);
            p2.Location = new Point(GameW - 200, GameH / 2);
            p1.SetAirState(false); p1.SetVelocityY(0);
            p2.SetAirState(false); p2.SetVelocityY(0);
            p1.ChangeAttackDir("Right");
            p2.ChangeAttackDir("Left");

            lag1 = 1f;
            lag2 = 1f;

            now = Environment.TickCount64;
            roundEndsAt = now + Settings.RoundSeconds * 1000;
            aiNextDecision = now + 500;
        }
        
        private void TogglePause()
        {
            if (gameOver) return;
            if (paused) ResumeGame(); else ShowPause();
        }

        private void ShowPause()
        {
            paused = true;
            pausedAt = Environment.TickCount64;
            heldKeys.Clear();
            p1.SetMoving(false);
            p2.SetMoving(false);

            pausePanel = MakePanel("PAUSED");
            AddPanelButton(pausePanel, "Resume", 100, ResumeGame);
            AddResultButton(pausePanel, "Change Character", 170, "Character");
            AddResultButton(pausePanel, "Change Map", 240, "Map");
            AddResultButton(pausePanel, "Main Menu", 310, "Menu");

            Controls.Add(pausePanel);
            pausePanel.BringToFront();
        }

        private void ResumeGame()
        {
            long delta = Environment.TickCount64 - pausedAt;
            roundEndsAt += delta;            
            intermissionEnds += delta;
            paused = false;
            Controls.Remove(pausePanel);
            pausePanel.Dispose();
            pausePanel = null;
            Focus();
        }


        private void PlayerMovement(PlayerModel player, PlayerModel opponent, int x_axis, string dir)
        {
            player.SetMoving(true);
            if (player.OnAir) airMoved.Add(player);  

            int dirStep = Math.Sign(x_axis);
            for (int step = 0; step < Math.Abs(x_axis); step++)
            {
                player.Left += dirStep;
                if (!CanPassThrough(player, opponent) && player.ActualHitbox.IntersectsWith(opponent.ActualHitbox))
                {
                    player.Left -= dirStep;
                    break;
                }
            }

            player.Left = Math.Max(0, Math.Min(player.Left, GameW - player.Width));
            player.ChangeAttackDir(dir);
        }

        private void PlayerJump(PlayerModel player)
        {
            if (player.OnAir == true) return;
            jumpStartX[player] = player.Left;  
            airMoved.Remove(player);
            player.SetAirState(true);
            player.SetVelocityY(JumpVelocity);
        }

        private void ApplyGravity(PlayerModel player, PlayerModel opponent)
        {
            if (!player.OnAir) return;

            player.GravityAdd(Gravity);
            float movingTop = player.Top + player.VelocitY;

            if (!CanPassThrough(player, opponent) && player.ActualHitbox.IntersectsWith(opponent.ActualHitbox))
                PushOut(player, opponent);

            if (movingTop >= player.GroundY)
            {
                player.Top = player.GroundY;
                player.SetVelocityY(0);
                player.SetAirState(false);

                if (!airMoved.Contains(player) && jumpStartX.TryGetValue(player, out int startX))
                    player.Left = startX;

                if (!CanPassThrough(player, opponent) && player.ActualHitbox.IntersectsWith(opponent.ActualHitbox))
                    PushOut(player, opponent);
                player.ChangeAttackDir(CenterX(opponent) > CenterX(player) ? "Right" : "Left");
            }
            else
            {
                player.Top = (int)movingTop;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;

            float scale = Math.Min(ClientSize.Width / (float)GameW, ClientSize.Height / (float)GameH);
            if (scale <= 0f) return;
            float offX = (ClientSize.Width - GameW * scale) / 2f;
            float offY = (ClientSize.Height - GameH * scale) / 2f;

            g.TranslateTransform(offX, offY);
            g.ScaleTransform(scale, scale);
            g.SetClip(new Rectangle(0, 0, GameW, GameH));

            if (mapImage != null) g.DrawImage(mapImage, new Rectangle(0, 0, GameW, GameH));
            else g.Clear(Color.FromArgb(30, 30, 30));

            p1.Render(g);
            p2.Render(g);
            DrawHud(g);

            if (banner != null)
                DrawText(g, banner, new Rectangle(0, 220, GameW, 80), bannerFont, StringAlignment.Center);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            Panel p = pausePanel ?? endPanel;
            if (p != null)
                p.Location = new Point((ClientSize.Width - p.Width) / 2, (ClientSize.Height - p.Height) / 2);
            Invalidate();
        }

        private void ComputerTurn()
        {
            PlayerModel cpu = p2;
            PlayerModel target = p1;
            var d = cpu.data;

            bool targetIsRight = target.Left > cpu.Left;
            string toward = targetIsRight ? "Right" : "Left";
            int gap = targetIsRight
                ? target.ActualHitbox.Left - cpu.ActualHitbox.Right
                : cpu.ActualHitbox.Left - target.ActualHitbox.Right;

            if (cpu.direction != toward) cpu.ChangeAttackDir(toward);

            if (gap > AiBasicRange - 20)
                PlayerMovement(cpu, target, targetIsRight ? AiMoveSpeed : -AiMoveSpeed, toward);

            if (now < aiNextDecision) return;
            aiNextDecision = now + rng.Next(AiThinkMin, AiThinkMax);

            if (d.GetHealAmount() > 0 && d.GetHealth() < d.GetMaxHealth() * 0.4f
                && d.GetMeter() >= d.GetHealCost() && now >= d.nextHealAllowed)
            {
                cpu.HealSelf(now);
                return;
            }

            if (gap < 150 && now >= d.nextDefendAllowed && rng.Next(100) < 25)
            {
                cpu.activateDefense(now);
                return;
            }

            bool canSpecial = d.GetMeter() >= d.GetSpecialCost();
            int specialRange = d.GetSpecHitBox().Width - 20;

            if (gap <= AiBasicRange)
            {
                if (canSpecial && rng.Next(100) < 40) cpu.CreateHurtBox(target, "Special", now);
                else cpu.CreateHurtBox(target, "Basic", now);
                return;
            }

            if (canSpecial && gap <= specialRange && rng.Next(100) < 30)
            {
                cpu.CreateHurtBox(target, "Special", now);
                return;
            }

            if (gap < 200 && rng.Next(100) < 10) PlayerJump(cpu);
        }


        private void EndGame(string text)
        {
            gameOver = true;
            gameLoop.Stop();
            heldKeys.Clear();

            var panel = MakePanel(text);
            endPanel = panel;
            AddResultButton(panel, "Retry", 100, "Retry");
            AddResultButton(panel, "Change Character", 170, "Character");
            AddResultButton(panel, "Change Map", 240, "Map");
            AddResultButton(panel, "Quit", 310, "Quit");

            Controls.Add(panel);
            panel.BringToFront();
        }

        private Panel MakePanel(string titleText)
        {
            var panel = new Panel
            {
                Size = new Size(360, 380),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            panel.Location = new Point((ClientSize.Width - panel.Width) / 2,
                                       (ClientSize.Height - panel.Height) / 2);

            var title = new Label
            {
                Text = titleText,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panel.Width, 80),
                Location = new Point(0, 10)
            };
            panel.Controls.Add(title);
            return panel;
        }

        private void AddPanelButton(Panel panel, string text, int y, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Size = new Size(240, 50),
                Location = new Point((panel.Width - 240) / 2, y),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                TabStop = false
            };
            btn.Click += (s, e) => onClick();
            panel.Controls.Add(btn);
        }

        private void AddResultButton(Panel panel, string text, int y, string result)
        {
            AddPanelButton(panel, text, y, () => { Result = result; Close(); });
        }

        private float HealthFraction(PlayerModel p)
        {
            return (float)p.data.GetHealth() / p.data.GetMaxHealth();
        }

        private float EaseLag(float lag, float current)
        {
            if (current >= lag) return current;
            return Math.Max(current, lag - 0.008f);
        }

        private void DrawHud(Graphics g)
        {
            int w = GameW;
            Rectangle hp1 = new Rectangle(20, 15, 330, 28);
            Rectangle hp2 = new Rectangle(w - 350, 15, 330, 28);
            Rectangle mp1 = new Rectangle(20, 66, 200, 14);
            Rectangle mp2 = new Rectangle(w - 220, 66, 200, 14);

            DrawBar(g, hp1, HealthFraction(p1), lag1, Color.Gold, true);
            DrawBar(g, hp2, HealthFraction(p2), lag2, Color.Gold, false);
            DrawText(g, p1.data.GetHealth() + "/" + p1.data.GetMaxHealth(), hp1, hudFont, StringAlignment.Center);
            DrawText(g, p2.data.GetHealth() + "/" + p2.data.GetMaxHealth(), hp2, hudFont, StringAlignment.Center);

            DrawHeadTag(g, "P1", p1.data.GetCharacter(), Color.RoyalBlue, p1);
            DrawHeadTag(g, vsComputer ? "CPU" : practice ? "DUMMY" : "P2", p2.data.GetCharacter(),
                        Color.Firebrick, p2);

            float m1 = (float)p1.data.GetMeter() / p1.data.GetMaxMeter();
            float m2 = (float)p2.data.GetMeter() / p2.data.GetMaxMeter();
            DrawBar(g, mp1, m1, m1, Color.DeepSkyBlue, false);
            DrawBar(g, mp2, m2, m2, Color.DeepSkyBlue, true);
            DrawText(g, p1.data.GetMeter() + "/" + p1.data.GetMaxMeter(), mp1, smallFont, StringAlignment.Center);
            DrawText(g, p2.data.GetMeter() + "/" + p2.data.GetMaxMeter(), mp2, smallFont, StringAlignment.Center);

            if (!practice)
            {
                DrawPips(g, p1Wins, winsNeeded, 20, 48, false);
                DrawPips(g, p2Wins, winsNeeded, w - 20, 48, true);

                string roundText = Settings.Rounds > 1
                    ? "ROUND " + currentRound + " / " + Settings.Rounds
                    : "ROUND 1";
                DrawText(g, roundText, new Rectangle(w / 2 - 60, 88, 120, 16), roundFont, StringAlignment.Center);
            }

            DrawText(g, Settings.Guide("P1", Settings.P1Keys),
                     new Rectangle(20, 84, 330, 14), smallFont, StringAlignment.Near);
            if (!vsComputer && !practice)
                DrawText(g, Settings.Guide("P2", Settings.P2Keys),
                         new Rectangle(w - 350, 84, 330, 14), smallFont, StringAlignment.Far);
            DrawText(g, "ESC: Pause", new Rectangle(w / 2 - 40, 72, 80, 14), smallFont, StringAlignment.Center);

            int secondsLeft = Math.Max(0, (int)Math.Ceiling((roundEndsAt - now) / 1000.0));
            Rectangle timerBox = new Rectangle(w / 2 - 40, 10, 80, 60);
            using (var back = new SolidBrush(Color.FromArgb(200, 20, 20, 20)))
                g.FillRectangle(back, timerBox);
            g.DrawRectangle(Pens.White, timerBox);
            using (var f = new Font(timerFont, FontStyle.Bold))
            {
                string timeText = practice ? "\u221E" : secondsLeft.ToString("00");
                var color = (!practice && secondsLeft <= 10) ? Brushes.Red : Brushes.White;
                using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                    g.DrawString(timeText, f, color, timerBox, sf);
            }
        }

        private void DrawPips(Graphics g, int wins, int total, int x, int y, bool anchorRight)
        {
            const int size = 12;
            const int spacing = 18;
            for (int i = 0; i < total; i++)
            {
                int px = anchorRight ? x - size - i * spacing : x + i * spacing;
                var r = new Rectangle(px, y, size, size);
                using (var back = new SolidBrush(i < wins ? Color.Gold : Color.FromArgb(200, 20, 20, 20)))
                    g.FillEllipse(back, r);
                g.DrawEllipse(Pens.White, r);
            }
        }

        private void DrawHeadTag(Graphics g, string label, string name, Color accent, PlayerModel p)
        {
            string text = label + " - " + name;
            SizeF size = g.MeasureString(text, tagFont);
            int w = (int)size.Width + 16;
            int h = 20;

            int x = CenterX(p) - w / 2;
            int y = p.ActualHitbox.Top - h - 6;

            x = Math.Max(0, Math.Min(x, GameW - w));
            y = Math.Max(100, y);

            var r = new Rectangle(x, y, w, h);
            using (var back = new SolidBrush(Color.FromArgb(215, accent)))
                g.FillRectangle(back, r);
            g.DrawRectangle(Pens.White, r);
            DrawText(g, text, r, tagFont, StringAlignment.Center);
        }

        private void DrawBar(Graphics g, Rectangle r, float frac, float lag, Color fill, bool anchorRight)
        {
            frac = Math.Max(0f, Math.Min(1f, frac));
            lag = Math.Max(frac, Math.Min(1f, lag));

            using (var back = new SolidBrush(Color.FromArgb(200, 20, 20, 20)))
                g.FillRectangle(back, r);
            using (var lagBrush = new SolidBrush(Color.Crimson))
            using (var fillBrush = new SolidBrush(fill))
            {
                FillPart(g, lagBrush, r, lag, anchorRight);
                FillPart(g, fillBrush, r, frac, anchorRight);
            }
            g.DrawRectangle(Pens.White, r);
        }

        private void FillPart(Graphics g, Brush b, Rectangle r, float frac, bool anchorRight)
        {
            int width = (int)(r.Width * frac);
            int x = anchorRight ? r.Right - width : r.Left;
            g.FillRectangle(b, x, r.Y, width, r.Height);
        }

        private void DrawText(Graphics g, string text, Rectangle r, Font f, StringAlignment align)
        {
            using (var sf = new StringFormat { Alignment = align, LineAlignment = StringAlignment.Center, FormatFlags = StringFormatFlags.NoWrap })
            {
                g.DrawString(text, f, Brushes.Black, new Rectangle(r.X + 1, r.Y + 1, r.Width, r.Height), sf);
                g.DrawString(text, f, Brushes.White, r, sf);
            }
        }

        private const int CrossHeight = 80;

        private bool CanPassThrough(PlayerModel a, PlayerModel b)
        {
            return (a.OnAir && a.GroundY - a.Top >= CrossHeight)
                || (b.OnAir && b.GroundY - b.Top >= CrossHeight);
        }

        private int CenterX(PlayerModel p)
        {
            return p.ActualHitbox.Left + p.ActualHitbox.Width / 2;
        }

        private void PushOut(PlayerModel player, PlayerModel opponent)
        {
            int step = CenterX(opponent) > CenterX(player) ? -1 : 1;
            int guard = 0;
            while (player.ActualHitbox.IntersectsWith(opponent.ActualHitbox) && guard++ < 600)
                player.Left += step;
            player.Left = Math.Max(0, Math.Min(player.Left, GameW - player.Width));
        }
    }
}