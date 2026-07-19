import os
import struct
import fcntl

class JoystickReader:
    def __init__(self):
        self.connected = False
        self.device = None
        self.device_path = None
        self.axes = {
            'left_x': 0, 'left_y': 0,
            'right_x': 0, 'right_y': 0,
        }
        self.buttons = {
            'cross': False, 'circle': False, 'triangle': False,
            'square': False, 'l1': False, 'r1': False,
            'l2': False, 'r2': False, 'l3': False, 'r3': False,
            'select': False, 'start': False, 'ps': False,
            'dpad': (0, 0),
        }
        self._find_device()
    
    def _find_device(self):
        for i in range(4):
            path = f'/dev/input/js{i}'
            if os.path.exists(path):
                self.device_path = path
                try:
                    self.device = open(path, 'rb')
                    flags = fcntl.fcntl(self.device, fcntl.F_GETFL)
                    fcntl.fcntl(self.device, fcntl.F_SETFL, flags | os.O_NONBLOCK)
                    self.connected = True
                    print(f'Found joystick: {path}')
                    return
                except:
                    pass
        
        # Try by-id
        try:
            by_id = '/dev/input/by-id/'
            if os.path.exists(by_id):
                for f in os.listdir(by_id):
                    if 'Sony' in f or 'PLAYSTATION' in f or 'DualShock' in f:
                        path = os.path.join(by_id, f)
                        real = os.path.realpath(path)
                        self.device_path = real
                        self.device = open(real, 'rb')
                        flags = fcntl.fcntl(self.device, fcntl.F_GETFL)
                        fcntl.fcntl(self.device, fcntl.F_SETFL, flags | os.O_NONBLOCK)
                        self.connected = True
                        print(f'Found joystick: {real}')
                        return
        except:
            pass
    
    def update(self):
        if not self.device:
            self._find_device()
            return
        
        try:
            while True:
                data = self.device.read(8)
                if not data:
                    break
                
                time, value, type_, number = struct.unpack('IhBB', data)
                
                if type_ & 0x02:  # Axis
                    self._handle_axis(number, value)
                elif type_ & 0x01:  # Button
                    self._handle_button(number, value)
        except BlockingIOError:
            pass
        except:
            self.connected = False
            if self.device:
                self.device.close()
            self.device = None
    
    def _handle_axis(self, number, value):
        normalized = value / 32767.0
        
        axis_map = {
            0: 'left_x',
            1: 'left_y',
            2: 'right_x',
            3: 'right_y',
        }
        
        if number in axis_map:
            self.axes[axis_map[number]] = normalized
    
    def _handle_button(self, number, value):
        button_map = {
            0: 'cross', 1: 'circle', 2: 'triangle', 3: 'square',
            4: 'l1', 5: 'r1', 6: 'l2', 7: 'r2',
            8: 'select', 9: 'start', 10: 'l3', 11: 'r3',
            12: 'ps',
        }
        
        if number in button_map:
            self.buttons[button_map[number]] = bool(value)
