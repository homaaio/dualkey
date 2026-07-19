using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DualKey
{
    public class WebServer
    {
        private HttpListener listener;
        private Func<string> getJoystickData;

        public WebServer(Func<string> dataProvider)
        {
            getJoystickData = dataProvider;
        }

        public async Task StartAsync()
        {
            listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/");
            listener.Start();

            Console.WriteLine("Web interface: http://localhost:8080");

            while (listener.IsListening)
            {
                try
                {
                    var context = await listener.GetContextAsync();
                    ProcessRequest(context);
                }
                catch { }
            }
        }

        public void Stop()
        {
            listener?.Stop();
            listener?.Close();
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            string path = context.Request.Url.AbsolutePath;

            if (path == "/" || path == "/index.html")
                ServeHtml(context);
            else if (path == "/api")
                ServeApi(context);
        }

        private void ServeHtml(HttpListenerContext context)
        {
            string html = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>DualKey Web Tester</title>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body {
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
            color: white;
            font-family: 'Segoe UI', Arial, sans-serif;
            min-height: 100vh;
            padding: 20px;
        }
        .container { max-width: 900px; margin: 0 auto; }
        h1 { 
            color: #e94560; 
            text-align: center; 
            font-size: 2.5em; 
            margin-bottom: 30px;
            text-shadow: 0 0 10px rgba(233,69,96,0.5);
        }
        .status {
            text-align: center;
            font-size: 1.2em;
            margin: 20px 0;
            padding: 10px;
            background: rgba(255,255,255,0.1);
            border-radius: 10px;
        }
        .sticks-container {
            display: flex;
            justify-content: center;
            gap: 60px;
            margin: 40px 0;
            flex-wrap: wrap;
        }
        .stick-wrapper {
            text-align: center;
        }
        .stick-label {
            font-size: 1.1em;
            margin-bottom: 15px;
            color: #aaa;
        }
        .stick-base {
            width: 200px;
            height: 200px;
            border: 3px solid rgba(255,255,255,0.2);
            border-radius: 50%;
            position: relative;
            background: rgba(0,0,0,0.3);
        }
        .stick-dot {
            width: 28px;
            height: 28px;
            border-radius: 50%;
            position: absolute;
            transition: all 0.05s linear;
        }
        .left-stick {
            background: #00ff88;
            box-shadow: 0 0 20px rgba(0,255,136,0.5);
        }
        .right-stick {
            background: #e94560;
            box-shadow: 0 0 20px rgba(233,69,96,0.5);
        }
        .info-panel {
            background: rgba(255,255,255,0.05);
            border-radius: 15px;
            padding: 20px;
            margin: 20px 0;
        }
        .coordinates {
            display: flex;
            justify-content: space-around;
            margin: 20px 0;
            font-family: 'Consolas', monospace;
        }
        .coord-item {
            text-align: center;
            padding: 10px;
            background: rgba(0,0,0,0.3);
            border-radius: 10px;
        }
        .coord-value {
            font-size: 1.5em;
            color: #e94560;
        }
        .buttons-grid {
            display: grid;
            grid-template-columns: repeat(auto-fill, minmax(70px, 1fr));
            gap: 10px;
            margin: 20px 0;
        }
        .btn-indicator {
            padding: 10px;
            text-align: center;
            background: rgba(255,255,255,0.1);
            border-radius: 8px;
            border: 2px solid rgba(255,255,255,0.1);
            transition: all 0.1s;
        }
        .btn-indicator.active {
            background: #e94560;
            border-color: #e94560;
            box-shadow: 0 0 15px rgba(233,69,96,0.5);
            transform: scale(1.05);
        }
        .raw-data {
            background: rgba(0,0,0,0.4);
            padding: 15px;
            border-radius: 10px;
            font-family: 'Consolas', monospace;
            font-size: 0.85em;
            overflow-x: auto;
            white-space: pre-wrap;
            margin-top: 20px;
        }
    </style>
</head>
<body>
    <div class='container'>
        <h1>🎮 DualKey Web Tester</h1>
        
        <div class='status' id='status'>
            <span id='statusText'>🔍 Поиск DualShock 3...</span>
        </div>

        <div class='sticks-container'>
            <div class='stick-wrapper'>
                <div class='stick-label'>Левый стик</div>
                <div class='stick-base'>
                    <div class='stick-dot left-stick' id='leftStick' style='left: 86px; top: 86px;'></div>
                </div>
                <div class='coord-item' style='margin-top: 10px;'>
                    <div>X: <span class='coord-value' id='leftX'>0.00</span></div>
                    <div>Y: <span class='coord-value' id='leftY'>0.00</span></div>
                </div>
            </div>

            <div class='stick-wrapper'>
                <div class='stick-label'>Правый стик</div>
                <div class='stick-base'>
                    <div class='stick-dot right-stick' id='rightStick' style='left: 86px; top: 86px;'></div>
                </div>
                <div class='coord-item' style='margin-top: 10px;'>
                    <div>X: <span class='coord-value' id='rightX'>0.00</span></div>
                    <div>Y: <span class='coord-value' id='rightY'>0.00</span></div>
                </div>
            </div>
        </div>

        <div class='info-panel'>
            <h3>🔘 Кнопки</h3>
            <div class='buttons-grid' id='buttonsGrid'></div>
        </div>

        <div class='raw-data' id='rawData'>Ожидание данных...</div>
    </div>

    <script>
        const buttonNames = ['✕','○','△','□','L1','R1','L2','R2','Select','Start','L3','R3','PS'];
        
        function createButtons(count) {
            const grid = document.getElementById('buttonsGrid');
            grid.innerHTML = '';
            for(let i = 0; i < count; i++) {
                const btn = document.createElement('div');
                btn.className = 'btn-indicator';
                btn.id = 'btn-' + i;
                btn.textContent = buttonNames[i] || 'B' + i;
                grid.appendChild(btn);
            }
        }
        
        createButtons(13);

        setInterval(() => {
            fetch('/api')
                .then(r => r.json())
                .then(d => {
                    if(d.connected) {
                        document.getElementById('statusText').innerHTML = 
                            '✅ DualShock 3 подключен';
                        
                        const lx = 86 + d.leftStick.x * 70;
                        const ly = 86 + d.leftStick.y * 70;
                        const rx = 86 + d.rightStick.x * 70;
                        const ry = 86 + d.rightStick.y * 70;
                        
                        document.getElementById('leftStick').style.left = lx + 'px';
                        document.getElementById('leftStick').style.top = ly + 'px';
                        document.getElementById('rightStick').style.left = rx + 'px';
                        document.getElementById('rightStick').style.top = ry + 'px';
                        
                        document.getElementById('leftX').textContent = d.leftStick.x.toFixed(2);
                        document.getElementById('leftY').textContent = d.leftStick.y.toFixed(2);
                        document.getElementById('rightX').textContent = d.rightStick.x.toFixed(2);
                        document.getElementById('rightY').textContent = d.rightStick.y.toFixed(2);
                        
                        // Buttons
                        for(let i = 0; i < 13; i++) {
                            const btn = document.getElementById('btn-' + i);
                            if(btn) {
                                if(d.buttons & (1 << i)) {
                                    btn.classList.add('active');
                                } else {
                                    btn.classList.remove('active');
                                }
                            }
                        }
                    } else {
                        document.getElementById('statusText').innerHTML = 
                            '❌ Джойстик не подключен';
                    }
                    
                    document.getElementById('rawData').textContent = 
                        JSON.stringify(d, null, 2);
                })
                .catch(e => {
                    document.getElementById('statusText').innerHTML = 
                        '⚠️ Нет соединения с сервером';
                });
        }, 50);
    </script>
</body>
</html>";

            byte[] buffer = Encoding.UTF8.GetBytes(html);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();
        }

        private void ServeApi(HttpListenerContext context)
        {
            string json = getJoystickData();
            byte[] buffer = Encoding.UTF8.GetBytes(json);
            context.Response.ContentType = "application/json";
            context.Response.ContentLength64 = buffer.Length;
            context.Response.OutputStream.Write(buffer, 0, buffer.Length);
            context.Response.Close();
        }
    }
}
