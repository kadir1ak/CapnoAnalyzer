using System;
using System.Collections.Generic;
using CapnoAnalyzer.ViewModels.CalibrationViewModels; // Data ve Coefficients sınıfları için

namespace CapnoAnalyzer.Services
{
    /// <summary>
    /// Kapnografi sensör verileri için sıcaklık kompanzasyonu hesaplamalarını yürüten
    /// merkezi hesaplama motoru. Bu sınıf statiktir ve durum bilgisi tutmaz.
    /// </summary>
    public static class ThermalCompensationEngine
    {
        #region Veri Yapıları

        // Alfa ve Beta gibi termal model parametrelerini tutar.
        public class ThermalModelParameters
        {
            public double Alfa { get; set; } = 0.00070; // Varsayılan değer
            public double Beta { get; set; } = -0.09850; // Varsayılan değer
        }

        // Tek bir veri noktasını işlemek için gereken tüm girdileri bir araya getiren yapı.
        public class CalculationContext
        {
            // Mevcut Ölçüm Değerleri
            public Data CurrentDataPoint { get; set; }

            // Referans Test Değerleri
            public double ReferenceZero { get; set; }
            public double ReferenceTemperature { get; set; }
            public Coefficients ReferenceCoefficients { get; set; }

            // Termal Model Parametreleri
            public ThermalModelParameters ModelParameters { get; set; }
        }

        #endregion

        /// <summary>
        /// Tek bir veri satırı için tüm sıcaklık kompanzasyonu hesaplamalarını yapar.
        /// </summary>
        public static void ProcessDataPoint(CalculationContext context)
        {
            var data = context.CurrentDataPoint;
            var refCoeffs = context.ReferenceCoefficients;

            // --- Yeşil Sütunlar ---

            // Normalized Ratio (NR) = V_act / (V_ref * Zero_ref)
            data.NormalizedRatio = (data.Ref != 0 && context.ReferenceZero != 0)
                ? data.Gas / (data.Ref * context.ReferenceZero)
                : (double?)null;

            // Normalized Absorbance (NA) = 1 - NR
            data.NormalizedAbsorbance = data.NormalizedRatio.HasValue
                ? 1 - data.NormalizedRatio
                : (double?)null;

            // Span = NA / (1 - exp(-b_ref * x^c_ref))
            double spanDenominator = 1 - Math.Exp(-refCoeffs.B * Math.Pow(data.GasConcentration, refCoeffs.C));
            data.Span = (data.NormalizedAbsorbance.HasValue && Math.Abs(spanDenominator) > 1e-12)
                ? data.NormalizedAbsorbance / spanDenominator
                : (double?)null;

            // NR(comp) = NR * [1 + alfa * (T - T_ref)]
            double tempDifference = data.Temperature - context.ReferenceTemperature;
            data.CompensatedNormalizedRatio = data.NormalizedRatio.HasValue
                ? data.NormalizedRatio * (1 + context.ModelParameters.Alfa * tempDifference)
                : (double?)null;

            // Span(comp) = Span + beta * [(T - T_ref) / T_ref]
            data.CompensatedSpan = (data.Span.HasValue && context.ReferenceTemperature != 0)
                ? data.Span + context.ModelParameters.Beta * (tempDifference / context.ReferenceTemperature)
                : (double?)null;

            // Final Compensated Concentration (X)
            if (data.CompensatedNormalizedRatio.HasValue && data.CompensatedSpan.HasValue && data.CompensatedSpan != 0 && refCoeffs.B != 0 && refCoeffs.C != 0)
            {
                double termInsideLog = 1 - ((1 - data.CompensatedNormalizedRatio.Value) / data.CompensatedSpan.Value);
                if (termInsideLog > 0)
                {
                    double baseForPower = (-1 / refCoeffs.B) * Math.Log(termInsideLog);
                    if (baseForPower >= 0)
                    {
                        data.FinalCompensatedConcentration = Math.Pow(baseForPower, 1 / refCoeffs.C);
                    }
                    else { data.FinalCompensatedConcentration = null; }
                }
                else { data.FinalCompensatedConcentration = null; }
            }
            else
            {
                data.FinalCompensatedConcentration = null;
            }
        }
    }
}
