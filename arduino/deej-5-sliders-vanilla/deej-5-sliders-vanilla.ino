
#include <SPI.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>

#define SCREEN_WIDTH 128 // OLED display width, in pixels
#define SCREEN_HEIGHT 64 // OLED display height, in pixels

// Declaration for an SSD1306 display connected to I2C (SDA, SCL pins)
// The pins for I2C are defined by the Wire-library. 
// On an arduino UNO:       A4(SDA), A5(SCL)
// On an arduino MEGA 2560: 20(SDA), 21(SCL)
// On an arduino LEONARDO:   2(SDA),  3(SCL), ...
#define OLED_RESET     4 // Reset pin # (or -1 if sharing Arduino reset pin)
#define SCREEN_ADDRESS 0x3C 
Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);

#define LOGO_HEIGHT   25
#define LOGO_WIDTH    25

#define MICLEVEL 1023

static const unsigned char PROGMEM talk_bmp[] =
{ 0x00, 0x7f, 0x00, 0x00, 
  0x03, 0xe3, 0xe0, 0x00, 
  0x07, 0x00, 0x70, 0x00, 
  0x0c, 0x00, 0x18, 0x00, 
  0x18, 0x00, 0x0c, 0x00, 
  0x30, 0x00, 0x06, 0x00, 
  0x63, 0xc0, 0x03, 0x00, 
  0x63, 0xc0, 0x03, 0x00, 
  0x43, 0xc0, 0x01, 0x00, 
  0xc3, 0xc0, 0x01, 0x80, 
  0xc3, 0x80, 0x01, 0x80, 
  0x83, 0x80, 0x00, 0x80, 
  0x81, 0xc0, 0x00, 0x80, 
  0x81, 0xc0, 0x00, 0x80, 
  0xc0, 0xe0, 0x01, 0x80, 
  0xc0, 0xf9, 0xe1, 0x80, 
  0x40, 0x7f, 0xe1, 0x00, 
  0x60, 0x1f, 0xe3, 0x00, 
  0x60, 0x07, 0xe3, 0x00, 
  0x30, 0x00, 0x06, 0x00, 
  0x18, 0x00, 0x0c, 0x00, 
  0x0c, 0x00, 0x18, 0x00, 
  0x07, 0x00, 0x70, 0x00, 
  0x03, 0xe3, 0xe0, 0x00, 
  0x00, 0x7f, 0x00, 0x00};

static const unsigned char PROGMEM game_bmp[] =
{ 0x00, 0x00, 0x03, 0x00, 
  0x00, 0x00, 0x03, 0x00, 
  0x00, 0x00, 0x03, 0x00, 
  0x00, 0x00, 0x07, 0x00, 
  0x00, 0x01, 0xfe, 0x00, 
  0x00, 0x07, 0xf8, 0x00, 
  0x00, 0x0f, 0x00, 0x00, 
  0x00, 0x0c, 0x00, 0x00, 
  0x00, 0x0c, 0x00, 0x00, 
  0x07, 0x0c, 0x70, 0x00, 
  0x0f, 0x88, 0xf8, 0x00, 
  0x1f, 0xe3, 0xfc, 0x00, 
  0x3f, 0xff, 0xfe, 0x00, 
  0x3c, 0xff, 0xde, 0x00, 
  0x7c, 0xff, 0xff, 0x00, 
  0x70, 0x3f, 0x77, 0x00, 
  0x70, 0x3f, 0xff, 0x80, 
  0xfc, 0xff, 0xdf, 0x80, 
  0xfc, 0xff, 0xff, 0x80, 
  0xff, 0xff, 0xff, 0x80, 
  0xff, 0xc1, 0xff, 0x80, 
  0xff, 0x00, 0x7f, 0x80, 
  0xfe, 0x00, 0x3f, 0x80, 
  0x7c, 0x00, 0x1f, 0x00, 
  0x38, 0x00, 0x0e, 0x00};
  
static const unsigned char PROGMEM edge_bmp[] =
{ 0x00, 0x00, 0x00, 0x00, 
  0x00, 0xff, 0x80, 0x00, 
  0x03, 0xff, 0xe0, 0x00, 
  0x07, 0x7f, 0x70, 0x00, 
  0x0c, 0xc9, 0x98, 0x00, 
  0x19, 0x99, 0x8c, 0x00, 
  0x3f, 0xff, 0xfe, 0x00, 
  0x3f, 0xff, 0xfe, 0x00, 
  0x63, 0x18, 0x63, 0x00, 
  0x00, 0x00, 0x00, 0x00, 
  0x00, 0x00, 0x00, 0x00, 
  0x5a, 0x5b, 0x2d, 0x00, 
  0x5a, 0x36, 0x35, 0x00, 
  0x36, 0x36, 0x36, 0x00, 
  0x00, 0x00, 0x00, 0x00, 
  0x00, 0x00, 0x00, 0x00, 
  0x63, 0x18, 0x63, 0x00, 
  0x3f, 0xff, 0xfe, 0x00, 
  0x3f, 0xff, 0xfe, 0x00, 
  0x19, 0x99, 0x8c, 0x00, 
  0x0c, 0xc9, 0x98, 0x00, 
  0x07, 0x7f, 0x70, 0x00, 
  0x03, 0xff, 0xe0, 0x00, 
  0x00, 0xff, 0x80, 0x00, 
  0x00, 0x00, 0x00, 0x00};
  
static const unsigned char PROGMEM music_bmp[] =
{ 0x00, 0x00, 0x00, 0x00, 
  0x00, 0x00, 0xfc, 0x00, 
  0x00, 0x1f, 0xfc, 0x00, 
  0x00, 0xff, 0xfc, 0x00, 
  0x01, 0xff, 0xfc, 0x00, 
  0x01, 0xff, 0xfc, 0x00, 
  0x01, 0xff, 0x8c, 0x00, 
  0x01, 0xf0, 0x0c, 0x00, 
  0x01, 0x80, 0x0c, 0x00, 
  0x01, 0x80, 0x0c, 0x00, 
  0x01, 0x80, 0x0c, 0x00, 
  0x01, 0x80, 0x0c, 0x00, 
  0x01, 0x80, 0x0c, 0x00, 
  0x01, 0x80, 0x0c, 0x00, 
  0x01, 0x80, 0x0c, 0x00, 
  0x01, 0x80, 0x0c, 0x00, 
  0x01, 0x80, 0xfc, 0x00, 
  0x01, 0x83, 0xfc, 0x00, 
  0x01, 0x83, 0xfc, 0x00, 
  0x1f, 0x83, 0xfc, 0x00, 
  0x7f, 0x83, 0xf8, 0x00, 
  0x7f, 0x81, 0xf0, 0x00, 
  0x7f, 0x80, 0x00, 0x00, 
  0x7f, 0x00, 0x00, 0x00, 
  0x3e, 0x00, 0x00, 0x00};
  
static const unsigned char PROGMEM empty_bmp[] =
{ 0x00, 0x00, 0x00, 0x00, 
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00,
  0x00, 0x00, 0x00, 0x00};




int x00 = 8;
int x1 = 5;
int y0 = 5;
int dx = 4;
int dy = 5;

const int NUM_SLIDERS = 4;

#define PIN_CALL A0
#define PIN_MUSIC A1
#define PIN_WEB A2
#define PIN_GAME A3

const int analogInputs[NUM_SLIDERS] = {PIN_CALL, PIN_MUSIC, PIN_WEB, PIN_GAME};

#define MUTELIGHT 3
#define BUTTON 4

int analogSliderValues[NUM_SLIDERS];
int analogSliderValues_old[NUM_SLIDERS];
int linearSliderValues[NUM_SLIDERS];

int analogSliderValues_old6[NUM_SLIDERS];
int analogSliderValues_old5[NUM_SLIDERS];
int analogSliderValues_old4[NUM_SLIDERS];
int analogSliderValues_old3[NUM_SLIDERS];
int analogSliderValues_old2[NUM_SLIDERS];
int analogSliderValues_old1[NUM_SLIDERS];

bool mute = false;

int x0 = x00;

void setup() {
  display.begin(SSD1306_SWITCHCAPVCC, SCREEN_ADDRESS);
  for (int i = 0; i < NUM_SLIDERS; i++) {
    pinMode(analogInputs[i], INPUT);
  }
  pinMode(MUTELIGHT, OUTPUT);    // Use Built-In LED for Indication
  pinMode(BUTTON, INPUT_PULLUP);      // Push-Button On Bread Board
  display.clearDisplay();
    
  display.clearDisplay();

  
  display.drawBitmap(
    9,
    5,
    talk_bmp, LOGO_WIDTH, LOGO_HEIGHT, 1);
  display.drawBitmap(
    9+25+5,
    5,
    music_bmp, LOGO_WIDTH, LOGO_HEIGHT, 1);
  display.drawBitmap(
    9+25+5+25+5,
    5,
    edge_bmp, LOGO_WIDTH, LOGO_HEIGHT, 1);
  display.drawBitmap(
    9+25+5+25+5+25+5,
    5,
    game_bmp, LOGO_WIDTH, LOGO_HEIGHT, 1);
  display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+dx,            display.height()-dy,  SSD1306_WHITE);
  display.drawLine(x0-dx+LOGO_WIDTH,  y0+LOGO_HEIGHT+dy,    x0-dx+LOGO_WIDTH, display.height()-dy,  SSD1306_WHITE);
  display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+LOGO_WIDTH-dx, y0+LOGO_HEIGHT+dy,    SSD1306_WHITE);
  display.drawLine(x0+dx,             display.height()-dy,  x0+LOGO_WIDTH-dx, display.height()-dy,  SSD1306_WHITE);

  x0 = x0 + LOGO_WIDTH + x1;
  display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+dx,            display.height()-dy,  SSD1306_WHITE);
  display.drawLine(x0-dx+LOGO_WIDTH,  y0+LOGO_HEIGHT+dy,    x0-dx+LOGO_WIDTH, display.height()-dy,  SSD1306_WHITE);
  display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+LOGO_WIDTH-dx, y0+LOGO_HEIGHT+dy,    SSD1306_WHITE);
  display.drawLine(x0+dx,             display.height()-dy,  x0+LOGO_WIDTH-dx, display.height()-dy,  SSD1306_WHITE);

  x0 = x0 + LOGO_WIDTH + x1;
  display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+dx,            display.height()-dy,  SSD1306_WHITE);
  display.drawLine(x0-dx+LOGO_WIDTH,  y0+LOGO_HEIGHT+dy,    x0-dx+LOGO_WIDTH, display.height()-dy,  SSD1306_WHITE);
  display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+LOGO_WIDTH-dx, y0+LOGO_HEIGHT+dy,    SSD1306_WHITE);
  display.drawLine(x0+dx,             display.height()-dy,  x0+LOGO_WIDTH-dx, display.height()-dy,  SSD1306_WHITE);

  x0 = x0 + LOGO_WIDTH + x1;
  display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+dx,            display.height()-dy,  SSD1306_WHITE);
  display.drawLine(x0-dx+LOGO_WIDTH,  y0+LOGO_HEIGHT+dy,    x0-dx+LOGO_WIDTH, display.height()-dy,  SSD1306_WHITE);
  display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+LOGO_WIDTH-dx, y0+LOGO_HEIGHT+dy,    SSD1306_WHITE);
  display.drawLine(x0+dx,             display.height()-dy,  x0+LOGO_WIDTH-dx, display.height()-dy,  SSD1306_WHITE);
  display.display();
//  delay(1000);
//  display.invertDisplay(true);
//  delay(1000);
//  display.invertDisplay(false);
//  delay(1000);
  delay(2000);
  Serial.begin(9600);
}

float height_ = 0;


void loop() {
  updateSliderValues();
  sendSliderValues(); // Actually send data (all the time)
  // printSliderValues(); // For debug
  delay(20);
}

bool buttonState = true;
bool prev_buttonState = true;
int count_to_sleep = 0;
void updateSliderValues() {
  int count_to_sleep_ = 0;
  for (int i = 0; i < NUM_SLIDERS; i++) {
    float read_ = (1023.-analogRead(analogInputs[i]))/1023.;
    analogSliderValues_old6[i] = analogSliderValues_old5[i];
    analogSliderValues_old5[i] = analogSliderValues_old4[i];
    analogSliderValues_old4[i] = analogSliderValues_old3[i];
    analogSliderValues_old3[i] = analogSliderValues_old2[i];
    analogSliderValues_old2[i] = analogSliderValues_old1[i];
    analogSliderValues_old1[i] = analogSliderValues_old[i];
    analogSliderValues_old[i] = analogSliderValues[i];
    analogSliderValues[i] = read_*read_*1023;
    linearSliderValues[i] = read_*1023;
    if (abs(analogSliderValues_old6[i] - analogSliderValues[i]) < 40) {
      count_to_sleep_ = min(count_to_sleep_+1,50);
    }
  }
  if (count_to_sleep_ >= NUM_SLIDERS) {
      count_to_sleep = min(count_to_sleep+1,101);
  }
  else {
    count_to_sleep = 0;
  }
  bool buttonState = digitalRead(BUTTON);  // store current state of pin 12
  if (!buttonState && buttonState != prev_buttonState){
    mute = !mute;
  }
  prev_buttonState = buttonState;
  digitalWrite(MUTELIGHT, mute);
}

void sendSliderValues() {
  String builtString = String("");
  
  for (int i = 0; i < NUM_SLIDERS; i++) {
    builtString += String((int)((analogSliderValues[i]+analogSliderValues_old[i])/2));

    if (i < NUM_SLIDERS) {
      builtString += String("|");
    }
  }
   builtString += String((int)!mute*MICLEVEL);
  
  Serial.println(builtString);
  
  if (count_to_sleep < 100) {
    
    x0 = x00;
    display.drawBitmap(
      9,
      5,
      talk_bmp, LOGO_WIDTH, LOGO_HEIGHT, 1);
    display.drawBitmap(
      9+25+5,
      5,
      music_bmp, LOGO_WIDTH, LOGO_HEIGHT, 1);
    display.drawBitmap(
      9+25+5+25+5,
      5,
      edge_bmp, LOGO_WIDTH, LOGO_HEIGHT, 1);
    display.drawBitmap(
      9+25+5+25+5+25+5,
      5,
      game_bmp, LOGO_WIDTH, LOGO_HEIGHT, 1);
    display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+dx,            display.height()-dy,  SSD1306_WHITE);
    display.drawLine(x0-dx+LOGO_WIDTH,  y0+LOGO_HEIGHT+dy,    x0-dx+LOGO_WIDTH, display.height()-dy,  SSD1306_WHITE);
    display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+LOGO_WIDTH-dx, y0+LOGO_HEIGHT+dy,    SSD1306_WHITE);
    display.drawLine(x0+dx,             display.height()-dy,  x0+LOGO_WIDTH-dx, display.height()-dy,  SSD1306_WHITE);
  
    x0 = x0 + LOGO_WIDTH + x1;
    display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+dx,            display.height()-dy,  SSD1306_WHITE);
    display.drawLine(x0-dx+LOGO_WIDTH,  y0+LOGO_HEIGHT+dy,    x0-dx+LOGO_WIDTH, display.height()-dy,  SSD1306_WHITE);
    display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+LOGO_WIDTH-dx, y0+LOGO_HEIGHT+dy,    SSD1306_WHITE);
    display.drawLine(x0+dx,             display.height()-dy,  x0+LOGO_WIDTH-dx, display.height()-dy,  SSD1306_WHITE);
  
    x0 = x0 + LOGO_WIDTH + x1;
    display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+dx,            display.height()-dy,  SSD1306_WHITE);
    display.drawLine(x0-dx+LOGO_WIDTH,  y0+LOGO_HEIGHT+dy,    x0-dx+LOGO_WIDTH, display.height()-dy,  SSD1306_WHITE);
    display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+LOGO_WIDTH-dx, y0+LOGO_HEIGHT+dy,    SSD1306_WHITE);
    display.drawLine(x0+dx,             display.height()-dy,  x0+LOGO_WIDTH-dx, display.height()-dy,  SSD1306_WHITE);
  
    x0 = x0 + LOGO_WIDTH + x1;
    display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+dx,            display.height()-dy,  SSD1306_WHITE);
    display.drawLine(x0-dx+LOGO_WIDTH,  y0+LOGO_HEIGHT+dy,    x0-dx+LOGO_WIDTH, display.height()-dy,  SSD1306_WHITE);
    display.drawLine(x0+dx,             y0+LOGO_HEIGHT+dy,    x0+LOGO_WIDTH-dx, y0+LOGO_HEIGHT+dy,    SSD1306_WHITE);
    display.drawLine(x0+dx,             display.height()-dy,  x0+LOGO_WIDTH-dx, display.height()-dy,  SSD1306_WHITE);
    
    int ymax_ = display.height()-dy-2;
    int ymin_ = y0+LOGO_HEIGHT+dy+2;
    int num_of_lines_max_ = ymax_-ymin_+1;
    int x0_ = x00;
  
    for (int i = 0; i < NUM_SLIDERS; i++) {
      height_ = ((float)analogSliderValues[i])/1023.*(float)(num_of_lines_max_);
      int height =  round(height_);
      //Serial.println(String(height_));
      for (int j = 0; j < height; j++) {
        display.drawLine(x0_+dx+2, display.height()-dy-j-2, x0_+LOGO_WIDTH-dx-2, display.height()-dy-j-2, SSD1306_WHITE);
      }
      for (int j = num_of_lines_max_; j >= height; j--) {
        display.drawLine(x0_+dx+2, display.height()-dy-j-2, x0_+LOGO_WIDTH-dx-2, display.height()-dy-j-2, SSD1306_BLACK);
      }
      x0_ = x0_ + LOGO_WIDTH + x1;
    }
  }
  else {
    display.clearDisplay();
  }
  display.display();
}

void printSliderValues() {
  for (int i = 0; i < NUM_SLIDERS; i++) {
    String printedString = String("Slider #") + String(i + 1) + String(": ") + String(analogSliderValues[i]) + String(" mV");
    Serial.write(printedString.c_str());

    if (i < NUM_SLIDERS - 1) {
      Serial.write(" | ");
    } else {
      Serial.write("\n");
    }
  }
}
