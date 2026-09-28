using System;

namespace Game
{
    public class Special_List
    {
        private string[] names;
        private string[] classTypes;
        private string[] resourceTypes;
        private int[] maxHealths;
        private int[] maxMeters;
        private string[] basicNames;
        private int[] basicDamages;
        private string[] specialNames;
        private int[] specialCosts;
        private int[] specialDamages;
        private string[] healNames;
        private int[] healCosts;
        private int[] healAmounts;

        public Special_List()
        {
            names          = new[] { "Ken", "Ren", "Mira", "Thorne", "Vex", "Elowen", "Rene"};
            classTypes     = new[] { "Fighter", "Fighter", "Mage", "Tank", "Assassin", "Support", "Support"};
            resourceTypes  = new[] { "Energy", "Energy", "Mana", "Energy", "Energy", "Mana", "Mana"};
            maxHealths     = new[] { 1000, 1000, 1080, 1300, 900,              850,                1500};
            maxMeters      = new[] { 10,                 10,                 12,              8,                  10,              12 ,                 100};
            basicNames     = new[] { "Straight Punch",   "Roundhouse Kick",  "Arcane Bolt",   "Shield Bash",      "Quick Slash",   "Spirit Bolt" ,     "Dunk ni Bobet"};
            basicDamages   = new[] { 100,                 100,                 82,               92,                  28,              70,       50};
            specialNames   = new[] { "Shoryuken",        "Tatsumaki Storm",  "Meteor Surge",  "Earthquake Slam",  "Shadow Strike", "Sanctified Blast" ,     "Mama's Roar"};
            specialCosts   = new[] { 5,                  7,                  6,               8,                  4,               5,         5};
            specialDamages = new[] { 25,                 35,                 40,              30,                 20,              30,         150};
            healNames      = new[] { "None",             "None",             "None",          "None",             "None",          "Mending Light", "Bituin ng Mindanao"};
            healCosts      = new[] { 0,                  0,                  0,               0,                  0,               4 ,  10};
            healAmounts    = new[] { 0,                  0,                  0,               0,                  0,               15 , 50};
        }

        public int GetCount() 
        { 
            return names.Length; 
        }

        public string GetName(int i) 
        { 
            return names[i]; 
        }

        public string GetClassType(int i) 
        { 
            return classTypes[i]; 
        }

        public string GetResourceType(int i) 
        { 
            return resourceTypes[i]; 
        }

        public int GetMaxHealth(int i) 
        { 
            return maxHealths[i]; 
        }

        public int GetMaxMeter(int i) 
        { 
            return maxMeters[i]; 
        }

        public string GetBasicName(int i) 
        { 
            return basicNames[i]; 
        }

        public int GetBasicDamage(int i) 
        { 
            return basicDamages[i]; 
        }

        public string GetSpecialName(int i) 
        { 
            return specialNames[i]; 
        }

        public int GetSpecialCost(int i) 
        { 
            return specialCosts[i]; 
        }

        public int GetSpecialDamage(int i) 
        { 
            return specialDamages[i]; 
        }

        public string GetHealName(int i) 
        { 
            return healNames[i]; 
        }

        public int GetHealCost(int i) 
        { 
            return healCosts[i]; 
        }

        public int GetHealAmount(int i) 
        { 
            return healAmounts[i]; 
        }

        public int FindIndexByName(string name)
        {
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == name) 
                
                return i;
            }
            return 0;
        }

        public Players CreatePlayer(string name)
        {
            int i = FindIndexByName(name);

            return new Players(
                names[i], classTypes[i], resourceTypes[i],
                maxHealths[i], maxMeters[i],
                basicNames[i], basicDamages[i],
                specialNames[i], specialCosts[i], specialDamages[i],
                healNames[i], healCosts[i], healAmounts[i]
            );
        }
    }
}