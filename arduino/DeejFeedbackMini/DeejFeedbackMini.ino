#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include "LegacyIcons.h"

// Existing hardware ----------------------------------------------------------
constexpr uint8_t SCREEN_WIDTH = 128;
constexpr uint8_t SCREEN_HEIGHT = 64;
constexpr uint8_t SCREEN_ADDRESS = 0x3C;
// The original sketch assigned D4 to both OLED reset and the button. Most
// I2C SSD1306 modules reset themselves, so no Arduino reset pin is used here.
Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, -1);

constexpr uint8_t NUM_FADERS = 4;
constexpr uint8_t FADER_PINS[NUM_FADERS] = {A0, A1, A2, A3};
constexpr uint8_t MUTE_LED_PIN = 3;
constexpr uint8_t MIC_BUTTON_PIN = 4;
constexpr uint32_t SERIAL_BAUD = 115200;

// Protocol/state -------------------------------------------------------------
// Arduino -> PC: F|rawA0|rawA1|rawA2|rawA3|buttonPressed
//               B|MIC|sequence
//               HELLO|DeejFeedbackMini|1
// PC -> Arduino: S|vol0|vol1|vol2|vol3|privacy|sleepSeconds
//               A|MIC|sequence
// privacy: 0 = active, 1 = confirmed muted, 2 = uncertain/disconnected

uint16_t rawValues[NUM_FADERS] = {0, 0, 0, 0};
uint16_t sentValues[NUM_FADERS] = {65535, 65535, 65535, 65535};
uint8_t actualPercent[NUM_FADERS] = {0, 0, 0, 0};
uint8_t privacyState = 2;
uint16_t displaySleepSeconds = 30;
uint16_t heartbeatIntervalMs = 1000;
uint32_t pcTimeoutMs = 2500;

bool lastButtonReading = HIGH;
bool stableButtonState = HIGH;
uint32_t lastButtonChangeMs = 0;
uint32_t lastSendMs = 0;
uint32_t lastPcMessageMs = 0;
uint32_t lastInteractionMs = 0;
uint32_t lastDrawMs = 0;
bool displayAwake = true;
uint16_t buttonSequence = 0;
uint16_t pendingButtonSequence = 0;
bool buttonCommandAwaitingAck = false;
uint32_t lastButtonCommandMs = 0;

char receiveBuffer[80];
uint8_t receiveLength = 0;

void sendInputFrame();
void sendButtonCommand();

void setup() {
  pinMode(MUTE_LED_PIN, OUTPUT);
  pinMode(MIC_BUTTON_PIN, INPUT_PULLUP);
  for (uint8_t i = 0; i < NUM_FADERS; ++i) pinMode(FADER_PINS[i], INPUT);

  Serial.begin(SERIAL_BAUD);
  display.begin(SSD1306_SWITCHCAPVCC, SCREEN_ADDRESS);
  display.clearDisplay();
  display.setTextColor(SSD1306_WHITE);
  display.setTextSize(1);
  display.setCursor(18, 24);
  display.print(F("Deej Feedback"));
  display.setCursor(31, 38);
  display.print(F("connecting"));
  display.display();
  delay(250);
  Serial.println(F("HELLO|DeejFeedbackMini|1"));
  lastInteractionMs = millis();
}

void loop() {
  readFaders();
  readButton();
  readSerial();
  retryButtonCommand();
  updateConnectionState();
  updateLed();
  updateDisplay();
}

void readFaders() {
  bool changed = false;
  for (uint8_t i = 0; i < NUM_FADERS; ++i) {
    // Four-sample average. Raw, non-inverted ADC values are sent; calibration,
    // inversion and response curve belong to the PC configuration.
    uint16_t sum = 0;
    for (uint8_t sample = 0; sample < 4; ++sample) sum += analogRead(FADER_PINS[i]);
    rawValues[i] = sum / 4;
    if (abs((int)rawValues[i] - (int)sentValues[i]) >= 3) changed = true;
  }

  const bool heartbeatDue = millis() - lastSendMs >= heartbeatIntervalMs;
  if (millis() - lastSendMs < 20) return;
  if (!changed && !heartbeatDue) return;
  sendInputFrame();
  if (changed) { lastInteractionMs = millis(); wakeDisplay(); }
}

void readButton() {
  const bool reading = digitalRead(MIC_BUTTON_PIN);
  if (reading != lastButtonReading) { lastButtonReading = reading; lastButtonChangeMs = millis(); }
  if (millis() - lastButtonChangeMs < 35 || reading == stableButtonState) return;
  stableButtonState = reading;
  // Send both press and release immediately, allowing repeated presses even
  // when they occur faster than the one-second heartbeat.
  sendInputFrame();
  if (stableButtonState == LOW) {
    ++buttonSequence;
    if (buttonSequence == 0) ++buttonSequence;
    pendingButtonSequence = buttonSequence;
    buttonCommandAwaitingAck = true;
    sendButtonCommand();
    lastInteractionMs = millis();
    wakeDisplay();
  }
}

void sendInputFrame() {
  Serial.print(F("F"));
  for (uint8_t i = 0; i < NUM_FADERS; ++i) {
    Serial.print('|'); Serial.print(rawValues[i]); sentValues[i] = rawValues[i];
  }
  Serial.print('|');
  Serial.println(stableButtonState == LOW ? 1 : 0);
  lastSendMs = millis();
}

void sendButtonCommand() {
  Serial.print(F("B|MIC|"));
  Serial.println(pendingButtonSequence);
  lastButtonCommandMs = millis();
}

void retryButtonCommand() {
  if (buttonCommandAwaitingAck && millis() - lastButtonCommandMs >= 120) {
    sendButtonCommand();
  }
}

void readSerial() {
  while (Serial.available()) {
    const char c = (char)Serial.read();
    if (c == '\r') continue;
    if (c == '\n') {
      receiveBuffer[receiveLength] = '\0';
      if (receiveLength > 0) parseStatus(receiveBuffer);
      receiveLength = 0;
    } else if (receiveLength < sizeof(receiveBuffer) - 1) {
      receiveBuffer[receiveLength++] = c;
    } else {
      receiveLength = 0;
    }
  }
}

void parseStatus(char* line) {
  char* token = strtok(line, "|");
  if (!token) return;
  if (strcmp(token, "A") == 0) {
    token = strtok(nullptr, "|");
    if (!token || strcmp(token, "MIC") != 0) return;
    token = strtok(nullptr, "|");
    if (!token) return;
    const uint16_t acknowledged = atoi(token);
    if (buttonCommandAwaitingAck && acknowledged == pendingButtonSequence) {
      buttonCommandAwaitingAck = false;
    }
    return;
  }
  if (strcmp(token, "S") != 0) return;
  uint8_t incomingVolumes[NUM_FADERS];
  for (uint8_t i = 0; i < NUM_FADERS; ++i) {
    token = strtok(nullptr, "|"); if (!token) return;
    incomingVolumes[i] = constrain(atoi(token), 0, 100);
  }
  token = strtok(nullptr, "|"); if (!token) return;
  const uint8_t incomingPrivacy = constrain(atoi(token), 0, 2);
  token = strtok(nullptr, "|"); if (!token) return;
  displaySleepSeconds = constrain(atoi(token), 0, 3600);
  token = strtok(nullptr, "|");
  if (token) heartbeatIntervalMs = constrain(atol(token), 50, 5000);
  token = strtok(nullptr, "|");
  if (token) pcTimeoutMs = max(2500UL, (uint32_t)constrain(atol(token), 100, 2000) * 3UL);

  bool changed = incomingPrivacy != privacyState;
  privacyState = incomingPrivacy;
  for (uint8_t i = 0; i < NUM_FADERS; ++i) {
    changed |= incomingVolumes[i] != actualPercent[i];
    actualPercent[i] = incomingVolumes[i];
  }
  lastPcMessageMs = millis();
  if (changed) { lastInteractionMs = millis(); wakeDisplay(); }
}

void updateConnectionState() {
  if (lastPcMessageMs == 0 || millis() - lastPcMessageMs > pcTimeoutMs) privacyState = 2;
}

void updateLed() {
  if (privacyState == 1) digitalWrite(MUTE_LED_PIN, HIGH);            // confirmed mute
  else if (privacyState == 0) digitalWrite(MUTE_LED_PIN, LOW);       // active
  else digitalWrite(MUTE_LED_PIN, (millis() / 180) % 2);             // uncertain/disconnected
}

void wakeDisplay() {
  if (!displayAwake) { display.ssd1306_command(SSD1306_DISPLAYON); displayAwake = true; }
}

void updateDisplay() {
  if (displaySleepSeconds > 0 && millis() - lastInteractionMs > (uint32_t)displaySleepSeconds * 1000UL) {
    if (displayAwake) { display.ssd1306_command(SSD1306_DISPLAYOFF); displayAwake = false; }
    return;
  }
  if (!displayAwake || millis() - lastDrawMs < 100) return;
  lastDrawMs = millis();
  display.clearDisplay();

  // Original icons and positions from the previous controller sketch.
  const unsigned char* icons[NUM_FADERS] = {talk_bmp, music_bmp, edge_bmp, game_bmp};
  constexpr int16_t iconX[NUM_FADERS] = {9, 39, 69, 99};
  constexpr int16_t frameX[NUM_FADERS] = {12, 42, 72, 102};
  constexpr int16_t frameTop = 35;
  constexpr int16_t frameBottom = 59;
  constexpr int16_t innerTop = 37;
  constexpr int16_t innerBottom = 57;

  for (uint8_t i = 0; i < NUM_FADERS; ++i) {
    display.drawBitmap(iconX[i], 5, icons[i], LEGACY_ICON_WIDTH, LEGACY_ICON_HEIGHT, SSD1306_WHITE);

    // Same frame geometry as the old sketch: x0+dx to x0+width-dx,
    // y0+height+dy to display.height()-dy.
    display.drawRect(frameX[i], frameTop, 18, frameBottom - frameTop + 1, SSD1306_WHITE);

    // Filled height is now the confirmed Windows value instead of the locally
    // calculated Arduino command value.
    const uint8_t barHeight = map(actualPercent[i], 0, 100, 0, innerBottom - innerTop + 1);
    if (barHeight > 0) {
      display.fillRect(frameX[i] + 2, innerBottom - barHeight + 1, 14, barHeight, SSD1306_WHITE);
    }

    // Small side marker for the physical fader position. It does not alter the
    // legacy icon or bar placement.
    const uint8_t markerY = innerTop + map(rawValues[i], 0, 1023, 0, innerBottom - innerTop);
    display.drawPixel(frameX[i] - 2, markerY, SSD1306_WHITE);
    display.drawPixel(frameX[i] - 1, markerY, SSD1306_WHITE);
  }
  display.display();
}
