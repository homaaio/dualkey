// src/windows/JoystickEmulator.cs
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DualKey
{
    public class JoystickEmulator
    {
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        const uint KEYEVENTF_KEYDOWN = 0x0000;
        const uint KEYEVENTF_KEYUP = 0x0002;

        public bool Enabled { get; set; } = false;
        public float Deadzone { get; set; } = 0.3f;

        public Dictionary<string, int> Bindings { get; set; }
        private HashSet<int> pressedKeys = new HashSet<int>();

        public JoystickEmulator()
        {
            Bindings = new Dictionary<string, int>
            {
                {"left_stick_up", 0x57},
                {"left_stick_down", 0x53},
                {"left_stick_left", 0x41},
                {"left_stick_right", 0x44},
                {"right_stick_up", 0x26},
                {"right_stick_down", 0x28},
                {"right_stick_left", 0x25},
                {"right_stick_right", 0x27},
                {"cross", 0x20},
                {"circle", 0x45},
                {"triangle", 0x51},
                {"square", 0x52},
                {"l1", 0x10},
                {"r1", 0x11},
                {"l2", 0x31},
                {"r2", 0x32},
                {"l3", 0x46},
                {"r3", 0x47},
                {"select", 0x09},
                {"start", 0x0D},
                {"ps_button", 0x1B},
            };
        }

        public void ResetBindings()
        {
            Bindings = new Dictionary<string, int>
            {
                {"left_stick_up", 0x57},
                {"left_stick_down", 0x53},
                {"left_stick_left", 0x41},
                {"left_stick_right", 0x44},
                {"right_stick_up", 0x26},
                {"right_stick_down", 0x28},
                {"right_stick_left", 0x25},
                {"right_stick_right", 0x27},
                {"cross", 0x20},
                {"circle", 0x45},
                {"triangle", 0x51},
                {"square", 0x52},
                {"l1", 0x10},
                {"r1", 0x11},
                {"l2", 0x31},
                {"r2", 0x32},
                {"l3", 0x46},
                {"r3", 0x47},
                {"select", 0x09},
                {"start", 0x0D},
                {"ps_button", 0x1B},
            };
        }

        public void UpdateBinding(string action, int keyCode)
        {
            if (Bindings.ContainsKey(action))
                Bindings[action] = keyCode;
        }

        public void PressKey(string action)
        {
            if (!Enabled || !Bindings.ContainsKey(action)) return;
            
            int key = Bindings[action];
            if (!pressedKeys.Contains(key))
            {
                keybd_event((byte)key, 0, KEYEVENTF_KEYDOWN, UIntPtr.Zero);
                pressedKeys.Add(key);
            }
        }

        public void ReleaseKey(string action)
        {
            if (!Bindings.ContainsKey(action)) return;
            
            int key = Bindings[action];
            if (pressedKeys.Contains(key))
            {
                keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
                pressedKeys.Remove(key);
            }
        }

        public void ReleaseAll()
        {
            foreach (int key in new List<int>(pressedKeys))
            {
                keybd_event((byte)key, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            pressedKeys.Clear();
        }
    }
}