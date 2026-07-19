# src/linux/main.py
#!/usr/bin/env python3
import sys
import os
import threading
import time
from joystick_reader import JoystickReader
from keyboard_emulator import KeyboardEmulator
from joystick_hider import JoystickHider
from web_server import start_web_server
from gui import ReJoyGUI

DEFAULT_BINDINGS = {
    'left_stick_up': 'w',
    'left_stick_down': 's',
    'left_stick_left': 'a',
    'left_stick_right': 'd',
    'right_stick_up': 'up',
    'right_stick_down': 'down',
    'right_stick_left': 'left',
    'right_stick_right': 'right',
    'cross': 'space',
    'circle': 'e',
    'triangle': 'q',
    'square': 'r',
    'l1': 'shift',
    'r1': 'ctrl',
    'l2': '1',
    'r2': '2',
    'l3': 'f',
    'r3': 'g',
    'select': 'tab',
    'start': 'enter',
    'ps_button': 'esc',
}

class ReJoyLinux:
    def __init__(self):
        self.bindings = DEFAULT_BINDINGS.copy()
        self.emulation_enabled = False
        self.deadzone = 0.3
        self.running = True
        
        self.reader = JoystickReader()
        self.emulator = KeyboardEmulator()
        self.hider = JoystickHider()
    
    def get_state(self):
        return {
            'connected': self.reader.connected,
            'emulation': self.emulation_enabled,
            'hidden': self.hider.is_hidden,
            'deadzone': self.deadzone,
            'bindings': self.bindings,
            'axes': self.reader.axes,
            'buttons': self.reader.buttons,
        }
    
    def process_emulation(self):
        if not self.emulation_enabled:
            return
        
        dz = self.deadzone
        
        lx = self.reader.axes.get('left_x', 0)
        ly = self.reader.axes.get('left_y', 0)
        rx = self.reader.axes.get('right_x', 0)
        ry = self.reader.axes.get('right_y', 0)
        
        if lx < -dz:
            self.emulator.press('left_stick_left', self.bindings)
        elif lx > dz:
            self.emulator.press('left_stick_right', self.bindings)
        else:
            self.emulator.release('left_stick_left', self.bindings)
            self.emulator.release('left_stick_right', self.bindings)
        
        if ly < -dz:
            self.emulator.press('left_stick_up', self.bindings)
        elif ly > dz:
            self.emulator.press('left_stick_down', self.bindings)
        else:
            self.emulator.release('left_stick_up', self.bindings)
            self.emulator.release('left_stick_down', self.bindings)
        
        if rx < -dz:
            self.emulator.press('right_stick_left', self.bindings)
        elif rx > dz:
            self.emulator.press('right_stick_right', self.bindings)
        else:
            self.emulator.release('right_stick_left', self.bindings)
            self.emulator.release('right_stick_right', self.bindings)
        
        if ry < -dz:
            self.emulator.press('right_stick_up', self.bindings)
        elif ry > dz:
            self.emulator.press('right_stick_down', self.bindings)
        else:
            self.emulator.release('right_stick_up', self.bindings)
            self.emulator.release('right_stick_down', self.bindings)
        
        button_map = {
            'cross': 'cross', 'circle': 'circle', 'triangle': 'triangle',
            'square': 'square', 'l1': 'l1', 'r1': 'r1', 'l2': 'l2',
            'r2': 'r2', 'l3': 'l3', 'r3': 'r3',
            'select': 'select', 'start': 'start', 'ps': 'ps_button',
        }
        
        for btn_name, action in button_map.items():
            if self.reader.buttons.get(btn_name, False):
                self.emulator.press(action, self.bindings)
            else:
                self.emulator.release(action, self.bindings)
    
    def run(self):
        print("ReJoy Linux starting...")
        print("GUI opened in separate window")
        print("Web interface: http://localhost:8080")
        
        web_thread = threading.Thread(target=start_web_server, args=(self,), daemon=True)
        web_thread.start()
        
        gui = ReJoyGUI(self)
        
        def update_loop():
            while self.running:
                self.reader.update()
                self.process_emulation()
                time.sleep(0.016)
        
        update_thread = threading.Thread(target=update_loop, daemon=True)
        update_thread.start()
        
        try:
            gui.run()
        except KeyboardInterrupt:
            print("\nShutting down...")
        finally:
            self.emulator.release_all(self.bindings)
            self.running = False

if __name__ == '__main__':
    app = ReJoyLinux()
    app.run()
