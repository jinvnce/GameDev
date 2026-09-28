using System;
using System.Drawing;
using System.Reflection;
using System.Collections.Generic;
using System.Windows.Forms;

namespace Game
{
    public class MainGUI : Form
    {
        private PlayerModel p1;
        private PlayerModel p2;
        private readonly bool vsComputer;
        private readonly HashSet<Keys> heldKeys = new HashSet<Keys>();
        private readonly Random rng = new Random();
        private long aiNextDecision = 0;
        private const int AiMoveSpeed = 5;
        private const int AiThinkMin = 250;
        private const int AiThinkMax = 500;
        private const int AiBasicRange = 110;
        private readonly float Gravity = 1.2f;
        private readonly float JumpVelocity = -23f;
        public long now { get; private set; }
        private System.Windows.Forms.Timer gameLoop;
        private bool gameOver;
        public string Result { get; private set; } = "Quit";
        private const int RoundSeconds = 99;
        private long roundEndsAt;
        private float lag1 = 1f, lag2 = 1f;
        private readonly Font hudFont = new Font("Segoe UI", 9, FontStyle.Bold);
        private readonly Font nameFont = new Font("Segoe UI", 11, FontStyle.Bold);
        private readonly Font timerFont = new Font("Segoe UI", 28, FontStyle.Bold);
        private readonly Font smallFont = new Font("Segoe UI", 7, FontStyle.Bold);
        int i = 0;

        public MainGUI(string p1Name, string p2Name, string mapPath, bool vsComputer)
        {
            this.vsComputer = vsComputer;
            now = Environment.TickCount64;
            roundEndsAt = now + RoundSeconds * 1000;
            p1 = new PlayerModel(p1Name, Color.Blue, "Right", "P1");
            p2 = new PlayerModel(p2Name, Color.Red, "Left", "P2");

            Text = "Game Title";
            ClientSize = new Size(800, 600);
            DoubleBuffered = true;
            CenterToScreen();
            int form_width = ClientSize.Width;

            BackgroundImage = Image.FromFile(mapPath);
            BackgroundImageLayout = ImageLayout.Stretch;

            Controls.Add(p1);
            p1.Location = new Point(100, ClientSize.Height / 2);
            p1.SetGroundY(ClientSize.Height / 2);

            Controls.Add(p2);
            p2.Location = new Point(form_width - 200, ClientSize.Height / 2);
            p2.SetGroundY(ClientSize.Height / 2);

            p1.LoadSprites(Roster.SpritePath(p1Name, "idle"), Roster.SpritePath(p1Name, "walk"),
                        Roster.SpritePath(p1Name, "basic"), Roster.SpecialPath(p1Name));
            p2.LoadSprites(Roster.SpritePath(p2Name, "idle"), Roster.SpritePath(p2Name, "walk"),
                        Roster.SpritePath(p2Name, "basic"), Roster.SpecialPath(p2Name));

            KeyPreview = true;
            KeyDown += (s, e) => heldKeys.Add(e.KeyCode);
            KeyUp += (s, e) => heldKeys.Remove(e.KeyCode);
            KeyDown += PlayerActions;
            gameLoop = new System.Windows.Forms.Timer { Interval = 15 };
            gameLoop.Tick += gameLoop_Tick;
            gameLoop.Start();
            FormClosed += (s, e) => gameLoop.Stop();
        }

        private void gameLoop_Tick(object sender, EventArgs e)
        {
            if (gameOver) return;
            now = Environment.TickCount64;
            p1.checkDefense(now);
            p2.checkDefense(now);
            Invalidate();
            int movement = 6;

            p1.SetMoving(false);
            p2.SetMoving(false);

            if (heldKeys.Contains(Keys.A)) PlayerMovement(p1, p2, -movement, "Left");
            if (heldKeys.Contains(Keys.D)) PlayerMovement(p1, p2, movement, "Right");

            if (vsComputer)
            {
                ComputerTurn();
            }
            else
            {
                if (heldKeys.Contains(Keys.Left)) PlayerMovement(p2, p1, -movement, "Left");
                if (heldKeys.Contains(Keys.Right)) PlayerMovement(p2, p1, movement, "Right");
            }

            p1.UpdateAnimation();
            p2.UpdateAnimation();

            ApplyGravity(p1, p2);
            ApplyGravity(p2, p1);
            lag1 = EaseLag(lag1, HealthFraction(p1));
            lag2 = EaseLag(lag2, HealthFraction(p2));

            if (!p1.data.IsAlive() || !p2.data.IsAlive() || now >= roundEndsAt) EndGame();
        }

        private void PlayerActions(object sender, KeyEventArgs e)
        {
            if (gameOver) return;
            if (vsComputer && (e.KeyCode == Keys.Up || e.KeyCode == Keys.J || e.KeyCode == Keys.K
                || e.KeyCode == Keys.U || e.KeyCode == Keys.I)) return;
            switch (e.KeyCode)
            {
                // P1
                case Keys.W:
                    PlayerJump(p1);
                    break;
                case Keys.C:
                    p1.CreateHurtBox(p2, "Basic", now);
                    break;
                case Keys.V:
                    p1.CreateHurtBox(p2, "Special", now);
                    break;
                case Keys.F:
                    p1.activateDefense(now);
                    break;
                case Keys.G:
                    p1.HealSelf(now);
                    break;

                // P2
                case Keys.Up:
                    PlayerJump(p2);
                    break;
                case Keys.J:
                    p2.CreateHurtBox(p1, "Basic", now);
                    break;
                case Keys.K:
                    p2.CreateHurtBox(p1, "Special", now);
                    break;
                case Keys.U:
                    p2.activateDefense(now);
                    break;
                case Keys.I:
                    p2.HealSelf(now);
                    break;
            }

        }

        private void PlayerMovement(PlayerModel player, PlayerModel opponent, int x_axis, string dir)
        {
            player.SetMoving(true);
            player.Left += x_axis;

            if (player.Bounds.IntersectsWith(opponent.Bounds))
            {
                player.Left -= x_axis;
            }

            player.Left = Math.Max(0, Math.Min(player.Left, ClientSize.Width - player.Width));
            player.ChangeAttackDir(dir);

        }

        private void PlayerJump(PlayerModel player)
        {
            if (player.OnAir == true) return;
            player.SetAirState(true);
            player.SetVelocityY(JumpVelocity);
        }

        private void ApplyGravity(PlayerModel player, PlayerModel opponent)
        {
            if (!player.OnAir) return;

            player.GravityAdd(Gravity);
            float movingTop = player.Top + player.VelocitY;

            if (player.ActualHitbox.IntersectsWith(opponent.ActualHitbox))
            {
                bool opponentIsRight = opponent.Left > player.Left;
                int newLeft = player.Left + (opponentIsRight ? -35 : 35);
                player.Left = Math.Max(0, Math.Min(newLeft, ClientSize.Width - player.Width));
            }


            if (movingTop >= player.GroundY)
            {
                player.Top = player.GroundY;
                player.SetVelocityY(0);
                player.SetAirState(false);
            }
            else
            {
                player.Top = (int)movingTop;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            DrawHud(e.Graphics);
            if (p1.HurtBox != Rectangle.Empty)
            {
                using (var hitboxPen = new Pen(Color.Red))
                {
                    e.Graphics.DrawRectangle(hitboxPen, p1.HurtBox);
                }
            }

            if (p2.HurtBox != Rectangle.Empty)
            {
                using (var hitboxPen = new Pen(Color.Red))
                {
                    e.Graphics.DrawRectangle(hitboxPen, p2.HurtBox);
                }
            }
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

            // Always face the player
            if (cpu.direction != toward) cpu.ChangeAttackDir(toward);

            // Walk toward the player until in attack range
            if (gap > AiBasicRange - 20)
                PlayerMovement(cpu, target, targetIsRight ? AiMoveSpeed : -AiMoveSpeed, toward);

            // Make one decision every few hundred milliseconds
            if (now < aiNextDecision) return;
            aiNextDecision = now + rng.Next(AiThinkMin, AiThinkMax);

            // 1. Heal when health is low
            if (d.GetHealAmount() > 0 && d.GetHealth() < d.GetMaxHealth() * 0.4f
                && d.GetMeter() >= d.GetHealCost() && now >= d.nextHealAllowed)
            {
                cpu.HealSelf(now);
                return;
            }

            // 2. Defend sometimes when the player is close
            if (gap < 150 && now >= d.nextDefendAllowed && rng.Next(100) < 25)
            {
                cpu.activateDefense(now);
                return;
            }

            // 3. Attack
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

            // 4. Sometimes jump
            if (gap < 200 && rng.Next(100) < 10) PlayerJump(cpu);
        }
        private void EndGame()
        {
            gameOver = true;
            gameLoop.Stop();
            heldKeys.Clear();

           string text;
bool p1Alive = p1.data.IsAlive();
bool p2Alive = p2.data.IsAlive();
if (p1Alive && p2Alive)   // time is up
{
    float h1 = HealthFraction(p1), h2 = HealthFraction(p2);
    if (Math.Abs(h1 - h2) < 0.001f) text = "TIME UP: DRAW";
    else if (h1 > h2) text = "TIME UP: P1 WINS!";
    else text = vsComputer ? "TIME UP: CPU WINS!" : "TIME UP: P2 WINS!";
}
else if (!p1Alive && !p2Alive) text = "DRAW";
else if (!p2Alive) text = "PLAYER 1 WINS!";
else text = vsComputer ? "COMPUTER WINS!" : "PLAYER 2 WINS!";

            var panel = new Panel
            {
                Size = new Size(360, 380),
                BackColor = Color.FromArgb(40, 40, 40)
            };
            panel.Location = new Point((ClientSize.Width - panel.Width) / 2,
                                       (ClientSize.Height - panel.Height) / 2);

            var title = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.White,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(panel.Width, 70),
                Location = new Point(0, 15)
            };
            panel.Controls.Add(title);

            AddResultButton(panel, "Retry", 100, "Retry");
            AddResultButton(panel, "Change Character", 170, "Character");
            AddResultButton(panel, "Change Map", 240, "Map");
            AddResultButton(panel, "Quit", 310, "Quit");

            Controls.Add(panel);
            panel.BringToFront();
        }

        private void AddResultButton(Panel panel, string text, int y, string result)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Size = new Size(240, 50),
                Location = new Point((panel.Width - 240) / 2, y),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat
            };
            btn.Click += (s, e) => { Result = result; Close(); };
            panel.Controls.Add(btn);
        }

private float HealthFraction(PlayerModel p)
{
    return (float)p.data.GetHealth() / p.data.GetMaxHealth();
}

// The red "damage trail" slowly catches up to the real health
private float EaseLag(float lag, float current)
{
    if (current >= lag) return current;
    return Math.Max(current, lag - 0.008f);
}

private void DrawHud(Graphics g)
{
    int w = ClientSize.Width;
    Rectangle hp1 = new Rectangle(20, 15, 330, 28);
    Rectangle hp2 = new Rectangle(w - 350, 15, 330, 28);
    Rectangle mp1 = new Rectangle(20, 66, 200, 14);
    Rectangle mp2 = new Rectangle(w - 220, 66, 200, 14);

    // Health bars (true/false = which side the bar is anchored to; swap to reverse)
    DrawBar(g, hp1, HealthFraction(p1), lag1, Color.Gold, true);
    DrawBar(g, hp2, HealthFraction(p2), lag2, Color.Gold, false);
    DrawText(g, p1.data.GetHealth() + "/" + p1.data.GetMaxHealth(), hp1, hudFont, StringAlignment.Center);
    DrawText(g, p2.data.GetHealth() + "/" + p2.data.GetMaxHealth(), hp2, hudFont, StringAlignment.Center);

    // Names
    DrawText(g, "P1  " + p1.data.GetCharacter(), new Rectangle(20, 44, 330, 20), nameFont, StringAlignment.Near);
    DrawText(g, (vsComputer ? "CPU  " : "P2  ") + p2.data.GetCharacter(),
             new Rectangle(w - 350, 44, 330, 20), nameFont, StringAlignment.Far);

    // Meter bars
    float m1 = (float)p1.data.GetMeter() / p1.data.GetMaxMeter();
    float m2 = (float)p2.data.GetMeter() / p2.data.GetMaxMeter();
    DrawBar(g, mp1, m1, m1, Color.DeepSkyBlue, false);
    DrawBar(g, mp2, m2, m2, Color.DeepSkyBlue, true);
    DrawText(g, p1.data.GetMeter() + "/" + p1.data.GetMaxMeter(), mp1, smallFont, StringAlignment.Center);
    DrawText(g, p2.data.GetMeter() + "/" + p2.data.GetMaxMeter(), mp2, smallFont, StringAlignment.Center);

    // Timer in the center
    int secondsLeft = Math.Max(0, (int)Math.Ceiling((roundEndsAt - now) / 1000.0));
    Rectangle timerBox = new Rectangle(w / 2 - 40, 10, 80, 60);
    using (var back = new SolidBrush(Color.FromArgb(200, 20, 20, 20)))
        g.FillRectangle(back, timerBox);
    g.DrawRectangle(Pens.White, timerBox);
    using (var f = new Font(timerFont, FontStyle.Bold))
    {
        var color = secondsLeft <= 10 ? Brushes.Red : Brushes.White;
        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            g.DrawString(secondsLeft.ToString("00"), f, color, timerBox, sf);
    }
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
    using (var sf = new StringFormat { Alignment = align, LineAlignment = StringAlignment.Center })
    {
        g.DrawString(text, f, Brushes.Black, new Rectangle(r.X + 1, r.Y + 1, r.Width, r.Height), sf);
        g.DrawString(text, f, Brushes.White, r, sf);
    }
}
    }
}