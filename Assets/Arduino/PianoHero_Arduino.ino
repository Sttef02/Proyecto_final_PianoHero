const byte BUTTON_COUNT = 4;
const byte LED_COUNT = 4;
const byte BUTTON_PINS[BUTTON_COUNT] = {6, 7, 8, 9};   // verde, rojo, amarillo, azul
const byte LED_PINS[LED_COUNT] = {2, 3, 4, 5};         // se encienden en este orden
const byte KNOB_PIN = A0;

// Con resistencia externa a tierra los botones leen HIGH al presionar. Si se conectan directo a GND, poner true.
const bool USE_INTERNAL_PULLUP = false;
// true = muestra en el Monitor Serie que pin cambia al presionar cada boton (para revisar el cableado).
const bool DIAGNOSTIC_MODE = false;

const long BAUD_RATE = 115200;
const unsigned long DEBOUNCE_US = 8000;
const unsigned long KNOB_INTERVAL_MS = 15;
const int KNOB_MIN_CHANGE = 6;
const int KNOB_SMOOTHING = 4;
const unsigned long BLINK_INTERVAL_MS = 120;
const unsigned long HEARTBEAT_INTERVAL_MS = 1000;
const unsigned long STARTUP_SWEEP_MS = 150;
const unsigned long STARTUP_PAUSE_MS = 200;
const unsigned long SETTLE_MS = 20;
const byte BLINK_COMMAND = 5;

const char READY_MESSAGE[] = "READY";
const char KNOB_PREFIX[] = "P:";
const char BUTTON_PREFIX = 'B';
const char BUTTON_SEPARATOR = ':';
const char PRESSED_FLAG = '1';
const char RELEASED_FLAG = '0';
const char HEARTBEAT = 'H';
const char LED_COMMAND = 'L';
const char STATUS_REQUEST = '?';
const char FIRST_DIGIT = '0';
const char LINE_FEED = '\n';
const char CARRIAGE_RETURN = '\r';
const byte FIRST_BUTTON_NUMBER = 1;
const byte COMMAND_VALUE_INDEX = 1;
const byte MIN_LED_COMMAND_LENGTH = 2;
const byte COMMAND_BUFFER_SIZE = 8;
const int NEVER_SENT = -100;

const byte FIRST_DIAGNOSTIC_PIN = 2;
const byte LAST_DIAGNOSTIC_PIN = 13;
const unsigned long DIAGNOSTIC_START_MS = 1500;
const unsigned long DIAGNOSTIC_LED_MS = 900;
const unsigned long DIAGNOSTIC_LOOP_MS = 5;
const int DIAGNOSTIC_KNOB_CHANGE = 30;

bool pressed[BUTTON_COUNT];
bool idleLevel[BUTTON_COUNT];
unsigned long lastButtonChangeUs[BUTTON_COUNT];

int smoothedKnob = 0;
int lastSentKnob = NEVER_SENT;
unsigned long lastKnobReadMs = 0;
unsigned long lastHeartbeatMs = 0;

byte litLeds = 0;
bool blinking = false;
bool blinkOn = false;
unsigned long lastBlinkMs = 0;

char command[COMMAND_BUFFER_SIZE];
byte commandLength = 0;

int lastPinLevel[LAST_DIAGNOSTIC_PIN + 1];
int lastDiagnosticKnob = NEVER_SENT;

// Presionado = el pin esta distinto a como estaba al encender, asi sirve con resistencia a tierra o a 5V.
// Por eso no hay que presionar botones mientras arranca.
bool isButtonDown(byte button) {
  return digitalRead(BUTTON_PINS[button]) != idleLevel[button];
}

void sendButton(byte button) {
  Serial.print(BUTTON_PREFIX);
  Serial.print(button + FIRST_BUTTON_NUMBER);
  Serial.print(BUTTON_SEPARATOR);
  Serial.println(pressed[button] ? PRESSED_FLAG : RELEASED_FLAG);
}

void sendKnob() {
  Serial.print(KNOB_PREFIX);
  Serial.println(smoothedKnob);
  lastSentKnob = smoothedKnob;
}

void setupPins() {
  for (byte i = 0; i < BUTTON_COUNT; i++) pinMode(BUTTON_PINS[i], USE_INTERNAL_PULLUP ? INPUT_PULLUP : INPUT);
  for (byte i = 0; i < LED_COUNT; i++) pinMode(LED_PINS[i], OUTPUT);
  delay(SETTLE_MS);
  for (byte i = 0; i < BUTTON_COUNT; i++) {
    idleLevel[i] = digitalRead(BUTTON_PINS[i]);
    pressed[i] = false;
    lastButtonChangeUs[i] = 0;
  }
}

void playStartupSweep() {
  for (byte i = 0; i < LED_COUNT; i++) {
    digitalWrite(LED_PINS[i], HIGH);
    delay(STARTUP_SWEEP_MS);
  }
  delay(STARTUP_PAUSE_MS);
  for (byte i = 0; i < LED_COUNT; i++) digitalWrite(LED_PINS[i], LOW);
}

void setup() {
  Serial.begin(BAUD_RATE);
  if (DIAGNOSTIC_MODE) {
    setupDiagnostics();
    return;
  }
  setupPins();
  playStartupSweep();
  smoothedKnob = analogRead(KNOB_PIN);
  Serial.println(READY_MESSAGE);
  sendKnob();
}

void readButtons() {
  // El cambio se acepta de inmediato y luego se ignoran los rebotes por unos milisegundos.
  unsigned long nowUs = micros();
  for (byte i = 0; i < BUTTON_COUNT; i++) {
    bool down = isButtonDown(i);
    if (down != pressed[i] && nowUs - lastButtonChangeUs[i] >= DEBOUNCE_US) {
      pressed[i] = down;
      lastButtonChangeUs[i] = nowUs;
      sendButton(i);
    }
  }
}

void readKnob(unsigned long nowMs) {
  if (nowMs - lastKnobReadMs < KNOB_INTERVAL_MS) return;
  lastKnobReadMs = nowMs;
  smoothedKnob = (smoothedKnob * (KNOB_SMOOTHING - 1) + analogRead(KNOB_PIN)) / KNOB_SMOOTHING;
  if (abs(smoothedKnob - lastSentKnob) >= KNOB_MIN_CHANGE) sendKnob();
}

void sendHeartbeat(unsigned long nowMs) {
  if (nowMs - lastHeartbeatMs < HEARTBEAT_INTERVAL_MS) return;
  lastHeartbeatMs = nowMs;
  Serial.println(HEARTBEAT);
}

void runCommand() {
  if (command[0] == LED_COMMAND && commandLength >= MIN_LED_COMMAND_LENGTH) {
    int value = command[COMMAND_VALUE_INDEX] - FIRST_DIGIT;
    if (value >= 0 && value <= LED_COUNT) {
      litLeds = value;
      blinking = false;
    } else if (value == BLINK_COMMAND) {
      litLeds = LED_COUNT;
      blinking = true;
    }
  } else if (command[0] == STATUS_REQUEST) {
    for (byte i = 0; i < BUTTON_COUNT; i++) sendButton(i);
    sendKnob();
  }
}

void readCommands() {
  while (Serial.available()) {
    char received = Serial.read();
    if (received == LINE_FEED || received == CARRIAGE_RETURN) {
      if (commandLength > 0) {
        command[commandLength] = 0;
        runCommand();
      }
      commandLength = 0;
    } else if (commandLength < sizeof(command) - 1) {
      command[commandLength++] = received;
    }
  }
}

void updateLeds(unsigned long nowMs) {
  if (blinking && nowMs - lastBlinkMs >= BLINK_INTERVAL_MS) {
    lastBlinkMs = nowMs;
    blinkOn = !blinkOn;
  }
  for (byte i = 0; i < LED_COUNT; i++) {
    bool on = blinking ? blinkOn : i < litLeds;
    digitalWrite(LED_PINS[i], on ? HIGH : LOW);
  }
}

void loop() {
  if (DIAGNOSTIC_MODE) {
    loopDiagnostics();
    return;
  }
  unsigned long nowMs = millis();
  readButtons();
  readKnob(nowMs);
  sendHeartbeat(nowMs);
  readCommands();
  updateLeds(nowMs);
}

// Diagnostico de cableado

void setupDiagnostics() {
  delay(DIAGNOSTIC_START_MS);
  Serial.println(F("DIAGNOSTICO 1/2: LEDs"));
  Serial.println(F("Mira que LED se enciende con cada pin y anotalo."));
  for (byte pin = FIRST_DIAGNOSTIC_PIN; pin <= LAST_DIAGNOSTIC_PIN; pin++) {
    // Solo se pone el pin en HIGH, asi es seguro aunque sea un boton.
    digitalWrite(pin, HIGH);
    pinMode(pin, OUTPUT);
    Serial.print(F("Pin "));
    Serial.print(pin);
    Serial.println(F(" encendido"));
    delay(DIAGNOSTIC_LED_MS);
    pinMode(pin, INPUT);
    digitalWrite(pin, LOW);
  }
  for (byte pin = FIRST_DIAGNOSTIC_PIN; pin <= LAST_DIAGNOSTIC_PIN; pin++) {
    pinMode(pin, INPUT);
    lastPinLevel[pin] = digitalRead(pin);
  }
  Serial.println(F("DIAGNOSTICO 2/2: BOTONES Y PERILLA"));
  Serial.println(F("Presiona cada boton y anota que pin cambia. Gira la perilla para probar A0."));
}

void loopDiagnostics() {
  for (byte pin = FIRST_DIAGNOSTIC_PIN; pin <= LAST_DIAGNOSTIC_PIN; pin++) {
    int level = digitalRead(pin);
    if (level == lastPinLevel[pin]) continue;
    lastPinLevel[pin] = level;
    Serial.print(F("Pin "));
    Serial.print(pin);
    Serial.println(level ? F(" -> HIGH") : F(" -> LOW"));
  }
  int knob = analogRead(KNOB_PIN);
  if (abs(knob - lastDiagnosticKnob) > DIAGNOSTIC_KNOB_CHANGE) {
    lastDiagnosticKnob = knob;
    Serial.print(F("A0 = "));
    Serial.println(knob);
  }
  delay(DIAGNOSTIC_LOOP_MS);
}
