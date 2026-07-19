# src/linux/joystick_hider.py
import os
import subprocess
import time

class JoystickHider:
    def __init__(self):
        self.is_hidden = False
    
    def hide(self):
        if self.is_hidden:
            return True
        
        if os.geteuid() != 0:
            return False
        
        try:
            subprocess.run(['modprobe', '-r', 'hid_sony'], capture_output=True)
            time.sleep(0.5)
            
            result = subprocess.run(['lsmod'], capture_output=True, text=True)
            if 'hid_sony' not in result.stdout:
                self.is_hidden = True
                return True
            
            return self._disable_sysfs()
        except:
            return False
    
    def show(self):
        if not self.is_hidden:
            return True
        
        if os.geteuid() != 0:
            return False
        
        try:
            subprocess.run(['modprobe', 'hid_sony'], capture_output=True)
            self.is_hidden = False
            return True
        except:
            return False
    
    def _disable_sysfs(self):
        try:
            result = subprocess.run(
                ['find', '/sys/bus/usb/devices/', '-name', '*054c*'],
                capture_output=True, text=True
            )
            
            for path in result.stdout.strip().split('\n'):
                if path:
                    auth = os.path.join(path, 'authorized')
                    if os.path.exists(auth):
                        with open(auth, 'w') as f:
                            f.write('0')
                        self.is_hidden = True
                        return True
            return False
        except:
            return False
