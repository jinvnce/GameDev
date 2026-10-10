using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace Game
{
    public static class Roster
    {
        public static readonly Dictionary<string, string> Sprites = new Dictionary<string, string>
        {
            { "Coach",  "gab-pred-blue" },
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

    public class OutlineLabel : Label
    {
        public Color OutlineColor { get; set; } = Color.FromArgb(15, 15, 15);
        public int OutlineWidth { get; set; } = 6;

        public OutlineLabel()
        {
            SetStyle(ControlStyles.SupportsTransparentBackColor |
                     ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.Transparent;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            using (var path = new GraphicsPath())
            using (var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center
            })
            {
                float emSize = g.DpiY * Font.SizeInPoints / 72f;
                path.AddString(Text, Font.FontFamily, (int)Font.Style, emSize, ClientRectangle, sf);

                using (var pen = new Pen(OutlineColor, OutlineWidth) { LineJoin = LineJoin.Round })
                    g.DrawPath(pen, path);
                using (var fill = new SolidBrush(ForeColor))
                    g.FillPath(fill, path);
            }
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
        private const int BgDarkness = 130;   
        private Action redraw;

        private readonly List<GameButton> navButtons = new List<GameButton>();
        private readonly List<Action> navActions = new List<Action>();
        private int navIndex;        
        private int gridCount;       // buttons laid out in 2 columns at the start of the list (Controls screen)
        private Action lastScreen;   // used to reset the selection when the screen changes
        private Action goBack;       // what Esc does on the current screen
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
            ClientSize = new Size(1200, 900);
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = true;
            MinimumSize = SizeFromClientSize(new Size(640, 480));
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            CenterToScreen();

            string menuDir = Path.Combine(assets, "menu");
            if (Directory.Exists(menuDir))
            {
                string bg = Directory.GetFiles(menuDir).FirstOrDefault(IsImage);
                if (bg != null)
                {
                    BackgroundImage = Darken(LoadImage(bg), BgDarkness);
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

        // Bakes a dark overlay into the background image once, so text on top stays readable
        private static Image Darken(Image src, int alpha)
        {
            if (src == null) return null;
            var bmp = new Bitmap(src.Width, src.Height);
            using (var g = Graphics.FromImage(bmp))
            {
                g.DrawImage(src, 0, 0, src.Width, src.Height);
                using (var tint = new SolidBrush(Color.FromArgb(alpha, 0, 0, 0)))
                    g.FillRectangle(tint, 0, 0, bmp.Width, bmp.Height);
            }
            src.Dispose();
            return bmp;
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            resizeTimer.Stop();
            resizeTimer.Start();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (selectScreen != null && selectScreen.HandleKey(keyData & Keys.KeyCode, (keyData & Keys.Shift) == Keys.Shift)) return true;
            if (bindAction == null)
                return HandleMenuKey(keyData) || base.ProcessCmdKey(ref msg, keyData);

            Keys k = keyData & Keys.KeyCode;
            if (k == Keys.None || k == Keys.ShiftKey || k == Keys.ControlKey || k == Keys.Menu) return true;

            if (k != Keys.Escape)
                Settings.Assign(bindPlayer, bindAction, k);

            bindAction = null;
            ShowControls();
            return true;
        }

        // Tab / Shift+Tab / arrows to move, Enter or Space to press, Esc to go back
        private bool HandleMenuKey(Keys keyData)
        {
            if (selectScreen != null || navButtons.Count == 0) return false;

            Keys k = keyData & Keys.KeyCode;
            bool shift = (keyData & Keys.Shift) == Keys.Shift;

            switch (k)
            {
                case Keys.Tab:   MoveNav(shift ? -1 : 1, false); return true;
                case Keys.Right: MoveNav(1, false); return true;
                case Keys.Left:  MoveNav(-1, false); return true;
                case Keys.Down:  MoveNav(1, true); return true;
                case Keys.Up:    MoveNav(-1, true); return true;
                case Keys.Enter:
                case Keys.Space:
                    navActions[navIndex]();
                    return true;
                case Keys.Escape:
                    if (goBack == null) return false;
                    goBack();
                    return true;
            }
            return false;
        }

        private void MoveNav(int d, bool vertical)
        {
            int n = navButtons.Count;
            int t;

            if (vertical && navIndex < gridCount)
            {
                t = navIndex + 2 * d;               // up/down = same column, next row
                if (t >= gridCount) t = gridCount;  // below the last row -> first button under the grid
                else if (t < 0) t = n - 1;          // above the first row -> last button
            }
            else t = navIndex + d;

            t = ((t % n) + n) % n;                  // wrap around
            navIndex = t;
            for (int i = 0; i < n; i++) navButtons[i].Active = (i == navIndex);
        }

        // ---------- "are you sure?" screen ----------

        // Shows a confirmation screen. "No" (and Esc) return to `back`, with the same button selected as before.
        private void AskConfirm(string message, string yesText, Action onYes, Action back)
        {
            BuildConfirm(message, yesText, onYes, back, navIndex);
        }

        private void BuildConfirm(string message, string yesText, Action onYes, Action back, int keep)
        {
            Action cancel = () => { back(); SelectNav(keep); };
            redraw = () => BuildConfirm(message, yesText, onYes, back, keep);   // survives window resizes
            goBack = cancel;
            Clear();

            AddTitle("Are you sure?", 130, 28);
            AddMessage(message, 220, 90, 14);
            AddButton("No, go back", 340, cancel);   // first = selected by default, the safe choice
            AddButton(yesText, 405, onYes);
        }

        private void SelectNav(int i)
        {
            if (navButtons.Count == 0) return;
            navIndex = Math.Max(0, Math.Min(i, navButtons.Count - 1));
            for (int k = 0; k < navButtons.Count; k++) navButtons[k].Active = (k == navIndex);
        }

        // multi-line, outlined text block (wraps inside its box)
        private void AddMessage(string text, int y, int h, float size)
        {
            var label = new OutlineLabel
            {
                Text = text,
                Font = Theme.Body(size * UI, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 235, 235),
                OutlineWidth = Math.Max(2, S(3)),
                AutoSize = false,
                Size = new Size(S(600), S(h)),
                Location = new Point(SX(100), SY(y))
            };
            Controls.Add(label);
        }

        private void ShowMainMenu()
        {
            redraw = ShowMainMenu;
            goBack = null;
            Clear();
            AddTitle("PINOY BRAWLER", 120, 40);
            AddButton("Start Game", 280, ShowModeMenu);
            AddButton("Settings", 350, ShowOptions);
            AddButton("Quit", 420, () => AskConfirm(
                "Are you sure you want to exit the game?",
                "Yes, exit game",
                () => Application.Exit(),
                ShowMainMenu));
        }

        private void ShowModeMenu()
        {
            redraw = ShowModeMenu;
            goBack = ShowMainMenu;
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
            navButtons.Clear();
            navActions.Clear();
            gridCount = 0;
            if (redraw != lastScreen) { navIndex = 0; lastScreen = redraw; }   // new screen -> start at the top
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
            var label = new OutlineLabel
            {
                Text = text,
                Font = Theme.Display(size * UI),
                ForeColor = Theme.Text,
                OutlineWidth = Math.Max(2, S(6)),
                AutoSize = false,
                Size = new Size(ClientSize.Width, (int)(size * 2.2f * UI)),
                Location = new Point(0, SY(y))
            };
            Controls.Add(label);
        }

        private void AddTextAt(string text, int x, int y, int w, float size)
        {
            var label = new OutlineLabel
            {
                Text = text,
                Font = Theme.Body(size * UI, FontStyle.Bold),
                ForeColor = Color.FromArgb(235, 235, 235),   // brighter than Theme.Muted so it reads on the background
                OutlineWidth = Math.Max(2, S(3)),
                AutoSize = false,
                Size = new Size(S(w), (int)(size * 2.2f * UI)),
                Location = new Point(SX(x), SY(y))
            };
            Controls.Add(label);
        }

        private void AddButton(string text, int y, Action onClick)
        {
            AddButtonAt(text, (VW - 260) / 2, y, 260, 50, 14, onClick);
        }

        private GameButton AddButtonAt(string text, int x, int y, int w, int h, float size, Action onClick)
        {
            var btn = new GameButton
            {
                Text = text,
                Font = Theme.Display(size * UI),
                Size = new Size(S(w), S(h)),
                Location = new Point(SX(x), SY(y))
            };
            int idx = navButtons.Count;
            navButtons.Add(btn);
            navActions.Add(onClick);
            btn.Active = bindAction == null && idx == navIndex;
            btn.Click += (s, e) => { navIndex = idx; onClick(); };   // mouse clicks move the selection too
            Controls.Add(btn);
            return btn;
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
            goBack = ShowMainMenu;
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

            AddButton("Save Settings", 410, () => AskConfirm(
                "Save your current settings? They will be kept the next time you play.",
                "Yes, save",
                () =>
                {
                    settingsNote = Settings.Save() ? "Settings saved." : "Could not save settings.";
                    ShowOptions();
                },
                ShowOptions));

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
            goBack = ShowOptions;
            Clear();
            gridCount = Settings.Actions.Length * 2;   // P1/P2 binding buttons come first, two per row
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
            AddButton("Reset to Default", 455, () => AskConfirm(
                "Reset all controls to their default keys? This cannot be undone.",
                "Yes, reset controls",
                () => { Settings.ResetKeys(); bindAction = null; ShowControls(); },
                ShowControls));
            AddButton("Back", 520, ShowOptions);
        }

        private void AddBindButton(int player, string action, string name, int x, int y)
        {
            var map = player == 1 ? Settings.P1Keys : Settings.P2Keys;
            bool waiting = bindAction == action && bindPlayer == player;
            string text = waiting ? name + ": press a key..." : name + ": " + Settings.KeyName(map[action]);

            var b = AddButtonAt(text, x, y, 300, 38, 11, () =>
            {
                bindPlayer = player;
                bindAction = action;
                ShowControls();
            });
            b.Active = bindAction != null ? waiting : navButtons.IndexOf(b) == navIndex;
        }
    }
}