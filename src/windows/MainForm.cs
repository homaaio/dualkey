// src/windows/MainForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX.XInput;

namespace DualKey
{
    public partial class MainForm : Form
    {
        private Controller controller;
        private JoystickEmulator emulator;
        private JoystickHider hider;
        private WebServer webServer;
        private Timer updateTimer;
        private Timer indicatorTimer;

        // UI элементы
        private MenuStrip menuStrip;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;
        private ToolStripStatusLabel connectionLabel;
        private Panel keyboardPanel;
        private KeyboardVisualizer keyboardView;
        private GroupBox groupEmulation;
        private CheckBox chkEmulation;
        private Label lblDeadzone;
        private TrackBar trackDeadzone;
        private Label lblDeadzoneValue;
        private GroupBox groupController;
        private Button btnHide;
        private Button btnWeb;

        // Индикаторы игроков
        private Panel playerIndicatorPanel;
        private Label[] playerIndicators;
        private int currentPlayer = 1;
        private Dictionary<int, Dictionary<string, int>> playerBindings;

        // Настройки индикаторов
        private bool indicatorsEnabled = true;
        private int indicatorMode = 0;
        private int indicatorSpeed = 500;
        private int indicatorStep = 0;
        private bool indicatorBlinkState = false;
        private Color[] indicatorColors = new Color[] { Color.Red, Color.Red, Color.Red, Color.Red };

        // Состояние джойстика
        private float leftX, leftY, rightX, rightY;
        private bool connected;
        private HashSet<int> activeKeyCodes = new HashSet<int>();

        private static readonly string LogFile = "dualkey.log";

        public MainForm()
        {
            emulator = new JoystickEmulator();
            hider = new JoystickHider();
            playerBindings = new Dictionary<int, Dictionary<string, int>>();

            for (int i = 1; i <= 4; i++)
            {
                playerBindings[i] = new Dictionary<string, int>(emulator.Bindings);
            }

            Log("Application starting...");

            Task.Run(async () =>
            {
                webServer = new WebServer(GetJsonData);
                await webServer.StartAsync();
            });

            InitializeComponent();
            Log("UI initialized.");

            controller = new Controller(UserIndex.One);

            updateTimer = new Timer();
            updateTimer.Interval = 16;
            updateTimer.Tick += UpdateJoystickState;
            updateTimer.Start();

            indicatorTimer = new Timer();
            indicatorTimer.Interval = indicatorSpeed;
            indicatorTimer.Tick += UpdateIndicators;
            indicatorTimer.Start();
        }

        private static void Log(string message)
        {
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";
            try { File.AppendAllText(LogFile, logEntry + Environment.NewLine); } catch { }
            System.Diagnostics.Debug.WriteLine(logEntry);
        }

        private void InitializeComponent()
        {
            this.Text = "DualKey";
            this.Size = new Size(920, 640);
            this.MinimumSize = new Size(800, 500);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Меню
            menuStrip = new MenuStrip();

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add("Save configuration (.hrc)", null, OnSaveConfig);
            fileMenu.DropDownItems.Add("Import configuration (.hrc)", null, OnLoadConfig);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Exit", null, (s, e) => Application.Exit());

            ToolStripMenuItem settingsMenu = new ToolStripMenuItem("Settings");
            settingsMenu.DropDownItems.Add("Open settings", null, OnOpenSettings);
            settingsMenu.DropDownItems.Add("Clear all settings", null, OnClearSettings);

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(settingsMenu);
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);

            // Индикаторы игроков
            CreatePlayerIndicators();

            // Статусная строка
            statusStrip = new StatusStrip();
            connectionLabel = new ToolStripStatusLabel("Not connected");
            connectionLabel.ForeColor = Color.Red;
            statusLabel = new ToolStripStatusLabel("Web: http://localhost:8080");
            statusStrip.Items.Add(connectionLabel);
            statusStrip.Items.Add(statusLabel);
            this.Controls.Add(statusStrip);

            // Панель клавиатуры
            keyboardPanel = new Panel
            {
                Location = new Point(12, playerIndicatorPanel.Bottom + 5),
                Size = new Size(this.ClientSize.Width - 24, this.ClientSize.Height - playerIndicatorPanel.Height - statusStrip.Height - 130),
                BorderStyle = BorderStyle.FixedSingle,
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
            };

            keyboardView = new KeyboardVisualizer
            {
                Dock = DockStyle.Fill
            };
            keyboardPanel.Controls.Add(keyboardView);
            this.Controls.Add(keyboardPanel);

            // Группа эмуляции
            groupEmulation = new GroupBox
            {
                Text = "Emulation",
                Location = new Point(12, keyboardPanel.Bottom + 5),
                Size = new Size(280, 90),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            chkEmulation = new CheckBox
            {
                Text = "Enable keyboard emulation",
                Location = new Point(10, 20),
                Size = new Size(200, 20)
            };
            chkEmulation.CheckedChanged += (s, e) =>
            {
                emulator.Enabled = chkEmulation.Checked;
                if (!chkEmulation.Checked) emulator.ReleaseAll();
            };

            lblDeadzone = new Label
            {
                Text = "Deadzone:",
                Location = new Point(10, 45),
                Size = new Size(60, 20)
            };

            trackDeadzone = new TrackBar
            {
                Minimum = 0,
                Maximum = 50,
                Value = 15,
                Location = new Point(70, 42),
                Size = new Size(150, 30),
                TickFrequency = 10
            };
            trackDeadzone.ValueChanged += (s, e) =>
            {
                emulator.Deadzone = trackDeadzone.Value / 50f;
                lblDeadzoneValue.Text = $"{emulator.Deadzone:F2}";
            };

            lblDeadzoneValue = new Label
            {
                Text = "0.30",
                Location = new Point(225, 45),
                Size = new Size(40, 20)
            };

            groupEmulation.Controls.Add(chkEmulation);
            groupEmulation.Controls.Add(lblDeadzone);
            groupEmulation.Controls.Add(trackDeadzone);
            groupEmulation.Controls.Add(lblDeadzoneValue);
            this.Controls.Add(groupEmulation);

            // Группа контроллера
            groupController = new GroupBox
            {
                Text = "Controller",
                Location = new Point(302, keyboardPanel.Bottom + 5),
                Size = new Size(280, 90),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };

            btnHide = new Button
            {
                Text = "Hide Controller",
                Location = new Point(10, 20),
                Size = new Size(120, 30)
            };
            btnHide.Click += (s, e) =>
            {
                if (!hider.IsHidden)
                {
                    if (hider.HideJoystick())
                    {
                        btnHide.Text = "Show Controller";
                        Log("Controller hidden.");
                    }
                    else
                    {
                        MessageBox.Show("Failed to hide controller. Run as Administrator.", "DualKey");
                        Log("Hide failed.");
                    }
                }
                else
                {
                    if (hider.ShowJoystick())
                    {
                        btnHide.Text = "Hide Controller";
                        Log("Controller shown.");
                    }
                }
            };

            btnWeb = new Button
            {
                Text = "Web Interface",
                Location = new Point(140, 20),
                Size = new Size(120, 30)
            };
            btnWeb.Click += (s, e) =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "http://localhost:8080",
                    UseShellExecute = true
                });

            groupController.Controls.Add(btnHide);
            groupController.Controls.Add(btnWeb);
            this.Controls.Add(groupController);
        }

        private void CreatePlayerIndicators()
        {
            playerIndicatorPanel = new Panel
            {
                Location = new Point(12, menuStrip.Bottom + 3),
                Size = new Size(this.ClientSize.Width - 24, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            Label lblPlayers = new Label
            {
                Text = "Player:",
                Location = new Point(0, 5),
                Size = new Size(45, 20)
            };
            playerIndicatorPanel.Controls.Add(lblPlayers);

            playerIndicators = new Label[4];
            for (int i = 0; i < 4; i++)
            {
                int player = i + 1;
                Label indicator = new Label
                {
                    Text = player.ToString(),
                    Location = new Point(50 + i * 35, 3),
                    Size = new Size(28, 24),
                    TextAlign = ContentAlignment.MiddleCenter,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = (player == currentPlayer) ? Color.LimeGreen : indicatorColors[i],
                    ForeColor = Color.White,
                    Cursor = Cursors.Hand,
                    Tag = player
                };
                indicator.Click += (s, e) =>
                {
                    Label clicked = (Label)s;
                    int playerNum = (int)clicked.Tag;
                    SwitchPlayer(playerNum);
                };
                playerIndicators[i] = indicator;
                playerIndicatorPanel.Controls.Add(indicator);
            }

            this.Controls.Add(playerIndicatorPanel);
        }

        private void SwitchPlayer(int player)
        {
            currentPlayer = player;

            for (int i = 0; i < 4; i++)
            {
                if (i + 1 == player)
                {
                    playerIndicators[i].BackColor = Color.LimeGreen;
                    playerIndicators[i].ForeColor = Color.Black;
                }
                else
                {
                    playerIndicators[i].BackColor = indicatorColors[i];
                    playerIndicators[i].ForeColor = Color.White;
                }
            }

            if (playerBindings.ContainsKey(player))
            {
                emulator.Bindings = new Dictionary<string, int>(playerBindings[player]);
            }

            Log($"Switched to player {player}");
        }

        private void UpdateIndicators(object sender, EventArgs e)
        {
            if (!indicatorsEnabled)
            {
                for (int i = 0; i < 4; i++)
                {
                    if (i + 1 != currentPlayer)
                        playerIndicators[i].BackColor = Color.DarkGray;
                }
                return;
            }

            for (int i = 0; i < 4; i++)
            {
                if (i + 1 == currentPlayer) continue;
            }

            switch (indicatorMode)
            {
                case 0: // Static
                    for (int i = 0; i < 4; i++)
                    {
                        if (i + 1 != currentPlayer)
                            playerIndicators[i].BackColor = indicatorColors[i];
                    }
                    break;

                case 1: // Blink All
                    indicatorBlinkState = !indicatorBlinkState;
                    for (int i = 0; i < 4; i++)
                    {
                        if (i + 1 != currentPlayer)
                            playerIndicators[i].BackColor = indicatorBlinkState ? indicatorColors[i] : Color.DarkGray;
                    }
                    break;

                case 2: // Running Light
                    for (int i = 0; i < 4; i++)
                    {
                        if (i + 1 != currentPlayer)
                            playerIndicators[i].BackColor = (i == indicatorStep) ? indicatorColors[i] : Color.DarkGray;
                    }
                    indicatorStep = (indicatorStep + 1) % 4;
                    break;

                case 3: // Alternating
                    indicatorBlinkState = !indicatorBlinkState;
                    for (int i = 0; i < 4; i++)
                    {
                        if (i + 1 == currentPlayer) continue;
                        if (i % 2 == 0)
                            playerIndicators[i].BackColor = indicatorBlinkState ? indicatorColors[i] : Color.DarkGray;
                        else
                            playerIndicators[i].BackColor = !indicatorBlinkState ? indicatorColors[i] : Color.DarkGray;
                    }
                    break;
            }
        }

        private void UpdateJoystickState(object sender, EventArgs e)
        {
            try
            {
                if (!controller.IsConnected)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        var testController = new Controller((UserIndex)i);
                        if (testController.IsConnected)
                        {
                            controller = testController;
                            break;
                        }
                    }
                }

                if (!controller.IsConnected)
                {
                    connected = false;
                    connectionLabel.Text = "Not connected";
                    connectionLabel.ForeColor = Color.Red;
                    leftX = leftY = rightX = rightY = 0;
                    activeKeyCodes.Clear();
                    keyboardView.SetActiveKeys(activeKeyCodes);
                    keyboardView.Invalidate();
                    return;
                }

                connected = true;
                connectionLabel.Text = "Connected";
                connectionLabel.ForeColor = Color.Green;

                State state = controller.GetState();
                Gamepad gamepad = state.Gamepad;

                leftX = gamepad.LeftThumbX / 32768f;
                leftY = gamepad.LeftThumbY / 32768f;
                rightX = gamepad.RightThumbX / 32768f;
                rightY = gamepad.RightThumbY / 32768f;

                float dz = emulator.Deadzone;
                activeKeyCodes.Clear();

                AddStickKeys("left_stick_left", "left_stick_right", leftX, dz);
                AddStickKeys("left_stick_up", "left_stick_down", leftY, dz);
                AddStickKeys("right_stick_left", "right_stick_right", rightX, dz);
                AddStickKeys("right_stick_up", "right_stick_down", rightY, dz);

                GamepadButtonFlags buttons = gamepad.Buttons;
                var buttonMap = new (GamepadButtonFlags flag, string action)[]
                {
                    (GamepadButtonFlags.A, "cross"),
                    (GamepadButtonFlags.B, "circle"),
                    (GamepadButtonFlags.X, "triangle"),
                    (GamepadButtonFlags.Y, "square"),
                    (GamepadButtonFlags.LeftShoulder, "l1"),
                    (GamepadButtonFlags.RightShoulder, "r1"),
                    (GamepadButtonFlags.LeftThumb, "l3"),
                    (GamepadButtonFlags.RightThumb, "r3"),
                    (GamepadButtonFlags.Start, "start"),
                    (GamepadButtonFlags.Back, "select"),
                };

                foreach (var (flag, action) in buttonMap)
                {
                    if ((buttons & flag) != 0 && emulator.Bindings.ContainsKey(action))
                        activeKeyCodes.Add(emulator.Bindings[action]);
                }

                if (gamepad.LeftTrigger > 128 && emulator.Bindings.ContainsKey("l2"))
                    activeKeyCodes.Add(emulator.Bindings["l2"]);
                if (gamepad.RightTrigger > 128 && emulator.Bindings.ContainsKey("r2"))
                    activeKeyCodes.Add(emulator.Bindings["r2"]);

                if ((buttons & GamepadButtonFlags.DPadUp) != 0 && emulator.Bindings.ContainsKey("dpad_up"))
                    activeKeyCodes.Add(emulator.Bindings["dpad_up"]);
                if ((buttons & GamepadButtonFlags.DPadDown) != 0 && emulator.Bindings.ContainsKey("dpad_down"))
                    activeKeyCodes.Add(emulator.Bindings["dpad_down"]);
                if ((buttons & GamepadButtonFlags.DPadLeft) != 0 && emulator.Bindings.ContainsKey("dpad_left"))
                    activeKeyCodes.Add(emulator.Bindings["dpad_left"]);
                if ((buttons & GamepadButtonFlags.DPadRight) != 0 && emulator.Bindings.ContainsKey("dpad_right"))
                    activeKeyCodes.Add(emulator.Bindings["dpad_right"]);

                if (emulator.Enabled)
                {
                    ProcessStickEmulation("left_stick_left", "left_stick_right", leftX, dz);
                    ProcessStickEmulation("left_stick_up", "left_stick_down", leftY, dz);
                    ProcessStickEmulation("right_stick_left", "right_stick_right", rightX, dz);
                    ProcessStickEmulation("right_stick_up", "right_stick_down", rightY, dz);

                    foreach (var (flag, action) in buttonMap)
                    {
                        if ((buttons & flag) != 0) emulator.PressKey(action);
                        else emulator.ReleaseKey(action);
                    }

                    EmulateTrigger("l2", gamepad.LeftTrigger > 128);
                    EmulateTrigger("r2", gamepad.RightTrigger > 128);

                    EmulateDpad("dpad_up", (buttons & GamepadButtonFlags.DPadUp) != 0);
                    EmulateDpad("dpad_down", (buttons & GamepadButtonFlags.DPadDown) != 0);
                    EmulateDpad("dpad_left", (buttons & GamepadButtonFlags.DPadLeft) != 0);
                    EmulateDpad("dpad_right", (buttons & GamepadButtonFlags.DPadRight) != 0);
                }

                keyboardView.SetActiveKeys(activeKeyCodes);
                keyboardView.Invalidate();
            }
            catch (Exception ex)
            {
                Log($"Error: {ex.Message}");
                connected = false;
            }
        }

        private void AddStickKeys(string negAction, string posAction, float value, float deadzone)
        {
            if (value < -deadzone && emulator.Bindings.ContainsKey(negAction))
                activeKeyCodes.Add(emulator.Bindings[negAction]);
            else if (value > deadzone && emulator.Bindings.ContainsKey(posAction))
                activeKeyCodes.Add(emulator.Bindings[posAction]);
        }

        private void ProcessStickEmulation(string negAction, string posAction, float value, float deadzone)
        {
            if (value < -deadzone) { emulator.PressKey(negAction); emulator.ReleaseKey(posAction); }
            else if (value > deadzone) { emulator.PressKey(posAction); emulator.ReleaseKey(negAction); }
            else { emulator.ReleaseKey(negAction); emulator.ReleaseKey(posAction); }
        }

        private void EmulateTrigger(string action, bool pressed)
        {
            if (pressed) emulator.PressKey(action);
            else emulator.ReleaseKey(action);
        }

        private void EmulateDpad(string action, bool pressed)
        {
            if (pressed) emulator.PressKey(action);
            else emulator.ReleaseKey(action);
        }

        private string GetJsonData() =>
            $"{{\"connected\":{connected.ToString().ToLower()},\"leftStick\":{{\"x\":{leftX:F2},\"y\":{leftY:F2}}},\"rightStick\":{{\"x\":{rightX:F2},\"y\":{rightY:F2}}}}}";

        private void OnSaveConfig(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog
            {
                Filter = "DualKey Config (*.hrc)|*.hrc",
                FileName = "dualkey_config.hrc"
            };
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                var config = new Dictionary<string, object>
                {
                    ["deadzone"] = emulator.Deadzone,
                    ["currentPlayer"] = currentPlayer,
                    ["playerBindings"] = playerBindings,
                    ["indicatorsEnabled"] = indicatorsEnabled,
                    ["indicatorMode"] = indicatorMode,
                    ["indicatorSpeed"] = indicatorSpeed,
                    ["indicatorColors"] = new int[] { indicatorColors[0].ToArgb(), indicatorColors[1].ToArgb(), indicatorColors[2].ToArgb(), indicatorColors[3].ToArgb() }
                };
                string json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(sfd.FileName, json);
                Log($"Saved config to {sfd.FileName}");
            }
        }

        private void OnLoadConfig(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Filter = "DualKey Config (*.hrc)|*.hrc"
            };
            if (ofd.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    string json = File.ReadAllText(ofd.FileName);
                    var config = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
                    if (config != null)
                    {
                        if (config.ContainsKey("deadzone"))
                        {
                            float dz = config["deadzone"].GetSingle();
                            emulator.Deadzone = dz;
                            trackDeadzone.Value = (int)(dz * 50);
                            lblDeadzoneValue.Text = dz.ToString("F2");
                        }
                        if (config.ContainsKey("currentPlayer"))
                        {
                            int player = config["currentPlayer"].GetInt32();
                            SwitchPlayer(player);
                        }
                        if (config.ContainsKey("playerBindings"))
                        {
                            var bindingsJson = config["playerBindings"].GetRawText();
                            playerBindings = JsonSerializer.Deserialize<Dictionary<int, Dictionary<string, int>>>(bindingsJson);
                            if (playerBindings.ContainsKey(currentPlayer))
                                emulator.Bindings = playerBindings[currentPlayer];
                        }
                        if (config.ContainsKey("indicatorsEnabled"))
                            indicatorsEnabled = config["indicatorsEnabled"].GetBoolean();
                        if (config.ContainsKey("indicatorMode"))
                            indicatorMode = config["indicatorMode"].GetInt32();
                        if (config.ContainsKey("indicatorSpeed"))
                        {
                            indicatorSpeed = config["indicatorSpeed"].GetInt32();
                            indicatorTimer.Interval = indicatorSpeed;
                        }
                        if (config.ContainsKey("indicatorColors"))
                        {
                            var colors = config["indicatorColors"].Deserialize<int[]>();
                            for (int i = 0; i < 4; i++)
                                indicatorColors[i] = Color.FromArgb(colors[i]);
                        }
                        Log($"Loaded config from {ofd.FileName}");
                    }
                }
                catch (Exception ex)
                {
                    Log($"Error loading config: {ex.Message}");
                    MessageBox.Show($"Error: {ex.Message}", "DualKey");
                }
            }
        }

        private void OnOpenSettings(object sender, EventArgs e)
        {
            using (var sf = new SettingsForm(emulator))
            {
                if (sf.ShowDialog(this) == DialogResult.OK)
                {
                    playerBindings[currentPlayer] = new Dictionary<string, int>(emulator.Bindings);
                    indicatorsEnabled = sf.IndicatorsEnabled;
                    indicatorMode = sf.IndicatorMode;
                    indicatorSpeed = sf.IndicatorSpeed;
                    indicatorColors = sf.IndicatorColors;
                    indicatorTimer.Interval = indicatorSpeed;
                    Log("Settings updated.");
                }
            }
        }

        private void OnClearSettings(object sender, EventArgs e)
        {
            if (MessageBox.Show("Reset all settings to defaults?", "DualKey", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                emulator.ResetBindings();
                emulator.Deadzone = 0.3f;
                trackDeadzone.Value = (int)(emulator.Deadzone * 50);
                lblDeadzoneValue.Text = emulator.Deadzone.ToString("F2");
                currentPlayer = 1;
                SwitchPlayer(1);
                for (int i = 1; i <= 4; i++)
                    playerBindings[i] = new Dictionary<string, int>(emulator.Bindings);
                indicatorsEnabled = true;
                indicatorMode = 0;
                indicatorSpeed = 500;
                indicatorColors = new Color[] { Color.Red, Color.Red, Color.Red, Color.Red };
                indicatorTimer.Interval = 500;
                Log("Settings cleared.");
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            updateTimer?.Stop();
            indicatorTimer?.Stop();
            emulator?.ReleaseAll();
            webServer?.Stop();
            Log("Application closed.");
            base.OnFormClosing(e);
        }
    }

    public class KeyboardVisualizer : Panel
    {
        private HashSet<int> activeKeys = new HashSet<int>();
        private Dictionary<int, Rectangle> keyRects = new Dictionary<int, Rectangle>();
        private Dictionary<int, string> keyLabels = new Dictionary<int, string>();
        private readonly Font keyFont = new Font("Microsoft Sans Serif", 8, FontStyle.Bold);

        public KeyboardVisualizer()
        {
            this.DoubleBuffered = true;
            InitializeKeyboardLayout();
        }

        public void SetActiveKeys(HashSet<int> keys)
        {
            activeKeys = new HashSet<int>(keys);
        }

        private void InitializeKeyboardLayout()
        {
            AddKey(0x1B, new Rectangle(30, 20, 40, 35), "Esc");
            AddKey(0x31, new Rectangle(90, 20, 40, 35), "1");
            AddKey(0x32, new Rectangle(140, 20, 40, 35), "2");
            AddKey(0x46, new Rectangle(330, 20, 40, 35), "F");
            AddKey(0x47, new Rectangle(380, 20, 40, 35), "G");

            AddKey(0x09, new Rectangle(30, 70, 55, 35), "Tab");
            AddKey(0x51, new Rectangle(95, 70, 40, 35), "Q");
            AddKey(0x57, new Rectangle(145, 70, 40, 35), "W");
            AddKey(0x45, new Rectangle(195, 70, 40, 35), "E");
            AddKey(0x52, new Rectangle(245, 70, 40, 35), "R");

            AddKey(0x41, new Rectangle(95, 120, 40, 35), "A");
            AddKey(0x53, new Rectangle(145, 120, 40, 35), "S");
            AddKey(0x44, new Rectangle(195, 120, 40, 35), "D");

            AddKey(0x10, new Rectangle(30, 170, 75, 35), "Shift");
            AddKey(0x11, new Rectangle(30, 220, 60, 35), "Ctrl");

            AddKey(0x20, new Rectangle(140, 270, 200, 35), "Space");

            AddKey(0x26, new Rectangle(600, 170, 40, 35), "Up");
            AddKey(0x25, new Rectangle(550, 220, 40, 35), "Left");
            AddKey(0x27, new Rectangle(650, 220, 40, 35), "Right");
            AddKey(0x28, new Rectangle(600, 270, 40, 35), "Down");

            AddKey(0x0D, new Rectangle(700, 170, 60, 60), "Enter");
        }

        private void AddKey(int vkCode, Rectangle rect, string label)
        {
            keyRects[vkCode] = rect;
            keyLabels[vkCode] = label;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.Clear(SystemColors.Control);

            foreach (var kvp in keyRects)
            {
                int vk = kvp.Key;
                Rectangle rect = kvp.Value;
                bool active = activeKeys.Contains(vk);

                Color back = active ? Color.LightCoral : SystemColors.ControlLight;
                Color border = active ? Color.Red : SystemColors.ControlDark;
                Color textColor = active ? Color.White : SystemColors.ControlText;

                using (SolidBrush brush = new SolidBrush(back))
                    g.FillRectangle(brush, rect);
                g.DrawRectangle(new Pen(border, 1), rect);

                string label = keyLabels.ContainsKey(vk) ? keyLabels[vk] : "";
                SizeF textSize = g.MeasureString(label, keyFont);
                float x = rect.X + (rect.Width - textSize.Width) / 2;
                float y = rect.Y + (rect.Height - textSize.Height) / 2;
                using (SolidBrush textBrush = new SolidBrush(textColor))
                    g.DrawString(label, keyFont, textBrush, x, y);
            }
        }
    }
}
