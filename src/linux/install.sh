# src/linux/install.sh
#!/bin/bash
echo "Installing ReJoy Linux dependencies..."
sudo apt-get update
sudo apt-get install -y python3-pip python3-tk
sudo modprobe uinput
sudo chmod 666 /dev/uinput
sudo usermod -a -G input $USER
sudo bash -c 'echo "KERNEL==\"uinput\", MODE=\"0666\"" > /etc/udev/rules.d/99-uinput.rules'
pip3 install python-uinput
echo "Done. Reboot or run: newgrp input"
