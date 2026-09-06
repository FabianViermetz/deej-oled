# Deej Feedback
Windows audio control for a four-fader Arduino controller with OLED and microphone button.

Version: **0.2.0-preview**. This is a source preview, not a signed or production-validated release. Windows build and hardware acceptance testing are required before distributing binaries.

## Features
- English UI with dark controls, tray menu and optional Windows sign-in startup.
- Four process groups with editable volume limits and response curves.
- Optional **Continuously apply fader values**: reapply received hardware levels every PC feedback cycle, including applications started later.
- Independent controller heartbeat and PC feedback intervals.
- Windows input-endpoint privacy mute with LED feedback.
- Original OLED icons and slider positions.
- Legacy deej YAML import and persistent JSON settings.

## Hardware
| Component | Connection |
| --- | --- |
| Communication fader | A0 |
| Music fader | A1 |
| Browser fader | A2 |
| Games fader | A3 |
| Momentary microphone button | D4 to GND, internal pull-up |
| Mute LED | D3 with suitable series resistor |
| SSD1306 128×64 OLED | I²C address 0x3C; A4 SDA / A5 SCL on ATmega328P Pro/Pro Mini |
| USB serial | Adapter matched to the board voltage |

Select the actual board, processor and clock in Arduino IDE. Pro Mini variants differ in voltage and clock speed. The sketch uses no separate OLED reset pin. Keep `LegacyIcons.h` next to the sketch.

## Build and run
On Windows 10/11 x64, install the .NET 8 SDK, extract this archive and run `build-windows.bat`. This does not require enabling PowerShell scripts. Dependencies are restored from NuGet.

Output: `release/DeejFeedback/DeejFeedback.exe`. The executable is self-contained; target machines do not need a separate .NET runtime. Use `start-development.bat` for development.

For firmware, install **Adafruit GFX Library** and **Adafruit SSD1306**, open `arduino/DeejFeedbackMini/DeejFeedbackMini.ino`, select your board/port and upload. Close the desktop app and serial monitor before uploading. Start the app and select the serial port at **115200 baud**.

## Upgrade to this version
1. Exit the running app through its tray menu.
2. Extract into a new folder and build the desktop app.
3. Flash the included firmware to enable configurable controller heartbeat timing.
4. Start the app, open **Controller & settings**, configure the options and click **Save settings**.

User settings are preserved in `%APPDATA%/DeejFeedback/config.json`. Existing custom channel names are preserved, even when they are not English. Windows-provided device names and native error messages may follow the OS language.

## Continuous fader mode
Enable **Continuously apply fader values** and save. The app reapplies calibrated/curved fader values on each feedback cycle. This includes newly created playback sessions. It overwrites external volume adjustments while active.

The option defaults to off when absent from an existing configuration. The first received sample is now accepted even when the ADC value is zero. No default zero-valued sample is applied before real controller data arrives. Continuous writes pause when the serial connection is closed or input frames become stale.

## Timing
| Setting | Range | Default | Effect |
| --- | --- | --- | --- |
| Controller heartbeat | 50–5000 ms | 1000 ms | Unchanged input frames from Arduino to PC |
| PC feedback interval | 100–2000 ms | 500 ms | Audio refresh, continuous volume application and PC-to-Arduino status |
| Display sleep | 0–3600 s | 30 s | 0 keeps OLED on |

Smaller intervals give more frequent updates and higher traffic/CPU use. A 100 ms PC interval targets 10 updates/s; 2000 ms targets 0.5 updates/s. Actual timing depends on Windows audio calls, serial throughput and OLED processing. Fader changes are sent independently of the idle heartbeat (at most one regular change frame per 20 ms); button press/release events remain immediate after debounce.

The Arduino adjusts its PC timeout to at least three feedback intervals (minimum 2.5 seconds). Slower feedback also delays polling-based detection of externally changed mute states and newly attached devices. The settings therefore control more than visual animation speed.

## Configuration and startup
### CPU usage and application discovery
Output devices, audio-session handles and process names are cached. Fader writes target cached sessions directly, without scanning devices or looking up processes on every movement. The output inventory is rebuilt approximately every three seconds (on the next feedback tick), independently of the configured feedback interval. Newly started applications can therefore take up to three seconds plus one feedback interval to become controllable. Continuous mode applies the current fader values after discovery; otherwise move the fader after discovery.

The Assignments tab loads its application list once when first opened. Use **Refresh applications** to scan again immediately. This is a list of applications with audio sessions, not every running Windows process; play audio in a missing application and refresh. Selection is preserved when possible. The list no longer displays continuously changing volume percentages.

Mixer bars and input-level lists are updated only while the Mixer tab is visible. Unchanged input rows and connection/privacy indicators are not rebuilt. Audio feedback, controller communication and microphone monitoring continue in the tray. Microphone discovery and mute verification retain their existing feedback-cycle timing; they are deliberately not covered by the slower output cache.

This reduces repeated discovery and UI allocation; CPU savings have not been measured on Windows. Compare Task Manager usage with the same heartbeat/feedback settings, both on the Mixer tab and in the tray. Check that a newly launched mapped app becomes controllable, the refresh button retains selection, and microphone mute/LED feedback still works after reconnecting an input device.

### Settings location
Configuration is stored per user, not beside the executable. This is a no-install application, but not a fully portable configuration layout.
Autostart uses `HKCU/Software/Microsoft/Windows/CurrentVersion/Run`. Enable and save it again if you move the EXE. X/minimize hides the window; use **Exit** in the tray menu to stop the application. Running several copies simultaneously is not supported.

Legacy YAML imports process groups and connection settings. This hardware's new firmware expects 115200 baud, so select it after importing an old 9600-baud file. The original sketch's inversion and quadratic curve are represented by the new channel defaults.

## Microphone behavior and limits
Privacy mute controls Windows input endpoints, not the mute button inside a meeting app. The confirmed state means the monitored endpoint properties report muted. It does **not** prove silence at a remote participant or across every audio path.

The implementation reads actual endpoint mute states on every feedback cycle, including while in the tray. External Windows mute/unmute changes update the GUI and controller feedback on the next cycle. No input volume/gain is changed. Hardware/exclusive paths, virtual routing, app audio sharing, driver behavior and application-specific mute states require separate testing. An endpoint peak reading of zero is not proof of mute.

The button reads the monitored inputs before toggling. If all report muted, it explicitly unmutes all of them, including inputs muted externally. Otherwise it requests mute for all monitored inputs. Every write is followed by a readback. Mixed states, read/write failures and missing inputs are reported as mixed/unknown. A mixed group is muted first; a second press unmutes the confirmed group.

LED: on = all monitored inputs report muted; off = all report unmuted; blinking = mixed/unknown/disconnected. Mute is not continuously enforced. Newly connected inputs are monitored at the next poll, but retain their own mute state until an explicit button action. With all-input monitoring disabled, only the current default communications input is monitored and controlled. Exiting does not change mute. Restarting reads the current Windows state. The obsolete `MuteNewCaptureDevicesWhileLocked` configuration field is ignored and removed on the next save. Do not use this preview as a guaranteed privacy barrier.

## Serial protocol
New timing fields are appended to the status frame:
```text
Arduino -> PC: F|adc0|adc1|adc2|adc3|buttonPressed
Arduino -> PC: B|MIC|sequence
Arduino -> PC: HELLO|DeejFeedbackMini|1
PC -> Arduino: A|MIC|sequence
PC -> Arduino: S|vol0|vol1|vol2|vol3|privacy|sleepSeconds|heartbeatMs|feedbackMs
```
Volumes are 0–100; privacy is 0 (available), 1 (confirmed endpoint mute), 2 (unknown).
Firmware accepts status frames without timing fields using defaults. Older firmware ignores appended timing fields; flash this version for timing control.
Button requests are retransmitted until acknowledged. The current protocol deduplicates the last sequence within one connection, but is not a durable exactly-once protocol across resets/disconnections.

## Validation and release checklist
Run `python tests/verify_package.py` for basic source-contract checks. These checks do not compile or execute WPF or firmware.

Before publishing a stable binary:
- Compile on Windows and compile firmware for the exact Arduino board.
- Verify zero/startup samples, continuous-mode on/off, app launches and reconnects.
- Test both interval endpoints, persisted settings and rejected invalid input.
- Test short/repeated button presses, USB loss and sleep/wake.
- Inspect UI at 100%, 150% and 200% scaling and test tray exit/autostart.
- Review mute behavior using all actual microphones and meeting applications.
- Mute in Windows, then press the hardware button: verify unmute in Windows, GUI and LED. Repeat with external unmute after Deej mute: it must remain unmuted.
- With multiple inputs, verify mixed-state feedback, first-press mute and second-press unmute. Check disconnect/reconnect, no inputs, and restart while muted. Input gain must stay unchanged.

Known limitations: no complete soft-takeover implementation; polling on the UI thread; grouped volume is an average; firmware buffers a single pending button command; no signed installer; no single-instance guard. These are preview limitations, not production guarantees.

## Credits and dependencies
Inspired by [omriharel/deej](https://github.com/omriharel/deej). This is an independent application; the slider mark in this package is a custom motif, not the official deej logo.
The OLED bitmaps came from the user-supplied original sketch.

Dependencies: NAudio (MIT), YamlDotNet (MIT), System.IO.Ports (.NET), Adafruit GFX and SSD1306. Review their upstream licenses and preserve required notices when distributing binaries. Confirm rights to the supplied OLED artwork and select a license for this project's own code before public redistribution.
