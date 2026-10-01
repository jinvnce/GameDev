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
private const float BodyFit = 1.0f;   

private int BodyWidth
{
    get
    {
        if (standImg == null) return Width;
        float scale = Math.Min((float)Width / standImg.Width, (float)(Height - BarArea) / standImg.Height);
        return (int)(standImg.Width * scale * BodyFit);
    }
}

public Rectangle ActualHitbox => new Rectangle(Left + (Width - BodyWidth) / 2, Top + BarArea, BodyWidth, Height - BarArea);        
        public Rectangle HurtBox {get; private set;}
        private const int BasicReach = 100;  
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
        private Image jumpImg;
private long landSquashUntil;
        private readonly Font popupFont = new Font("Segoe UI", 9, FontStyle.Bold);
        public PlayerModel(string name,Color ModelColor, string dir, string player)
        {
            data = new Special_List().CreatePlayer(name);
            this.ModelColor = ModelColor;
            direction = dir;
            playerNum = player;
            Size = new Size(280, 300);
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
    if (OnAir && !state) landSquashUntil = Environment.TickCount64 + 120;   
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

        public void Render(Graphics g)
{
    var state = g.Save();
    g.TranslateTransform(Left, Top);

    DrawSprite(g);

    if (DefenseActive)
    {
        int bw = BodyWidth;
        var shield = new Rectangle((Width - bw) / 2 - 6, BarArea - 4, bw + 12, Height - BarArea + 4);
        using (var fill = new SolidBrush(Color.FromArgb(50, Color.Cyan)))
            g.FillEllipse(fill, shield);
        using (var pen = new Pen(Color.FromArgb(200, Color.Cyan), 3))
            g.DrawEllipse(pen, shield);
    }

    DrawPopups(g);
    g.Restore(state);
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
       int hurtX = direction == "Right" ? ActualHitbox.Right : ActualHitbox.Left - BasicReach;
HurtBox = new Rectangle(hurtX, ActualHitbox.Y, BasicReach, 130);
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
        public void LoadSprites(string stand, string walk, string attack, string special, string jump = null, bool facesRight = true)
{
    standImg = Image.FromFile(stand);
    walkImg = Image.FromFile(walk);
    attackImg = Image.FromFile(attack);
    specialImg = Image.FromFile(special);
    jumpImg = (jump != null && System.IO.File.Exists(jump)) ? Image.FromFile(jump) : null;
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
    if (OnAir && jumpImg != null) return jumpImg;
    if (IsMoving && walkFrame) return walkImg;
    return standImg;
}

private void DrawSprite(Graphics g)
{
    Image img = GetCurrentSprite();
    if (img == null)
    {
        using (var b = new SolidBrush(ModelColor))
            g.FillRectangle(b, 0, BarArea, Width, Height - BarArea);
        return;
    }

    int areaH = Height - BarArea;
    float scale = Math.Min((float)Width / img.Width, (float)areaH / img.Height);

    float sx = 1f, sy = 1f;
    if (OnAir && img == jumpImg)
    {
        float v = Math.Min(1f, Math.Abs(VelocitY) / 23f);
        sy = 1f + v * 0.08f;
        sx = 1f - v * 0.05f;
    }
    else if (!OnAir && Environment.TickCount64 < landSquashUntil)
    {
        sy = 0.92f;
        sx = 1.06f;
    }

    int w = (int)(img.Width * scale * sx);
    int h = (int)(img.Height * scale * sy);
    int x = (Width - w) / 2;
    int y = Height - h;

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
    if (popups.Exists(p => p.Text == text && t - p.Start < 600)) return; 
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
