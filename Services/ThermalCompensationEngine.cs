using System;
using CapnoAnalyzer.ViewModels.CalibrationViewModels;

namespace CapnoAnalyzer.Services
{
    public static class ThermalCompensationEngine
    {
        #region Data Structures

        public class ThermalModelParameters
        {
            public double Alfa { get; set; }
            public double Beta { get; set; }
        }

        public class CalculationContext
        {
            public Data CurrentDataPoint { get; set; }
            public double ReferenceZero { get; set; }
            public double ReferenceTemperature { get; set; }
            public Coefficients ReferenceCoefficients { get; set; }
            public ThermalModelParameters ModelParameters { get; set; }
        }

        #endregion

        #region Processing Logic

        public static void ProcessDataPoint(CalculationContext context)
        {
            var data = context.CurrentDataPoint;
            var refCoeffs = context.ReferenceCoefficients;
            var par = context.ModelParameters;

            // 1. Normalized Ratio (NR)
            // NR = V_act / (V_ref * Zero_ref)
            if (data.Ref != 0 && context.ReferenceZero != 0)
            {
                data.NormalizedRatio = data.Gas / (data.Ref * context.ReferenceZero);
            }
            else
            {
                data.NormalizedRatio = null;
            }

            // 2. Normalized Absorbance (NA)
            // NA = 1 - NR
            if (data.NormalizedRatio.HasValue)
            {
                data.NormalizedAbsorbance = 1 - data.NormalizedRatio.Value;
            }
            else
            {
                data.NormalizedAbsorbance = null;
            }

            // 3. Span (Theoretical Span at T_act based on Main Calibration Curve)
            // Span = NA / (1 - exp(-b * x^c))
            // Not: Bu değer, o anki gaz konsantrasyonunda olması gereken teorik Span değeridir.
            if (data.NormalizedAbsorbance.HasValue && refCoeffs.B != 0 && refCoeffs.C != 0)
            {
                double denominator = 1 - Math.Exp(-refCoeffs.B * Math.Pow(data.GasConcentration, refCoeffs.C));
                if (Math.Abs(denominator) > 1e-9)
                {
                    data.Span = data.NormalizedAbsorbance.Value / denominator;
                }
                else
                {
                    data.Span = null;
                }
            }
            else
            {
                data.Span = null;
            }

            // 4. Compensated Normalized Ratio (NR_comp)
            // NR_comp = NR * [1 + alfa * (T - T_ref)]
            if (data.NormalizedRatio.HasValue)
            {
                double deltaT = data.Temperature - context.ReferenceTemperature;
                data.CompensatedNormalizedRatio = data.NormalizedRatio.Value * (1 + par.Alfa * deltaT);
            }
            else
            {
                data.CompensatedNormalizedRatio = null;
            }

            // 5. Compensated Span (Span_comp)
            // Span_comp = Span + beta * [(T - T_ref) / T_ref]
            // Not: Span değeri null ise (örn: 0 gazda), Span_comp hesaplanamaz, ancak A katsayısı Span yerine kullanılabilir.
            if (data.Span.HasValue && context.ReferenceTemperature != 0)
            {
                double deltaT = data.Temperature - context.ReferenceTemperature;
                data.CompensatedSpan = data.Span.Value + par.Beta * (deltaT / context.ReferenceTemperature);
            }
            else if (data.Span == null && refCoeffs.A != 0 && context.ReferenceTemperature != 0)
            {
                // Fallback: Span hesaplanamıyorsa (0 gaz), Ana A katsayısını baz alarak kompanzasyon dene
                double deltaT = data.Temperature - context.ReferenceTemperature;
                data.CompensatedSpan = refCoeffs.A + par.Beta * (deltaT / context.ReferenceTemperature);
            }
            else
            {
                data.CompensatedSpan = null;
            }

            // 6. Final Compensated Concentration (X_comp)
            // Ters Fonksiyon: x = [ -1/b * ln( 1 - (1 - NR_comp)/Span_comp ) ] ^ (1/c)
            if (data.CompensatedNormalizedRatio.HasValue &&
                data.CompensatedSpan.HasValue &&
                data.CompensatedSpan.Value != 0 &&
                refCoeffs.B != 0 &&
                refCoeffs.C != 0)
            {
                double term = 1 - ((1 - data.CompensatedNormalizedRatio.Value) / data.CompensatedSpan.Value);

                if (term > 0)
                {
                    double logVal = Math.Log(term);
                    double baseVal = (-1.0 / refCoeffs.B) * logVal;

                    if (baseVal >= 0)
                    {
                        data.FinalCompensatedConcentration = Math.Pow(baseVal, 1.0 / refCoeffs.C);
                    }
                    else
                    {
                        data.FinalCompensatedConcentration = 0; // Negatif kök hatası yerine 0
                    }
                }
                else
                {
                    // Logaritma içi negatif veya sıfır ise (aşırı doygunluk)
                    data.FinalCompensatedConcentration = double.NaN;
                }
            }
            else
            {
                data.FinalCompensatedConcentration = null;
            }
        }

        #endregion
    }
}
