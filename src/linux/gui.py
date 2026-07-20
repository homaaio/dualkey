# src/linux/gui.py
import tkinter as tk
from tkinter import ttk, colorchooser

class DualKeyGUI:
    def __init__(self, app):
        self.app = app
        
        self.root = tk.Tk()
        self.root.title("DualKey - DualShock 3 Emulator")
        self.root.geometry("820x640")
        self.root.configure(bg="#f0f0f0")
        self.root.resizable(False, False)
        
        self.colors = {
            'bg': '#f0f0f0',
            'panel_bg': '#e0e0e0',
            'text': '#000000',
            'accent': '#cc0000',
            'green': '#00aa00',
            'yellow': '#cc8800',
            'white': '#ffffff',
            'dark_gray': '#555555',
            'light_gray': '#cccccc',
        }
        
        self.create_menu()
        self.create_player_indicators()
        self.create_keyboard_view()
        self.create_controls()
        self.create_status_bar()
        
        self.update_ui()
    
    def create_menu(self):
        menubar = tk.Menu(self.root)
        
        file_menu = tk.Menu(menubar, tearoff=0)
        file_menu.add_command(label="Save configuration (.hrc)", command=self.save_config)
        file_menu.add_command(label="Import configuration (.hrc)", command=self.load_config)
        file_menu.add_separator()
        file_menu.add_command(label="Exit", command=self.root.quit)
        menubar.add_cascade(label="File", menu=file_menu)
        
        settings_menu = tk.Menu(menubar, tearoff=0)
        settings_menu.add_command(label="Open settings", command=self.open_settings)
        settings_menu.add_command(label="Clear all settings", command=self.clear_settings)
        menubar.add_cascade(label="Settings", menu=settings_menu)
        
        self.root.config(menu=menubar)
    
    def create_player_indicators(self):
        indicator_frame = tk.Frame(self.root, bg=self.colors['panel_bg'], height=35)
        indicator_frame.pack(fill=tk.X, padx=10, pady=(5, 0))
        
        tk.Label(indicator_frame, text="Player:", bg=self.colors['panel_bg']).pack(side=tk.LEFT, padx=(5, 10))
        
        self.indicator_labels = []
        for i in range(4):
            color = self.app.indicator_colors[i] if i + 1 != self.app.current_player else '#00ff00'
            lbl = tk.Label(
                indicator_frame,
                text=str(i + 1),
                bg=color,
                fg='black' if i + 1 == self.app.current_player else 'white',
                width=3,
                height=1,
                relief=tk.RAISED,
                cursor='hand2'
            )
            lbl.bind('<Button-1>', lambda e, p=i+1: self.switch_player(p))
            lbl.pack(side=tk.LEFT, padx=2)
            self.indicator_labels.append(lbl)
    
    def create_keyboard_view(self):
        self.keyboard_frame = tk.Frame(self.root, bg=self.colors['white'], relief=tk.SUNKEN, bd=1)
        self.keyboard_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        self.key_canvas = tk.Canvas(self.keyboard_frame, bg=self.colors['white'], highlightthickness=0)
        self.key_canvas.pack(fill=tk.BOTH, expand=True)
        
        self.key_rects = {}
        self.key_labels = {}
        self.draw_keyboard()
    
    def draw_keyboard(self):
        keys = [
            (0x1B, 30, 20, 40, 35, "Esc"),
            (0x31, 90, 20, 40, 35, "1"),
            (0x32, 140, 20, 40, 35, "2"),
            (0x46, 330, 20, 40, 35, "F"),
            (0x47, 380, 20, 40, 35, "G"),
            (0x09, 30, 70, 55, 35, "Tab"),
            (0x51, 95, 70, 40, 35, "Q"),
            (0x57, 145, 70, 40, 35, "W"),
            (0x45, 195, 70, 40, 35, "E"),
            (0x52, 245, 70, 40, 35, "R"),
            (0x41, 95, 120, 40, 35, "A"),
            (0x53, 145, 120, 40, 35, "S"),
            (0x44, 195, 120, 40, 35, "D"),
            (0x10, 30, 170, 75, 35, "Shift"),
            (0x11, 30, 220, 60, 35, "Ctrl"),
            (0x20, 140, 270, 200, 35, "Space"),
            (0x26, 600, 170, 40, 35, "Up"),
            (0x25, 550, 220, 40, 35, "Left"),
            (0x27, 650, 220, 40, 35, "Right"),
            (0x28, 600, 270, 40, 35, "Down"),
            (0x0D, 700, 170, 60, 60, "Enter"),
        ]
        
        for vk, x, y, w, h, label in keys:
            rect = self.key_canvas.create_rectangle(
                x, y, x + w, y + h,
                fill=self.colors['light_gray'],
                outline=self.colors['dark_gray']
            )
            text = self.key_canvas.create_text(
                x + w // 2, y + h // 2,
                text=label,
                font=('Arial', 8, 'bold')
            )
            self.key_rects[vk] = rect
            self.key_labels[vk] = text
    
    def create_controls(self):
        control_frame = tk.Frame(self.root, bg=self.colors['panel_bg'])
        control_frame.pack(fill=tk.X, padx=10, pady=5)
        
        left_frame = tk.Frame(control_frame, bg=self.colors['panel_bg'])
        left_frame.pack(side=tk.LEFT, fill=tk.X, expand=True)
        
        self.emu_var = tk.BooleanVar(value=False)
        tk.Checkbutton(
            left_frame, text="Enable keyboard emulation",
            variable=self.emu_var, command=self.toggle_emulation,
            bg=self.colors['panel_bg']
        ).pack(anchor=tk.W, padx=5, pady=2)
        
        dz_frame = tk.Frame(left_frame, bg=self.colors['panel_bg'])
        dz_frame.pack(fill=tk.X, padx=5, pady=2)
        
        tk.Label(dz_frame, text="Deadzone:", bg=self.colors['panel_bg']).pack(side=tk.LEFT)
        
        self.dz_slider = tk.Scale(
            dz_frame, from_=0, to=50, orient=tk.HORIZONTAL,
            length=150, command=self.update_deadzone,
            bg=self.colors['panel_bg']
        )
        self.dz_slider.set(15)
        self.dz_slider.pack(side=tk.LEFT, padx=5)
        
        self.dz_value = tk.Label(dz_frame, text="0.30", bg=self.colors['panel_bg'], width=5)
        self.dz_value.pack(side=tk.LEFT)
        
        right_frame = tk.Frame(control_frame, bg=self.colors['panel_bg'])
        right_frame.pack(side=tk.RIGHT)
        
        self.hide_btn = tk.Button(
            right_frame, text="Hide Controller",
            command=self.toggle_hide, width=15
        )
        self.hide_btn.pack(side=tk.LEFT, padx=5)
        
        tk.Button(
            right_frame, text="Web Interface",
            command=lambda: __import__('webbrowser').open('http://localhost:8080'),
            width=15
        ).pack(side=tk.LEFT, padx=5)
    
    def create_status_bar(self):
        status_frame = tk.Frame(self.root, bg=self.colors['panel_bg'], height=22)
        status_frame.pack(fill=tk.X, side=tk.BOTTOM)
        
        self.status_label = tk.Label(
            status_frame,
            text="Not connected",
            bg=self.colors['panel_bg'],
            fg=self.colors['accent']
        )
        self.status_label.pack(side=tk.LEFT, padx=5)
        
        tk.Label(
            status_frame,
            text="Web: http://localhost:8080",
            bg=self.colors['panel_bg']
        ).pack(side=tk.RIGHT, padx=5)
    
    def update_ui(self):
        state = self.app.reader
        
        if state.connected:
            self.status_label.config(text="Connected", fg=self.colors['green'])
        else:
            self.status_label.config(text="Not connected", fg=self.colors['accent'])
        
        # Подсветка клавиш
        active_keys = set()
        for action, key in self.app.bindings.items():
            if action.startswith('left_stick') or action.startswith('right_stick'):
                continue
            btn_name = action.replace('dpad_', '')
            if btn_name in state.buttons and state.buttons[btn_name]:
                active_keys.add(self._key_name_to_vk(key))
        
        # Стики
        dz = self.app.deadzone
        lx = state.axes.get('left_x', 0)
        ly = state.axes.get('left_y', 0)
        rx = state.axes.get('right_x', 0)
        ry = state.axes.get('right_y', 0)
        
        if lx < -dz: active_keys.add(self._key_name_to_vk(self.app.bindings.get('left_stick_left', '')))
        elif lx > dz: active_keys.add(self._key_name_to_vk(self.app.bindings.get('left_stick_right', '')))
        if ly < -dz: active_keys.add(self._key_name_to_vk(self.app.bindings.get('left_stick_up', '')))
        elif ly > dz: active_keys.add(self._key_name_to_vk(self.app.bindings.get('left_stick_down', '')))
        if rx < -dz: active_keys.add(self._key_name_to_vk(self.app.bindings.get('right_stick_left', '')))
        elif rx > dz: active_keys.add(self._key_name_to_vk(self.app.bindings.get('right_stick_right', '')))
        if ry < -dz: active_keys.add(self._key_name_to_vk(self.app.bindings.get('right_stick_up', '')))
        elif ry > dz: active_keys.add(self._key_name_to_vk(self.app.bindings.get('right_stick_down', '')))
        
        for vk, rect in self.key_rects.items():
            if vk in active_keys:
                self.key_canvas.itemconfig(rect, fill='lightcoral', outline='red')
                self.key_canvas.itemconfig(self.key_labels[vk], fill='white')
            else:
                self.key_canvas.itemconfig(rect, fill=self.colors['light_gray'], outline=self.colors['dark_gray'])
                self.key_canvas.itemconfig(self.key_labels[vk], fill='black')
        
        self.root.after(16, self.update_ui)
    
    def _key_name_to_vk(self, key_name):
        mapping = {
            'w': 0x57, 'a': 0x41, 's': 0x53, 'd': 0x44,
            'up': 0x26, 'down': 0x28, 'left': 0x25, 'right': 0x27,
            'space': 0x20, 'e': 0x45, 'q': 0x51, 'r': 0x52,
            'shift': 0x10, 'ctrl': 0x11, '1': 0x31, '2': 0x32,
            'f': 0x46, 'g': 0x47, 'tab': 0x09, 'enter': 0x0D, 'esc': 0x1B,
        }
        return mapping.get(key_name, 0)
    
    def switch_player(self, player):
        self.app.switch_player(player)
        for i, lbl in enumerate(self.indicator_labels):
            if i + 1 == player:
                lbl.config(bg='#00ff00', fg='black')
            else:
                lbl.config(bg=self.app.indicator_colors[i], fg='white')
    
    def toggle_emulation(self):
        self.app.emulation_enabled = self.emu_var.get()
        if not self.app.emulation_enabled:
            self.app.emulator.release_all(self.app.bindings)
    
    def toggle_hide(self):
        if self.app.hider.is_hidden:
            self.app.hider.show()
            self.hide_btn.config(text="Hide Controller")
        else:
            self.app.hider.hide()
            self.hide_btn.config(text="Show Controller")
    
    def update_deadzone(self, value):
        dz = int(value) / 50.0
        self.app.deadzone = dz
        self.dz_value.config(text=f"{dz:.2f}")
    
    def save_config(self):
        from tkinter import filedialog
        filename = filedialog.asksaveasfilename(
            defaultextension=".hrc",
            filetypes=[("DualKey Config", "*.hrc")]
        )
        if filename:
            self.app.config_file = filename
            self.app.save_config()
    
    def load_config(self):
        from tkinter import filedialog
        filename = filedialog.askopenfilename(
            filetypes=[("DualKey Config", "*.hrc")]
        )
        if filename:
            self.app.config_file = filename
            self.app.load_config()
            self.dz_slider.set(int(self.app.deadzone * 50))
            self.dz_value.config(text=f"{self.app.deadzone:.2f}")
    
    def open_settings(self):
        SettingsDialog(self.root, self.app)
    
    def clear_settings(self):
        from tkinter import messagebox
        if messagebox.askyesno("DualKey", "Reset all settings to defaults?"):
            self.app.bindings = self.app.bindings.__class__()
            self.app.bindings.update({
                'left_stick_up': 'w', 'left_stick_down': 's',
                'left_stick_left': 'a', 'left_stick_right': 'd',
                'right_stick_up': 'up', 'right_stick_down': 'down',
                'right_stick_left': 'left', 'right_stick_right': 'right',
                'dpad_up': 'up', 'dpad_down': 'down',
                'dpad_left': 'left', 'dpad_right': 'right',
                'cross': 'space', 'circle': 'e', 'triangle': 'q',
                'square': 'r', 'l1': 'shift', 'r1': 'ctrl',
                'l2': '1', 'r2': '2', 'l3': 'f', 'r3': 'g',
                'select': 'tab', 'start': 'enter', 'ps_button': 'esc',
            })
            self.app.deadzone = 0.3
            self.dz_slider.set(15)
            self.dz_value.config(text="0.30")
    
    def run(self):
        self.root.mainloop()


class SettingsDialog:
    def __init__(self, parent, app):
        self.app = app
        self.dialog = tk.Toplevel(parent)
        self.dialog.title("DualKey Settings")
        self.dialog.geometry("450x400")
        self.dialog.resizable(False, False)
        self.dialog.transient(parent)
        self.dialog.grab_set()
        
        notebook = ttk.Notebook(self.dialog)
        notebook.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        self.bindings_frame = ttk.Frame(notebook)
        self.sensitivity_frame = ttk.Frame(notebook)
        self.indicators_frame = ttk.Frame(notebook)
        
        notebook.add(self.bindings_frame, text="Key Bindings")
        notebook.add(self.sensitivity_frame, text="Sensitivity")
        notebook.add(self.indicators_frame, text="Player Indicators")
        
        self.build_bindings_tab()
        self.build_sensitivity_tab()
        self.build_indicators_tab()
        
        self.dialog.wait_window()
    
    def build_bindings_tab(self):
        canvas = tk.Canvas(self.bindings_frame)
        scrollbar = ttk.Scrollbar(self.bindings_frame, orient=tk.VERTICAL, command=canvas.yview)
        scroll_frame = ttk.Frame(canvas)
        
        scroll_frame.bind("<Configure>", lambda e: canvas.configure(scrollregion=canvas.bbox("all")))
        canvas.create_window((0, 0), window=scroll_frame, anchor="nw")
        canvas.configure(yscrollcommand=scrollbar.set)
        
        canvas.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        actions = [
            ("Left Stick Up", "left_stick_up"),
            ("Left Stick Down", "left_stick_down"),
            ("Left Stick Left", "left_stick_left"),
            ("Left Stick Right", "left_stick_right"),
            ("Right Stick Up", "right_stick_up"),
            ("Right Stick Down", "right_stick_down"),
            ("Right Stick Left", "right_stick_left"),
            ("Right Stick Right", "right_stick_right"),
            ("Cross", "cross"),
            ("Circle", "circle"),
            ("Triangle", "triangle"),
            ("Square", "square"),
            ("L1", "l1"),
            ("R1", "r1"),
            ("L2", "l2"),
            ("R2", "r2"),
            ("L3", "l3"),
            ("R3", "r3"),
            ("Select", "select"),
            ("Start", "start"),
            ("PS Button", "ps_button"),
            ("D-Pad Up", "dpad_up"),
            ("D-Pad Down", "dpad_down"),
            ("D-Pad Left", "dpad_left"),
            ("D-Pad Right", "dpad_right"),
        ]
        
        self.bind_buttons = {}
        for i, (label, action) in enumerate(actions):
            tk.Label(scroll_frame, text=label).grid(row=i, column=0, sticky=tk.W, padx=5, pady=2)
            btn = tk.Button(
                scroll_frame,
                text=self.app.bindings.get(action, 'None').upper(),
                width=12,
                command=lambda a=action, b=None: self.start_binding(a)
            )
            btn.grid(row=i, column=1, padx=5, pady=2)
            self.bind_buttons[action] = btn
    
    def start_binding(self, action):
        btn = self.bind_buttons[action]
        btn.config(text="Press key...", bg='lightyellow')
        
        def on_key(event):
            key = event.keysym.lower()
            self.app.bindings[action] = key
            btn.config(text=key.upper(), bg='SystemButtonFace')
            self.dialog.unbind('<Key>')
        
        self.dialog.bind('<Key>', on_key)
        btn.focus_set()
    
    def build_sensitivity_tab(self):
        frame = ttk.Frame(self.sensitivity_frame)
        frame.pack(fill=tk.BOTH, expand=True, padx=20, pady=20)
        
        ttk.Label(frame, text="Stick Deadzone").pack(anchor=tk.W)
        
        self.dz_var = tk.IntVar(value=int(self.app.deadzone * 100))
        dz_scale = ttk.Scale(
            frame, from_=0, to=100,
            variable=self.dz_var,
            orient=tk.HORIZONTAL,
            command=lambda v: self.dz_value_label.config(text=f"{float(v)/100:.2f}")
        )
        dz_scale.pack(fill=tk.X, pady=5)
        
        self.dz_value_label = ttk.Label(frame, text=f"{self.app.deadzone:.2f}")
        self.dz_value_label.pack()
        
        ttk.Button(
            frame, text="Apply",
            command=lambda: self.apply_sensitivity()
        ).pack(pady=10)
    
    def apply_sensitivity(self):
        self.app.deadzone = self.dz_var.get() / 100.0
    
    def build_indicators_tab(self):
        frame = ttk.Frame(self.indicators_frame)
        frame.pack(fill=tk.BOTH, expand=True, padx=20, pady=20)
        
        self.ind_enabled_var = tk.BooleanVar(value=self.app.indicators_enabled)
        ttk.Checkbutton(
            frame, text="Enable player indicators",
            variable=self.ind_enabled_var
        ).pack(anchor=tk.W, pady=5)
        
        ttk.Label(frame, text="Mode:").pack(anchor=tk.W, pady=(10, 0))
        self.ind_mode_var = tk.StringVar(value=['Static', 'Blink All', 'Running Light', 'Alternating'][self.app.indicator_mode])
        mode_combo = ttk.Combobox(
            frame,
            textvariable=self.ind_mode_var,
            values=['Static', 'Blink All', 'Running Light', 'Alternating'],
            state='readonly'
        )
        mode_combo.pack(fill=tk.X, pady=5)
        
        ttk.Label(frame, text="Speed (ms):").pack(anchor=tk.W, pady=(10, 0))
        self.ind_speed_var = tk.IntVar(value=self.app.indicator_speed)
        speed_scale = ttk.Scale(
            frame, from_=100, to=2000,
            variable=self.ind_speed_var,
            orient=tk.HORIZONTAL,
            command=lambda v: speed_label.config(text=f"{int(float(v))} ms")
        )
        speed_scale.pack(fill=tk.X, pady=5)
        speed_label = ttk.Label(frame, text=f"{self.app.indicator_speed} ms")
        speed_label.pack()
        
        ttk.Label(frame, text="Indicator Colors:").pack(anchor=tk.W, pady=(15, 5))
        
        self.ind_color_buttons = []
        colors_frame = ttk.Frame(frame)
        colors_frame.pack(fill=tk.X)
        
        for i in range(4):
            btn_frame = ttk.Frame(colors_frame)
            btn_frame.pack(side=tk.LEFT, padx=5)
            
            ttk.Label(btn_frame, text=f"P{i+1}").pack()
            
            color_btn = tk.Button(
                btn_frame,
                bg=self.app.indicator_colors[i],
                width=4,
                height=2,
                command=lambda idx=i: self.choose_color(idx)
            )
            color_btn.pack()
            self.ind_color_buttons.append(color_btn)
        
        ttk.Button(
            frame, text="Apply",
            command=lambda: self.apply_indicators()
        ).pack(pady=15)
    
    def choose_color(self, index):
        color = colorchooser.askcolor(
            color=self.app.indicator_colors[index],
            title=f"Choose color for Player {index+1}"
        )
        if color[1]:
            self.app.indicator_colors[index] = color[1]
            self.ind_color_buttons[index].config(bg=color[1])
    
    def apply_indicators(self):
        self.app.indicators_enabled = self.ind_enabled_var.get()
        self.app.indicator_mode = ['Static', 'Blink All', 'Running Light', 'Alternating'].index(self.ind_mode_var.get())
        self.app.indicator_speed = self.ind_speed_var.get()
        self.app.save_config()
