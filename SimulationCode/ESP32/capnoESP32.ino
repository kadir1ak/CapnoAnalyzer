/*
 * CapnoAnalyzer ESP32 Simulator
 * 
 * Düzeltme: Referans kanalı artık 0'a düşmez. Sürekli sabit voltaj + gürültü verir.
 * Hız: 1 kHz
 * Baud: 921600
 * 
 * Senaryo:
 * 1. CO2: %0 -> %6 -> %0 döngüsü (5'er saniye).
 * 2. Referans (voltIIR3): 2.5V SABİT (+ ufak gürültü).
 * 3. Gaz (voltIIR2): CO2 arttıkça düşer (Beer-Lambert).
 */

#include <Arduino.h>

#define BAUD_RATE 921600
#define LOOP_INTERVAL_US 1000 // 1 ms

// --- DEĞİŞKENLER ---
double currentTime = 0.0;

// CO2 Döngüsü (0 - 6 arası)
double co2Level = 0.0;
bool co2Rising = true;
const double co2Step = 0.0012; // 5 saniyede 0'dan 6'ya ulaşmak için adım
const double co2Max = 6.0;
const double co2Min = 0.0;

// Voltaj Temel Değerleri
const double V_REF_BASE = 2.5000; // Referans Sabit Voltajı
const double V_GAS_BASE = 2.2000; // Gaz Sıfır Voltajı
const double ABSORPTION_K = 0.15; // Absorpsiyon katsayısı

unsigned long lastMicros = 0;
char packetBuffer[256];

// Gürültü Fonksiyonu (Amplitude: Voltaj cinsinden gürültü miktarı)
double getNoise(double amplitude) {
  // -1.0 ile +1.0 arası rastgele sayı * genlik
  return ((random(0, 2000) / 1000.0) - 1.0) * amplitude;
}

void setup() {
  Serial.begin(BAUD_RATE);
  randomSeed(analogRead(0));
}

void loop() {
  unsigned long currentMicros = micros();

  // 1 kHz Döngü
  if (currentMicros - lastMicros >= LOOP_INTERVAL_US) {
    lastMicros = currentMicros;
    currentTime += 0.001;

    // 1. CO2 Seviyesini Güncelle (Üçgen Dalga)
    if (co2Rising) {
      co2Level += co2Step;
      if (co2Level >= co2Max) {
        co2Level = co2Max;
        co2Rising = false;
      }
    } else {
      co2Level -= co2Step;
      if (co2Level <= co2Min) {
        co2Level = co2Min;
        co2Rising = true;
      }
    }

    // 2. Voltajları Hesapla
    // ARTIK IR KAPATMA YOK. SÜREKLİ ÖLÇÜM VAR.
    
    // Referans Kanalı: Sadece Gürültü ekle, ASLA 0 olma.
    // Gürültü: +/- 0.0015V (1.5mV)
    double finalRefVoltage = V_REF_BASE + getNoise(0.0015); 

    // Gaz Kanalı: Beer-Lambert Yasası + Gürültü
    // Gürültü: +/- 0.0020V (2.0mV)
    double finalGasVoltage = (V_GAS_BASE * exp(-ABSORPTION_K * co2Level)) + getNoise(0.0020);

    // 3. Diğer Sensörler (Simülasyon)
    double temp = 36.5 + getNoise(0.05);
    double hum = 45.0 + getNoise(0.2);
    double pres = 1013.0 + getNoise(0.1);

    // 4. Ham Değerler (Raw)
    double rawGas = finalGasVoltage * 10000;
    double rawRef = finalRefVoltage * 10000;

    // 5. Paketi Gönder
    // Format: GV t,co2,temp,hum,pres,ang1,ang2,raw1,raw2,v1,v2,vf2,vf3,DUMMY,viir2,viir3,ir
    // C# ParsePacket2 İndeksleri:
    // parts[14] -> voltIIR2 -> GAZ (Değişken)
    // parts[15] -> voltIIR3 -> REF (Sabit)
    
    int len = snprintf(packetBuffer, sizeof(packetBuffer), 
      "GV %.3f,%.2f,%.2f,%.2f,%.2f,%.3f,%.3f,%.0f,%.0f,%.4f,%.4f,%.4f,%.4f,0.0,%.4f,%.4f,1",
      currentTime,      // t
      co2Level,         // co2
      temp,             // temp
      hum,              // hum
      pres,             // pres
      0.0, 0.0,         // ang
      rawGas, rawRef,   // raw
      finalGasVoltage, finalRefVoltage, // volt
      finalGasVoltage, finalRefVoltage, // voltF
      // DUMMY (0.0)
      finalGasVoltage,  // voltIIR2 (GAZ - Değişken)
      finalRefVoltage,  // voltIIR3 (REF - SABİT)
      1                 // irStatus (Sürekli 1 gönderiyoruz)
    );

    Serial.println(packetBuffer);
  }
}
