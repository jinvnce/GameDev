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
        private const int AiBasicRange = 90;   
        private readonly float Gravity = 1.2f;
        private readonly float JumpVelocity = -23f;
        public long now{get; private set;}
        
        int i =0;

       public MainGUI(string p1Name, string p2Name, string mapPath, bool vsComputer)
        {
            this.vsComputer = vsComputer;
            p1 = new PlayerModel(p1Name, Color.Blue, "Right", "P1");
            p2 = new PlayerModel(p2Name, Color.Red, "Left", "P2");

            Text = "Game Title";
            ClientSize = new Size(800,600);
            DoubleBuffered = true;
            CenterToScreen();
            int form_width = ClientSize.Width;

            BackgroundImage = Image.FromFile(mapPath);
            BackgroundImageLayout = ImageLayout.Stretch;

            Controls.Add(p1);
            p1.Location = new Point(100,ClientSize.Height/2);
            p1.SetGroundY(ClientSize.Height/2);

            Controls.Add(p2);
            p2.Location = new Point(form_width-200,ClientSize.Height/2);
            p2.SetGroundY(ClientSize.Height/2);

            p1.LoadSprites(Roster.SpritePath(p1Name,"idle"), Roster.SpritePath(p1Name,"walk"),
                        Roster.SpritePath(p1Name,"basic"), Roster.SpecialPath(p1Name));
            p2.LoadSprites(Roster.SpritePath(p2Name,"idle"), Roster.SpritePath(p2Name,"walk"),
                        Roster.SpritePath(p2Name,"basic"), Roster.SpecialPath(p2Name));

            KeyPreview = true;
            KeyDown += (s,e) => heldKeys.Add(e.KeyCode);
            KeyUp += (s,e) => heldKeys.Remove(e.KeyCode);
            KeyDown += PlayerActions;
            var gameLoop = new System.Windows.Forms.Timer {Interval = 15};
            gameLoop.Tick += gameLoop_Tick;
            gameLoop.Start();
        }

        private void gameLoop_Tick (object sender, EventArgs e)
{
    now = Environment.TickCount64;
    p1.checkDefense(now);
    p2.checkDefense(now);
    Invalidate();
    int movement = 6;

    p1.SetMoving(false);
    p2.SetMoving(false);

    if(heldKeys.Contains(Keys.A)) PlayerMovement(p1,p2,-movement,"Left");
    if(heldKeys.Contains(Keys.D)) PlayerMovement(p1,p2,movement,"Right");

    if (vsComputer)
    {
        ComputerTurn();
    }
    else
    {
        if(heldKeys.Contains(Keys.Left)) PlayerMovement(p2,p1,-movement,"Left");
        if(heldKeys.Contains(Keys.Right)) PlayerMovement(p2,p1,movement,"Right");
    }

    p1.UpdateAnimation();
    p2.UpdateAnimation();

    ApplyGravity(p1,p2);
    ApplyGravity(p2,p1);
}

        private void PlayerActions(object sender, KeyEventArgs e)
        {
            if (vsComputer && (e.KeyCode == Keys.Up || e.KeyCode == Keys.J || e.KeyCode == Keys.K
                || e.KeyCode == Keys.U  || e.KeyCode == Keys.I)) return;
            switch(e.KeyCode)
            {
                // P1
                case Keys.W:
                PlayerJump(p1);
                break;
                case Keys.C:
                p1.CreateHurtBox(p2,"Basic",now);
                break;
                case Keys.V:
                p1.CreateHurtBox(p2,"Special",now);
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
                p2.CreateHurtBox(p1,"Basic",now);
                break;
                case Keys.K:
                p2.CreateHurtBox(p1,"Special",now);
                break;
                case Keys.U:
                p2.activateDefense(now);
                break;
                case Keys.I:
                p2.HealSelf(now);
                break;
            }
            
        }
           
        private void PlayerMovement(PlayerModel player, PlayerModel opponent, int x_axis,string dir)
        {
            player.SetMoving(true);
            player.Left += x_axis;

            if (player.Bounds.IntersectsWith(opponent.Bounds))
            {
                player.Left -= x_axis;
            }
            
            player.Left = Math.Max(0,Math.Min(player.Left, ClientSize.Width-player.Width));
            player.ChangeAttackDir(dir);
 
        }

        private void PlayerJump(PlayerModel player)
        {
            if(player.OnAir == true) return;
            player.SetAirState(true);
            player.SetVelocityY(JumpVelocity);
        }

        private void ApplyGravity(PlayerModel player, PlayerModel opponent)
        {
            if(!player.OnAir) return;

            player.GravityAdd(Gravity);
            float movingTop = player.Top + player.VelocitY;

            if (player.ActualHitbox.IntersectsWith(opponent.ActualHitbox))
                {
                    bool opponentIsRight = opponent.Left > player.Left;
                    int newLeft = player.Left + (opponentIsRight ? -35 : 35);
                    player.Left = Math.Max(0, Math.Min(newLeft, ClientSize.Width - player.Width));
                }


            if(movingTop >= player.GroundY)
            {
                player.Top = player.GroundY;
                player.SetVelocityY(0);
                player.SetAirState(false);
            }
            else
            {
                player.Top = (int) movingTop;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

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
    }
}