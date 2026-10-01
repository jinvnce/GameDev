using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Game
{
    public static class Roster
    {
        public static readonly Dictionary<string, string> Sprites = new Dictionary<string, string>
        {
            { "Ren",  "gab-pred-blue" },
            { "Rene", "bins-rene" }
        };

        public static string SpritePath(string name, string kind)
        {
            return Path.Combine(AppContext.BaseDirectory, "assets", "avatar", Sprites[name] + "-" + kind + ".png");
        }

        public static string SpecialPath(string name)
        {
            string p = SpritePath(name, "special");
            return System.IO.File.Exists(p) ? p : SpritePath(name, "basic");
        }
        public static string JumpPath(string name)
{
    string p = SpritePath(name, "jump");
    return System.IO.File.Exists(p) ? p : SpritePath(name, "idle");
}
    }

    public class MenuForm : Form
    {
        private bool vsComputer;
        private bool practice;
        private string p1Name, p2Name;
        private readonly string assets = Path.Combine(AppContext.BaseDirectory, "assets");
        private string bindAction;
        private int bindPlayer;
        private string settingsNote;  

        private const int VW = 800, VH = 600;
        private Action redraw;   
        private readonly System.Windows.Forms.Timer resizeTimer = new System.Windows.Forms.Timer { Interval = 120 };

        private float UI { get { return Math.Max(0.1f, Math.Min(ClientSize.Width / (float)VW, ClientSize.Height / (float)VH)); } }
        private int OffX { get { return (int)((ClientSize.Width - VW * UI) / 2); } }
        private int OffY { get { return (int)((ClientSize.Height - VH * UI) / 2); } }
        private int S(int v) { return (int)(v * UI); }
        private int SX(int v) { return OffX + (int)(v * UI); }
        private int SY(int v) { return OffY + (int)(v * UI); }

        public MenuForm()
        {
            Text = "Pinoy Brawler";
            ClientSize = new Size(800, 600);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = SizeFromClientSize(new Size(640, 480));
            DoubleBuffered = true;
            BackColor = Color.FromArgb(30, 30, 30);
            CenterToScreen();

            string menuDir = Path.Combine(assets, "menu");
            if (Directory.Exists(menuDir))
            {
                string bg = Directory.GetFiles(menuDir).FirstOrDefault(IsImage);
                if (bg != null)
                {
                    BackgroundImage = LoadImage(bg);
                    BackgroundImageLayout = ImageLayout.Stretch;
                }
            }

            resizeTimer.Tick += (s, e) =>
            {
                resizeTimer.Stop();
                if (redraw != null && WindowState != FormWindowState.Minimized && ClientSize.Width > 0 && ClientSize.Height > 0)
                    redraw();
            };

            ShowMainMenu();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            resizeTimer.Stop();
            resizeTimer.Start();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (selectScreen != null && selectScreen.HandleKey(keyData & Keys.KeyCode)) return true;
            if (bindAction == null) return base.ProcessCmdKey(ref msg, keyData);

            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.None || k == Keys.ShiftKey || k == Keys.ControlKey || k == Keys.Menu) return true;

            if (k != Keys.Escape)   
                Settings.Assign(bindPlayer, bindAction, k);

            bindAction = null;
            ShowControls();
            return true;
        }

        private void ShowMainMenu()
        {
            redraw = ShowMainMenu;
            Clear();
            AddTitle("PINOY BRAWLER", 120, 40);
            AddButton("Start Game", 280, ShowModeMenu);
            AddButton("Settings", 350, ShowOptions);
            AddButton("Quit", 420, () => Application.Exit());
        }

        private void ShowModeMenu()
        {
            redraw = ShowModeMenu;
            Clear();
            AddTitle("Choose Mode", 100, 30);
            AddButton("Player vs Player", 240, () => { vsComputer = false; practice = false; ShowSelect(1); });
            AddButton("Player vs Computer", 300, () => { vsComputer = true; practice = false; ShowSelect(1); });
            AddButton("Practice", 360, () => { vsComputer = false; practice = true; ShowSelect(1); });
            AddButton("Back", 440, ShowMainMenu);
        }

        private SelectScreen selectScreen;

        private void ShowSelect(int startPhase)
{
    redraw = null;   
    Clear();

    string mapDir = Path.Combine(assets, "map");
    string[] maps = Directory.Exists(mapDir)
        ? Directory.GetFiles(mapDir).Where(IsImage).OrderBy(p => p).ToArray()
        : new string[0];

    string badge = vsComputer ? "CPU" : practice ? "DUM" : "P2";
    string prompt = vsComputer ? "CHOOSE THE COMPUTER'S FIGHTER"
                  : practice ? "CHOOSE THE PRACTICE DUMMY"
                  : "P2: CHOOSE YOUR FIGHTER";

    string selBg = Path.Combine(assets, "select", "select-bg.png");

    selectScreen = new SelectScreen(Roster.Sprites.Keys.ToList(), maps, badge, prompt, startPhase, p1Name, p2Name, selBg)
    {
        Dock = DockStyle.Fill
    };
    selectScreen.BackRequested += ShowModeMenu;
    selectScreen.StartRequested += (p1, p2, map) => { p1Name = p1; p2Name = p2; StartGame(map); };
    Controls.Add(selectScreen);
}

        internal static Image LoadImage(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return null;
                using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var img = Image.FromStream(fs))
                    return new Bitmap(img);   
            }
            catch { return null; }           
        }

        private void StartGame(string mapPath)
        {
            var game = new MainGUI(p1Name, p2Name, mapPath, vsComputer, practice);
            game.FormClosed += (s, e) =>
            {
                if (game.EndState == FormWindowState.Normal) { WindowState = FormWindowState.Normal; ClientSize = game.EndClientSize; }
                else WindowState = game.EndState;

                switch (game.Result)
                {
                    case "Retry": StartGame(mapPath); break;
                    case "Character": Show(); ShowSelect(1); break;
                    case "Map": Show(); ShowSelect(3); break;
                    case "Menu": Show(); ShowMainMenu(); break;
                    default: Application.Exit(); break;
                }
            };
            game.StartPosition = FormStartPosition.Manual;
            game.Location = Location;
            if (WindowState == FormWindowState.Normal) game.ClientSize = ClientSize;
            game.WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Maximized : FormWindowState.Normal;
            Hide();
            game.Show();
        }

        private static bool IsImage(string path)
        {
            string ext = Path.GetExtension(path).ToLower();
            return ext == ".png" || ext == ".jpg" || ext == ".jpeg";
        }

        private void Clear()
        {
            selectScreen = null;
            foreach (Control c in Controls.Cast<Control>().ToList())
            {
                Controls.Remove(c);
                foreach (var pic in c.Controls.OfType<PictureBox>())
                {
                    pic.Image?.Dispose();
                    pic.Image = null;
                }
                c.Dispose();
            }
        }

        private void AddTitle(string text, int y, float size)
        {
            var label = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", size * UI, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width, (int)(size * 2.2f * UI)),
                Location = new Point(0, SY(y))
            };
            Controls.Add(label);
        }

        private void AddTextAt(string text, int x, int y, int w, float size)
        {
            var label = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", size * UI, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(S(w), (int)(size * 2.2f * UI)),
                Location = new Point(SX(x), SY(y))
            };
            Controls.Add(label);
        }

        private void AddButton(string text, int y, Action onClick)
        {
            AddButtonAt(text, (VW - 260) / 2, y, 260, 50, 14, onClick);
        }

        private void AddButtonAt(string text, int x, int y, int w, int h, float size, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", size * UI, FontStyle.Bold),
                Size = new Size(S(w), S(h)),
                Location = new Point(SX(x), SY(y)),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                TabStop = false
            };
            btn.Click += (s, e) => onClick();
            Controls.Add(btn);
        }


        public static class Settings
        {
            public static int RoundSeconds = 99; 
            public static int Rounds = 3;
            public static int AiLevel = 1;      
            
            public static readonly string[] Actions = { "Left", "Right", "Jump", "Attack", "Special", "Defend", "Heal" };
            public static readonly string[] ActionNames = { "Move Left", "Move Right", "Jump", "Attack", "Special", "Defend", "Heal" };

            public static readonly Dictionary<string, Keys> P1Keys = new Dictionary<string, Keys>();
            public static readonly Dictionary<string, Keys> P2Keys = new Dictionary<string, Keys>();

            static Settings() { ResetKeys(); Load(); }

            private static string FilePath
            {
                get
                {
                    string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PinoyBrawler");
                    return Path.Combine(dir, "settings.txt");
                }
            }

            public static bool Save()
            {
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    var lines = new List<string>
                    {
                        "RoundSeconds=" + RoundSeconds,
                        "Rounds=" + Rounds,
                        "AiLevel=" + AiLevel
                    };
                    foreach (string a in Actions) lines.Add("P1." + a + "=" + P1Keys[a]);
                    foreach (string a in Actions) lines.Add("P2." + a + "=" + P2Keys[a]);
                    System.IO.File.WriteAllLines(FilePath, lines);
                    return true;
                }
                catch { return false; }
            }

            private static void Load()
            {
                try
                {
                    if (!System.IO.File.Exists(FilePath)) return;
                    foreach (string line in System.IO.File.ReadAllLines(FilePath))
                    {
                        int eq = line.IndexOf('=');
                        if (eq < 0) continue;
                        string key = line.Substring(0, eq).Trim();
                        string val = line.Substring(eq + 1).Trim();
                        int n;

                        if (key == "RoundSeconds" && int.TryParse(val, out n) && (n == 30 || n == 60 || n == 99)) RoundSeconds = n;
                        else if (key == "Rounds" && int.TryParse(val, out n) && (n == 1 || n == 3 || n == 5)) Rounds = n;
                        else if (key == "AiLevel" && int.TryParse(val, out n) && n >= 0 && n <= 2) AiLevel = n;
                        else if (key.StartsWith("P1.") || key.StartsWith("P2."))
                        {
                            string action = key.Substring(3);
                            var map = key[1] == '1' ? P1Keys : P2Keys;
                            Keys k;
                            if (map.ContainsKey(action) && Enum.TryParse(val, out k) && k != Keys.None && k != Keys.Escape)
                                map[action] = k;
                        }
                    }
                }
                catch { }   // bad file -> keep the defaults
            }

            public static void ResetKeys()
            {
                P1Keys.Clear();
                P1Keys["Left"] = Keys.A;
                P1Keys["Right"] = Keys.D;
                P1Keys["Jump"] = Keys.W;
                P1Keys["Attack"] = Keys.C;
                P1Keys["Special"] = Keys.V;
                P1Keys["Defend"] = Keys.F;
                P1Keys["Heal"] = Keys.G;

                P2Keys.Clear();
                P2Keys["Left"] = Keys.Left;
                P2Keys["Right"] = Keys.Right;
                P2Keys["Jump"] = Keys.Up;
                P2Keys["Attack"] = Keys.J;
                P2Keys["Special"] = Keys.K;
                P2Keys["Defend"] = Keys.U;
                P2Keys["Heal"] = Keys.I;
            }

            // Sets a key. If another action already uses it, that action gets the old key (swap).
            public static void Assign(int player, string action, Keys k)
            {
                var mine = player == 1 ? P1Keys : P2Keys;
                Keys old = mine[action];
                foreach (var map in new[] { P1Keys, P2Keys })
                    foreach (string a in Actions)
                        if (map[a] == k && !(ReferenceEquals(map, mine) && a == action))
                            map[a] = old;
                mine[action] = k;
            }

            public static string KeyName(Keys k)
            {
                switch (k)
                {
                    case Keys.Left: return "\u2190";
                    case Keys.Right: return "\u2192";
                    case Keys.Up: return "\u2191";
                    case Keys.Down: return "\u2193";
                }
                string s = k.ToString();
                if (s.Length == 2 && s[0] == 'D' && char.IsDigit(s[1])) return s.Substring(1);
                return s;
            }

            // Short controls line for the in-game HUD
            public static string Guide(string tag, Dictionary<string, Keys> m)
            {
                return tag + ":  " + KeyName(m["Left"]) + "/" + KeyName(m["Right"]) + " Move   "
                     + KeyName(m["Jump"]) + " Jump   " + KeyName(m["Attack"]) + " Attack   "
                     + KeyName(m["Special"]) + " Special   " + KeyName(m["Defend"]) + " Defend   "
                     + KeyName(m["Heal"]) + " Heal";
            }
        }

        private void ShowOptions()
        {
            redraw = ShowOptions;
            bindAction = null;
            Clear();
            AddTitle("Settings", 60, 28);

            AddButton("Round Time: " + Settings.RoundSeconds + "s", 150, () =>
            {
                Settings.RoundSeconds = Settings.RoundSeconds == 30 ? 60
                                      : Settings.RoundSeconds == 60 ? 99 : 30;
                ShowOptions();
            });

            AddButton("Rounds: " + Settings.Rounds, 215, () =>
            {
                Settings.Rounds = Settings.Rounds == 1 ? 3
                                : Settings.Rounds == 3 ? 5 : 1;
                ShowOptions();
            });

            string[] levels = { "Easy", "Normal", "Hard" };
            AddButton("CPU Difficulty: " + levels[Settings.AiLevel], 280, () =>
            {
                Settings.AiLevel = (Settings.AiLevel + 1) % 3;
                ShowOptions();
            });

            AddButton("Controls", 345, ShowControls);

            AddButton("Save Settings", 410, () =>
            {
                settingsNote = Settings.Save() ? "Settings saved." : "Could not save settings.";
                ShowOptions();
            });

            AddButton("Back", 475, ShowMainMenu);

            if (settingsNote != null)
            {
                AddTextAt(settingsNote, 0, 540, VW, 11);
                settingsNote = null;
            }
        }

        private void ShowControls()
        {
            redraw = ShowControls;
            Clear();
            AddTitle("Controls", 15, 24);

            AddTextAt("PLAYER 1", 40, 75, 300, 12);
            AddTextAt("PLAYER 2", 460, 75, 300, 12);

            for (int i = 0; i < Settings.Actions.Length; i++)
            {
                string action = Settings.Actions[i];
                string name = Settings.ActionNames[i];
                int y = 110 + i * 44;

                AddBindButton(1, action, name, 40, y);
                AddBindButton(2, action, name, 460, y);
            }

            AddTextAt("Click a button, then press the new key. Esc cancels.", 0, 420, VW, 10);
            AddButton("Reset to Default", 455, () => { Settings.ResetKeys(); bindAction = null; ShowControls(); });
            AddButton("Back", 520, ShowOptions);
        }

        private void AddBindButton(int player, string action, string name, int x, int y)
        {
            var map = player == 1 ? Settings.P1Keys : Settings.P2Keys;
            bool waiting = bindAction == action && bindPlayer == player;
            string text = waiting ? name + ": press a key..." : name + ": " + Settings.KeyName(map[action]);

            AddButtonAt(text, x, y, 300, 38, 11, () =>
            {
                bindPlayer = player;
                bindAction = action;
                ShowControls();
            });
        }
    }
}