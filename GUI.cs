using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;
using static Game.MenuForm;
using System.Drawing.Imaging;

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
        private readonly List<GameButton> panelButtons = new List<GameButton>();
        private readonly List<Action> panelActions = new List<Action>();
        private int panelIndex;
        private Panel confirmPanel;
        private List<GameButton> savedButtons;
        private List<Action> savedActions;
        private int savedIndex;
        public string Result { get; private set; } = "Quit";
        private long roundEndsAt;
        private readonly BarFx fx1 = new BarFx(), fx2 = new BarFx();
        private readonly Font keyFont = Theme.Body(8, FontStyle.Bold);
        private readonly Font chipFont = Theme.Body(9, FontStyle.Bold);
        private readonly Font calloutFont = Theme.Display(38);
        private readonly Font calloutSubFont = Theme.Display(12);
        private readonly Font timerFontBold = new Font(Theme.Display(28), FontStyle.Bold);
        private static readonly Color ChipGrey = Color.FromArgb(210, 210, 215);
        private class Callout { public string Title, Sub; public Color Accent; public Color Fill = Color.Gold; public long Start; }
        private Callout callout;
        private const int CalloutMs = 1100;
        private readonly Dictionary<PlayerModel, int> jumpStartX = new Dictionary<PlayerModel, int>();
        private readonly HashSet<PlayerModel> airMoved = new HashSet<PlayerModel>();
        private readonly Font hudFont = Theme.Body(11, FontStyle.Bold);
        private readonly Font nameFont = Theme.Display(11);
        private readonly Font timerFont = Theme.Display(28);
        private readonly Font smallFont = Theme.Body(8, FontStyle.Bold);
        private readonly Font tagFont = Theme.Display(9);
        private readonly Font roundFont = Theme.Display(9);
        private readonly Font bannerFont = Theme.Display(30);
        private int p1Wins, p2Wins;
        private int currentRound = 1;
        private readonly int winsNeeded;
        private bool intermission;
        private long intermissionEnds;
        private string banner;
        private const int GameW = 800, GameH = 600;
        private const int SpawnMargin = 100; 
        private Image mapImage;
        private Panel endPanel;
        public FormWindowState EndState { get; private set; } = FormWindowState.Normal;
        public Size EndClientSize { get; private set; } = new Size(1200, 900);

        public MainGUI(string p1Name, string p2Name, string mapPath, bool vsComputer, bool practice = false)
        {
            this.vsComputer = vsComputer;
            this.practice = practice;
            winsNeeded = Settings.Rounds;
            now = Environment.TickCount64;
            roundEndsAt = now + Settings.RoundSeconds * 1000;
            p1 = new PlayerModel(p1Name, Color.Blue, "Right", "P1");
            p2 = new PlayerModel(p2Name, Color.Red, "Left", "P2");

            Text = "Pinoy Brawler";
            ClientSize = new Size(GameW * 3 / 2, GameH * 3 / 2);   
            MinimumSize = SizeFromClientSize(new Size(400, 300));
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = Color.Black;
            CenterToScreen();

            mapImage = LoadImage(mapPath);
            if (mapImage != null)
            {
                var cached = BuildMapCache(mapImage);
                mapImage.Dispose();
                mapImage = cached;
            }
            p1.SetGroundY(GameH / 2);
            p2.SetGroundY(GameH / 2);

            LoadVisuals(p1, p1Name);
            LoadVisuals(p2, p2Name);

            p1.SpecialUsed += skill => TriggerSpecial(p1, skill);
            p2.SpecialUsed += skill => TriggerSpecial(p2, skill);
            p1.HealUsed += skill => TriggerHeal(p1, skill);
            p2.HealUsed += skill => TriggerHeal(p2, skill);
            ResetPositions();

            KeyPreview = true;
            KeyDown += (s, e) => heldKeys.Add(e.KeyCode);
            KeyUp += (s, e) => heldKeys.Remove(e.KeyCode);
            KeyDown += PlayerActions;
            gameLoop = new System.Windows.Forms.Timer { Interval = 15 };
            gameLoop.Tick += gameLoop_Tick;
            gameLoop.Start();
            FormClosed += (s, e) => { mapImage?.Dispose(); mapImage = null; p1.Dispose(); p2.Dispose(); };
            FormClosing += (s, e) =>
            {
                EndState = WindowState == FormWindowState.Minimized ? FormWindowState.Normal : WindowState;
                EndClientSize = ClientSize;
            };
            FormClosed += (s, e) => { mapImage?.Dispose(); mapImage = null; };
        }

        private int SideGap(PlayerModel p)
        {
            return p.ActualHitbox.Left - p.Left;
        }

        private void ClampToScreen(PlayerModel p)
        {
            int gap = SideGap(p);
            p.Left = Math.Max(-gap, Math.Min(p.Left, GameW - p.Width + gap));
        }

        private void ResetPositions()
        {
            p1.Left = SpawnMargin - SideGap(p1);
            p1.Top = GameH / 2;

            p2.Left = GameW - SpawnMargin - p2.ActualHitbox.Width - SideGap(p2);
            p2.Top = GameH / 2;
        }

        private void LoadVisuals(PlayerModel p, string name)
        {
            string idle = Roster.SpritePath(name, "idle");
            string sheetPath = idle.Replace("-idle.png", "-sheet.png");

            if (System.IO.File.Exists(sheetPath))
                p.LoadSheet(sheetPath);
            else
                p.LoadSprites(idle, Roster.SpritePath(name, "walk"),
                              Roster.SpritePath(name, "basic"), Roster.SpecialPath(name), Roster.JumpPath(name));
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

            UpdateBarFx(fx1, p1, !practice);
            UpdateBarFx(fx2, p2, true);

            if (practice)
            {
                p1.data.Restore();
                if (p2.data.GetHealth() < p2.data.GetMaxHealth() * 0.3f) p2.data.Restore();
            }

            if (!practice && (!p1.data.IsAlive() || !p2.data.IsAlive() || now >= roundEndsAt)) EndRound();
        }

        private void PlayerActions(object sender, KeyEventArgs e)
        {
            Keys k = e.KeyCode;
            if (k == Keys.Escape) { if (confirmPanel != null) CancelConfirm(); else TogglePause(); return; }
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
                if (winsNeeded > 1) text += "\n" + p1Wins + " - " + p2Wins;
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

            p1.SetAirState(false); p1.SetVelocityY(0);
            p2.SetAirState(false); p2.SetVelocityY(0);
            p1.ChangeAttackDir("Right");
            p2.ChangeAttackDir("Left");
            ResetPositions();

            ResetFx(fx1);
            ResetFx(fx2);
            callout = null;

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
            AddPanelButton(pausePanel, "Resume", 96, ResumeGame);
            AddConfirmButton(pausePanel, "Change Character", 152, "Character");
            AddConfirmButton(pausePanel, "Change Map", 208, "Map");
            AddConfirmButton(pausePanel, "Main Menu", 264, "Menu");
            AddConfirmButton(pausePanel, "Quit", 320, "Quit");

            Controls.Add(pausePanel);
            pausePanel.BringToFront();
        }

        private void ResumeGame()
        {
            long delta = Environment.TickCount64 - pausedAt;
            roundEndsAt += delta;
            intermissionEnds += delta;
            paused = false;
            panelButtons.Clear();
            panelActions.Clear();
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

            ClampToScreen(player);
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
            g.InterpolationMode = InterpolationMode.Low;
            g.CompositingQuality = CompositingQuality.HighSpeed;
            g.PixelOffsetMode = PixelOffsetMode.HighSpeed;
            if (mapImage != null) g.DrawImage(mapImage, new Rectangle(0, 0, GameW, GameH));
            else g.Clear(Color.FromArgb(30, 30, 30));

            p1.Render(g);
            p2.Render(g);
            DrawHud(g);
            DrawCallout(g);

            if (banner != null)
                DrawText(g, banner, new Rectangle(0, 220, GameW, 80), bannerFont, StringAlignment.Center);
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if ((paused || gameOver) && panelButtons.Count > 0)
            {
                Keys k = keyData & Keys.KeyCode;
                bool shift = (keyData & Keys.Shift) == Keys.Shift;

                switch (k)
                {
                    case Keys.Tab:
                        MovePanel(shift ? -1 : 1);
                        return true;
                    case Keys.Down:
                    case Keys.Right:
                    case Keys.S:
                    case Keys.D:
                        MovePanel(1);
                        return true;
                    case Keys.Up:
                    case Keys.Left:
                    case Keys.W:
                    case Keys.A:
                        MovePanel(-1);
                        return true;
                    case Keys.Enter:
                    case Keys.Space:
                        panelActions[panelIndex]();
                        return true;
                }
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private void MovePanel(int d)
        {
            int n = panelButtons.Count;
            panelIndex = ((panelIndex + d) % n + n) % n;   
            for (int i = 0; i < n; i++) panelButtons[i].Active = (i == panelIndex);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            foreach (Panel p in new[] { pausePanel, endPanel, confirmPanel })
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
            AddResultButton(panel, "Retry", 96, "Retry");
            AddConfirmButton(panel, "Change Character", 152, "Character");
            AddConfirmButton(panel, "Change Map", 208, "Map");
            AddConfirmButton(panel, "Main Menu", 264, "Menu");
            AddConfirmButton(panel, "Quit", 320, "Quit");

            Controls.Add(panel);
            panel.BringToFront();
        }

        private Panel MakePanel(string titleText, int height = 420, string hintText = "Tab / Arrows: Move     Enter / Space: Select")
        {
            panelButtons.Clear();
            panelActions.Clear();
            panelIndex = 0;

            var panel = new Panel { Size = new Size(360, height), BackColor = Theme.Panel };
            panel.Location = new Point((ClientSize.Width - panel.Width) / 2,
                                       (ClientSize.Height - panel.Height) / 2);
            panel.Paint += (s, e) =>
            {
                using (var p = new Pen(Theme.Accent, 2))
                    e.Graphics.DrawRectangle(p, 1, 1, panel.Width - 3, panel.Height - 3);
            };

            var title = new Label
            {
                Text = titleText,
                Font = Theme.Display(20),
                ForeColor = Theme.Text,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panel.Width, 80),
                Location = new Point(0, 10)
            };
            panel.Controls.Add(title);

            var hint = new Label
            {
                Text = hintText,
                Font = Theme.Body(8, FontStyle.Bold),
                ForeColor = Theme.Muted,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panel.Width, 18),
                Location = new Point(0, panel.Height - 26)
            };
            panel.Controls.Add(hint);
            return panel;
        }

        private void AddPanelButton(Panel panel, string text, int y, Action onClick)
        {
            var btn = new GameButton
            {
                Text = text,
                Font = Theme.Display(12),
                Size = new Size(260, 46),
                Location = new Point((panel.Width - 260) / 2, y)
            };
            int idx = panelButtons.Count;
            panelButtons.Add(btn);
            panelActions.Add(onClick);
            btn.Active = idx == panelIndex;
            btn.Click += (s, e) => { panelIndex = idx; onClick(); };
            panel.Controls.Add(btn);
        }

        private void AddConfirmButton(Panel panel, string text, int y, string result)
        {
            AddPanelButton(panel, text, y, () => ShowConfirm(result));
        }

        private void ShowConfirm(string result)
        {
            Panel behind = pausePanel ?? endPanel;
            if (behind == null || confirmPanel != null) return;

            string note = gameOver ? "" : " Your current match will be lost.";
            string message, yesText;
            switch (result)
            {
                case "Character":
                    message = "Are you sure you want to change your character?" + note;
                    yesText = "Yes, change character";
                    break;
                case "Map":
                    message = "Are you sure you want to change the map?" + note;
                    yesText = "Yes, change map";
                    break;
                case "Menu":
                    message = "Are you sure you want to go back to the main menu?" + note;
                    yesText = "Yes, go to main menu";
                    break;
                default:
                    message = "Are you sure you want to exit the game?";
                    yesText = "Yes, exit game";
                    break;
            }

            savedButtons = new List<GameButton>(panelButtons);
            savedActions = new List<Action>(panelActions);
            savedIndex = panelIndex;
            behind.Visible = false;

            confirmPanel = MakePanel("ARE YOU SURE?", 320, "Tab / Arrows: Move     Enter / Space: Select     Esc: Cancel");

            var msg = new Label
            {
                Text = message,
                Font = Theme.Body(11, FontStyle.Bold),
                ForeColor = Theme.Text,
                BackColor = Color.Transparent,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(confirmPanel.Width - 40, 70),
                Location = new Point(20, 96)
            };
            confirmPanel.Controls.Add(msg);

            AddPanelButton(confirmPanel, "No, go back", 176, CancelConfirm);
            AddPanelButton(confirmPanel, yesText, 232, () => { Result = result; Close(); });

            Controls.Add(confirmPanel);
            confirmPanel.BringToFront();
        }

        private void CancelConfirm()
        {
            if (confirmPanel == null) return;

            Controls.Remove(confirmPanel);
            confirmPanel.Dispose();
            confirmPanel = null;

            Panel behind = pausePanel ?? endPanel;
            if (behind != null)
            {
                behind.Visible = true;
                behind.BringToFront();
            }

            panelButtons.Clear();
            panelActions.Clear();
            panelButtons.AddRange(savedButtons);
            panelActions.AddRange(savedActions);
            panelIndex = savedIndex;
            for (int i = 0; i < panelButtons.Count; i++) panelButtons[i].Active = (i == panelIndex);
            Focus();
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
        private class BarFx
        {
            public int PrevHp = -1;
            public float Lag = 1f;
            public long Hold, HitAt, HealAt;
            public int Delta;
        }

        private void ResetFx(BarFx fx)
        {
            fx.PrevHp = -1;
            fx.Lag = 1f;
            fx.Hold = 0;
            fx.HitAt = 0;
            fx.HealAt = 0;
            fx.Delta = 0;
        }

        private void UpdateBarFx(BarFx fx, PlayerModel p, bool healFx)
        {
            int hp = p.data.GetHealth();
            float frac = HealthFraction(p);

            if (fx.PrevHp >= 0)
            {
                if (hp < fx.PrevHp) { fx.HitAt = now; fx.Delta = fx.PrevHp - hp; fx.Hold = now + 450; }
                else if (hp > fx.PrevHp && healFx) fx.HealAt = now;
            }
            fx.PrevHp = hp;

            if (frac >= fx.Lag) fx.Lag = frac;
            else if (now >= fx.Hold) fx.Lag = Math.Max(frac, fx.Lag - 0.006f);
        }

        private void TriggerSpecial(PlayerModel p, string skill)
        {
            bool isP1 = p == p1;
            string tag = isP1 ? "P1" : (vsComputer ? "CPU" : practice ? "DUMMY" : "P2");
            callout = new Callout
            {
                Title = (skill ?? "SPECIAL").ToUpper() + "!",
                Sub = tag + " - " + p.data.GetCharacter().ToUpper(),
                Accent = isP1 ? Color.RoyalBlue : Color.Firebrick,
                Start = now
            };
        }

        private static Color Blend(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb((int)(a.R + (b.R - a.R) * t), (int)(a.G + (b.G - a.G) * t), (int)(a.B + (b.B - a.B) * t));
        }

        private void DrawHud(Graphics g)
        {
            int w = GameW;
            Rectangle hp1 = new Rectangle(20, 30, 330, 26);
            Rectangle hp2 = new Rectangle(w - 350, 30, 330, 26);
            Rectangle mp1 = new Rectangle(20, 60, 230, 14);
            Rectangle mp2 = new Rectangle(w - 250, 60, 230, 14);

            string p2Tag = vsComputer ? "CPU" : practice ? "DUMMY" : "P2";
            DrawText(g, "P1 - " + p1.data.GetCharacter(), new Rectangle(20, 8, 330, 20), nameFont, StringAlignment.Near);
            DrawText(g, p2.data.GetCharacter() + " - " + p2Tag, new Rectangle(w - 350, 8, 330, 20), nameFont, StringAlignment.Far);

            DrawHealthBar(g, hp1, p1, fx1, true);
            DrawHealthBar(g, hp2, p2, fx2, false);

            DrawHeadTag(g, "P1", p1.data.GetCharacter(), Color.RoyalBlue, p1);
            DrawHeadTag(g, p2Tag, p2.data.GetCharacter(), Color.Firebrick, p2);

            DrawMeter(g, mp1, p1, false);
            DrawMeter(g, mp2, p2, true);

            if (!practice)
            {
                DrawPips(g, p1Wins, winsNeeded, 20, 78, false);
                DrawPips(g, p2Wins, winsNeeded, w - 20, 78, true);

                string roundText = "ROUND " + currentRound + " - FIRST TO " + winsNeeded;
                DrawText(g, roundText, new Rectangle(w / 2 - 100, 72, 200, 16), roundFont, StringAlignment.Center);
            }

            DrawControls(g, p1, Settings.P1Keys, new Rectangle(20, 96, 330, 0));
            if (!vsComputer && !practice)
                DrawControls(g, p2, Settings.P2Keys, new Rectangle(w - 350, 96, 330, 0));

            var pause = new Chip { Key = "ESC", Label = "Pause", Accent = ChipGrey, Dim = 1f };
            DrawChip(g, w / 2 - ChipWidth(g, pause) / 2, 92, pause);

            int secondsLeft = Math.Max(0, (int)Math.Ceiling((roundEndsAt - now) / 1000.0));
            Rectangle timerBox = new Rectangle(w / 2 - 40, 10, 80, 60);
            using (var back = new SolidBrush(Color.FromArgb(200, 20, 20, 20)))
                g.FillRectangle(back, timerBox);
            g.DrawRectangle(Pens.White, timerBox);
            string timeText = practice ? "\u221E" : secondsLeft.ToString("00");
            var color = (!practice && secondsLeft <= 10) ? Brushes.Red : Brushes.White;
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString(timeText, timerFontBold, color, timerBox, sf);

        }

        private void DrawHealthBar(Graphics g, Rectangle r, PlayerModel p, BarFx fx, bool anchorRight)
        {
            float frac = Math.Max(0f, Math.Min(1f, HealthFraction(p)));
            float lag = Math.Max(frac, Math.Min(1f, fx.Lag));
            long hitAge = now - fx.HitAt;
            long healAge = now - fx.HealAt;

            int shake = 0;
            if (hitAge < 300)
            {
                float dmgFrac = fx.Delta / (float)p.data.GetMaxHealth();
                float amp = 2f + Math.Min(5f, dmgFrac * 60f);
                shake = (int)(Math.Sin(hitAge * 0.12) * amp * (1f - hitAge / 300f));
            }

            var state = g.Save();
            g.TranslateTransform(shake, 0);

            Color fill = frac > 0.5f ? Color.Gold : frac > 0.25f ? Color.Orange : Color.OrangeRed;
            if (frac > 0f && frac <= 0.25f)
            {
                float pulse = (float)((Math.Sin(now / 120.0) + 1) / 2);
                fill = Blend(Color.OrangeRed, Color.White, pulse * 0.35f);
            }

            using (var back = new SolidBrush(Color.FromArgb(200, 20, 20, 20)))
                g.FillRectangle(back, r);

            using (var lagBrush = new SolidBrush(hitAge < 450 ? Color.White : Color.Crimson))
            using (var fillBrush = new SolidBrush(fill))
            using (var shine = new SolidBrush(Color.FromArgb(55, 255, 255, 255)))
            {
                FillPart(g, lagBrush, r, lag, anchorRight);
                FillPart(g, fillBrush, r, frac, anchorRight);
                FillPart(g, shine, new Rectangle(r.X, r.Y, r.Width, r.Height / 2), frac, anchorRight);
            }

            if (hitAge < 160)
                using (var fl = new SolidBrush(Color.FromArgb((int)(170 * (1f - hitAge / 160f)), Color.White)))
                    g.FillRectangle(fl, r);
            if (healAge < 600)
                using (var fl = new SolidBrush(Color.FromArgb((int)(140 * (1f - healAge / 600f)), Color.LimeGreen)))
                    g.FillRectangle(fl, r);

            Color border = hitAge < 300 ? Color.Red : healAge < 600 ? Color.LimeGreen : Color.White;
            using (var pen = new Pen(border, hitAge < 300 ? 3f : 1f))
                g.DrawRectangle(pen, r);

            DrawText(g, p.data.GetHealth() + " / " + p.data.GetMaxHealth(), r, hudFont, StringAlignment.Center);
            g.Restore(state);
        }

        private void DrawMeter(Graphics g, Rectangle r, PlayerModel p, bool anchorRight)
        {
            var d = p.data;
            int meter = d.GetMeter(), max = d.GetMaxMeter();
            float frac = Math.Max(0f, Math.Min(1f, meter / (float)max));
            bool ready = meter >= d.GetSpecialCost();

            Color fill = Color.DeepSkyBlue;
            if (ready)
            {
                float pulse = (float)((Math.Sin(now / 150.0) + 1) / 2);
                fill = Blend(Color.DeepSkyBlue, Color.Gold, pulse);
            }
            DrawBar(g, r, frac, frac, fill, anchorRight);

            DrawTick(g, r, d.GetSpecialCost() / (float)max, Color.Gold, anchorRight);
            if (d.GetHealAmount() > 0)
                DrawTick(g, r, d.GetHealCost() / (float)max, Color.LimeGreen, anchorRight);

            string text = ready ? "SPECIAL READY  " + meter + "/" + max : meter + "/" + max;
            DrawText(g, text, r, smallFont, StringAlignment.Center);
        }

        private void DrawTick(Graphics g, Rectangle r, float frac, Color c, bool anchorRight)
        {
            frac = Math.Max(0f, Math.Min(1f, frac));
            int x = anchorRight ? r.Right - (int)(r.Width * frac) : r.Left + (int)(r.Width * frac);
            using (var pen = new Pen(c, 2))
                g.DrawLine(pen, x, r.Top - 2, x, r.Bottom + 2);
        }

        private struct Chip
        {
            public string Key, Label;
            public Color Accent;
            public float Dim;
        }

        private static GraphicsPath RoundedRect(Rectangle r, int rad)
        {
            int d = rad * 2;
            var path = new GraphicsPath();
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private int KeyCapWidth(Graphics g, string key)
        {
            return Math.Max(22, (int)Math.Ceiling(TextW(g, key, keyFont)) + 12);
        }


        private int ChipWidth(Graphics g, Chip c)
        {
            return KeyCapWidth(g, c.Key) + 5 + (int)Math.Ceiling(TextW(g, c.Label, chipFont));
        }


        private void DrawChip(Graphics g, int x, int y, Chip c)
        {
            int kw = KeyCapWidth(g, c.Key);
            var cap = new Rectangle(x, y, kw, 18);
            float k = c.Dim;

            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(cap, 4))
            {
                using (var fill = new SolidBrush(Color.FromArgb((int)(240 * k), 34, 36, 48))) g.FillPath(fill, path);
                using (var pen = new Pen(Color.FromArgb((int)(255 * k), c.Accent), 2)) g.DrawPath(pen, path);
            }
            g.Restore(st);

            var tf = StringFormat.GenericTypographic;
            float tw = TextW(g, c.Key, keyFont);
            float ty = y + (18 - keyFont.GetHeight(g)) / 2f;
            using (var br = new SolidBrush(Color.FromArgb((int)(255 * k), Color.White)))
                g.DrawString(c.Key, keyFont, br, x + (kw - tw) / 2f, ty, tf);

            float lx = x + kw + 5;
            float ly = y + (18 - chipFont.GetHeight(g)) / 2f;
            using (var sh = new SolidBrush(Color.FromArgb((int)(200 * k), Color.Black)))
                g.DrawString(c.Label, chipFont, sh, lx + 1, ly + 1, tf);
            using (var br = new SolidBrush(Color.FromArgb((int)(255 * k), Color.White)))
                g.DrawString(c.Label, chipFont, br, lx, ly, tf);
        }

        private void DrawControls(Graphics g, PlayerModel p, Dictionary<string, Keys> m, Rectangle area)
        {
            var d = p.data;
            int meter = d.GetMeter();
            var chips = new List<Chip>();

            chips.Add(new Chip { Key = Settings.KeyName(m["Left"]) + " / " + Settings.KeyName(m["Right"]), Label = "Move", Accent = ChipGrey, Dim = 1f });
            chips.Add(new Chip { Key = Settings.KeyName(m["Jump"]), Label = "Jump", Accent = ChipGrey, Dim = 1f });
            chips.Add(new Chip { Key = Settings.KeyName(m["Attack"]), Label = "Attack", Accent = ChipGrey, Dim = 1f });

            bool specialReady = meter >= d.GetSpecialCost();
            chips.Add(new Chip
            {
                Key = Settings.KeyName(m["Special"]),
                Label = "Special (" + d.GetSpecialCost() + ")",
                Accent = specialReady ? Color.Gold : ChipGrey,
                Dim = specialReady ? 1f : 0.5f
            });

            long defCd = d.nextDefendAllowed - now;
            chips.Add(new Chip
            {
                Key = Settings.KeyName(m["Defend"]),
                Label = defCd > 0 ? "Defend " + (int)Math.Ceiling(defCd / 1000.0) + "s" : "Defend",
                Accent = Color.Cyan,
                Dim = defCd > 0 ? 0.5f : 1f
            });

            if (d.GetHealAmount() > 0)
            {
                long healCd = d.nextHealAllowed - now;
                bool ok = healCd <= 0 && meter >= d.GetHealCost();
                chips.Add(new Chip
                {
                    Key = Settings.KeyName(m["Heal"]),
                    Label = healCd > 0 ? "Heal " + (int)Math.Ceiling(healCd / 1000.0) + "s" : "Heal (" + d.GetHealCost() + ")",
                    Accent = Color.LimeGreen,
                    Dim = ok ? 1f : 0.5f
                });
            }

            const int padX = 8, padY = 6, gap = 10, rowH = 22;
            int n = chips.Count;
            var xs = new int[n];
            var rowOf = new int[n];
            int rows = 1, cur = padX;
            for (int i = 0; i < n; i++)
            {
                int cw = ChipWidth(g, chips[i]);
                if (cur > padX && cur + cw > area.Width - padX) { rows++; cur = padX; }
                xs[i] = cur;
                rowOf[i] = rows - 1;
                cur += cw + gap;
            }

            var panel = new Rectangle(area.X, area.Y, area.Width, rows * rowH + padY * 2 - 4);
            using (var back = new SolidBrush(Color.FromArgb(150, 10, 10, 14))) g.FillRectangle(back, panel);
            using (var pen = new Pen(Color.FromArgb(80, 255, 255, 255), 1)) g.DrawRectangle(pen, panel);

            for (int i = 0; i < n; i++)
                DrawChip(g, area.X + xs[i], area.Y + padY + rowOf[i] * rowH, chips[i]);
        }

        private void DrawCallout(Graphics g)
        {
            if (callout == null) return;
            long age = Math.Max(0, now - callout.Start);
            if (age >= CalloutMs) { callout = null; return; }

            float t = age / (float)CalloutMs;
            float fade = t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            float pop = age < 160 ? 1.35f - 0.35f * (age / 160f) : 1f;

            if (age < 120)
                using (var fl = new SolidBrush(Color.FromArgb((int)(70 * (1f - age / 120f)), Color.White)))
                    g.FillRectangle(fl, 0, 0, GameW, GameH);

            int cy = 215;
            var band = new Rectangle(0, cy - 55, GameW, 110);
            using (var bb = new SolidBrush(Color.FromArgb((int)(150 * fade), 0, 0, 0)))
                g.FillRectangle(bb, band);
            using (var edge = new Pen(Color.FromArgb((int)(255 * fade), callout.Accent), 3))
            {
                g.DrawLine(edge, 0, band.Top, GameW, band.Top);
                g.DrawLine(edge, 0, band.Bottom, GameW, band.Bottom);
            }

            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            using (var br = new SolidBrush(Color.FromArgb((int)(255 * fade), Color.White)))
                g.DrawString(callout.Sub, calloutSubFont, br, new Rectangle(0, band.Top + 6, GameW, 22), sf);

            var st = g.Save();
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TranslateTransform(GameW / 2f, cy + 10);
            g.ScaleTransform(pop, pop);
            using (var path = new GraphicsPath())
            using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                float em = g.DpiY * calloutFont.SizeInPoints / 72f;
                path.AddString(callout.Title, calloutFont.FontFamily, (int)calloutFont.Style, em,
                               new RectangleF(-GameW / 2f, -30, GameW, 60), sf);
                using (var pen = new Pen(Color.FromArgb((int)(255 * fade), callout.Accent), 8f) { LineJoin = LineJoin.Round })
                    g.DrawPath(pen, path);
                using (var fillB = new SolidBrush(Color.FromArgb((int)(255 * fade), callout.Fill)))
                    g.FillPath(fillB, path);
            }
            g.Restore(st);
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
            int w = (int)Math.Ceiling(TextW(g, text, tagFont)) + 24;
            int h = 20;

            int x = CenterX(p) - w / 2;
            int y = p.ActualHitbox.Top - h - 6;

            x = Math.Max(0, Math.Min(x, GameW - w));
            y = Math.Max(150, y);

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
            ClampToScreen(player);
        }
        private Image BuildMapCache(Image src)
        {
            var bmp = new Bitmap(GameW * 3 / 2, GameH * 3 / 2, PixelFormat.Format32bppPArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(src, 0, 0, bmp.Width, bmp.Height);
            }
            return bmp;
        }
        private readonly Dictionary<string, float> textWidths = new Dictionary<string, float>();

        private float TextW(Graphics g, string s, Font f)
        {
            string key = f.Name + f.SizeInPoints + f.Style + "|" + s;
            if (!textWidths.TryGetValue(key, out float w))
            {
                w = g.MeasureString(s, f, PointF.Empty, StringFormat.GenericTypographic).Width;
                textWidths[key] = w;
            }
            return w;
        }

private void TriggerHeal(PlayerModel p, string skill)
{
    bool isP1 = p == p1;
    string tag = isP1 ? "P1" : (vsComputer ? "CPU" : practice ? "DUMMY" : "P2");
    callout = new Callout
    {
        Title = (string.IsNullOrEmpty(skill) ? "HEAL" : skill.ToUpper()) + "!",
        Sub = tag + " - " + p.data.GetCharacter().ToUpper(),
        Accent = isP1 ? Color.RoyalBlue : Color.Firebrick,
        Fill = Color.LimeGreen,
        Start = now
    };
}
    }
}