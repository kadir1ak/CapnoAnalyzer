/*
 * CapnoAnalyzer ESP32 Real Simulator (Corrected for C# Parser)
 * 
 * Hız: 1 kHz (1000 us)
 * Baud: 921600
 * 
 * PARSER UYUMLULUĞU:
 * parts[14] (voltIIR2) -> REFERANS Voltajı (Sabit ~2.5V)
 * parts[15] (voltIIR3) -> GAZ Voltajı (CO2 arttıkça düşer)
 * 
 * SİMÜLASYON:
 * - Gerçekçi Kapnogram Eğrisi (Exhale -> Plateau -> Inhale)
 * - Beer-Lambert Yasası (V_gas = V_base * e^(-k * C))
 */

#include <Arduino.h>

#define BAUD_RATE 921600
#define LOOP_INTERVAL_US 1000 // 1 ms döngü hızı

// --- SİMÜLASYON PARAMETRELERİ ---
double currentTime = 0.0;
unsigned long lastMicros = 0;
char packetBuffer[256];

// Solunum Döngüsü (5 Saniye = 12 BPM)
// 0-2s: Nefes Verme (Yükseliş + Plato)
// 2-5s: Nefes Alma (Sıfıra düşüş ve bekleme)
int breathPhase = 0; 
int phaseCounter = 0;
const int CYCLE_DURATION_MS = 5000; // 5 saniyelik döngü

// CO2 Değerleri
double currentCO2 = 0.0;
const double MAX_CO2_PERCENT = 5.3; // %6 CO2 (EtCO2)

// Voltaj Temel Değerleri
const double V_REF_BASE = 2.5000; // Referans Kanalı (Sabit)
const double V_GAS_BASE = 2.2000; // Gaz Kanalı (Sıfır noktasındaki voltaj)
const double ABSORPTION_K = 0.15; // Beer-Lambert Absorpsiyon Katsayısı

// Gürültü Üreteci
double getNoise(double amplitude) {
  // -1.0 ile +1.0 arası rastgele * genlik
  return ((random(0, 2000) / 1000.0) - 1.0) * amplitude;
}

void setup() {
  Serial.begin(BAUD_RATE);
  randomSeed(analogRead(0));
}

void loop() {
  unsigned long currentMicros = micros();

  // 1 kHz Hassas Zamanlama
  if (currentMicros - lastMicros >= LOOP_INTERVAL_US) {
    lastMicros = currentMicros;
    currentTime += 0.001;
    
    // --- 1. GERÇEKÇİ SOLUNUM EĞRİSİ OLUŞTURMA ---
    // Döngü sayacı (0 - 5000 ms arası)
    phaseCounter++;
    if (phaseCounter >= CYCLE_DURATION_MS) {
      phaseCounter = 0;
    }

    // Kapnogram Fazları
    if (phaseCounter < 500) {
      // Faz II: Hızlı Yükseliş (0.5 sn) -> Akciğerden hava çıkışı başlar
      double progress = (double)phaseCounter / 500.0;
      currentCO2 = MAX_CO2_PERCENT * progress; 
    } 
    else if (phaseCounter < 2000) {
      // Faz III: Alveolar Plato (1.5 sn) -> Hafif eğimli tepe noktası
      // %6'dan %6.2'ye çok hafif yükselir (gerçekçi görünüm için)
      double progress = (double)(phaseCounter - 500) / 1500.0;
      currentCO2 = MAX_CO2_PERCENT + (progress * 0.2); 
    } 
    else if (phaseCounter < 2300) {
      // Faz 0: İnspirasyon (0.3 sn) -> Nefes alma, CO2 hızla 0'a düşer
      double progress = (double)(phaseCounter - 2000) / 300.0;
      currentCO2 = (MAX_CO2_PERCENT + 0.2) * (1.0 - progress);
      if (currentCO2 < 0) currentCO2 = 0;
    } 
    else {
      // Faz I: Bazal Çizgi (2.7 sn) -> Temiz hava, CO2 sıfır
      currentCO2 = 0.0;
    }

    // --- 2. VOLTAJ HESAPLAMALARI (Beer-Lambert) ---
    
    // REFERANS KANALI (voltIIR2):
    // CO2'den etkilenmez. Sabit voltaj + Gürültü.
    // C# Parser: parts[14] -> gainIIR_Ref
    double finalRefVoltage = V_REF_BASE + getNoise(0.0010); 

    // GAZ KANALI (voltIIR3):
    // CO2 arttıkça voltaj düşer (Absorpsiyon).
    // Formül: V = V0 * e^(-k * C)
    // C# Parser: parts[15] -> gainIIR_Gas
    double signalSignal = V_GAS_BASE * exp(-ABSORPTION_K * currentCO2);
    double finalGasVoltage = signalSignal + getNoise(0.0015);

    // --- 3. DİĞER SENSÖRLER (Simülasyon) ---
    double temp = 36.5 + getNoise(0.02);
    double hum = 45.0 + getNoise(0.1);
    double pres = 1013.0 + getNoise(0.05);

    // Ham ADC değerleri (Simüle edilmiş)
    double rawRef = finalRefVoltage * 10000; // Örn: 25000
    double rawGas = finalGasVoltage * 10000; // Örn: 22000 -> düşer

    // --- 4. PAKET OLUŞTURMA ---
    // Format: GV t,co2,temp,hum,pres,ang1,ang2,raw1,raw2,v1,v2,vf2,vf3,UNK,voltIIR2,voltIIR3,ir
    // DİKKAT: C# Parser'a göre sıralama yapıyoruz.
    
    int len = snprintf(packetBuffer, sizeof(packetBuffer), 
      "GV %.3f,%.2f,%.2f,%.2f,%.2f,%.3f,%.3f,%.0f,%.0f,%.4f,%.4f,%.4f,%.4f,0,%.4f,%.4f,1",
      currentTime,      // [0] t
      currentCO2,       // [1] co2Val
      temp,             // [2] bmeVal0
      hum,              // [3] bmeVal1
      pres,             // [4] bmeVal2
      0.0, 0.0,         // [5,6] ang1, ang2
      rawRef, rawGas,   // [7,8] raw1, raw2 (Ref önce, Gas sonra mantığıyla)
      finalRefVoltage, finalGasVoltage, // [9,10] volt1, volt2
      finalRefVoltage, finalGasVoltage, // [11,12] voltF2, voltF3 (Filtrelenmişler)
      // [13] UNK (0)
      finalRefVoltage,  // [14] voltIIR2 -> C# Parser: gainIIR_Ref (REFERANS - SABİT)
      finalGasVoltage,  // [15] voltIIR3 -> C# Parser: gainIIR_Gas (GAZ - DEĞİŞKEN)
      1                 // [16] irStatus (Sürekli açık)
    );

    Serial.println(packetBuffer);
  }
}
