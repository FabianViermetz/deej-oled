#!/usr/bin/env python3
"""Dependency-free source-distribution contract checks."""
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
firmware = (ROOT / "arduino/DeejFeedbackMini/DeejFeedbackMini.ino").read_text(encoding="utf-8")
serial = (ROOT / "src/DeejFeedback/SerialController.cs").read_text(encoding="utf-8")
models = (ROOT / "src/DeejFeedback/Models.cs").read_text(encoding="utf-8")
config_store = (ROOT / "src/DeejFeedback/ConfigStore.cs").read_text(encoding="utf-8")

required = [
    "DeejFeedback.sln", "README.md", "build-windows.ps1",
    "src/DeejFeedback/DeejFeedback.csproj", "src/DeejFeedback/MainWindow.xaml",
    "src/DeejFeedback/MainWindow.xaml.cs", "src/DeejFeedback/AudioEngine.cs",
    "src/DeejFeedback/BrandIcon.cs",
    "arduino/DeejFeedbackMini/DeejFeedbackMini.ino",
    "arduino/DeejFeedbackMini/LegacyIcons.h",
]
for relative in required:
    assert (ROOT / relative).is_file(), f"missing {relative}"

for pin, value in {"MUTE_LED_PIN": "3", "MIC_BUTTON_PIN": "4"}.items():
    assert re.search(rf"{pin}\s*=\s*{value}\s*;", firmware), f"wrong {pin}"
assert "{A0, A1, A2, A3}" in firmware
assert "Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, -1)" in firmware
assert 'Serial.print(F("F"))' in firmware
assert "sendInputFrame();" in firmware
assert "stableButtonState == LOW ? 1 : 0" in firmware
assert 'fields[0] == "F"' in serial and "MicButtonStateReceived" in serial
assert 'B|MIC|' in firmware and 'A|MIC|' in firmware
assert "buttonCommandAwaitingAck" in firmware and "MicButtonCommandReceived" in serial
assert '$"S|{values}|{state}|{displaySleepSeconds}|{heartbeatMs}|{feedbackMs}"' in serial

def mapped(raw: int, inverted=True, exponent=2.0) -> float:
    normalized = max(0.0, min(1.0, raw / 1023.0))
    if inverted:
        normalized = 1.0 - normalized
    return normalized**exponent

assert abs(mapped(0) - 1.0) < 1e-9
assert abs(mapped(1023)) < 1e-9
assert 0.24 < mapped(512) < 0.26
assert "CurveExponent { get; set; } = 2.0" in models
assert "Inverted { get; set; } = true" in models
assert "using System.IO;" in config_store
icons = (ROOT / "arduino/DeejFeedbackMini/LegacyIcons.h").read_text(encoding="utf-8")
for icon in ("talk_bmp", "music_bmp", "edge_bmp", "game_bmp"):
    assert icon in icons
for position in ("{9, 39, 69, 99}", "{12, 42, 72, 102}"):
    assert position in firmware
main_window = (ROOT / "src/DeejFeedback/MainWindow.xaml.cs").read_text(encoding="utf-8")
app_xaml = (ROOT / "src/DeejFeedback/App.xaml").read_text(encoding="utf-8")
project = (ROOT / "src/DeejFeedback/DeejFeedback.csproj").read_text(encoding="utf-8")
assert "NotifyIcon" in main_window and "ContextMenuStrip" in main_window
assert "Enable privacy mute" in main_window and "ShowFromTray" in main_window
assert "ComboBoxItem" in app_xaml and "TabItem" in app_xaml and "Expander" in app_xaml
assert "<UseWindowsForms>true</UseWindowsForms>" in project
print("Package contract checks passed")
assert "ContinuousFaderUpdates" in models
assert "channel.HasRawValue && Math.Abs" in main_window
assert "_config.ContinuousFaderUpdates && _serial.IsConnected" in main_window
assert "DateTime.UtcNow - _lastFaderFrameUtc" in main_window
assert "heartbeatIntervalMs" in firmware
assert "pcTimeoutMs" in firmware
assert "Math.Clamp(_config.ControllerHeartbeatMs, 50, 5000)" in main_window
assert "Math.Clamp(_config.FeedbackIntervalMs, 100, 2000)" in main_window
print("Continuous mode and timing source contracts passed (not runtime tests)")
