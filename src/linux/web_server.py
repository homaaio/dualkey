# src/linux/web_server.py
import json
import http.server
import socketserver

class ReJoyHandler(http.server.SimpleHTTPRequestHandler):
    app = None
    
    def do_GET(self):
        if self.path == '/api':
            self.send_response(200)
            self.send_header('Content-Type', 'application/json')
            self.send_header('Access-Control-Allow-Origin', '*')
            self.end_headers()
            
            state = self.app.get_state()
            self.wfile.write(json.dumps(state).encode())
        
        elif self.path == '/api/toggle_emu':
            self.app.emulation_enabled = not self.app.emulation_enabled
            if not self.app.emulation_enabled:
                self.app.emulator.release_all(self.app.bindings)
            self.send_response(200)
            self.end_headers()
            self.wfile.write(b'ok')
        
        elif self.path == '/api/toggle_hide':
            if self.app.hider.is_hidden:
                self.app.hider.show()
            else:
                self.app.hider.hide()
            self.send_response(200)
            self.end_headers()
            self.wfile.write(b'ok')
        
        elif self.path == '/api/reset':
            self.app.bindings = {
                'left_stick_up': 'w', 'left_stick_down': 's',
                'left_stick_left': 'a', 'left_stick_right': 'd',
                'right_stick_up': 'up', 'right_stick_down': 'down',
                'right_stick_left': 'left', 'right_stick_right': 'right',
                'cross': 'space', 'circle': 'e', 'triangle': 'q',
                'square': 'r', 'l1': 'shift', 'r1': 'ctrl',
                'l2': '1', 'r2': '2', 'l3': 'f', 'r3': 'g',
                'select': 'tab', 'start': 'enter', 'ps_button': 'esc',
            }
            self.send_response(200)
            self.end_headers()
            self.wfile.write(b'ok')
        
        elif self.path == '/' or self.path == '/index.html':
            self.send_response(200)
            self.send_header('Content-Type', 'text/html; charset=utf-8')
            self.end_headers()
            self.wfile.write(self.get_html().encode())
        
        else:
            super().do_GET()
    
    def get_html(self):
        return '''<!DOCTYPE html>
<html>
<head>
    <meta charset="UTF-8">
    <title>DualKey Linux Tester</title>
    <style>
        *{margin:0;padding:0;box-sizing:border-box}
        body{background:#121224;color:#fff;font-family:Segoe UI,Arial;min-height:100vh;padding:20px}
        .container{max-width:900px;margin:0 auto}
        h1{color:#e94560;text-align:center;font-size:2.5em;margin-bottom:30px;font-weight:bold}
        .status{text-align:center;font-size:1.2em;margin:20px 0;padding:10px;background:#1c1c34;border-radius:10px}
        .sticks{display:flex;justify-content:center;gap:60px;margin:40px 0}
        .stick-wrapper{text-align:center}
        .stick-label{font-size:1.1em;margin-bottom:15px;color:#9696aa}
        .base{width:200px;height:200px;border:3px solid #3c3c50;border-radius:50%;position:relative;background:#282840}
        .dot{width:28px;height:28px;border-radius:50%;position:absolute;transition:all 0.05s}
        .left-dot{background:#00ff88;box-shadow:0 0 20px rgba(0,255,136,0.5)}
        .right-dot{background:#e94560;box-shadow:0 0 20px rgba(233,69,96,0.5)}
        .coords{font-family:Consolas,monospace;margin-top:10px;color:#9696aa}
        .data{background:#1c1c34;padding:20px;border-radius:15px;font-family:Consolas,monospace;margin:20px 0;white-space:pre-wrap;font-size:0.85em}
    </style>
</head>
<body>
    <div class="container">
        <h1>DualKey Linux Tester</h1>
        <div class="status" id="status">Searching for DualShock 3...</div>
        <div class="sticks">
            <div class="stick-wrapper">
                <div class="stick-label">Left Stick</div>
                <div class="base"><div class="dot left-dot" id="ls" style="left:86px;top:86px"></div></div>
                <div class="coords">X: <span id="lx">0.00</span> Y: <span id="ly">0.00</span></div>
            </div>
            <div class="stick-wrapper">
                <div class="stick-label">Right Stick</div>
                <div class="base"><div class="dot right-dot" id="rs" style="left:86px;top:86px"></div></div>
                <div class="coords">X: <span id="rx">0.00</span> Y: <span id="ry">0.00</span></div>
            </div>
        </div>
        <div class="data" id="data">Loading...</div>
    </div>
    <script>
        setInterval(()=>{
            fetch('/api').then(r=>r.json()).then(d=>{
                document.getElementById('status').textContent=d.connected?'Connected':'Disconnected';
                document.getElementById('status').style.color=d.connected?'#00ff88':'#e94560';
                if(d.connected){
                    document.getElementById('ls').style.left=(86+d.axes.left_x*70)+'px';
                    document.getElementById('ls').style.top=(86+d.axes.left_y*70)+'px';
                    document.getElementById('rs').style.left=(86+d.axes.right_x*70)+'px';
                    document.getElementById('rs').style.top=(86+d.axes.right_y*70)+'px';
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

def start_web_server(app):
    DualKeyHandler.app = app
    
    with socketserver.TCPServer(('', 8080), DualKeyHandler) as httpd:
        httpd.serve_forever()
