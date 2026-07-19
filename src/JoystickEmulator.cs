using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace ReJoy
{
    public class JoystickEmulator
    {
        [DllImport("user32.dll")]
        static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        const uint KEYEVENTF_KEYDOWN = 0x0000;
        const uint KEYEVENTF_KEYUP = 0x0002;

        public bool Enabled { get; set; } = false;
        public float Deadzone { get; set; } = 0.3f;

        public Dictionary<string, int> Bindings { get; private set; }
        private HashSet<int> pressedKeys = new HashSet<int>();

        public JoystickEmulator()
        {
            Bindings = new Dictionary<string, int>
            {
                // Левый стик - WASD
                {"left_stick_up", 0x57},      // W
                {"left_stick_down", 0x53},    // S
                {"left_stick_left", 0x41},    // A
                {"left_stick_right", 0x44},   // D
                
                // Правый стик - стрелки
                {"right_stick_up", 0x26},     // Up arrow
                {"right_stick_down", 0x28},   // Down arrow
                {"right_stick_left", 0x25},   // Left arrow
                {"right_stick_right", 0x27},  // Right arrow
                
                // Кнопки действий
                {"cross", 0x20},              // Space
                {"circle", 0x45},             // E
                {"triangle", 0x51},           // Q
                {"square", 0x52},             // R
                
                // Бамперы и триггеры
                {"l1", 0x10},                 // Shift
                {"r1", 0x11},                 // Ctrl
                {"l2", 0x31},                 // 1
                {"r2", 0x32},                 // 2
                
                // Стики (нажатия)
                {"l3", 0x46},                 // F
                {"r3", 0x47},                 // G
                
                // Системные
                {"select", 0x09},             // Tab
                {"start", 0x0D},              // Enter
                {"ps_button", 0x1B}           // Escape
            };
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
