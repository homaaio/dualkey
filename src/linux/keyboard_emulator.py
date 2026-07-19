# src/linux/keyboard_emulator.py
import uinput

class KeyboardEmulator:
    def __init__(self):
        self.device = None
        self.pressed = set()
        self._init_device()
    
    def _init_device(self):
        try:
            events = (
                uinput.KEY_W, uinput.KEY_A, uinput.KEY_S, uinput.KEY_D,
                uinput.KEY_UP, uinput.KEY_DOWN, uinput.KEY_LEFT, uinput.KEY_RIGHT,
                uinput.KEY_SPACE, uinput.KEY_E, uinput.KEY_Q, uinput.KEY_R,
                uinput.KEY_LEFTSHIFT, uinput.KEY_LEFTCTRL,
                uinput.KEY_1, uinput.KEY_2, uinput.KEY_F, uinput.KEY_G,
                uinput.KEY_TAB, uinput.KEY_ENTER, uinput.KEY_ESC,
            )
            self.device = uinput.Device(events)
        except:
            print("Warning: uinput not available. Run install.sh first.")
            self.device = None
    
    def _get_key_code(self, key):
        key_map = {
            'w': uinput.KEY_W,
            'a': uinput.KEY_A,
            's': uinput.KEY_S,
            'd': uinput.KEY_D,
            'up': uinput.KEY_UP,
            'down': uinput.KEY_DOWN,
            'left': uinput.KEY_LEFT,
            'right': uinput.KEY_RIGHT,
            'space': uinput.KEY_SPACE,
            'e': uinput.KEY_E,
            'q': uinput.KEY_Q,
            'r': uinput.KEY_R,
            'shift': uinput.KEY_LEFTSHIFT,
            'ctrl': uinput.KEY_LEFTCTRL,
            '1': uinput.KEY_1,
            '2': uinput.KEY_2,
            'f': uinput.KEY_F,
            'g': uinput.KEY_G,
            'tab': uinput.KEY_TAB,
            'enter': uinput.KEY_ENTER,
            'esc': uinput.KEY_ESC,
        }
        return key_map.get(key)
    
    def press(self, action, bindings):
        if not self.device:
            return
        
        key = bindings.get(action)
        if not key:
            return
        
        code = self._get_key_code(key)
        if code and key not in self.pressed:
            self.device.emit(code, 1)
            self.pressed.add(key)
    
    def release(self, action, bindings):
        if not self.device:
            return
        
        key = bindings.get(action)
        if not key:
            return
        
        code = self._get_key_code(key)
        if code and key in self.pressed:
            self.device.emit(code, 0)
            self.pressed.discard(key)
    
    def release_all(self, bindings):
        if not self.device:
            return
        
        for key in list(self.pressed):
            code = self._get_key_code(key)
            if code:
                self.device.emit(code, 0)
        self.pressed.clear()
