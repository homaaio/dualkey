// src/windows/MainForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DualKey
{
    public partial class MainForm : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        struct JOYINFOEX
        {
            public int dwSize;
            public int dwFlags;
            public int dwXpos, dwYpos, dwZpos, dwRpos, dwUpos, dwVpos;
            public int dwButtons;
            public int dwButtonNumber;
            public int dwPOV;
            public int dwReserved1, dwReserved2;
        }

        [DllImport("winmm.dll")]
        static extern int joyGetPosEx(int uJoyID, ref JOYINFOEX pji);

        private JoystickEmulator emulator;
        private JoystickHider hider;
        private WebServer webServer;
        private Timer updateTimer;

        private Panel topPanel;
        private Label titleLabel;
        private Label statusLabel;
        private KeyboardVisualizer keyboardView;
        private CheckBox emulationCheckbox;
        private TrackBar deadzoneSlider;
        private Label deadzoneValue;
        private Button hideButton;
        private Button webButton;
        private Label statusBar;

        private float leftX, leftY, rightX, rightY;
        private int buttons;
        private bool connected;
        private HashSet<int> activeKeyCodes = new HashSet<int>();

        private static readonly string LogFile = "dualkey.log";

        public MainForm()
        {
            emulator = new JoystickEmulator();
            hider = new JoystickHider();

            Log("Application starting...");

            Task.Run(async () =>
            {
                webServer = new WebServer(GetJsonData);
                await webServer.StartAsync();
            });

            InitializeUI();
            BuildMenu();
            Log("UI initialized.");

            updateTimer = new Timer();
            updateTimer.Interval = 16;
            updateTimer.Tick += UpdateJoystickState;
            updateTimer.Start();
        }

        private static void Log(string message)
        {
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} - {message}";
            try { File.AppendAllText(LogFile, logEntry + Environment.NewLine); } catch { }
            System.Diagnostics.Debug.WriteLine(logEntry);
        }

        private void BuildMenu()
        {
            MenuStrip menuStrip = new MenuStrip
            {
                BackColor = Color.FromArgb(24, 24, 48),
                ForeColor = Color.White,
                Renderer = new ToolStripProfessionalRenderer(new CustomColorTable()) { RoundedEdges = false }
            };

            ToolStripMenuItem fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] {
                new ToolStripMenuItem("Save configuration (.hrc)", null, OnSaveConfig, Keys.Control | Keys.S),
                new ToolStripMenuItem("Import configuration (.hrc)", null, OnLoadConfig, Keys.Control | Keys.O),
                new ToolStripSeparator(),
                new ToolStripMenuItem("Exit", null, (s, e) => { Log("Application exit."); Application.Exit(); })
            });

            ToolStripMenuItem settingsMenu = new ToolStripMenuItem("Settings");
            settingsMenu.DropDownItems.AddRange(new ToolStripItem[] {
                new ToolStripMenuItem("Open settings", null, OnOpenSettings, Keys.Control | Keys.P),
                new ToolStripSeparator(),
                new ToolStripMenuItem("Clear all settings", null, OnClearSettings, Keys.Control | Keys.Shift | Keys.R)
            });

            menuStrip.Items.Add(fileMenu);
            menuStrip.Items.Add(settingsMenu);
            this.MainMenuStrip = menuStrip;
            this.Controls.Add(menuStrip);
        }

        private void InitializeUI()
        {
            this.Text = "DualKey - DualShock 3 Emulator";
            this.Size = new Size(900, 640);
            this.MinimumSize = new Size(900, 640);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 18, 36);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Top panel: title + status
            topPanel = new Panel
            {
                Location = new Point(0, 24),
                Size = new Size(this.ClientSize.Width, 70),
                BackColor = Color.FromArgb(24, 24, 48)
            };

            titleLabel = new Label
            {
                Text = "DualKey Controller",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 69, 96),
                Location = new Point(20, 10),
                Size = new Size(250, 30)
            };

            statusLabel = new Label
            {
                Text = "Status: Searching...",
                Font = new Font("Segoe UI", 10),
                ForeColor = Color.FromArgb(200, 200, 200),
                Location = new Point(20, 42),
                Size = new Size(350, 22)
            };

            topPanel.Controls.Add(titleLabel);
            topPanel.Controls.Add(statusLabel);
            this.Controls.Add(topPanel);

            // Keyboard visualizer (main area)
            keyboardView = new KeyboardVisualizer
            {
                Location = new Point(15, topPanel.Bottom + 10),
                Size = new Size(850, 430),
                BackColor = Color.FromArgb(22, 22, 40),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(keyboardView);

            // Bottom panel with controls
            Panel bottomPanel = new Panel
            {
                Location = new Point(15, keyboardView.Bottom + 5),
                Size = new Size(850, 70),
                BackColor = Color.FromArgb(28, 28, 52)
            };
            RoundedCorners(bottomPanel, 8);

            emulationCheckbox = new CheckBox
            {
                Text = "Enable keyboard emulation",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
                Location = new Point(15, 10),
                Size = new Size(200, 22)
            };
            emulationCheckbox.CheckedChanged += (s, e) =>
            {
                emulator.Enabled = emulationCheckbox.Checked;
                if (!emulationCheckbox.Checked) emulator.ReleaseAll();
            };

            Label dzLabel = new Label
            {
                Text = "Deadzone:",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.FromArgb(180, 180, 180),
                Location = new Point(15, 38),
                Size = new Size(65, 18)
            };

            deadzoneSlider = new TrackBar
            {
                Minimum = 0, Maximum = 50, Value = 15,
                Location = new Point(80, 36),
                Size = new Size(130, 25),
                BackColor = Color.FromArgb(28, 28, 52)
            };
            deadzoneSlider.ValueChanged += (s, e) =>
            {
                emulator.Deadzone = deadzoneSlider.Value / 50f;
                deadzoneValue.Text = $"{emulator.Deadzone:F2}";
            };

            deadzoneValue = new Label
            {
                Text = "0.30",
                Font = new Font("Segoe UI", 8),
                ForeColor = Color.White,
                Location = new Point(218, 38),
                Size = new Size(40, 18)
            };

            hideButton = new Button
            {
                Text = "Hide Controller",
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(255, 170, 0),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(320, 8),
                Size = new Size(130, 30),
                Cursor = Cursors.Hand
            };
            hideButton.FlatAppearance.BorderSize = 0;
            RoundedCorners(hideButton, 6);
            hideButton.Click += (s, e) =>
            {
                if (!hider.IsHidden)
                {
                    if (hider.HideJoystick()) { hideButton.Text = "Show Controller"; hideButton.BackColor = Color.FromArgb(0, 200, 100); Log("Controller hidden."); }
                    else { MessageBox.Show("Failed to hide controller. Run as Administrator.", "DualKey"); Log("Hide failed."); }
                }
                else
                {
                    if (hider.ShowJoystick()) { hideButton.Text = "Hide Controller"; hideButton.BackColor = Color.FromArgb(255, 170, 0); Log("Controller shown."); }
                }
            };

            webButton = new Button
            {
                Text = "Web Interface",
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(255, 69, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(460, 8),
                Size = new Size(130, 30),
                Cursor = Cursors.Hand
            };
            webButton.FlatAppearance.BorderSize = 0;
            RoundedCorners(webButton, 6);
            webButton.Click += (s, e) => System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "http://localhost:8080",
                UseShellExecute = true
            });

            bottomPanel.Controls.Add(emulationCheckbox);
            bottomPanel.Controls.Add(dzLabel);
            bottomPanel.Controls.Add(deadzoneSlider);
            bottomPanel.Controls.Add(deadzoneValue);
            bottomPanel.Controls.Add(hideButton);
            bottomPanel.Controls.Add(webButton);
            this.Controls.Add(bottomPanel);

            // Status bar
            statusBar = new Label
            {
                Location = new Point(0, this.ClientSize.Height - 22),
                Size = new Size(this.ClientSize.Width, 22),
                BackColor = Color.FromArgb(24, 24, 48),
                ForeColor = Color.FromArgb(140, 140, 160),
                Text = "  Web: http://localhost:8080  |  Run as Administrator to hide controller",
                Font = new Font("Segoe UI", 8),
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.Add(statusBar);
        }

        private void UpdateJoystickState(object sender, EventArgs e)
        {
            try
            {
                JOYINFOEX joyInfo = new JOYINFOEX();
                joyInfo.dwSize = Marshal.SizeOf(typeof(JOYINFOEX));
                joyInfo.dwFlags = 0xFF;

                int result = joyGetPosEx(0, ref joyInfo);
                if (result == 0)
                {
                    connected = true;
                    statusLabel.Text = "Status: Connected";
                    statusLabel.ForeColor = Color.FromArgb(0, 255, 136);

                    leftX = (joyInfo.dwXpos - 32767) / 32767f;
                    leftY = (joyInfo.dwYpos - 32767) / 32767f;
                    rightX = (joyInfo.dwZpos - 32767) / 32767f;
                    rightY = (joyInfo.dwRpos - 32767) / 32767f;
                    buttons = joyInfo.dwButtons;

                    // Compute active keys based on bindings and current joystick state
                    activeKeyCodes.Clear();
                    float dz = emulator.Deadzone;
                    AddStickKeys("left_stick_left", "left_stick_right", leftX, dz);
                    AddStickKeys("left_stick_up", "left_stick_down", leftY, dz);
                    AddStickKeys("right_stick_left", "right_stick_right", rightX, dz);
                    AddStickKeys("right_stick_up", "right_stick_down", rightY, dz);

                    // Buttons mapping (same as emulator logic)
                    string[] buttonActions = { "cross", "circle", "triangle", "square", "l1", "r1", "l2", "r2", "select", "start", "l3", "r3", "ps_button" };
                    for (int i = 0; i < buttonActions.Length && i < 13; i++)
                    {
                        if ((buttons & (1 << i)) != 0 && emulator.Bindings.ContainsKey(buttonActions[i]))
                            activeKeyCodes.Add(emulator.Bindings[buttonActions[i]]);
                    }

                    if (emulator.Enabled)
                    {
                        ProcessStickEmulation("left_stick_left", "left_stick_right", leftX, dz);
                        ProcessStickEmulation("left_stick_up", "left_stick_down", leftY, dz);
                        ProcessStickEmulation("right_stick_left", "right_stick_right", rightX, dz);
                        ProcessStickEmulation("right_stick_up", "right_stick_down", rightY, dz);
                    }

                    keyboardView.SetActiveKeys(activeKeyCodes);
                    keyboardView.Invalidate();
                }
                else
                {
                    connected = false;
                    statusLabel.Text = "Status: Not connected";
                    statusLabel.ForeColor = Color.FromArgb(255, 69, 96);
                    leftX = leftY = rightX = rightY = 0;
                    buttons = 0;
                    activeKeyCodes.Clear();
                    keyboardView.SetActiveKeys(activeKeyCodes);
                    keyboardView.Invalidate();
                }
            }
            catch (Exception ex)
            {
                Log($"Error reading joystick: {ex.Message}");
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

        private string GetJsonData() => $"{{\"connected\":{connected.ToString().ToLower()},\"leftStick\":{{\"x\":{leftX:F2},\"y\":{leftY:F2}}},\"rightStick\":{{\"x\":{rightX:F2},\"y\":{rightY:F2}}},\"buttons\":{buttons}}}";

        private void OnSaveConfig(object sender, EventArgs e) { /* same as before */ }
        private void OnLoadConfig(object sender, EventArgs e) { /* same as before */ }
        private void OnOpenSettings(object sender, EventArgs e) { using (var sf = new SettingsForm(emulator)) sf.ShowDialog(this); }
        private void OnClearSettings(object sender, EventArgs e) { /* same as before */ }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            updateTimer?.Stop();
            emulator?.ReleaseAll();
            webServer?.Stop();
            Log("Application closed.");
            base.OnFormClosing(e);
        }

        private void RoundedCorners(Control control, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            path.AddArc(0, 0, radius * 2, radius * 2, 180, 90);
            path.AddArc(control.Width - radius * 2, 0, radius * 2, radius * 2, 270, 90);
            path.AddArc(control.Width - radius * 2, control.Height - radius * 2, radius * 2, radius * 2, 0, 90);
            path.AddArc(0, control.Height - radius * 2, radius * 2, radius * 2, 90, 90);
            path.CloseFigure();
            control.Region = new Region(path);
        }

        private class CustomColorTable : ProfessionalColorTable
        {
            public override Color MenuItemSelected => Color.FromArgb(255, 69, 96);
            public override Color MenuItemBorder => Color.Transparent;
            public override Color MenuBorder => Color.FromArgb(40, 40, 60);
            public override Color MenuItemPressedGradientBegin => Color.FromArgb(255, 69, 96);
            public override Color MenuItemPressedGradientEnd => Color.FromArgb(220, 50, 80);
            public override Color MenuStripGradientBegin => Color.FromArgb(24, 24, 48);
            public override Color MenuStripGradientEnd => Color.FromArgb(24, 24, 48);
        }
    }

    // ---------- Keyboard Visualizer ----------
    public class KeyboardVisualizer : Panel
    {
        private HashSet<int> activeKeys = new HashSet<int>();
        private Dictionary<int, Rectangle> keyRects = new Dictionary<int, Rectangle>();
        private Dictionary<int, string> keyLabels = new Dictionary<int, string>();
        private readonly Font keyFont = new Font("Segoe UI", 8, FontStyle.Bold);

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
            // Define rectangles for relevant keys (coords based on a 850x400 panel)
            // Row 1: Esc, 1, 2, F, G
            AddKey(0x1B, new Rectangle(30, 20, 40, 35), "Esc");
            AddKey(0x31, new Rectangle(90, 20, 40, 35), "1");
            AddKey(0x32, new Rectangle(140, 20, 40, 35), "2");
            AddKey(0x46, new Rectangle(330, 20, 40, 35), "F");
            AddKey(0x47, new Rectangle(380, 20, 40, 35), "G");

            // Row 2: Tab, Q, W, E, R
            AddKey(0x09, new Rectangle(30, 70, 55, 35), "Tab");
            AddKey(0x51, new Rectangle(95, 70, 40, 35), "Q");
            AddKey(0x57, new Rectangle(145, 70, 40, 35), "W");
            AddKey(0x45, new Rectangle(195, 70, 40, 35), "E");
            AddKey(0x52, new Rectangle(245, 70, 40, 35), "R");

            // Row 3: A, S, D
            AddKey(0x41, new Rectangle(95, 120, 40, 35), "A");
            AddKey(0x53, new Rectangle(145, 120, 40, 35), "S");
            AddKey(0x44, new Rectangle(195, 120, 40, 35), "D");

            // Row 4: Left Shift
            AddKey(0x10, new Rectangle(30, 170, 75, 35), "Shift");

            // Row 5: Left Ctrl
            AddKey(0x11, new Rectangle(30, 220, 60, 35), "Ctrl");

            // Space bar
            AddKey(0x20, new Rectangle(140, 270, 200, 35), "Space");

            // Arrow keys
            AddKey(0x26, new Rectangle(600, 170, 40, 35), "Up");
            AddKey(0x25, new Rectangle(550, 220, 40, 35), "Left");
            AddKey(0x27, new Rectangle(650, 220, 40, 35), "Right");
            AddKey(0x28, new Rectangle(600, 270, 40, 35), "Down");

            // Enter
            AddKey(0x0D, new Rectangle(700, 170, 60, 60), "Enter");

            // Additional keys (Escape already, Tab, etc.)
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
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Draw background
            g.Clear(Color.FromArgb(22, 22, 40));

            // Draw keys
            foreach (var kvp in keyRects)
            {
                int vk = kvp.Key;
                Rectangle rect = kvp.Value;
                bool active = activeKeys.Contains(vk);

                Color back = active ? Color.FromArgb(255, 69, 96) : Color.FromArgb(60, 60, 80);
                Color border = active ? Color.FromArgb(255, 100, 130) : Color.FromArgb(100, 100, 120);
                Color textColor = active ? Color.White : Color.FromArgb(180, 180, 180);

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

            // Draw legend
            using (Font legendFont = new Font("Segoe UI", 7))
            {
                g.DrawString("WASD – Left Stick   |   Arrows – Right Stick   |   Space/Enter/Esc – Buttons", legendFont, Brushes.Gray, 30, 340);
            }
        }
    }
}