using System;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;


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
        public PlayerModel(string name,Color ModelColor, string dir, string player)
        {
            data = new Special_List().CreatePlayer(name);
            this.ModelColor = ModelColor;
            direction = dir;
            playerNum = player;
            Size = new Size(100,145);
            DoubleBuffered = true;
            SetStyle(ControlStyles.Selectable, false);
            TabStop = false;
            SetStyle(ControlStyles.SupportsTransparentBackColor, true);
            BackColor = Color.Transparent;
        }

        private const int BarArea = 45;

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

            DrawStats(e.Graphics);
        }

        private void DrawStats(Graphics g)
        {
            
            Rectangle HealthBar = new Rectangle(0,0,Width,20);
            float HealthPercent = (float) data.GetHealth()/ data.GetMaxHealth();
            HealthPercent = Math.Max(0, Math.Min(1f,HealthPercent));

            using(var emptyBrush = new SolidBrush(Color.Red))
            {
                g.FillRectangle(emptyBrush, HealthBar);
            }

            int FilledWidth = (int)(HealthBar.Width * HealthPercent);
            Rectangle HealthFilledArea = new Rectangle(HealthBar.X,HealthBar.Y,FilledWidth,HealthBar.Height);

            using(var fillBrush = new SolidBrush(Color.Lime))
            {
                g.FillRectangle(fillBrush, HealthFilledArea);
            }

            string Health = $"{data.GetHealth()}/{data.GetMaxHealth()}";
            SizeF textSize = g.MeasureString(Health,Font);
            float HtextX = HealthBar.X + (HealthBar.Width - textSize.Width)/2;
            float HtextY = HealthBar.Y + (HealthBar.Height - textSize.Height)/2;
            g.DrawString(Health,Font,Brushes.Black,HtextX,HtextY);  

            Rectangle MeterBar = new Rectangle(0,BarArea-25,Width,20);
            float MeterPercent = (float) data.GetMeter()/data.GetMaxMeter();
            MeterPercent = Math.Max(0,Math.Min(1f,MeterPercent));

            using(var emptyBrush = new SolidBrush(Color.White))
            {
                g.FillRectangle(emptyBrush,MeterBar);
            }

            int FillMeterWidth = (int)(MeterBar.Width*MeterPercent);
            Rectangle FillMeterArea = new Rectangle(MeterBar.X,MeterBar.Y,FillMeterWidth,MeterBar.Height);

            using(var fillBrush = new SolidBrush(Color.Yellow))
            {
                g.FillRectangle(fillBrush, FillMeterArea);
            }  

            string Meter = $"{data.GetMeter()}/{data.GetMaxMeter()}";
            SizeF MeterText = g.MeasureString(Meter,Font);
            float MtextX  = MeterBar.X + (MeterBar.Width-MeterText.Width)/2;
            float MtextY  = MeterBar.Y + (MeterBar.Height-MeterText.Height)/2;
            g.DrawString(Meter,Font,Brushes.Black,MtextX,MtextY);

            // Defense Check
            string Defense = "";
            SizeF DefenseText;
            float DtextX =0;
            float DtextY =0;
            if (DefenseActive == true)
            {
                Defense = "[DEFENDING]";
                using(var defenseFont = new Font(Font.FontFamily,7f, FontStyle.Bold))
                {
                DefenseText = g.MeasureString(Defense,defenseFont);
                DtextX =(Width-DefenseText.Width)/2;
                DtextY =(Height+BarArea-DefenseText.Height)/2;
                g.DrawString(Defense,defenseFont,Brushes.Black,DtextX,DtextY);
                }
            }
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
            if(now<data.nextDefendAllowed) return;
            if(DefenseActive) return;
            data.TryDefend(now);
            DefenseActive = true;
            Invalidate();
        }

        public async void CreateHurtBox(PlayerModel opponent, string attack, long now)
        {
            if(attacking == true) return;
            SetAttackState(true);
            int damage = 0;

            if(attack.Equals("Basic"))
            {
                int hurtX = direction == "Right" ? ActualHitbox.Right : ActualHitbox.Left - 100;
                HurtBox = new Rectangle(hurtX,ActualHitbox.Y,100,100);
                damage = data.GetBasicDamage();
            }
            else if(attack.Equals("Special") && data.GetMeter() >= data.GetSpecialCost()) 
            {
                int hurtX = direction == "Right" ? ActualHitbox.Right : ActualHitbox.Left - data.GetSpecHitBox().Width;
                int hurtY = ActualHitbox.Y + (ActualHitbox.Height / 2) - (data.GetSpecHitBox().Height / 2);
                HurtBox = new Rectangle(hurtX,hurtY,data.GetSpecHitBox().Width,data.GetSpecHitBox().Height);
                damage = data.GetSpecialDamage();
                data.TrySpecialAttack();
                specialActive = true; 
            }
            Invalidate();
            try
            {
                if (HurtBox.IntersectsWith(opponent.ActualHitbox))
                {
                    int checkdamage = opponent.data.TakeDamage(damage,opponent.DefenseActive);
                    if(checkdamage != 0)
                    {
                    data.GainMeter(2);
                    opponent.data.GainMeter(1);
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
                specialActive = false;   // <-- new
                Invalidate();
            }
        }

        public void HealSelf(long now)
        {
            data.TryHeal(now);
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
    if (!IsMoving)
    {
        if (walkFrame) { walkFrame = false; frameCounter = 0; Invalidate(); }
        return;
    }
    frameCounter++;
    if (frameCounter >= 6)   // lower = faster steps
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
        
    }
}
