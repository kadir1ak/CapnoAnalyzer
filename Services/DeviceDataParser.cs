using CapnoAnalyzer.Models.Device;
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
                    case "1":
                        return ParsePacket1(device, data);
                    case "2":
                        return ParsePacket2(device, data);
                    case "3":
                        return ParsePacket3(device, data);
                    default:
                        return false;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Parse error: {ex.Message}");
                return false;
            }
        }

        // Paket Tipi 1
        private bool ParsePacket1(Device device, string data)
        {
            string[] parts = data.Split(',');
            if (parts.Length != 5)
                return false;

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
            }

            if (Application.Current?.Dispatcher.CheckAccess() == true)
                Update();
            else
                Application.Current?.Dispatcher.Invoke(Update);

            return true;
        }

        // Paket Tipi 2
        private bool ParsePacket2(Device device, string data)
        {
            if (!data.StartsWith("GV"))
                return false;

            string[] parts = data.Substring(2)
                                 .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length != 17)
                return false;

            if (!double.TryParse(parts[0], NumberStyles.Float, Inv, out double time)) return false;
            if (!double.TryParse(parts[1], NumberStyles.Float, Inv, out double ang1)) return false;
            if (!double.TryParse(parts[2], NumberStyles.Float, Inv, out double ang2)) return false;
            if (!double.TryParse(parts[3], NumberStyles.Float, Inv, out double ang3)) return false;
            if (!double.TryParse(parts[4], NumberStyles.Float, Inv, out double raw1)) return false;
            if (!double.TryParse(parts[5], NumberStyles.Float, Inv, out double raw2)) return false;
            if (!double.TryParse(parts[6], NumberStyles.Float, Inv, out double raw3)) return false;
            if (!double.TryParse(parts[7], NumberStyles.Float, Inv, out double raw4)) return false;
            if (!double.TryParse(parts[8], NumberStyles.Float, Inv, out double volt1)) return false;
            if (!double.TryParse(parts[9], NumberStyles.Float, Inv, out double volt2)) return false;
            if (!double.TryParse(parts[10], NumberStyles.Float, Inv, out double volt3)) return false;
            if (!double.TryParse(parts[11], NumberStyles.Float, Inv, out double volt4)) return false;
            if (!double.TryParse(parts[12], NumberStyles.Float, Inv, out double voltF2)) return false;
            if (!double.TryParse(parts[13], NumberStyles.Float, Inv, out double voltF3)) return false;
            if (!double.TryParse(parts[14], NumberStyles.Float, Inv, out double voltIIR2)) return false;
            if (!double.TryParse(parts[15], NumberStyles.Float, Inv, out double voltIIR3)) return false;
            if (!int.TryParse(parts[16], out int irStatus)) return false;

            double gainF2 = voltF2 * 10.0;
            double gainF3 = voltF3 * 10.0;
            double gainIIR2 = voltIIR2 * 10.0;
            double gainIIR3 = voltIIR3 * 10.0;

            void Update()
            {
                device.DataPacket_2.Time = time;
                device.DataPacket_2.AngVoltages = new[] { ang1, ang2, ang3 };
                device.DataPacket_2.AdsRawValues = new[] { raw1, raw2, raw3, raw4 };
                device.DataPacket_2.AdsVoltages = new[] { volt1, volt2, volt3, volt4 };
                device.DataPacket_2.GainAdsVoltagesF = new[] { gainF2, gainF3 };
                device.DataPacket_2.GainAdsVoltagesIIR = new[] { gainIIR2, gainIIR3 };
                device.DataPacket_2.IrStatus = irStatus;

                device.DeviceData.SensorData.Time = time;
                device.DeviceData.SensorData.IIR_Gas_Voltage = gainIIR2;
                device.DeviceData.SensorData.IIR_Ref_Voltage = gainIIR3;
                device.DeviceData.SensorData.IR_Status = irStatus;
            }

            if (Application.Current?.Dispatcher.CheckAccess() == true)
                Update();
            else
                Application.Current?.Dispatcher.Invoke(Update);

            return true;
        }

        // Paket Tipi 3
        private bool ParsePacket3(Device device, string data)
        {
            string[] parts = data.Split(',');
            if (parts.Length != 5)
                return false;

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
            }

            if (Application.Current?.Dispatcher.CheckAccess() == true)
                Update();
            else
                Application.Current?.Dispatcher.Invoke(Update);

            return true;
        }
    }
}
