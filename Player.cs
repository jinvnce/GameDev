using System;

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
        private const int BasicCooldown = 500;    
        private const int SpecialCooldown = 2000;         
        private const int HealCooldown = 10000;   
        private const int DefendDuration = 3000;  
        private const int DefendCooldown = 3500; 
        private long nextBasicAllowed = 0;
        private long nextSpecialAllowed = 0;
        private long nextHealAllowed = 0;
        private long nextDefendAllowed = 0;
        private long defendActiveUntil = 0;

        public Players(string name, string classType, string resourceType,
                        int maxHealth, int maxMeter,
                        string basicName, int basicDamage,
                        string specialName, int specialCost, int specialDamage,
                        string healName, int healCost, int healAmount)
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

            Health = MaxHealth;
            Meter = MaxMeter / 2;
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

        public string TryBasicAttack(Players opponent, long now)
        {
            if (now < nextBasicAllowed)
            {
                return null;
            }
            nextBasicAllowed = now + BasicCooldown;

            bool blocked = opponent.IsDefendingNow(now);
            int damage = BasicDamage;
            if (blocked)
            {
                damage = 0;
            }

            opponent.TakeDamage(damage);
            GainMeter(2);

            if (blocked)
            {
                return $"{opponent.Name} blocks {Name}'s {BasicName}!";
            }
            else
            {
                return $"{Name} uses {BasicName} on {opponent.Name} for {damage} damage!";
            }
        }

        public string TrySpecialAttack(Players opponent, long now)
        {
            if (now < nextSpecialAllowed)
            {
                return null;
            }
            if (Meter < SpecialCost)
            {
                return $"{Name} needs more {ResourceType} for {SpecialName}!";
            }

            nextSpecialAllowed = now + SpecialCooldown;
            Meter -= SpecialCost;

            bool blocked = opponent.IsDefendingNow(now);
            int damage = SpecialDamage;

            if (blocked)
            {
                damage = 0;
            }

            opponent.TakeDamage(damage);

            if (blocked)
            {
                return $"{opponent.Name} blocks {Name}'s {SpecialName}!";
            }
            else
            {
                return $"{Name} unleashes {SpecialName} on {opponent.Name} for {damage} damage!";
            }
        }

        public string TryHeal(long now)
        {
            if (HealAmount <= 0)
            {
                return null;
            }
            if (now < nextHealAllowed)
            {
                return null;
            }
            if (Meter < HealCost)
            {
                return $"{Name} needs more {ResourceType} for {HealName}!";
            }

            nextHealAllowed = now + HealCooldown;
            Meter -= HealCost;

            Health = Health + HealAmount;

            if (Health > MaxHealth)
            {
                Health = MaxHealth;
            }

            return $"{Name} uses {HealName} and restores {HealAmount} HP!";
        }

        public string TryDefend(long now)
        {
            if (now < nextDefendAllowed)
            {
                return $"{Name}'s guard is cooling down!";
            }

            defendActiveUntil = now + DefendDuration;
            nextDefendAllowed = now + DefendCooldown;
            GainMeter(1);
            return $"{Name} raises their guard!";
        }

        public void TakeDamage(int damage)
        {
            Health = Health - damage;
            if (Health < 0)
            {
                Health = 0;
            }
        }

        private void GainMeter(int amount)
        {
            Meter = Meter + amount;
            if (Meter > MaxMeter)
            {
                Meter = MaxMeter;
            }
        }
    }
}