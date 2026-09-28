using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Metadata;
using System.Threading;

namespace Game
{
    public class GameManager
    {
        public static void Main(String[] args)
        {
            GameManager manager = new GameManager();
            manager.ShowTitle();

            Special_List rosterSource = new Special_List();

            String p1Name = manager.ChooseCharacter(1, rosterSource);
            String p2Name = manager.ChooseCharacter(2, rosterSource);

            Players p1 = rosterSource.CreatePlayer(p1Name);
            Players p2 = rosterSource.CreatePlayer(p2Name);

            manager.Fight(p1, p2);
        }

        public void ShowTitle()
        {
            Console.Clear();
            Console.WriteLine("=========================================");
            Console.WriteLine("         BINS PAYT: The Online Sapakan       ");
            Console.WriteLine("=========================================");
            Console.WriteLine();
            Console.WriteLine("Press ENTER to start...");
            Console.ReadLine();
        }

        public String ChooseCharacter(int playerNum, Special_List roster)
        {
            Console.Clear();
            Console.WriteLine();

            int count = roster.GetCount();
            for (int i = 0; i < count; i++)
            {
                Console.WriteLine($"{i + 1}. {roster.GetName(i)} ({roster.GetClassType(i)}) - HP {roster.GetMaxHealth(i)}, {roster.GetResourceType(i)} {roster.GetMaxMeter(i)}");
                Console.WriteLine($"     Basic:   {roster.GetBasicName(i)} - {roster.GetBasicDamage(i)} dmg");
                Console.WriteLine($"     Special: {roster.GetSpecialName(i)} - {roster.GetSpecialDamage(i)} dmg, costs {roster.GetSpecialCost(i)} {roster.GetResourceType(i)}");
                if (roster.GetHealAmount(i) > 0)
                {
                    Console.WriteLine($"     Heal:    {roster.GetHealName(i)} - restores {roster.GetHealAmount(i)} HP, costs {roster.GetHealCost(i)} {roster.GetResourceType(i)}");
                }
                Console.WriteLine();
            }
            Console.WriteLine($"Player {playerNum}, choose your fighter:");
            Console.WriteLine();
            Console.Write($"Enter choice (1-{count}): ");
            String input = Console.ReadLine();
            int choice;

            while (!int.TryParse(input, out choice) || choice < 1 || choice > count)
            {
                Console.Write($"Invalid choice. Enter 1-{count}: ");
                input = Console.ReadLine();
            }
            
            
            return roster.GetName(choice - 1);

        }

        public void Fight(Players p1, Players p2)
        {
            Console.Clear();
            Console.WriteLine("Controls");

            string p1Controls = "  Player 1: A = Basic Attack   S = Special   D = Defend";
            if (p1.GetHealAmount() > 0)
            {
                p1Controls = p1Controls + "   F = Heal";
            }

            string p2Controls = "  Player 2: J = Basic Attack   K = Special   L = Defend";

            if (p2.GetHealAmount() > 0)
            {
                p2Controls = p2Controls + "   I = Heal";
            }

            Console.WriteLine(p1Controls);
            Console.WriteLine(p2Controls);
            Console.WriteLine();
            Console.WriteLine("Press ENTER to begin the match!");
            Console.ReadLine();

            Console.Clear();
            Console.CursorVisible = false;

            List<string> log = new List<string>();
            Stopwatch clock = Stopwatch.StartNew();

            while (p1.IsAlive() && p2.IsAlive())
            {
                long now = clock.ElapsedMilliseconds;

                while (Console.KeyAvailable)
                {
                    ConsoleKeyInfo key = Console.ReadKey(true);
                    HandleInput(key.Key, p1, p2, now, log);
                }

                Render(p1, p2, log, now);
                Thread.Sleep(60);
            }
        
            Console.CursorVisible = true;
            Console.Clear();
            Console.WriteLine("===== AH ARAY TAPOS =====");

            if (p1.IsAlive() && !p2.IsAlive())
                Console.WriteLine($"{p1.GetCharacter()} wins!");
            else if (p2.IsAlive() && !p1.IsAlive())
                Console.WriteLine($"{p2.GetCharacter()} wins!");
            else
                Console.WriteLine("It's a draw!");
        }

        private void HandleInput(ConsoleKey key, Players p1, Players p2, long now, List<string> log)
        {
            string message = null;

            switch (key)
            {
                case ConsoleKey.A:
                    message = p1.TryBasicAttack(p2, now);
                    break;

                case ConsoleKey.S:
                    message = p1.TrySpecialAttack(p2, now);
                    break;

                case ConsoleKey.D:
                    message = p1.TryDefend(now);
                    break;

                case ConsoleKey.F:
                    message = p1.TryHeal(now);
                    break;

                case ConsoleKey.J:
                    message = p2.TryBasicAttack(p1, now);
                    break;

                case ConsoleKey.K:
                    message = p2.TrySpecialAttack(p1, now);
                    break;

                case ConsoleKey.L:
                    message = p2.TryDefend(now);
                    break;

                case ConsoleKey.I:
                    message = p2.TryHeal(now);
                    break;
            }

            if (message != null)
            {
                log.Add(message);
                while (log.Count > 5) log.RemoveAt(0);
            }
        }

        private void Render(Players p1, Players p2, List<string> log, long now)
        {
            List<string> lines = new List<string>();
            lines.Add("========== SAPAKAN ==========");
            lines.Add("");
            lines.Add(FormatStatus(p1, now));
            lines.Add(FormatSkills(p1));
            lines.Add("");
            lines.Add(FormatStatus(p2, now));
            lines.Add(FormatSkills(p2));
            lines.Add("");
            lines.Add(FormatControls(p1, "P1", "A", "S", "D", "F") + "      " + FormatControls(p2, "P2", "J", "K", "L", "I"));
            lines.Add("");
            lines.Add("Log:");

            for (int i = 0; i < 5; i++)
            {
                int logIndex = log.Count - 5 + i;
                if (logIndex >= 0)
                {
                    lines.Add(" " + log[logIndex]);
                }
                else
                {
                    lines.Add("");
                }
            }

            for (int i = 0; i < lines.Count; i++)
            {
                Console.SetCursorPosition(0, i);
                Console.Write(lines[i].PadRight(100));
            }
        }

        private string FormatStatus(Players p, long now)
        {
            string hpBar = MakeBar(p.GetHealth(), p.GetMaxHealth(), 20);
            string meterBar = MakeBar(p.GetMeter(), p.GetMaxMeter(), 10);

            string guard = "";
            if (p.IsDefendingNow(now))
            {
                guard = " [GUARD]";
            }

            return $"{p.GetCharacter(),-6} ({p.GetClassType(),-8}) HP [{hpBar}] {p.GetHealth(),3}/{p.GetMaxHealth()}   {p.GetResourceType(),-6} [{meterBar}] {p.GetMeter(),2}/{p.GetMaxMeter()}{guard}";
        }

        private string FormatControls(Players p, string label, string basicKey, string specialKey, string defendKey, string healKey)
        {
            string line = $"{label}: [{basicKey}] Basic  [{specialKey}] Special  [{defendKey}] Defend";

            if (p.GetHealAmount() > 0)
            {
                line = line + $"  [{healKey}] Heal";
            }

            return line;
        }

        private string FormatSkills(Players p)
        {
            string line = $"        Basic: {p.GetBasicName()} ({p.GetBasicDamage()} dmg)   Special: {p.GetSpecialName()} ({p.GetSpecialDamage()} dmg, {p.GetSpecialCost()} {p.GetResourceType()})";

            if (p.GetHealAmount() > 0)
            {
                line = line + $"   Heal: {p.GetHealName()} ({p.GetHealAmount()} HP, {p.GetHealCost()} {p.GetResourceType()})";
            }

            return line;
        }

        private string MakeBar(int value, int max, int width)
        {
            int filled = (int)((double)value / max * width);
            if (filled < 0) filled = 0;
            if (filled > width) filled = width;
            return new string('#', filled) + new string('-', width - filled);
        }
    }
}