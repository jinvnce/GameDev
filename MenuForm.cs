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

        // Uses the special image if it exists, otherwise the basic attack image
        public static string SpecialPath(string name)
        {
            string p = SpritePath(name, "special");
            return System.IO.File.Exists(p) ? p : SpritePath(name, "basic");
        }
    }

    public class MenuForm : Form
    {
        private bool vsComputer;
        private string p1Name, p2Name;
        private readonly string assets = Path.Combine(AppContext.BaseDirectory, "assets");

        public MenuForm()
        {
            Text = "Pinoy Brawl";
            ClientSize = new Size(800, 600);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            DoubleBuffered = true;
            BackColor = Color.FromArgb(30, 30, 30);
            CenterToScreen();

            string menuDir = Path.Combine(assets, "menu");
            if (Directory.Exists(menuDir))
            {
                string bg = Directory.GetFiles(menuDir).FirstOrDefault(IsImage);
                if (bg != null)
                {
                    BackgroundImage = Image.FromFile(bg);
                    BackgroundImageLayout = ImageLayout.Stretch;
                }
            }

            ShowMainMenu();
        }

        private void ShowMainMenu()
        {
            Clear();
            AddTitle("PINOY BRAWL", 120, 40);
            AddButton("Start Game", 300, ShowModeMenu);
            AddButton("Quit", 370, () => Application.Exit());
        }

        private void ShowModeMenu()
        {
            Clear();
            AddTitle("Choose Mode", 100, 30);
            AddButton("Player vs Player", 260, () => { vsComputer = false; ShowCharacterSelect(1); });
            AddButton("Player vs Computer", 330, () => { vsComputer = true; ShowCharacterSelect(1); });
            AddButton("Back", 420, ShowMainMenu);
        }

        private void ShowCharacterSelect(int player)
        {
            Clear();
            string title = player == 1 ? "Player 1: Choose Character"
                         : vsComputer ? "Choose the Computer's Character"
                                      : "Player 2: Choose Character";
            AddTitle(title, 50, 24);

            var tiles = new List<Control>();
            foreach (string name in Roster.Sprites.Keys)
            {
                string n = name; 
                tiles.Add(MakeTile(Roster.SpritePath(n, "idle"), n, 180, 260, () =>
                {
                    if (player == 1) { p1Name = n; ShowCharacterSelect(2); }
                    else { p2Name = n; ShowMapSelect(); }
                }));
            }
            PlaceGrid(tiles, 130, 4);

            AddButton("Back", 520, () =>
            {
                if (player == 1) ShowModeMenu(); else ShowCharacterSelect(1);
            });
        }

        private void ShowMapSelect()
        {
            Clear();
            AddTitle("Choose Map", 50, 24);

            string mapDir = Path.Combine(assets, "map");
            string[] maps = Directory.Exists(mapDir)
                ? Directory.GetFiles(mapDir).Where(IsImage).ToArray()
                : new string[0];

            var tiles = new List<Control>();
            foreach (string path in maps)
            {
                string p = path;
                tiles.Add(MakeTile(p, Path.GetFileNameWithoutExtension(p), 220, 140, () => StartGame(p)));
            }
            PlaceGrid(tiles, 130, 3);

            if (maps.Length == 0)
                AddTitle("No maps found in assets\\map", 250, 16);

            AddButton("Back", 520, () => ShowCharacterSelect(2));
        }

        private void StartGame(string mapPath)
{
    var game = new MainGUI(p1Name, p2Name, mapPath, vsComputer);
    game.FormClosed += (s, e) =>
    {
        switch (game.Result)
        {
            case "Retry":     StartGame(mapPath); break;
            case "Character": Show(); ShowCharacterSelect(1); break;
            case "Map":       Show(); ShowMapSelect(); break;
            default:          Application.Exit(); break;
        }
    };
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
            foreach (Control c in Controls.Cast<Control>().ToList())
            {
                Controls.Remove(c);
                c.Dispose();
            }
        }

        private void AddTitle(string text, int y, float size)
        {
            var label = new Label
            {
                Text = text,
                Font = new Font("Segoe UI", size, FontStyle.Bold),
                ForeColor = Color.White,
                BackColor = Color.Transparent,
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Size = new Size(ClientSize.Width, (int)(size * 2.2f)),
                Location = new Point(0, y)
            };
            Controls.Add(label);
        }

        private void AddButton(string text, int y, Action onClick)
        {
            var btn = new Button
            {
                Text = text,
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                Size = new Size(260, 50),
                Location = new Point((ClientSize.Width - 260) / 2, y),
                BackColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                TabStop = false
            };
            btn.Click += (s, e) => onClick();
            Controls.Add(btn);
        }

        private Control MakeTile(string imagePath, string caption, int w, int h, Action onClick)
        {
            var tile = new Panel
            {
                Size = new Size(w, h + 30),
                BackColor = Color.FromArgb(160, 0, 0, 0),
                Cursor = Cursors.Hand
            };

            var pic = new PictureBox
            {
                Dock = DockStyle.Top,
                Height = h,
                SizeMode = PictureBoxSizeMode.Zoom
            };
            if (System.IO.File.Exists(imagePath)) pic.Image = Image.FromFile(imagePath);

            var label = new Label
            {
                Text = caption,
                Dock = DockStyle.Bottom,
                Height = 30,
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 11, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter
            };

            tile.Controls.Add(pic);
            tile.Controls.Add(label);
            tile.Click += (s, e) => onClick();
            pic.Click += (s, e) => onClick();
            label.Click += (s, e) => onClick();
            return tile;
        }

        private void PlaceGrid(List<Control> tiles, int top, int perRow)
        {
            const int gap = 20;
            for (int i = 0; i < tiles.Count; i++)
            {
                int row = i / perRow;
                int indexInRow = i % perRow;
                int countInRow = Math.Min(perRow, tiles.Count - row * perRow);
                int rowWidth = countInRow * tiles[i].Width + (countInRow - 1) * gap;
                int startX = (ClientSize.Width - rowWidth) / 2;

                tiles[i].Location = new Point(startX + indexInRow * (tiles[i].Width + gap),
                                              top + row * (tiles[i].Height + gap));
                Controls.Add(tiles[i]);
            }
        }
    }
}