// src/windows/MainForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using SharpDX.XInput;
using DInput = SharpDX.DirectInput;

namespace DualKey
{
    public partial class MainForm : Form
    {
        private Controller controller;
        private DInput.DirectInput directInput;
        private DInput.Joystick directInputJoystick;
        private int diDiscoveryCounter = 0;
        private JoystickEmulator emulator;
        private JoystickHider hider;
        private WebServer webServer;
        private Timer updateTimer;
        private Timer indicatorTimer;

        // UI элементы
        private MenuStrip menuStrip;
        private NotifyIcon trayIcon;
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

        private static readonly string LogFile = GetLogFilePath();

        private static string GetLogFilePath()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DualKey");
                Directory.CreateDirectory(dir);
                return Path.Combine(dir, "dualkey.log");
            }
            catch
            {
                // Fall back to the working directory if LocalAppData is somehow unavailable.
                return "dualkey.log";
            }
        }

        private void ApplySavedGamepadBindings()
        {
            // The Edit Gamepad dialog remembers its imported layout + mappings across
            // restarts (see GamepadLayoutStore) - reapply the key bindings here so they
            // work again immediately, without the user having to reopen the dialog.
            var saved = GamepadLayoutStore.TryLoad();
            if (saved == null) return;

            foreach (var row in saved.Rows)
            {
                if (row.Action == "(Unassigned)" || string.IsNullOrEmpty(row.Action)) continue;
                emulator.UpdateBinding(row.Action, row.KeyCode);
            }

            for (int i = 1; i <= 4; i++)
                playerBindings[i] = new Dictionary<string, int>(emulator.Bindings);
        }

        public MainForm()
        {
            emulator = new JoystickEmulator();
            hider = new JoystickHider();
            playerBindings = new Dictionary<int, Dictionary<string, int>>();

            for (int i = 1; i <= 4; i++)
            {
                playerBindings[i] = new Dictionary<string, int>(emulator.Bindings);
            }

            ApplySavedGamepadBindings();

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

            // Menu
            menuStrip = new MenuStrip();

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add("Save configuration (.hrc)", null, OnSaveConfig);
            fileMenu.DropDownItems.Add("Import configuration (.hrc)", null, OnLoadConfig);
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Open Log", null, OnOpenLog);  // <-- НОВЫЙ ПУНКТ
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add("Exit", null, (s, e) => Application.Exit());

            ToolStripMenuItem settingsMenu = new ToolStripMenuItem("Settings");
            settingsMenu.DropDownItems.Add("Open settings", null, OnOpenSettings);
            settingsMenu.DropDownItems.Add("Edit Gamepad...", null, OnEditGamepad);
            settingsMenu.DropDownItems.Add(new ToolStripSeparator());
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

            // ---- system tray icon (minimize-to-tray) ----
            var trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("Open DualKey", null, (s, e) => RestoreFromTray());
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("Exit", null, (s, e) => Application.Exit());

            trayIcon = new NotifyIcon
            {
                Text = "DualKey",
                Icon = this.Icon ?? SystemIcons.Application,
                ContextMenuStrip = trayMenu,
                Visible = false
            };
            trayIcon.DoubleClick += (s, e) => RestoreFromTray();

            this.Resize += (s, e) =>
            {
                if (this.WindowState == FormWindowState.Minimized)
                {
                    this.Hide();
                    trayIcon.Visible = true;
                    trayIcon.ShowBalloonTip(1500, "DualKey", "Still running in the background.", ToolTipIcon.Info);
                }
            };
        }

        private void RestoreFromTray()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
            trayIcon.Visible = false;
        }

        private void OnOpenLog(object sender, EventArgs e)
        {
            try
            {
                if (File.Exists(LogFile))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = LogFile,
                        UseShellExecute = true
                    });
                    Log("Log opened.");
                }
                else
                {
                    MessageBox.Show("Log file not found yet. Run the application first to generate it.", "DualKey",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not open log file: {ex.Message}", "DualKey",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Log($"Error opening log: {ex.Message}");
            }
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

        private class PadState
        {
            public bool Connected;
            public string Source = "";
            public float LX, LY, RX, RY;
            public HashSet<string> Pressed = new HashSet<string>();
        }

        private static readonly string[] ButtonActions = new string[]
        {
            "cross", "circle", "triangle", "square",
            "l1", "r1", "l2", "r2", "l3", "r3",
            "start", "select", "ps_button",
        };

        private static readonly string[] DpadActions = new string[]
        {
            "dpad_up", "dpad_down", "dpad_left", "dpad_right",
        };

        private void UpdateJoystickState(object sender, EventArgs e)
        {
            try
            {
                // Try a real Xbox-compatible (XInput) controller first; if none is
                // connected, fall back to DirectInput so a DualShock 4 (which Windows
                // already recognizes as a plain HID gamepad, no extra driver needed)
                // still works.
                PadState pad = ReadXInputState();
                if (!pad.Connected)
                    pad = ReadDirectInputState();

                if (!pad.Connected)
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
                connectionLabel.Text = "Connected (" + pad.Source + ")";
                connectionLabel.ForeColor = Color.Green;

                leftX = pad.LX;
                leftY = pad.LY;
                rightX = pad.RX;
                rightY = pad.RY;

                float dz = emulator.Deadzone;
                activeKeyCodes.Clear();

                AddStickKeys("left_stick_left", "left_stick_right", leftX, dz);
                AddStickKeys("left_stick_down", "left_stick_up", leftY, dz);
                AddStickKeys("right_stick_left", "right_stick_right", rightX, dz);
                AddStickKeys("right_stick_down", "right_stick_up", rightY, dz);

                foreach (var action in ButtonActions)
                {
                    if (pad.Pressed.Contains(action) && emulator.Bindings.ContainsKey(action))
                        activeKeyCodes.Add(emulator.Bindings[action]);
                }
                foreach (var action in DpadActions)
                {
                    if (pad.Pressed.Contains(action) && emulator.Bindings.ContainsKey(action))
                        activeKeyCodes.Add(emulator.Bindings[action]);
                }

                if (emulator.Enabled)
                {
                    ProcessStickEmulation("left_stick_left", "left_stick_right", leftX, dz);
                    ProcessStickEmulation("left_stick_down", "left_stick_up", leftY, dz);
                    ProcessStickEmulation("right_stick_left", "right_stick_right", rightX, dz);
                    ProcessStickEmulation("right_stick_down", "right_stick_up", rightY, dz);

                    foreach (var action in ButtonActions)
                    {
                        if (pad.Pressed.Contains(action)) emulator.PressKey(action);
                        else emulator.ReleaseKey(action);
                    }
                    foreach (var action in DpadActions)
                    {
                        if (pad.Pressed.Contains(action)) emulator.PressKey(action);
                        else emulator.ReleaseKey(action);
                    }
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

        private PadState ReadXInputState()
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
                return new PadState { Connected = false };

            State state = controller.GetState();
            Gamepad gp = state.Gamepad;

            var pad = new PadState { Connected = true, Source = "XInput" };
            pad.LX = gp.LeftThumbX / 32768f;
            pad.LY = gp.LeftThumbY / 32768f;
            pad.RX = gp.RightThumbX / 32768f;
            pad.RY = gp.RightThumbY / 32768f;

            void AddIf(GamepadButtonFlags flag, string action)
            {
                if ((gp.Buttons & flag) != 0) pad.Pressed.Add(action);
            }
            AddIf(GamepadButtonFlags.A, "cross");
            AddIf(GamepadButtonFlags.B, "circle");
            AddIf(GamepadButtonFlags.X, "triangle");
            AddIf(GamepadButtonFlags.Y, "square");
            AddIf(GamepadButtonFlags.LeftShoulder, "l1");
            AddIf(GamepadButtonFlags.RightShoulder, "r1");
            AddIf(GamepadButtonFlags.LeftThumb, "l3");
            AddIf(GamepadButtonFlags.RightThumb, "r3");
            AddIf(GamepadButtonFlags.Start, "start");
            AddIf(GamepadButtonFlags.Back, "select");
            AddIf(GamepadButtonFlags.DPadUp, "dpad_up");
            AddIf(GamepadButtonFlags.DPadDown, "dpad_down");
            AddIf(GamepadButtonFlags.DPadLeft, "dpad_left");
            AddIf(GamepadButtonFlags.DPadRight, "dpad_right");
            // Note: XInput has no "Guide"/PS-button bit at all - that's an API limitation,
            // not something DualKey can work around, so ps_button never fires here.

            if (gp.LeftTrigger > 128) pad.Pressed.Add("l2");
            if (gp.RightTrigger > 128) pad.Pressed.Add("r2");

            return pad;
        }

        /// <summary>
        /// Fallback path for controllers Windows exposes as a plain HID gamepad rather
        /// than an XInput device - this is exactly how a DualShock 4 shows up out of the
        /// box (no DS4Windows/ViGEm required). We prefer a Sony device (vendor id 0x054C)
        /// if one is attached, otherwise fall back to whatever generic controller is plugged in.
        /// </summary>
        private PadState ReadDirectInputState()
        {
            EnsureDirectInputDevice();
            if (directInputJoystick == null)
                return new PadState { Connected = false };

            try
            {
                directInputJoystick.Poll();
                DInput.JoystickState state = directInputJoystick.GetCurrentState();

                var pad = new PadState { Connected = true, Source = "DirectInput" };

                // DirectInput's Y axis runs the opposite way to XInput's (pushing the
                // stick up produces a *smaller* raw value, not a bigger one) - the minus
                // sign below is what keeps "up" mapped to "up" instead of repeating the
                // stick-direction bug we already fixed for XInput.
                pad.LX = (state.X - 32768) / 32768f;
                pad.LY = -(state.Y - 32768) / 32768f;
                pad.RX = (state.Z - 32768) / 32768f;
                pad.RY = -(state.RotationZ - 32768) / 32768f;

                bool[] buttons = state.Buttons;
                void AddIf(int index, string action)
                {
                    if (index < buttons.Length && buttons[index]) pad.Pressed.Add(action);
                }
                // Standard DualShock 4 HID gamepad button order on Windows.
                AddIf(0, "square");
                AddIf(1, "cross");
                AddIf(2, "circle");
                AddIf(3, "triangle");
                AddIf(4, "l1");
                AddIf(5, "r1");
                AddIf(6, "l2");
                AddIf(7, "r2");
                AddIf(8, "select");   // Share
                AddIf(9, "start");    // Options
                AddIf(10, "l3");
                AddIf(11, "r3");
                AddIf(12, "ps_button");

                int[] povs = state.PointOfViewControllers;
                int pov = (povs != null && povs.Length > 0) ? povs[0] : -1;
                if (pov >= 0)
                {
                    // D-pad reported as an 8-way hat switch, in hundredths of a degree.
                    if (pov == 0 || pov == 4500 || pov == 31500) pad.Pressed.Add("dpad_up");
                    if (pov == 9000 || pov == 4500 || pov == 13500) pad.Pressed.Add("dpad_right");
                    if (pov == 18000 || pov == 13500 || pov == 22500) pad.Pressed.Add("dpad_down");
                    if (pov == 27000 || pov == 22500 || pov == 31500) pad.Pressed.Add("dpad_left");
                }

                return pad;
            }
            catch
            {
                // Most likely the controller was unplugged - drop it so the next
                // discovery pass can pick up whatever's connected now.
                try { directInputJoystick.Unacquire(); } catch { /* already gone */ }
                directInputJoystick = null;
                return new PadState { Connected = false };
            }
        }

        private void EnsureDirectInputDevice()
        {
            if (directInputJoystick != null) return;

            // Only retry discovery roughly once a second rather than on every 16ms tick.
            diDiscoveryCounter++;
            if (diDiscoveryCounter < 60) return;
            diDiscoveryCounter = 0;

            try
            {
                if (directInput == null)
                    directInput = new DInput.DirectInput();

                var devices = directInput.GetDevices(DInput.DeviceClass.GameControl, DInput.DeviceEnumerationFlags.AttachedOnly);
                if (devices.Count == 0) return;

                DInput.DeviceInstance chosen = null;
                foreach (var d in devices)
                {
                    byte[] guidBytes = d.ProductGuid.ToByteArray();
                    int vendorId = guidBytes[0] | (guidBytes[1] << 8);
                    if (vendorId == 0x054C) // Sony
                    {
                        chosen = d;
                        break;
                    }
                }
                if (chosen == null) chosen = devices[0]; // fall back to whatever's plugged in

                var joystick = new DInput.Joystick(directInput, chosen.InstanceGuid);
                foreach (var axis in joystick.GetObjects(DInput.DeviceObjectTypeFlags.Axis))
                    joystick.GetObjectPropertiesById(axis.ObjectId).Range = new DInput.InputRange(-32768, 32767);

                joystick.Properties.BufferSize = 128;
                joystick.SetCooperativeLevel(this.Handle, DInput.CooperativeLevel.NonExclusive | DInput.CooperativeLevel.Background);
                joystick.Acquire();

                directInputJoystick = joystick;
                Log("Controller connected via DirectInput: " + chosen.ProductName.Trim());
            }
            catch (Exception ex)
            {
                Log("DirectInput discovery failed: " + ex.Message);
                directInputJoystick = null;
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

        private void OnEditGamepad(object sender, EventArgs e)
        {
            using (var gf = new GamepadEditorForm(emulator))
            {
                if (gf.ShowDialog(this) == DialogResult.OK)
                {
                    playerBindings[currentPlayer] = new Dictionary<string, int>(emulator.Bindings);
                    Log("Gamepad layout bindings updated.");
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

            if (trayIcon != null)
            {
                trayIcon.Visible = false;
                trayIcon.Dispose();
            }

            if (directInputJoystick != null)
            {
                try { directInputJoystick.Unacquire(); } catch { /* ignore */ }
                directInputJoystick.Dispose();
                directInputJoystick = null;
            }
            directInput?.Dispose();

            if (hider != null && hider.IsHidden)
            {
                Log("Restoring hidden controller before exit...");
                hider.ShowJoystick();
            }

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