// =====================================================================
//  PianoHero - Control con Arduino Uno
//  4 botones (carriles), 1 potenciometro (seleccion de cancion),
//  4 LEDs (racha).
//
//  Protocolo serial (115200 baudios, un mensaje por linea):
//    Arduino -> PC:
//      "READY"        al arrancar
//      "B1:1"/"B1:0"  boton 1 presionado / soltado  (B1..B4)
//      "P:734"        valor del potenciometro (0-1023), solo cuando cambia
//      "H"            latido cada 1 s (para que Unity sepa que esta conectado)
//    PC -> Arduino:
//      "L0".."L4"     cuantos LEDs encender (de izquierda a derecha)
//      "L5"           los 4 LEDs parpadeando (racha maxima)
//      "?"            reenviar estado de botones y potenciometro
// =====================================================================

// ================== CONFIGURA AQUI TUS PINES ==================
// Orden: carril 1 (verde), 2 (rojo), 3 (amarillo), 4 (azul)
const byte PINES_BOTON[4] = {6, 7, 8, 9};
// Orden: LED 1 (se enciende primero) ... LED 4 (ultimo)
const byte PINES_LED[4]   = {2, 3, 4, 5};
const byte PIN_POT = A0;

// Tus botones tienen resistencia externa (pull-down): leen HIGH al presionar.
// Si algun dia conectas los botones directo a GND sin resistencia, pon true.
const bool USAR_PULLUP_INTERNO = false;

// Pon true, sube el sketch y abre el Monitor Serie (115200) para ver
// que pin cambia al presionar cada boton. Luego vuelve a false.
const bool MODO_DIAGNOSTICO = false;
// ===============================================================

const unsigned long DEBOUNCE_US = 8000;   // 8 ms: rapido para juego de ritmo
const int UMBRAL_POT = 6;                  // cambio minimo para reenviar
const unsigned long INTERVALO_POT_MS = 15;
const unsigned long INTERVALO_PARPADEO_MS = 120;

bool estado[4];
bool reposo[4];   // lectura del pin con el boton suelto (se mide al encender)
unsigned long ultimoCambio[4];

int potSuavizado = 0;
int ultimoPotEnviado = -100;
unsigned long ultimoPotMs = 0;
unsigned long ultimoLatidoMs = 0;

byte ledsPedidos = 0;
bool parpadeo = false;
bool faseParpadeo = false;
unsigned long ultimoParpadeoMs = 0;

char buffer[8];
byte bufLen = 0;

// Diagnostico
int prevPin[14];
int prevPotDiag = -100;

// Presionado = el pin esta distinto a como estaba en reposo.
// Asi funciona igual con resistencia a tierra o a 5V. NO presiones botones al encender.
bool leerBoton(byte i) {
  return digitalRead(PINES_BOTON[i]) != reposo[i];
}

void enviarBoton(byte i) {
  Serial.print('B');
  Serial.print(i + 1);
  Serial.print(':');
  Serial.println(estado[i] ? '1' : '0');
}

void enviarPot() {
  Serial.print(F("P:"));
  Serial.println(potSuavizado);
  ultimoPotEnviado = potSuavizado;
}

void setup() {
  Serial.begin(115200);

  if (MODO_DIAGNOSTICO) {
    delay(1500);
    Serial.println(F("=== DIAGNOSTICO 1/2: LEDs ==="));
    Serial.println(F("Mira que LED se enciende con cada pin y anotalo."));
    for (byte p = 2; p <= 13; p++) {
      // Solo se pone el pin en HIGH (nunca en LOW), asi es seguro aunque sea un boton.
      digitalWrite(p, HIGH);
      pinMode(p, OUTPUT);
      Serial.print(F("Pin "));
      Serial.print(p);
      Serial.println(F(" encendido"));
      delay(900);
      pinMode(p, INPUT);
      digitalWrite(p, LOW);
    }
    for (byte p = 2; p <= 13; p++) { pinMode(p, INPUT); prevPin[p] = digitalRead(p); }
    Serial.println(F("=== DIAGNOSTICO 2/2: BOTONES Y PERILLA ==="));
    Serial.println(F("Presiona cada boton y anota que pin cambia. Gira la perilla para probar A0."));
    return;
  }

  for (byte i = 0; i < 4; i++) {
    pinMode(PINES_BOTON[i], USAR_PULLUP_INTERNO ? INPUT_PULLUP : INPUT);
    pinMode(PINES_LED[i], OUTPUT);
  }
  delay(20);
  for (byte i = 0; i < 4; i++) {
    reposo[i] = digitalRead(PINES_BOTON[i]);
    estado[i] = false;
    ultimoCambio[i] = 0;
  }

  // Barrido de arranque: deben encenderse en orden 1 -> 4
  for (byte i = 0; i < 4; i++) { digitalWrite(PINES_LED[i], HIGH); delay(150); }
  delay(200);
  for (byte i = 0; i < 4; i++) digitalWrite(PINES_LED[i], LOW);

  potSuavizado = analogRead(PIN_POT);
  Serial.println(F("READY"));
  enviarPot();
}

void loopDiagnostico() {
  for (byte p = 2; p <= 13; p++) {
    int v = digitalRead(p);
    if (v != prevPin[p]) {
      prevPin[p] = v;
      Serial.print(F("Pin "));
      Serial.print(p);
      Serial.println(v ? F(" -> HIGH") : F(" -> LOW"));
    }
  }
  int pot = analogRead(PIN_POT);
  if (abs(pot - prevPotDiag) > 30) {
    prevPotDiag = pot;
    Serial.print(F("A0 = "));
    Serial.println(pot);
  }
  delay(5);
}

void leerSerial() {
  while (Serial.available()) {
    char c = Serial.read();
    if (c == '\n' || c == '\r') {
      if (bufLen > 0) {
        buffer[bufLen] = 0;
        if (buffer[0] == 'L' && bufLen >= 2) {
          int n = buffer[1] - '0';
          if (n >= 0 && n <= 4) { ledsPedidos = n; parpadeo = false; }
          else if (n == 5) { ledsPedidos = 4; parpadeo = true; }
        } else if (buffer[0] == '?') {
          for (byte i = 0; i < 4; i++) enviarBoton(i);
          enviarPot();
        }
      }
      bufLen = 0;
    } else if (bufLen < sizeof(buffer) - 1) {
      buffer[bufLen++] = c;
    }
  }
}

void actualizarLeds() {
  unsigned long ahora = millis();
  if (parpadeo) {
    if (ahora - ultimoParpadeoMs >= INTERVALO_PARPADEO_MS) {
      ultimoParpadeoMs = ahora;
      faseParpadeo = !faseParpadeo;
    }
    for (byte i = 0; i < 4; i++) digitalWrite(PINES_LED[i], faseParpadeo ? HIGH : LOW);
  } else {
    for (byte i = 0; i < 4; i++) digitalWrite(PINES_LED[i], i < ledsPedidos ? HIGH : LOW);
  }
}

void loop() {
  if (MODO_DIAGNOSTICO) { loopDiagnostico(); return; }

  // Botones: se acepta el cambio de inmediato y luego se ignoran rebotes 8 ms
  unsigned long ahoraUs = micros();
  for (byte i = 0; i < 4; i++) {
    bool lectura = leerBoton(i);
    if (lectura != estado[i] && (ahoraUs - ultimoCambio[i]) >= DEBOUNCE_US) {
      estado[i] = lectura;
      ultimoCambio[i] = ahoraUs;
      enviarBoton(i);
    }
  }

  // Potenciometro suavizado
  unsigned long ahoraMs = millis();
  if (ahoraMs - ultimoPotMs >= INTERVALO_POT_MS) {
    ultimoPotMs = ahoraMs;
    potSuavizado = (potSuavizado * 3 + analogRead(PIN_POT)) / 4;
    if (abs(potSuavizado - ultimoPotEnviado) >= UMBRAL_POT) enviarPot();
  }

  if (ahoraMs - ultimoLatidoMs >= 1000) {
    ultimoLatidoMs = ahoraMs;
    Serial.println('H');
  }

  leerSerial();
  actualizarLeds();
}
