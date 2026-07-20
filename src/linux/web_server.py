# src/linux/web_server.py
import json
import http.server
import socketserver

class DualKeyHandler(http.server.SimpleHTTPRequestHandler):
    app = None
    
    def log_message(self, format, *args):
        pass
    
    def do_GET(self):
        if self.path == '/api':
            self._send_json(self.app.get_state())
        elif self.path == '/api/toggle_emu':
            self.app.emulation_enabled = not self.app.emulation_enabled
            if not self.app.emulation_enabled:
                self.app.emulator.release_all(self.app.bindings)
            self._send_json({'ok': True, 'emulation': self.app.emulation_enabled})
        elif self.path == '/api/toggle_hide':
            if self.app.hider.is_hidden:
                success = self.app.hider.show()
            else:
                success = self.app.hider.hide()
            self._send_json({'ok': success, 'hidden': self.app.hider.is_hidden})
        elif self.path == '/api/player/1':
            self.app.switch_player(1)
            self._send_json({'ok': True, 'player': 1})
        elif self.path == '/api/player/2':
            self.app.switch_player(2)
            self._send_json({'ok': True, 'player': 2})
        elif self.path == '/api/player/3':
            self.app.switch_player(3)
            self._send_json({'ok': True, 'player': 3})
        elif self.path == '/api/player/4':
            self.app.switch_player(4)
            self._send_json({'ok': True, 'player': 4})
        elif self.path == '/api/reset':
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
            self._send_json({'ok': True})
        elif self.path == '/' or self.path == '/index.html':
            self._serve_html()
        else:
            self.send_response(404)
            self.end_headers()
    
    def _send_json(self, data):
        self.send_response(200)
        self.send_header('Content-Type', 'application/json')
        self.send_header('Access-Control-Allow-Origin', '*')
        self.end_headers()
        self.wfile.write(json.dumps(data).encode())
    
    def _serve_html(self):
        html = '''<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>DualKey Linux Tester</title>
    <style>
        *{margin:0;padding:0;box-sizing:border-box}
        body{background:#f0f0f0;color:#000;font-family:Arial;min-height:100vh;padding:20px}
        .container{max-width:900px;margin:0 auto}
        h1{text-align:center;font-size:2em;margin-bottom:20px}
        .status-bar{display:flex;justify-content:center;gap:20px;margin:15px 0}
        .status-item{padding:8px 16px;background:#e0e0e0;border-radius:5px;font-size:0.9em}
        .players{display:flex;justify-content:center;gap:10px;margin:15px 0}
        .player-btn{padding:8px 16px;border:1px solid #999;background:#ddd;cursor:pointer;border-radius:3px}
        .player-btn.active{background:#00ff00;color:#000}
        .sticks{display:flex;justify-content:center;gap:60px;margin:30px 0}
        .stick-wrapper{text-align:center}
        .base{width:200px;height:200px;border:2px solid #999;border-radius:50%;position:relative;background:#fff}
        .dot{width:24px;height:24px;border-radius:50%;position:absolute;transition:all 0.05s}
        .left-dot{background:#00aa00}
        .right-dot{background:#cc0000}
        .coords{font-family:monospace;margin-top:8px;font-size:0.85em}
        .controls{display:flex;justify-content:center;gap:10px;margin:20px 0}
        button{padding:8px 16px;border:1px solid #999;background:#e0e0e0;cursor:pointer;border-radius:3px}
        button:hover{background:#d0d0d0}
        .data{background:#fff;padding:15px;border:1px solid #999;border-radius:5px;font-family:monospace;margin:20px 0;font-size:0.8em;max-height:300px;overflow-y:auto}
    </style>
</head>
<body>
    <div class="container">
        <h1>DualKey Linux Tester</h1>
        <div class="status-bar">
            <div class="status-item" id="connStatus">Searching</div>
            <div class="status-item" id="emuStatus">Emulation: Off</div>
            <div class="status-item" id="hideStatus">Visible</div>
        </div>
        <div class="players" id="players"></div>
        <div class="sticks">
            <div class="stick-wrapper">
                <div>Left Stick</div>
                <div class="base"><div class="dot left-dot" id="ls" style="left:88px;top:88px"></div></div>
                <div class="coords">X: <span id="lx">0.00</span> Y: <span id="ly">0.00</span></div>
            </div>
            <div class="stick-wrapper">
                <div>Right Stick</div>
                <div class="base"><div class="dot right-dot" id="rs" style="left:88px;top:88px"></div></div>
                <div class="coords">X: <span id="rx">0.00</span> Y: <span id="ry">0.00</span></div>
            </div>
        </div>
        <div class="controls">
            <button onclick="fetch('/api/toggle_emu')">Toggle Emulation</button>
            <button onclick="fetch('/api/toggle_hide')">Toggle Hide</button>
            <button onclick="fetch('/api/reset')">Reset</button>
        </div>
        <div class="data" id="data">Loading...</div>
    </div>
    <script>
        const players=document.getElementById('players');
        for(let i=1;i<=4;i++){
            const btn=document.createElement('button');
            btn.className='player-btn';
            btn.textContent='Player '+i;
            btn.onclick=()=>fetch('/api/player/'+i);
            players.appendChild(btn);
        }
        setInterval(()=>{
            fetch('/api').then(r=>r.json()).then(d=>{
                document.getElementById('connStatus').textContent=d.connected?'Connected':'Disconnected';
                document.getElementById('connStatus').style.background=d.connected?'#aaffaa':'#ffaaaa';
                document.getElementById('emuStatus').textContent='Emulation: '+(d.emulation?'On':'Off');
                document.getElementById('hideStatus').textContent=d.hidden?'Hidden':'Visible';
                document.querySelectorAll('.player-btn').forEach((b,i)=>{
                    b.classList.toggle('active',i+1===d.current_player);
                });
                if(d.connected){
                    document.getElementById('ls').style.left=(88+d.axes.left_x*70)+'px';
                    document.getElementById('ls').style.top=(88+d.axes.left_y*70)+'px';
                    document.getElementById('rs').style.left=(88+d.axes.right_x*70)+'px';
                    document.getElementById('rs').style.top=(88+d.axes.right_y*70)+'px';
                    document.getElementById('lx').textContent=d.axes.left_x.toFixed(2);
                    document.getElementById('ly').textContent=d.axes.left_y.toFixed(2);
                    document.getElementById('rx').textContent=d.axes.right_x.toFixed(2);
                    document.getElementById('ry').textContent=d.axes.right_y.toFixed(2);
                }
                document.getElementById('data').textContent=JSON.stringify(d,null,2);
            });
        },50);
    </script>
</body>
</html>'''
        self.send_response(200)
        self.send_header('Content-Type', 'text/html; charset=utf-8')
        self.end_headers()
        self.wfile.write(html.encode())

def start_web_server(app):
    DualKeyHandler.app = app
    with socketserver.TCPServer(('', 8080), DualKeyHandler) as httpd:
        httpd.serve_forever()
