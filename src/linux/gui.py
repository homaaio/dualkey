# src/linux/gui.py
import tkinter as tk
from tkinter import ttk
import threading

class DualKeyGUI:
    def __init__(self, app):
        self.app = app
        
        self.root = tk.Tk()
        self.root.title("DualKey - DualShock 3 Emulator")
        self.root.geometry("800x600")
        self.root.configure(bg="#121224")
        self.root.resizable(False, False)
        
        self.setup_styles()
        self.create_top_bar()
        self.create_main_area()
        self.create_status_bar()
        
        self.update_ui()
    
    def setup_styles(self):
        self.colors = {
            'bg_dark': '#121224',
            'bg_panel': '#1c1c34',
            'bg_element': '#282840',
            'accent': '#e94560',
            'accent_green': '#00ff88',
            'accent_yellow': '#ffaa00',
            'text_primary': '#ffffff',
            'text_secondary': '#9696aa',
            'text_dim': '#78788c',
            'border': '#3c3c50',
        }
        
        self.fonts = {
            'title': ('Segoe UI', 20, 'bold'),
            'heading': ('Segoe UI', 12, 'bold'),
            'body': ('Segoe UI', 9),
            'mono': ('Consolas', 8),
            'small': ('Segoe UI', 8),
        }
    
    def create_top_bar(self):
        top_frame = tk.Frame(
            self.root,
            bg=self.colors['bg_panel'],
            height=80,
        )
        top_frame.pack(fill=tk.X)
        top_frame.pack_propagate(False)
        
        title = tk.Label(
            top_frame,
            text="ReJoy Controller",
            font=self.fonts['title'],
            fg=self.colors['accent'],
            bg=self.colors['bg_panel'],
        )
        title.place(x=20, y=15)
        
        self.status_label = tk.Label(
            top_frame,
            text="Searching for DualShock 3...",
            font=self.fonts['body'],
            fg=self.colors['text_secondary'],
            bg=self.colors['bg_panel'],
        )
        self.status_label.place(x=20, y=55)
    
    def create_main_area(self):
        main_frame = tk.Frame(
            self.root,
            bg=self.colors['bg_dark'],
        )
        main_frame.pack(fill=tk.BOTH, expand=True, padx=20, pady=10)
        
        left_panel = tk.Frame(
            main_frame,
            bg=self.colors['bg_panel'],
            width=420,
            height=420,
        )
        left_panel.pack(side=tk.LEFT, padx=(0, 10))
        left_panel.pack_propagate(False)
        
        right_panel = tk.Frame(
            main_frame,
            bg=self.colors['bg_panel'],
            width=330,
            height=420,
        )
        right_panel.pack(side=tk.RIGHT)
        right_panel.pack_propagate(False)
        
        self.create_sticks_panel(left_panel)
        self.create_controls_panel(right_panel)
    
    def create_sticks_panel(self, parent):
        heading = tk.Label(
            parent,
            text="Analog Sticks",
            font=self.fonts['heading'],
            fg=self.colors['text_primary'],
            bg=self.colors['bg_panel'],
        )
        heading.place(x=15, y=15)
        
        self.left_stick_canvas = tk.Canvas(
            parent,
            width=170,
            height=170,
            bg=self.colors['bg_element'],
            highlightthickness=0,
        )
        self.left_stick_canvas.place(x=30, y=60)
        
        left_label = tk.Label(
            parent,
            text="Left Stick",
            font=self.fonts['body'],
            fg=self.colors['accent_green'],
            bg=self.colors['bg_panel'],
        )
        left_label.place(x=30, y=40)
        
        self.left_stick_value = tk.Label(
            parent,
            text="X:  0.00  Y:  0.00",
            font=self.fonts['mono'],
            fg=self.colors['text_secondary'],
            bg=self.colors['bg_panel'],
        )
        self.left_stick_value.place(x=30, y=235)
        
        self.right_stick_canvas = tk.Canvas(
            parent,
            width=170,
            height=170,
            bg=self.colors['bg_element'],
            highlightthickness=0,
        )
        self.right_stick_canvas.place(x=220, y=60)
        
        right_label = tk.Label(
            parent,
            text="Right Stick",
            font=self.fonts['body'],
            fg=self.colors['accent'],
            bg=self.colors['bg_panel'],
        )
        right_label.place(x=220, y=40)
        
        self.right_stick_value = tk.Label(
            parent,
            text="X:  0.00  Y:  0.00",
            font=self.fonts['mono'],
            fg=self.colors['text_secondary'],
            bg=self.colors['bg_panel'],
        )
        self.right_stick_value.place(x=220, y=235)
    
    def create_controls_panel(self, parent):
        emulation_frame = tk.LabelFrame(
            parent,
            text="Keyboard Emulation",
            font=self.fonts['heading'],
            fg=self.colors['text_primary'],
            bg=self.colors['bg_panel'],
            foreground=self.colors['text_primary'],
        )
        emulation_frame.place(x=15, y=15, width=300, height=140)
        
        self.emulation_var = tk.BooleanVar(value=False)
        emulation_check = tk.Checkbutton(
            emulation_frame,
            text="Enable keyboard emulation",
            variable=self.emulation_var,
            command=self.toggle_emulation,
            font=self.fonts['body'],
            fg=self.colors['text_primary'],
            bg=self.colors['bg_panel'],
            selectcolor=self.colors['bg_panel'],
            activebackground=self.colors['bg_panel'],
            activeforeground=self.colors['text_primary'],
        )
        emulation_check.place(x=15, y=30)
        
        deadzone_label = tk.Label(
            emulation_frame,
            text="Deadzone",
            font=self.fonts['body'],
            fg=self.colors['text_secondary'],
            bg=self.colors['bg_panel'],
        )
        deadzone_label.place(x=15, y=65)
        
        self.deadzone_slider = tk.Scale(
            emulation_frame,
            from_=0,
            to=50,
            orient=tk.HORIZONTAL,
            length=190,
            bg=self.colors['bg_panel'],
            fg=self.colors['text_primary'],
            troughcolor=self.colors['bg_element'],
            highlightthickness=0,
            command=self.update_deadzone,
        )
        self.deadzone_slider.set(15)
        self.deadzone_slider.place(x=15, y=85)
        
        self.deadzone_value = tk.Label(
            emulation_frame,
            text="0.30",
            font=self.fonts['body'],
            fg=self.colors['text_primary'],
            bg=self.colors['bg_panel'],
        )
        self.deadzone_value.place(x=220, y=95)
        
        buttons_frame = tk.LabelFrame(
            parent,
            text="Controller Buttons",
            font=self.fonts['heading'],
            fg=self.colors['text_primary'],
            bg=self.colors['bg_panel'],
            foreground=self.colors['text_primary'],
        )
        buttons_frame.place(x=15, y=170, width=300, height=170)
        
        self.button_widgets = {}
        button_names = [
            ("Cross", 0, 30), ("Circle", 1, 30), ("Triangle", 2, 30), ("Square", 3, 30),
            ("L1", 0, 70), ("R1", 1, 70), ("L2", 2, 70), ("R2", 3, 70),
            ("Select", 0, 110), ("Start", 1, 110), ("L3", 2, 110), ("R3", 3, 110),
            ("PS", 1, 150),
        ]
        
        for name, col, y in button_names:
            x = 15 + col * 72
            btn = tk.Label(
                buttons_frame,
                text=name,
                font=self.fonts['small'],
                fg=self.colors['text_secondary'],
                bg=self.colors['bg_element'],
                width=10,
                height=2,
                relief=tk.FLAT,
            )
            btn.place(x=x, y=y)
            self.button_widgets[name.lower()] = btn
        
        self.hide_button = tk.Button(
            parent,
            text="Hide Controller",
            font=self.fonts['body'],
            bg=self.colors['accent_yellow'],
            fg='#000000',
            activebackground=self.colors['accent_yellow'],
            relief=tk.FLAT,
            cursor='hand2',
            command=self.toggle_hide,
        )
        self.hide_button.place(x=15, y=355, width=140, height=35)
        
        web_button = tk.Button(
            parent,
            text="Web Interface",
            font=self.fonts['body'],
            bg=self.colors['accent'],
            fg=self.colors['text_primary'],
            activebackground=self.colors['accent'],
            relief=tk.FLAT,
            cursor='hand2',
            command=lambda: __import__('webbrowser').open('http://localhost:8080'),
        )
        web_button.place(x=170, y=355, width=140, height=35)
    
    def create_status_bar(self):
        status_frame = tk.Frame(
            self.root,
            bg=self.colors['bg_panel'],
            height=25,
        )
        status_frame.pack(fill=tk.X, side=tk.BOTTOM)
        status_frame.pack_propagate(False)
        
        status_text = tk.Label(
            status_frame,
            text="  Web interface: http://localhost:8080  |  Run as root to hide controller",
            font=self.fonts['small'],
            fg=self.colors['text_dim'],
            bg=self.colors['bg_panel'],
            anchor='w',
        )
        status_text.pack(fill=tk.BOTH, expand=True)
    
    def draw_stick(self, canvas, x, y, color):
        canvas.delete('all')
        w = canvas.winfo_width()
        h = canvas.winfo_height()
        
        if w < 10 or h < 10:
            return
        
        cx = w // 2
        cy = h // 2
        r = min(w, h) // 2 - 15
        
        canvas.create_oval(
            cx - r, cy - r, cx + r, cy + r,
            outline='#3c3c50',
            width=2,
        )
        
        canvas.create_line(cx - r, cy, cx + r, cy, fill='#282840')
        canvas.create_line(cx, cy - r, cx, cy + r, fill='#282840')
        
        dot_x = cx + int(x * r)
        dot_y = cy + int(y * r)
        
        canvas.create_oval(
            dot_x - 8, dot_y - 8, dot_x + 8, dot_y + 8,
            fill=color,
            outline='',
        )
        
        canvas.create_oval(
            dot_x - 10, dot_y - 10, dot_x + 10, dot_y + 10,
            outline=color,
            width=2,
        )
    
    def update_ui(self):
        state = self.app.reader
        
        if state.connected:
            self.status_label.config(
                text="DualShock 3 connected",
                fg=self.colors['accent_green'],
            )
        else:
            self.status_label.config(
                text="Controller not connected",
                fg=self.colors['accent'],
            )
        
        lx = state.axes.get('left_x', 0)
        ly = state.axes.get('left_y', 0)
        rx = state.axes.get('right_x', 0)
        ry = state.axes.get('right_y', 0)
        
        self.draw_stick(self.left_stick_canvas, lx, ly, self.colors['accent_green'])
        self.draw_stick(self.right_stick_canvas, rx, ry, self.colors['accent'])
        
        self.left_stick_value.config(text=f"X: {lx:6.2f}  Y: {ly:6.2f}")
        self.right_stick_value.config(text=f"X: {rx:6.2f}  Y: {ry:6.2f}")
        
        button_state_map = {
            'cross': state.buttons.get('cross', False),
            'circle': state.buttons.get('circle', False),
            'triangle': state.buttons.get('triangle', False),
            'square': state.buttons.get('square', False),
            'l1': state.buttons.get('l1', False),
            'r1': state.buttons.get('r1', False),
            'l2': state.buttons.get('l2', False),
            'r2': state.buttons.get('r2', False),
            'select': state.buttons.get('select', False),
            'start': state.buttons.get('start', False),
            'l3': state.buttons.get('l3', False),
            'r3': state.buttons.get('r3', False),
            'ps': state.buttons.get('ps', False),
        }
        
        for name, widget in self.button_widgets.items():
            pressed = button_state_map.get(name, False)
            widget.config(
                bg=self.colors['accent'] if pressed else self.colors['bg_element'],
                fg=self.colors['text_primary'] if pressed else self.colors['text_secondary'],
            )
        
        self.root.after(16, self.update_ui)
    
    def toggle_emulation(self):
        self.app.emulation_enabled = self.emulation_var.get()
        if not self.app.emulation_enabled:
            self.app.emulator.release_all(self.app.bindings)
    
    def toggle_hide(self):
        if self.app.hider.is_hidden:
            success = self.app.hider.show()
            if success:
                self.hide_button.config(
                    text="Hide Controller",
                    bg=self.colors['accent_yellow'],
                )
        else:
            success = self.app.hider.hide()
            if success:
                self.hide_button.config(
                    text="Show Controller",
                    bg='#00c864',
                )
    
    def update_deadzone(self, value):
        dz = int(value) / 50.0
        self.app.deadzone = dz
        self.deadzone_value.config(text=f"{dz:.2f}")
    
    def run(self):
        self.root.mainloop()
