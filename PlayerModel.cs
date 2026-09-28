using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Game
{
    public class PlayerModel : Control
    {
        public Players data {get; private set;}
        public string direction {get; private set;}
        public bool attacking {get; private set;}
        public string playerNum {get; private set;}
        public bool OnAir {get; private set;}
        public float VelocitY {get; private set;}
        public int GroundY {get; private set;}
        private Color ModelColor {get; set;}
        public Rectangle ActualHitbox => new Rectangle(Left,Top+BarArea,Width,Height-BarArea);
        public Rectangle HurtBox {get; private set;}
        public bool DefenseActive{get; private set;}
        private Image standImg, walkImg, attackImg;
        private bool spritesFaceRight = true;
        private bool walkFrame;
        private int frameCounter;
        public bool IsMoving { get; private set; }
        private Image specialImg;
        private bool specialActive;
        private class Popup { public string Text; public Color Color; public long Start; public int Lane; }
        private readonly List<Popup> popups = new List<Popup>();
        private const int PopupMs = 1000;
        private readonly Font popupFont = new Font("Segoe UI", 9, FontStyle.Bold);
        public PlayerModel(string name,Color ModelColor, string dir, string player)
        {
            data = new Special_List().CreatePlayer(name);
            this.ModelColor = ModelColor;
            direction = dir;
            playerNum = player;
            Size = new Size(130, 190);
            DoubleBuffered = true;
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        private const int BarArea = 50;
        public void ChangeAttackDir(string dir)
        {
            direction = dir;
            Invalidate();
        }

        public void SetAttackState(bool state)
        {
            attacking = state;
        }

        public void SetAirState(bool state)
        {
            OnAir = state;
        }
        public String InitializeInfo()
        {
            string text = $"=====================\nName: {data.GetCharacter()}\nHP: {data.GetHealth()}\nMeter:{data.GetMeter()}\n=====================\n";
            return text;
        }

        public void SetVelocityY(float velocity)
        {
            VelocitY = velocity;
        }

        public void SetGroundY(int ground)
        {
            GroundY = ground;
        }

        public void GravityAdd(float Gravity)
        {
            VelocitY += Gravity;
        }

        protected override void OnPaint(PaintEventArgs e)
{
    base.OnPaint(e);
    DrawSprite(e.Graphics);

    if (DefenseActive)
    {
        using (var pen = new Pen(Color.FromArgb(180, Color.Cyan), 3))
            e.Graphics.DrawEllipse(pen, 2, BarArea, Width - 5, Height - BarArea - 2);
    }

    DrawPopups(e.Graphics);
}

       

        public void checkDefense(long now)
        {
            if (!data.IsDefendingNow(now))
            {
                DefenseActive = false;
                Invalidate();    
            }            
        }

        public void activateDefense(long now)
{
    if (now < data.nextDefendAllowed)
    {
        int secs = (int)Math.Ceiling((data.nextDefendAllowed - now) / 1000.0);
        ShowPopup($"Defend cooldown {secs}s", Color.Orange);
        return;
    }
    if (DefenseActive) return;
    data.TryDefend(now);
    DefenseActive = true;
    ShowPopup("Defend!", Color.Cyan);
    Invalidate();
}

        public async void CreateHurtBox(PlayerModel opponent, string attack, long now)
{
    if (attacking) return;

    int damage;
    string skillName;

    if (attack.Equals("Basic"))
    {
        int hurtX = direction == "Right" ? ActualHitbox.Right : ActualHitbox.Left - 130;
        HurtBox = new Rectangle(hurtX, ActualHitbox.Y, 130, 130);
        damage = data.GetBasicDamage();
        skillName = data.GetBasicName();
    }
    else if (attack.Equals("Special"))
    {
        if (data.GetMeter() < data.GetSpecialCost())
        {
            ShowPopup($"Need {data.GetSpecialCost()} meter", Color.Orange);
            return;
        }
        int hurtX = direction == "Right" ? ActualHitbox.Right : ActualHitbox.Left - data.GetSpecHitBox().Width;
        int hurtY = ActualHitbox.Y + (ActualHitbox.Height / 2) - (data.GetSpecHitBox().Height / 2);
        HurtBox = new Rectangle(hurtX, hurtY, data.GetSpecHitBox().Width, data.GetSpecHitBox().Height);
        damage = data.GetSpecialDamage();
        skillName = data.GetSpecialName();
        data.TrySpecialAttack();
        specialActive = true;
    }
    else return;

    SetAttackState(true);
    ShowPopup(skillName + "!", attack.Equals("Special") ? Color.Gold : Color.White);
    Invalidate();

    try
    {
        if (HurtBox.IntersectsWith(opponent.ActualHitbox))
        {
            int dealt = opponent.data.TakeDamage(damage, opponent.DefenseActive);
            if (dealt != 0)
            {
                data.GainMeter(2);
                opponent.data.GainMeter(1);
                opponent.ShowPopup("-" + dealt, Color.Red);
            }
            else
            {
                opponent.ShowPopup("BLOCKED", Color.Cyan);
            }
            Invalidate();
            opponent.Invalidate();
        }
        await Task.Delay(100);
    }
    finally
    {
        HurtBox = Rectangle.Empty;
        SetAttackState(false);
        specialActive = false;
        Invalidate();
    }
}

        public void HealSelf(long now)
{
    if (data.GetHealAmount() <= 0) { ShowPopup("No heal", Color.Gray); return; }
    if (now < data.nextHealAllowed)
    {
        int secs = (int)Math.Ceiling((data.nextHealAllowed - now) / 1000.0);
        ShowPopup($"Heal cooldown {secs}s", Color.Orange);
        return;
    }
    if (data.GetMeter() < data.GetHealCost())
    {
        ShowPopup($"Need {data.GetHealCost()} meter", Color.Orange);
        return;
    }
    int before = data.GetHealth();
    data.TryHeal(now);
    ShowPopup("+" + (data.GetHealth() - before) + " HP", Color.LimeGreen);
    Invalidate();
}
        public void LoadSprites(string stand, string walk, string attack, string special, bool facesRight = true)
{
    standImg = Image.FromFile(stand);
    walkImg = Image.FromFile(walk);
    attackImg = Image.FromFile(attack);
    specialImg = Image.FromFile(special);
    spritesFaceRight = facesRight;
}

public void SetMoving(bool moving)
{
    IsMoving = moving;
}

public void UpdateAnimation()
{
    long t = Environment.TickCount64;
    int removed = popups.RemoveAll(p => t - p.Start >= PopupMs);
    if (removed > 0 || popups.Count > 0) Invalidate();

    if (!IsMoving)
    {
        if (walkFrame) { walkFrame = false; frameCounter = 0; Invalidate(); }
        return;
    }
    frameCounter++;
    if (frameCounter >= 6)
    {
        frameCounter = 0;
        walkFrame = !walkFrame;
        Invalidate();
    }
}

private Image GetCurrentSprite()
{
    if (attacking) return specialActive ? specialImg : attackImg;
    if (IsMoving && walkFrame) return walkImg;
    return standImg;
}

private void DrawSprite(Graphics g)
{
    Image img = GetCurrentSprite();
    if (img == null)   // no image loaded, keep the old rectangle
    {
        using (var b = new SolidBrush(ModelColor))
            g.FillRectangle(b, 0, BarArea, Width, Height - BarArea);
        return;
    }

    int areaH = Height - BarArea;
    float scale = Math.Min((float)Width / img.Width, (float)areaH / img.Height);
    int w = (int)(img.Width * scale);
    int h = (int)(img.Height * scale);
    int x = (Width - w) / 2;
    int y = Height - h;   // feet at the bottom of the hitbox

    bool flip = (direction == "Left") == spritesFaceRight;
    var state = g.Save();
    if (flip)
    {
        g.TranslateTransform(x + w / 2f, 0);
        g.ScaleTransform(-1, 1);
        g.TranslateTransform(-(x + w / 2f), 0);
    }
    g.DrawImage(img, x, y, w, h);
    g.Restore(state);
}
        public void ShowPopup(string text, Color color)
{
    long t = Environment.TickCount64;
    popups.RemoveAll(p => t - p.Start >= PopupMs);
    if (popups.Exists(p => p.Text == text && t - p.Start < 600)) return; // no spam
    int lane = Math.Min(popups.Count, 2);
    popups.Add(new Popup { Text = text, Color = color, Start = t, Lane = lane });
    Invalidate();
}

private void DrawPopups(Graphics g)
{
    long t = Environment.TickCount64;
    using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
    {
        foreach (var p in popups)
        {
            float age = (t - p.Start) / (float)PopupMs;
            if (age >= 1f) continue;
            int alpha = (int)(255 * (1f - age * age));
            float y = BarArea - 12 - p.Lane * 18 - age * 10;
            var rect = new RectangleF(0, y - 9, Width, 18);
            var shadowRect = new RectangleF(1, y - 8, Width, 18);
            using (var shadow = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                g.DrawString(p.Text, popupFont, shadow, shadowRect, sf);
            using (var main = new SolidBrush(Color.FromArgb(alpha, p.Color)))
                g.DrawString(p.Text, popupFont, main, rect, sf);
        }
    }
}
    }
}
