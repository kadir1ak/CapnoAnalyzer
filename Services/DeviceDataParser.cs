using CapnoAnalyzer.Models.Device;
using CapnoAnalyzer.Models.PlotModels;
using System;
using System.Globalization;
using System.Linq;
using System.Windows;

namespace CapnoAnalyzer.Services
{
    internal class DeviceDataParser
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public bool TryParsePacket(Device device, string data)
        {
            try
            {
                switch (device.Properties.DataPacketType)
                {
                    case "1": return ParsePacket1(device, data);
                    case "2": return ParsePacket2(device, data);
                    case "3": return ParsePacket3(device, data);
                    default: return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Parse error: {ex.Message}");
                return false;
            }
        }

        // Paket Tipi 1  -> "t,ch1,ch2,temp,hum"
        private bool ParsePacket1(Device device, string data)
        {
            string[] parts = data.Split(',');
            if (parts.Length != 5) return false;

            if (!double.TryParse(parts[0], NumberStyles.Float, Inv, out double time)) return false;
            if (!double.TryParse(parts[1], NumberStyles.Float, Inv, out double ch1)) return false;
            if (!double.TryParse(parts[2], NumberStyles.Float, Inv, out double ch2)) return false;
            if (!double.TryParse(parts[3], NumberStyles.Float, Inv, out double temp)) return false;
            if (!double.TryParse(parts[4], NumberStyles.Float, Inv, out double hum)) return false;

            void Update()
            {
                device.DataPacket_1.Time = time;
                device.DataPacket_1.GasSensor = ch1;
                device.DataPacket_1.ReferenceSensor = ch2;
                device.DataPacket_1.Temperature = temp;
                device.DataPacket_1.Humidity = hum;

                device.DeviceData.SensorData.Time = time;
                device.DeviceData.SensorData.IIR_Gas_Voltage = ch1;
                device.DeviceData.SensorData.IIR_Ref_Voltage = ch2;
                device.DeviceData.SensorData.IR_Status = 0.0;

                // --- ÇİZ ---
                var plot = device.Interface?.SensorPlot;
                if (plot is IHighFreqPlot hf) hf.Enqueue(time, ch1, ch2);
                else plot?.AddDataPoint(time, ch1, ch2);
            }

            if (Application.Current?.Dispatcher.CheckAccess() == true) Update();
            else Application.Current?.Dispatcher.Invoke(Update);

            return true;
        }

        // Paket Tipi 2 -> "GV t,co2Val,bmeVal0,bmeVal1,bmeVal2,ang1,ang2,raw1,raw2,volt1,volt2,voltF2,voltF3,voltIIR2(ref),voltIIR3(gas),ir"
        private bool ParsePacket2(Device device, string data)
        {
            if (!data.StartsWith("GV")) return false;

            string[] parts = data.Substring(2).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // Verilen formata göre toplam 16 veri alanı var (0'dan 15'e kadar)
            if (parts.Length != 16) return false;

            if (!double.TryParse(parts[0], NumberStyles.Float, Inv, out double time)) return false;
            if (!double.TryParse(parts[1], NumberStyles.Float, Inv, out double co2Val)) return false;
            if (!double.TryParse(parts[2], NumberStyles.Float, Inv, out double bmeVal0)) return false;
            if (!double.TryParse(parts[3], NumberStyles.Float, Inv, out double bmeVal1)) return false;
            if (!double.TryParse(parts[4], NumberStyles.Float, Inv, out double bmeVal2)) return false;
            if (!double.TryParse(parts[5], NumberStyles.Float, Inv, out double ang1)) return false;
            if (!double.TryParse(parts[6], NumberStyles.Float, Inv, out double ang2)) return false;
            if (!double.TryParse(parts[7], NumberStyles.Float, Inv, out double raw1)) return false;
            if (!double.TryParse(parts[8], NumberStyles.Float, Inv, out double raw2)) return false;
            if (!double.TryParse(parts[9], NumberStyles.Float, Inv, out double volt1)) return false;
            if (!double.TryParse(parts[10], NumberStyles.Float, Inv, out double volt2)) return false;
            if (!double.TryParse(parts[11], NumberStyles.Float, Inv, out double voltF2)) return false;
            if (!double.TryParse(parts[12], NumberStyles.Float, Inv, out double voltF3)) return false;

            // --- GÜNCELLEME BURADA ---

            // 13 -> voltIIR2 (Referans olarak belirtilmiş)
            if (!double.TryParse(parts[13], NumberStyles.Float, Inv, out double voltIIR2_Ref)) return false;

            // 14 -> voltIIR3 (Gaz olarak belirtilmiş)
            if (!double.TryParse(parts[14], NumberStyles.Float, Inv, out double voltIIR3_Gas)) return false;

            // 15 -> IR Status
            if (!int.TryParse(parts[15], out int irStatus)) return false;

            double gainF2 = voltF2 * 1.0;
            double gainF3 = voltF3 * 1.0;

            // Değişken isimlerini netleştirelim
            double gainIIR_Ref = voltIIR2_Ref * 1.0; // Index 13
            double gainIIR_Gas = voltIIR3_Gas * 1.0; // Index 14

            void Update()
            {
                device.DataPacket_2.Time = time;
                device.DataPacket_2.CO2Value = new[] { co2Val };
                device.DataPacket_2.BMEValue = new[] { bmeVal0, bmeVal1, bmeVal2 };
                device.DataPacket_2.AngVoltages = new[] { ang1, ang2 };
                device.DataPacket_2.AdsRawValues = new[] { raw1, raw2 };
                device.DataPacket_2.AdsVoltages = new[] { volt1, volt2 };
                device.DataPacket_2.GainAdsVoltagesF = new[] { gainF2, gainF3 };

                // Array sıralaması: [Gaz, Referans] (Genelde grafiklerde Gaz önce istenir)
                device.DataPacket_2.GainAdsVoltagesIIR = new[] { gainIIR_Gas, gainIIR_Ref };
                device.DataPacket_2.IrStatus = irStatus;

                device.DeviceData.SensorData.Time = time;

                // Sensör datasına doğru atamalar
                device.DeviceData.SensorData.IIR_Gas_Voltage = gainIIR_Gas; // Gaz (Index 14)
                device.DeviceData.SensorData.IIR_Ref_Voltage = gainIIR_Ref; // Ref (Index 13)
                device.DeviceData.SensorData.IR_Status = irStatus;

                // --- ÇİZ (IIR’lı değerler) ---
                device.Interface?.CalculatedGasPlot?.AddDeviceCO2Data(device.DataPacket_2.Time, co2Val);
                var plot = device.Interface?.SensorPlot;

                // Grafiğe gönderirken (Gaz, Ref) sırasıyla
                if (plot is IHighFreqPlot hf) hf.Enqueue(time, gainIIR_Gas, gainIIR_Ref);
                else plot?.AddDataPoint(time, gainIIR_Gas, gainIIR_Ref);
            }

            if (Application.Current?.Dispatcher.CheckAccess() == true) Update();
            else Application.Current?.Dispatcher.Invoke(Update);

            return true;
        }

        // Paket Tipi 3  -> "t,ch0,ch1,frame,emitter"
        private bool ParsePacket3(Device device, string data)
        {
            string[] parts = data.Split(',');
            if (parts.Length != 5) return false;

            if (!double.TryParse(parts[0], NumberStyles.Float, Inv, out double time)) return false;
            if (!double.TryParse(parts[1], NumberStyles.Float, Inv, out double ch0)) return false;
            if (!double.TryParse(parts[2], NumberStyles.Float, Inv, out double ch1)) return false;
            if (!int.TryParse(parts[3], out int frame)) return false;
            if (!int.TryParse(parts[4], out int emitter)) return false;

            void Update()
            {
                device.DataPacket_3.Time = time;
                device.DataPacket_3.Ch0 = ch0;
                device.DataPacket_3.Ch1 = ch1;
                device.DataPacket_3.Frame = frame;
                device.DataPacket_3.Emitter = emitter;

                device.DeviceData.SensorData.Time = time;
                device.DeviceData.SensorData.IIR_Gas_Voltage = ch0;
                device.DeviceData.SensorData.IIR_Ref_Voltage = ch1;
                device.DeviceData.SensorData.IR_Status = emitter;

                // --- ÇİZ ---
                var plot = device.Interface?.SensorPlot;
                if (plot is IHighFreqPlot hf) hf.Enqueue(time, ch0, ch1);
                else plot?.AddDataPoint(time, ch0, ch1);
            }

            if (Application.Current?.Dispatcher.CheckAccess() == true) Update();
            else Application.Current?.Dispatcher.Invoke(Update);

            return true;
        }
    }
}
