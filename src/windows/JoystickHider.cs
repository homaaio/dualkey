using System;
using System.Diagnostics;
using System.IO;

namespace DualKey
{
    public class JoystickHider
    {
        // Kept so Program.cs can re-enable the controller even if the app crashes
        // or is killed while it's hidden, instead of leaving it disabled in Device Manager.
        private static JoystickHider activeInstance;

        public bool IsHidden { get; private set; } = false;

        public JoystickHider()
        {
            activeInstance = this;
        }

        public static void EmergencyRestoreIfHidden()
        {
            try
            {
                if (activeInstance != null && activeInstance.IsHidden)
                    activeInstance.ShowJoystick();
            }
            catch { /* best-effort only - the process may already be tearing down */ }
        }

        public bool HideJoystick()
        {
            if (IsHidden) return true;

            try
            {
                string psScript = @"
$ErrorActionPreference = 'Stop'
try {
    $device = Get-PnpDevice | Where-Object {
        $_.FriendlyName -like '*DualShock*' -or 
        $_.FriendlyName -like '*PLAYSTATION*' -or 
        $_.FriendlyName -like '*PS3*'
    } | Select-Object -First 1
    
    if ($device) {
        Disable-PnpDevice -InstanceId $device.InstanceId -Confirm:$false
        Write-Host 'SUCCESS'
        exit 0
    } else {
        Write-Host 'DEVICE_NOT_FOUND'
        exit 1
    }
} catch {
    Write-Host 'ERROR: ' + $_.Exception.Message
    exit 1
}";

                if (RunPowerShellScript(psScript))
                {
                    IsHidden = true;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hide error: {ex.Message}");
            }

            return false;
        }

        public bool ShowJoystick()
        {
            if (!IsHidden) return true;

            try
            {
                string psScript = @"
$ErrorActionPreference = 'Stop'
try {
    $device = Get-PnpDevice | Where-Object {
        $_.FriendlyName -like '*DualShock*' -or 
        $_.FriendlyName -like '*PLAYSTATION*' -or 
        $_.FriendlyName -like '*PS3*'
    } | Select-Object -First 1
    
    if ($device) {
        Enable-PnpDevice -InstanceId $device.InstanceId -Confirm:$false
        Write-Host 'SUCCESS'
        exit 0
    } else {
        Write-Host 'DEVICE_NOT_FOUND'
        exit 1
    }
} catch {
    Write-Host 'ERROR: ' + $_.Exception.Message
    exit 1
}";

                if (RunPowerShellScript(psScript))
                {
                    IsHidden = false;
                    return true;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Show error: {ex.Message}");
            }

            return false;
        }

        private bool RunPowerShellScript(string script)
        {
            string tempFile = Path.GetTempFileName() + ".ps1";
            File.WriteAllText(tempFile, script);

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -File \"{tempFile}\"",
                Verb = "runas",
                UseShellExecute = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            Process process = Process.Start(psi);
            process.WaitForExit(5000);

            try { File.Delete(tempFile); } catch { }

            return process.ExitCode == 0;
        }
    }
}
