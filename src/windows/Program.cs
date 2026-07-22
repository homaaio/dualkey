using System;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace DualKey
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            // Prevent two copies fighting over hiding/showing the controller (and double-pressing keys).
            bool createdNew;
            using (var singleInstanceMutex = new Mutex(true, "Local\\DualKey_SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show("DualKey is already running.", "DualKey", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                // Segoe UI is the font every native Windows dialog uses; the WinForms
                // default (Microsoft Sans Serif) is what makes hand-built forms look dated.
                Application.SetDefaultFont(new Font("Segoe UI", 9f));

                // Best-effort safety net: if the app crashes (or is force-killed) while the
                // DualShock controller is hidden via Device Manager, try to re-enable it
                // rather than leaving the device disabled with no UI to fix it from.
                AppDomain.CurrentDomain.ProcessExit += (s, e) => JoystickHider.EmergencyRestoreIfHidden();
                AppDomain.CurrentDomain.UnhandledException += (s, e) => JoystickHider.EmergencyRestoreIfHidden();
                Application.ThreadException += (s, e) => JoystickHider.EmergencyRestoreIfHidden();

                Application.Run(new MainForm());

                GC.KeepAlive(singleInstanceMutex);
            }
        }
    }
}
