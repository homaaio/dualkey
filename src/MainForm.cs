// src/windows/MainForm.cs
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ReJoy
{
    public partial class MainForm : Form
    {
        [StructLayout(LayoutKind.Sequential)]
        struct JOYINFOEX
        {
            public int dwSize;
            public int dwFlags;
            public int dwXpos;
            public int dwYpos;
            public int dwZpos;
            public int dwRpos;
            public int dwUpos;
            public int dwVpos;
            public int dwButtons;
            public int dwButtonNumber;
            public int dwPOV;
            public int dwReserved1;
            public int dwReserved2;
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
        private Panel mainPanel;
        private Panel sticksPanel;
        private Panel leftStickPanel;
        private Panel rightStickPanel;
        private Label leftStickLabel;
        private Label rightStickLabel;
        private Label leftStickValue;
        private Label rightStickValue;
        private Panel rightPanel;
        private GroupBox emulationGroup;
        private CheckBox emulationCheckbox;
        private Label deadzoneLabel;
        private TrackBar deadzoneSlider;
        private Label deadzoneValue;
        private Button hideButton;
        private Button webButton;
        private GroupBox buttonsGroup;
        private FlowLayoutPanel buttonsPanel;
        private Label statusBar;

        private float leftX, leftY, rightX, rightY;
        private int buttons;
        private bool connected;
        private List<Label> buttonLabels;

        private readonly string[] buttonNames = {
            "Cross", "Circle", "Triangle", "Square",
            "L1", "R1", "L2", "R2",
            "Select", "Start", "L3", "R3", "PS"
        };

        public MainForm()
        {
            emulator = new JoystickEmulator();
            hider = new JoystickHider();
            buttonLabels = new List<Label>();

            Task.Run(async () =>
            {
                webServer = new WebServer(GetJsonData);
                await webServer.StartAsync();
            });

            InitializeUI();

            updateTimer = new Timer();
            updateTimer.Interval = 16;
            updateTimer.Tick += UpdateJoystickState;
            updateTimer.Start();
        }

        private void InitializeUI()
        {
            this.Text = "ReJoy - DualShock 3 Emulator";
            this.Size = new Size(800, 600);
            this.MinimumSize = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(18, 18, 36);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            CreateTopPanel();
            CreateMainPanel();
            CreateStatusBar();
        }

        private void CreateTopPanel()
        {
            topPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 80,
                BackColor = Color.FromArgb(24, 24, 48),
            };

            titleLabel = new Label
            {
                Text = "ReJoy Controller",
                Font = new Font("Segoe UI", 20, FontStyle.Bold),
                ForeColor = Color.FromArgb(255, 69, 96),
                Location = new Point(20, 15),
                Size = new Size(300, 40),
            };

            statusLabel = new Label
            {
                Text = "Searching for DualShock 3...",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(150, 150, 170),
                Location = new Point(20, 55),
                Size = new Size(400, 20),
            };

            topPanel.Controls.Add(titleLabel);
            topPanel.Controls.Add(statusLabel);
            this.Controls.Add(topPanel);
        }

        private void CreateMainPanel()
        {
            mainPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(18, 18, 36),
                Padding = new Padding(20, 10, 20, 10),
            };

            CreateSticksPanel();
            CreateRightPanel();

            mainPanel.Controls.Add(sticksPanel);
            mainPanel.Controls.Add(rightPanel);
            this.Controls.Add(mainPanel);
        }

        private void CreateSticksPanel()
        {
            sticksPanel = new Panel
            {
                Location = new Point(20, 10),
                Size = new Size(420, 400),
                BackColor = Color.FromArgb(28, 28, 52),
            };

            Label sticksTitle = new Label
            {
                Text = "Analog Sticks",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(15, 15),
                Size = new Size(200, 25),
            };

            leftStickPanel = new Panel
            {
                Location = new Point(30, 60),
                Size = new Size(170, 170),
                BackColor = Color.FromArgb(35, 35, 60),
            };
            leftStickPanel.Paint += (s, e) => DrawStick(e.Graphics, true);

            leftStickLabel = new Label
            {
                Text = "Left Stick",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(0, 255, 136),
                Location = new Point(30, 40),
                Size = new Size(170, 20),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            leftStickValue = new Label
            {
                Text = "X: 0.00  Y: 0.00",
                Font = new Font("Consolas", 8),
                ForeColor = Color.FromArgb(150, 150, 170),
                Location = new Point(30, 235),
                Size = new Size(170, 20),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            rightStickPanel = new Panel
            {
                Location = new Point(220, 60),
                Size = new Size(170, 170),
                BackColor = Color.FromArgb(35, 35, 60),
            };
            rightStickPanel.Paint += (s, e) => DrawStick(e.Graphics, false);

            rightStickLabel = new Label
            {
                Text = "Right Stick",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(255, 69, 96),
                Location = new Point(220, 40),
                Size = new Size(170, 20),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            rightStickValue = new Label
            {
                Text = "X: 0.00  Y: 0.00",
                Font = new Font("Consolas", 8),
                ForeColor = Color.FromArgb(150, 150, 170),
                Location = new Point(220, 235),
                Size = new Size(170, 20),
                TextAlign = ContentAlignment.MiddleCenter,
            };

            sticksPanel.Controls.Add(sticksTitle);
            sticksPanel.Controls.Add(leftStickLabel);
            sticksPanel.Controls.Add(leftStickPanel);
            sticksPanel.Controls.Add(leftStickValue);
            sticksPanel.Controls.Add(rightStickLabel);
            sticksPanel.Controls.Add(rightStickPanel);
            sticksPanel.Controls.Add(rightStickValue);
        }

        private void CreateRightPanel()
        {
            rightPanel = new Panel
            {
                Location = new Point(460, 10),
                Size = new Size(310, 400),
                BackColor = Color.FromArgb(28, 28, 52),
            };

            CreateEmulationGroup();
            CreateButtonsGroup();

            rightPanel.Controls.Add(emulationGroup);
            rightPanel.Controls.Add(buttonsGroup);
        }

        private void CreateEmulationGroup()
        {
            emulationGroup = new GroupBox
            {
                Text = "Keyboard Emulation",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(15, 15),
                Size = new Size(280, 140),
            };

            emulationCheckbox = new CheckBox
            {
                Text = "Enable keyboard emulation",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
                Location = new Point(15, 30),
                Size = new Size(250, 25),
            };
            emulationCheckbox.CheckedChanged += (s, e) =>
            {
                emulator.Enabled = emulationCheckbox.Checked;
                if (!emulationCheckbox.Checked)
                    emulator.ReleaseAll();
            };

            deadzoneLabel = new Label
            {
                Text = "Deadzone",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.FromArgb(150, 150, 170),
                Location = new Point(15, 65),
                Size = new Size(250, 20),
            };

            deadzoneSlider = new TrackBar
            {
                Minimum = 0,
                Maximum = 50,
                Value = 15,
                Location = new Point(15, 85),
                Size = new Size(200, 30),
                BackColor = Color.FromArgb(28, 28, 52),
            };
            deadzoneSlider.ValueChanged += (s, e) =>
            {
                emulator.Deadzone = deadzoneSlider.Value / 50f;
                deadzoneValue.Text = $"{(deadzoneSlider.Value / 50f):F2}";
            };

            deadzoneValue = new Label
            {
                Text = "0.30",
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.White,
                Location = new Point(225, 85),
                Size = new Size(40, 30),
                TextAlign = ContentAlignment.MiddleLeft,
            };

            emulationGroup.Controls.Add(emulationCheckbox);
            emulationGroup.Controls.Add(deadzoneLabel);
            emulationGroup.Controls.Add(deadzoneSlider);
            emulationGroup.Controls.Add(deadzoneValue);
        }

        private void CreateButtonsGroup()
        {
            buttonsGroup = new GroupBox
            {
                Text = "Controller Buttons",
                Font = new Font("Segoe UI", 10, FontStyle.Bold),
                ForeColor = Color.White,
                Location = new Point(15, 170),
                Size = new Size(280, 170),
            };

            buttonsPanel = new FlowLayoutPanel
            {
                Location = new Point(10, 25),
                Size = new Size(260, 130),
                BackColor = Color.FromArgb(28, 28, 52),
            };

            for (int i = 0; i < buttonNames.Length; i++)
            {
                Label btnLabel = new Label
                {
                    Text = buttonNames[i],
                    Font = new Font("Segoe UI", 8),
                    ForeColor = Color.FromArgb(150, 150, 170),
                    BackColor = Color.FromArgb(40, 40, 70),
                    Size = new Size(75, 28),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = new Padding(3),
                };
                buttonLabels.Add(btnLabel);
                buttonsPanel.Controls.Add(btnLabel);
            }

            hideButton = new Button
            {
                Text = "Hide Controller",
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(255, 170, 0),
                ForeColor = Color.Black,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(15, 290),
                Size = new Size(130, 35),
                Cursor = Cursors.Hand,
            };
            hideButton.FlatAppearance.BorderSize = 0;
            hideButton.Click += (s, e) =>
            {
                if (!hider.IsHidden)
                {
                    if (hider.HideJoystick())
                    {
                        hideButton.Text = "Show Controller";
                        hideButton.BackColor = Color.FromArgb(0, 200, 100);
                    }
                    else
                    {
                        MessageBox.Show("Failed to hide controller. Run as Administrator.",
                            "ReJoy", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    if (hider.ShowJoystick())
                    {
                        hideButton.Text = "Hide Controller";
                        hideButton.BackColor = Color.FromArgb(255, 170, 0);
                    }
                }
            };

            webButton = new Button
            {
                Text = "Web Interface",
                Font = new Font("Segoe UI", 9),
                BackColor = Color.FromArgb(255, 69, 96),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Location = new Point(160, 290),
                Size = new Size(130, 35),
                Cursor = Cursors.Hand,
            };
            webButton.FlatAppearance.BorderSize = 0;
            webButton.Click += (s, e) =>
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "http://localhost:8080",
                    UseShellExecute = true,
                });

            buttonsGroup.Controls.Add(buttonsPanel);
            rightPanel.Controls.Add(hideButton);
            rightPanel.Controls.Add(webButton);
        }

        private void CreateStatusBar()
        {
            statusBar = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 25,
                BackColor = Color.FromArgb(24, 24, 48),
                ForeColor = Color.FromArgb(120, 120, 140),
                Text = "  Web interface: http://localhost:8080  |  Run as Administrator to hide controller",
                Font = new Font("Segoe UI", 8),
                TextAlign = ContentAlignment.MiddleLeft,
            };
            this.Controls.Add(statusBar);
        }

        private void DrawStick(Graphics g, bool isLeft)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int w = isLeft ? leftStickPanel.Width : rightStickPanel.Width;
            int h = isLeft ? leftStickPanel.Height : rightStickPanel.Height;
            int cx = w / 2;
            int cy = h / 2;
            int r = 65;

            Color color = isLeft ? Color.FromArgb(0, 255, 136) : Color.FromArgb(255, 69, 96);
            float x = isLeft ? leftX : rightX;
            float y = isLeft ? leftY : rightY;

            // Background circle
            using (Pen pen = new Pen(Color.FromArgb(60, 60, 80), 2))
            {
                g.DrawEllipse(pen, cx - r, cy - r, r * 2, r * 2);
            }

            // Cross lines
            using (Pen pen = new Pen(Color.FromArgb(40, 40, 60), 1))
            {
                g.DrawLine(pen, cx - r, cy, cx + r, cy);
                g.DrawLine(pen, cx, cy - r, cx, cy + r);
            }

            // Stick position
            int dotX = cx + (int)(x * r) - 8;
            int dotY = cy + (int)(y * r) - 8;

            using (Brush brush = new SolidBrush(color))
            {
                g.FillEllipse(brush, dotX, dotY, 16, 16);
            }

            // Outer ring
            using (Pen pen = new Pen(color, 2))
            {
                g.DrawEllipse(pen, dotX - 2, dotY - 2, 20, 20);
            }
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
                    statusLabel.Text = "DualShock 3 connected";
                    statusLabel.ForeColor = Color.FromArgb(0, 255, 136);

                    leftX = (joyInfo.dwXpos - 32767) / 32767f;
                    leftY = (joyInfo.dwYpos - 32767) / 32767f;
                    rightX = (joyInfo.dwZpos - 32767) / 32767f;
                    rightY = (joyInfo.dwRpos - 32767) / 32767f;
                    buttons = joyInfo.dwButtons;

                    leftStickValue.Text = $"X: {leftX,6:F2}  Y: {leftY,6:F2}";
                    rightStickValue.Text = $"X: {rightX,6:F2}  Y: {rightY,6:F2}";

                    // Update button indicators
                    for (int i = 0; i < buttonLabels.Count; i++)
                    {
                        bool pressed = (buttons & (1 << i)) != 0;
                        buttonLabels[i].BackColor = pressed
                            ? Color.FromArgb(255, 69, 96)
                            : Color.FromArgb(40, 40, 70);
                        buttonLabels[i].ForeColor = pressed
                            ? Color.White
                            : Color.FromArgb(150, 150, 170);
                    }

                    // Emulation
                    if (emulator.Enabled)
                    {
                        float dz = emulator.Deadzone;
                        ProcessStickEmulation("left_stick_left", "left_stick_right", leftX, dz);
                        ProcessStickEmulation("left_stick_up", "left_stick_down", leftY, dz);
                        ProcessStickEmulation("right_stick_left", "right_stick_right", rightX, dz);
                        ProcessStickEmulation("right_stick_up", "right_stick_down", rightY, dz);
                    }

                    leftStickPanel.Invalidate();
                    rightStickPanel.Invalidate();
                }
                else
                {
                    connected = false;
                    statusLabel.Text = "Controller not connected";
                    statusLabel.ForeColor = Color.FromArgb(255, 69, 96);
                    leftX = leftY = rightX = rightY = 0;
                    buttons = 0;
                }
            }
            catch
            {
                connected = false;
            }
        }

        private void ProcessStickEmulation(string negAction, string posAction, float value, float deadzone)
        {
            if (value < -deadzone)
            {
                emulator.PressKey(negAction);
                emulator.ReleaseKey(posAction);
            }
            else if (value > deadzone)
            {
                emulator.PressKey(posAction);
                emulator.ReleaseKey(negAction);
            }
            else
            {
                emulator.ReleaseKey(negAction);
                emulator.ReleaseKey(posAction);
            }
        }

        private string GetJsonData()
        {
            return "{" +
                   $"\"connected\":{connected.ToString().ToLower()}," +
                   $"\"leftStick\":{{\"x\":{leftX:F2},\"y\":{leftY:F2}}}," +
                   $"\"rightStick\":{{\"x\":{rightX:F2},\"y\":{rightY:F2}}}," +
                   $"\"buttons\":{buttons}" +
                   "}";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            updateTimer?.Stop();
            emulator?.ReleaseAll();
            webServer?.Stop();
            base.OnFormClosing(e);
        }
    }
}
