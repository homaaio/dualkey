# DualKey - Gamepad to Keyboard Emulator for Windows & Linux
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-8.0-blue)](https://dotnet.microsoft.com/)
[![Windows](https://img.shields.io/badge/Windows-10%2B-brightgreen)]()

Transform your DualShock 3 controller into a keyboard. Test inputs, bind buttons to keys, and hide the controller from the system so games think you are using a keyboard.

## Features

- Controller testing - real-time visualization of sticks and buttons
- Keyboard emulation - play games that do not support gamepads
- Controller hiding - remove the device from the system (games detect a keyboard instead)
- Deadzone adjustment - precise stick calibration
- Web interface - monitor controller state from a browser
- Native Windows GUI - lightweight desktop application

## Quick Start

### Requirements

- Windows 10/11 (64-bit)
- .NET 8.0 SDK (https://dotnet.microsoft.com/download/dotnet/8.0)
- DualShock 3/4 controller (USB or Bluetooth)

### Build and Run

Run build.bat to compile the project, then run.bat to launch.

After launching:
- The desktop GUI opens automatically
- Web interface available at: http://localhost:8080

## Usage

### Testing the Controller

1. Connect your Controller
2. Launch DualKey
3. Move the sticks to see visualization
4. Press buttons to see their state

### Keyboard Emulation

1. Enable the "Keyboard Emulation" checkbox
2. Default bindings:
   - Left stick: WASD
   - Right stick: Arrow keys
   - Buttons: Space, E, Q, R
3. Launch any game and play with your controller

### Hiding the Controller

1. Run the application as Administrator
2. Click "Hide Controller"
3. The system will no longer detect the controller
4. Games will treat your inputs as keyboard presses

## Default Key Bindings

| Action | Key |
|--------|-----|
| Left Stick Up | W |
| Left Stick Down | S |
| Left Stick Left | A |
| Left Stick Right | D |
| Right Stick Up | Up Arrow |
| Right Stick Down | Down Arrow |
| Right Stick Left | Left Arrow |
| Right Stick Right | Right Arrow |
| Cross | Space |
| Circle | E |
| Triangle | Q |
| Square | R |
| L1 | Shift |
| R1 | Ctrl |
| L2 | 1 |
| R2 | 2 |
| L3 | F |
| R3 | G |
| Select | Tab |
| Start | Enter |
| PS Button | Escape |

## Project Structure
```
DualKey/
├── icons/              # Application icons
│   └── app.ico
├── src/                # Source code
│   ├── DualKey.csproj    # Project file
│   ├── Program.cs      # Entry point
│   ├── MainForm.cs     # Main GUI window
│   ├── JoystickEmulator.cs  # Keyboard emulation
│   ├── JoystickHider.cs     # Device hiding
│   └── WebServer.cs         # Web interface
├── build.bat           # Build script
├── run.bat             # Run script
├── README.md           # Documentation
├── LICENSE             # MIT License
└── .gitignore          # Git ignore rules
```
## Technical Details

- Language: C# / .NET 8.0
- GUI Framework: Windows Forms
- Controller Input: XInput (via SharpDX.XInput)
- Keyboard Emulation: WinAPI (keybd_event)
- Device Management: PowerShell via WMI
- Web Server: HttpListener

## Known Limitations

- Administrator privileges required for controller hiding
- Some anti-cheat software may block keyboard emulation
- DualShock 3 may require third-party drivers on Windows
- Only one controller supported at a time

## Troubleshooting

**Controller not detected:**
- Ensure DualShock 3 is properly connected
- Install required drivers (ScpToolkit or DsHidMini) — DualKey talks to controllers via XInput only, so Windows must expose the device as an Xbox-style controller, not a raw HID device
- If using DsHidMini: install ViGEmBus alongside it, then open the DsHidMini control app (DSHMC) and set that specific controller's mode to **XInput** (it isn't the default) — unplug and replug after changing it
- Confirm it works at the Windows level first via "Set up USB game controllers" (`joy.cpl`) before assuming DualKey itself is at fault
- Check Device Manager for unrecognized devices

**Keyboard emulation not working:**
- Verify the "Keyboard Emulation" checkbox is enabled
- Try running the application as Administrator
- Some applications may require the window to be in focus
- Games that only read raw input, or that run anti-cheat, may not see `keybd_event`-based key presses at all — this is a Windows limitation, not something DualKey can work around

**Cannot hide controller:**
- Run DualKey as Administrator
- Check if PowerShell execution policy allows scripts
- Manually disable the device in Device Manager
- If DualKey crashes while the controller is hidden, it's automatically re-enabled on the next launch/close; if it's still disabled, re-enable it manually in Device Manager

**"Windows protected your PC" / SmartScreen warning on launch:**
- The executable isn't code-signed, so this warning is expected on first run — click "More info" → "Run anyway"

**Web interface not accessible:**
- Ensure port 8080 is not blocked by firewall
- Verify the application is running
- Try accessing http://127.0.0.1:8080

## Building from Source

git clone https://github.com/yourusername/DualKey.git
cd DualKey
build.bat

The compiled executable will be located in the build directory.

## Contributing

1. Fork the repository
2. Create a feature branch (git checkout -b feature/amazing)
3. Commit your changes (git commit -m 'Add amazing feature')
4. Push to the branch (git push origin feature/amazing)
5. Open a Pull Request

## Roadmap

- [ ] Custom key binding configuration
- [ ] Save and load binding profiles
- [ ] Support for additional controllers (DualShock 4, Xbox, etc.)
- [ ] Mouse emulation for right stick
- [ ] Macro recording
- [ ] Tray icon with quick settings
- [ ] Auto-hide on application launch

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- Microsoft WinMM API for joystick input
- Windows Forms for the GUI framework
- All contributors and testers

## Support

If you find this project useful, please consider giving it a star on GitHub.

For issues and feature requests, please use the GitHub Issues page.
