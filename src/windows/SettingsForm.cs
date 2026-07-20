// src/windows/SettingsForm.cs
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DualKey
{
    public class SettingsForm : Form
    {
        private JoystickEmulator emulator;
        private TabControl tabControl;
        private TabPage bindingsPage;
        private TabPage sensitivityPage;

        private Dictionary<string, Label> actionLabels;
        private Dictionary<string, Button> bindButtons;
        private string currentAction = null;

        private TrackBar deadzoneSlider;
        private Label deadzoneValue;

        public SettingsForm(JoystickEmulator emulator)
        {
            this.emulator = emulator;
            this.Text = "DualKey Settings";
            this.Size = new Size(450, 400);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            tabControl = new TabControl { Dock = DockStyle.Fill };
            
            bindingsPage = new TabPage("Key Bindings");
            sensitivityPage = new TabPage("Sensitivity");
            tabControl.TabPages.Add(bindingsPage);
            tabControl.TabPages.Add(sensitivityPage);
            this.Controls.Add(tabControl);

            BuildBindingsPage();
            BuildSensitivityPage();
            PopulateBindings();
        }

        private void BuildBindingsPage()
        {
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            bindingsPage.Controls.Add(panel);

            actionLabels = new Dictionary<string, Label>();
            bindButtons = new Dictionary<string, Button>();

            string[] actions = {
                "left_stick_up", "left_stick_down", "left_stick_left", "left_stick_right",
                "right_stick_up", "right_stick_down", "right_stick_left", "right_stick_right",
                "cross", "circle", "triangle", "square",
                "l1", "r1", "l2", "r2",
                "l3", "r3", "select", "start", "ps_button"
            };

            string[] friendlyNames = {
                "Left Stick Up", "Left Stick Down", "Left Stick Left", "Left Stick Right",
                "Right Stick Up", "Right Stick Down", "Right Stick Left", "Right Stick Right",
                "Cross", "Circle", "Triangle", "Square",
                "L1", "R1", "L2", "R2",
                "L3", "R3", "Select", "Start", "PS Button"
            };

            int y = 10;
            for (int i = 0; i < actions.Length; i++)
            {
                var label = new Label
                {
                    Text = friendlyNames[i],
                    Location = new Point(10, y),
                    Size = new Size(140, 25),
                    TextAlign = ContentAlignment.MiddleLeft
                };
                panel.Controls.Add(label);

                var btn = new Button
                {
                    Text = GetKeyName(emulator.Bindings[actions[i]]),
                    Location = new Point(160, y),
                    Size = new Size(120, 25),
                    Tag = actions[i],
                    BackColor = Color.FromArgb(40, 40, 70),
                    ForeColor = Color.White,
                    FlatStyle = FlatStyle.Flat
                };
                btn.Click += OnBindButtonClick;
                btn.KeyDown += OnBindKeyDown;
                panel.Controls.Add(btn);

                actionLabels[actions[i]] = label;
                bindButtons[actions[i]] = btn;

                y += 35;
            }
        }

        private string GetKeyName(int keyCode)
        {
            try { return ((Keys)keyCode).ToString(); }
            catch { return "Unknown"; }
        }

        private void PopulateBindings()
        {
            foreach (var kvp in emulator.Bindings)
            {
                if (bindButtons.ContainsKey(kvp.Key))
                {
                    bindButtons[kvp.Key].Text = GetKeyName(kvp.Value);
                }
            }
        }

        private void OnBindButtonClick(object sender, EventArgs e)
        {
            Button btn = sender as Button;
            if (btn == null) return;

            currentAction = (string)btn.Tag;
            btn.Text = "Press a key...";
            btn.BackColor = Color.FromArgb(255, 100, 100);
            btn.Focus();
        }

        private void OnBindKeyDown(object sender, KeyEventArgs e)
        {
            if (currentAction == null) return;

            Button btn = bindButtons[currentAction];
            int keyCode = (int)e.KeyCode;

            emulator.UpdateBinding(currentAction, keyCode);
            btn.Text = GetKeyName(keyCode);
            btn.BackColor = Color.FromArgb(40, 40, 70);
            currentAction = null;
            e.Handled = true;
        }

        private void BuildSensitivityPage()
        {
            var panel = new Panel { Dock = DockStyle.Fill };
            sensitivityPage.Controls.Add(panel);

            var label = new Label
            {
                Text = "Stick Deadzone",
                Location = new Point(20, 30),
                Size = new Size(200, 25)
            };
            panel.Controls.Add(label);

            deadzoneSlider = new TrackBar
            {
                Minimum = 0,
                Maximum = 100,
                Value = (int)(emulator.Deadzone * 100),
                Location = new Point(20, 70),
                Size = new Size(250, 30),
                TickFrequency = 5
            };
            deadzoneSlider.ValueChanged += (s, e) =>
            {
                emulator.Deadzone = deadzoneSlider.Value / 100f;
                deadzoneValue.Text = emulator.Deadzone.ToString("F2");
            };
            panel.Controls.Add(deadzoneSlider);

            deadzoneValue = new Label
            {
                Text = emulator.Deadzone.ToString("F2"),
                Location = new Point(280, 70),
                Size = new Size(60, 25)
            };
            panel.Controls.Add(deadzoneValue);
        }
    }
}