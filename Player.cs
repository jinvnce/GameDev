using System;
using System.Windows.Forms;

namespace Game
{
    public class Players
    {
        private string Name;
        private string ClassType;
        private string ResourceType;
        private int MaxHealth;
        private int MaxMeter;
        private string BasicName;
        private int BasicDamage;
        private string SpecialName;
        private int SpecialCost;
        private int SpecialDamage;
        private string HealName;
        private int HealCost;
        private int HealAmount;
        private int Health;
        private int Meter;
        private Size SpecHitbox;
        private const int BasicCooldown = 500;    
        private const int SpecialCooldown = 2000;         
        private const int HealCooldown = 10000;   
        private const int DefendDuration = 3000;  
        private const int DefendCooldown = 5000; 
        private long nextBasicAllowed = 0;
        private long nextSpecialAllowed = 0;
        public long nextHealAllowed {get; private set;} = 0;
        public long nextDefendAllowed {get; private set;} = 0;
        private long defendActiveUntil = 0;

        

        public Players(string name, string classType, string resourceType,
                        int maxHealth, int maxMeter,
                        string basicName, int basicDamage,
                        string specialName, int specialCost, int specialDamage,
                        string healName, int healCost, int healAmount, Size SpecHitbox)
        {
            this.Name = name;
            this.ClassType = classType;
            this.ResourceType = resourceType;
            this.MaxHealth = maxHealth;
            this.MaxMeter = maxMeter;
            this.BasicName = basicName;
            this.BasicDamage = basicDamage;
            this.SpecialName = specialName;
            this.SpecialCost = specialCost;
            this.SpecialDamage = specialDamage;
            this.HealName = healName;
            this.HealCost = healCost;
            this.HealAmount = healAmount;
            this.SpecHitbox = SpecHitbox;
            Health = MaxHealth;
            Meter = MaxMeter / 2;
        }
        public Size GetSpecHitBox()
        {
            return SpecHitbox;
        }

        public string GetCharacter() 
        { 
            return Name; 
        }

        public string GetClassType() 
        { 
            return ClassType; 
        }

        public string GetResourceType() 
        { 
            return ResourceType; 
        }

        public int GetHealth() 
        { 
            return Health; 
        }

        public int GetMaxHealth() 
        { 
            return MaxHealth; 
        }

        public int GetMeter() 
        { 
            return Meter; 
        }

        public int GetMaxMeter() 
        { 
            return MaxMeter; 
        }

        public string GetBasicName() 
        { 
            return BasicName; 
        }

        public int GetBasicDamage() 
        { 
            return BasicDamage; 
        }

        public string GetSpecialName() 
        { 
            return SpecialName; 
        }

        public int GetSpecialDamage() 
        { 
            return SpecialDamage; 
        }

        public int GetSpecialCost() 
        { 
            return SpecialCost; 
        }

        public string GetHealName() 
        { 
            return HealName; 
        }

        public int GetHealAmount() 
        { 
            return HealAmount; 
        }

        public int GetHealCost() 
        { 
            return HealCost; 
        }

        public bool IsAlive() 
        { 
            return Health > 0; 
        }

        public bool IsDefendingNow(long now)
        {
            if (now < defendActiveUntil)
            {
                return true;
            }
            return false;
        }

        public void TrySpecialAttack()
        {
            
            if (Meter < SpecialCost) return;

            Meter -= SpecialCost;
        }

        public void TryHeal(long now)
        {
            if (HealAmount <= 0) return;
            if (now < nextHealAllowed) return;
            if (Meter < HealCost) return;

            nextHealAllowed = now + HealCooldown;
            Meter -= HealCost;

            Health = Health + HealAmount;

            if (Health > MaxHealth)
            {
                Health = MaxHealth;
            }
            return;
        }

        public void TryDefend(long now)
        {
            if (now < nextDefendAllowed)
            {
                return;
            }

            defendActiveUntil = now + DefendDuration;
            nextDefendAllowed = now + DefendCooldown;
            GainMeter(1);
        }

        public int TakeDamage(int damage,bool defending)
        {
            int defense = 0;
            if (defending)
            {
                defense = 50;
            }
            int finaldamage = Math.Max(0,damage - defense);
            Health = Math.Max(0,Health-finaldamage);
            return finaldamage;
        }

        public void GainMeter(int amount)
        {
            Meter = Meter + amount;
            if (Meter > MaxMeter)
            {
                Meter = MaxMeter;
            }
        }
    }
}