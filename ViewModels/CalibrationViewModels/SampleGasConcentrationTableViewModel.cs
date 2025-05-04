using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using CapnoAnalyzer.Helpers;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using OxyPlot;
using OxyPlot.Series;
using static CapnoAnalyzer.Models.Device.DeviceData;

namespace CapnoAnalyzer.ViewModels.CalibrationViewModels
{
    public class SampleGasConcentrationTableViewModel : BindableBase
    {
        // Matematiksel model tanımı
        private readonly Func<Vector<double>, double, double> model = (parameters, xVal) =>
        {
            double a = parameters[0];
            double b = parameters[1];
            double c = parameters[2];
            return a * (1 - Math.Exp(-b * Math.Pow(xVal, c)));
        };

        // Özellikler
        public ObservableCollection<DeviceData> DeviceData { get; set; }
        public Coefficients Coefficients { get; set; }

        private PlotModel _plotModel;
        public PlotModel PlotModel
        {
            get => _plotModel;
            set
            {
                _plotModel = value;
                OnPropertyChanged();
            }
        }

        // CalculateCommand özelliği
        public ICommand CalculateCommand { get; }

        public SampleGasConcentrationTableViewModel()
        {
            DeviceData = new ObservableCollection<DeviceData>();
            Coefficients = new Coefficients();
            PlotModel = new PlotModel { Title = "Nonlineer Model Fit: y = a(1 - e^{-bx^c})" };
            LoadData();
            CalculateCommand = new RelayCommand(_ => CalculateCoefficients(), _ => CanCalculate());
        }

        // Veri yükleme
        private void LoadData()
        {
            var deviceData = new[]
            {
                new DeviceData { Sample = "1", GasConcentration = 0.00, Ref = 2688.4988, Gas = 4912.5496 },
                new DeviceData { Sample = "2", GasConcentration = 0.50, Ref = 2686.8541, Gas = 4653.6383 },
                new DeviceData { Sample = "3", GasConcentration = 1.00, Ref = 2698.5712, Gas = 4482.9614 },
                new DeviceData { Sample = "4", GasConcentration = 1.50, Ref = 2691.1024, Gas = 4324.9308 },
                new DeviceData { Sample = "5", GasConcentration = 2.00, Ref = 2698.5963, Gas = 4199.9262 },
                new DeviceData { Sample = "6", GasConcentration = 2.50, Ref = 2702.6907, Gas = 4104.1672 },
                new DeviceData { Sample = "7", GasConcentration = 3.00, Ref = 2691.3517, Gas = 3937.3630 },
                new DeviceData { Sample = "8", GasConcentration = 3.50, Ref = 2690.0622, Gas = 3833.1201 },
                new DeviceData { Sample = "9", GasConcentration = 4.00, Ref = 2692.0327, Gas = 3692.6704 },
                new DeviceData { Sample = "10", GasConcentration = 4.50, Ref = 2700.4150, Gas = 3643.4684 },
                new DeviceData { Sample = "11", GasConcentration = 5.00, Ref = 2690.9841, Gas = 3534.8201 },
                new DeviceData { Sample = "12", GasConcentration = 5.50, Ref = 2690.0577, Gas = 3456.5425 },
                new DeviceData { Sample = "13", GasConcentration = 6.00, Ref = 2695.2707, Gas = 3405.8441 },
                new DeviceData { Sample = "14", GasConcentration = 6.50, Ref = 2693.2138, Gas = 3341.9859 },
                new DeviceData { Sample = "15", GasConcentration = 7.00, Ref = 2697.0889, Gas = 3300.5941 }
            };

            foreach (var data in deviceData)
            {
                DeviceData.Add(data);
            }
        }

        // Hesaplama işlemi
        public void CalculateCoefficients()
        {
            double zero = DeviceData.First(d => d.GasConcentration == 0.00).Gas /
                          DeviceData.First(d => d.GasConcentration == 0.00).Ref;

            foreach (var data in DeviceData)
            {
                data.Ratio = data.Gas / data.Ref;
                data.Transmittance = data.Ratio / zero;
                data.Absorption = 1 - data.Transmittance;
            }

            var gasConcentration = DeviceData.Select(d => d.GasConcentration).ToArray();
            var absorption = DeviceData.Select(d => d.Absorption ?? 0.0).ToArray();

            PlotAndOptimize(gasConcentration, absorption);

            foreach (var data in DeviceData)
            {
                data.PredictedAbsorption = model(
                    Vector<double>.Build.DenseOfArray(new[] { Coefficients.A, Coefficients.B, Coefficients.C }),
                    data.GasConcentration);

                if (data.Absorption.HasValue && data.Absorption.Value > 0 && data.Absorption.Value < Coefficients.A)
                {
                    data.PredictedGasConcentration = Math.Pow(
                        -Math.Log(1 - (data.Absorption.Value / Coefficients.A)) / Coefficients.B,
                        1 / Coefficients.C);
                }
                else
                {
                    data.PredictedGasConcentration = double.NaN;
                }
            }

            // R^2 hesaplama
            double meanGasConc = DeviceData.Average(d => d.GasConcentration);
            double sst = DeviceData.Sum(d => Math.Pow(d.GasConcentration - meanGasConc, 2));
            double sse = DeviceData
                .Where(d => !double.IsNaN(d.PredictedGasConcentration ?? double.NaN))
                .Sum(d => Math.Pow(d.GasConcentration - (d.PredictedGasConcentration ?? 0), 2));

            Coefficients.R = 1 - (sse / sst);
        }

        // Optimizasyon ve grafikleme
        private void PlotAndOptimize(double[] x, double[] y)
        {
            Func<Vector<double>, double> objFunc = parameters =>
                x.Zip(y, (xx, yy) => Math.Pow(yy - model(parameters, xx), 2)).Sum();

            var initialGuess = CalculateInitialGuesses(x, y);

            var optimizer = new NelderMeadSimplex(1e-6, 1000);
            var result = optimizer.FindMinimum(ObjectiveFunction.Value(objFunc), initialGuess);

            Coefficients.A = result.MinimizingPoint[0];
            Coefficients.B = result.MinimizingPoint[1];
            Coefficients.C = result.MinimizingPoint[2];

            int numPoints = 100;
            double minX = x.Min();
            double maxX = x.Max();
            double[] xFit = Enumerable.Range(0, numPoints)
                                      .Select(i => minX + i * (maxX - minX) / (numPoints - 1))
                                      .ToArray();
            double[] yFit = xFit.Select(xx => model(result.MinimizingPoint, xx)).ToArray();

            var scatterSeries = new ScatterSeries { Title = "Ölçüm", MarkerType = MarkerType.Circle };
            for (int i = 0; i < x.Length; i++)
            {
                scatterSeries.Points.Add(new ScatterPoint(x[i], y[i]));
            }

            var lineSeries = new LineSeries { Title = "Fit", StrokeThickness = 2 };
            for (int i = 0; i < xFit.Length; i++)
            {
                lineSeries.Points.Add(new DataPoint(xFit[i], yFit[i]));
            }

            PlotModel.Series.Clear();
            PlotModel.Series.Add(scatterSeries);
            PlotModel.Series.Add(lineSeries);
            PlotModel.InvalidatePlot(true);
        }

        private Vector<double> CalculateInitialGuesses(double[] x, double[] y)
        {
            double aGuess = y.Max();
            double xRange = x.Max() - x.Min();
            double bGuess = 1.0 / xRange;
            double cGuess = 1.0;
            return Vector<double>.Build.DenseOfArray(new double[] { aGuess, bGuess, cGuess });
        }

        // CalculateCommand'in çalışabilirlik durumu
        private bool CanCalculate()
        {
            bool canCalculate = DeviceData != null && DeviceData.Any() &&
                                DeviceData.All(d => d.Ref > 0 && d.Gas > 0);
            Console.WriteLine($"CanCalculate: {canCalculate}");
            return canCalculate;
        }
    }

    // Veri model sınıfı
    public class DeviceData: BindableBase
    {
        public DeviceData()
        {
            Sample = string.Empty;
            GasConcentration = 0.0;
            Ref = 0.0;
            Gas = 0.0;
            Ratio = null;
            Transmittance = null;
            Absorption = null;
            PredictedAbsorption = null;
            PredictedGasConcentration = null;                
        }

        private string _sample;
        public string Sample
        {
            get => _sample;
            set
            {
                _sample = value;
                OnPropertyChanged();
            }
        }

        private double _gasConcentration;
        public double GasConcentration
        {
            get => _gasConcentration;
            set
            {
                _gasConcentration = value;
                OnPropertyChanged();
            }
        }

        private double _ref;
        public double Ref
        {
            get => _ref;
            set
            {
                _ref = value;
                OnPropertyChanged();
            }
        }

        private double _gas;
        public double Gas
        {
            get => _gas;
            set
            {
                _gas = value;
                OnPropertyChanged();
            }
        }

        private double? _ratio;
        public double? Ratio
        {
            get => _ratio;
            set
            {
                _ratio = value;
                OnPropertyChanged();
            }
        }

        private double? _transmittance;
        public double? Transmittance
        {
            get => _transmittance;
            set
            {
                _transmittance = value;
                OnPropertyChanged();
            }
        }

        private double? _absorption;
        public double? Absorption
        {
            get => _absorption;
            set
            {
                _absorption = value;
                OnPropertyChanged();
            }
        }

        private double? _predictedAbsorption;
        public double? PredictedAbsorption
        {
            get => _predictedAbsorption;
            set
            {
                _predictedAbsorption = value;
                OnPropertyChanged();
            }
        }

        private double? _predictedGasConcentration;
        public double? PredictedGasConcentration
        {
            get => _predictedGasConcentration;
            set
            {
                _predictedGasConcentration = value;
                OnPropertyChanged();
            }
        }
    }

    // Katsayılar sınıfı
    public class Coefficients : BindableBase
    {
        public Coefficients()
        {
            A = 1.0;
            B = 1.0;
            C = 1.0;
            R = 1.0;
        }

        private double _a;
        public double A
        {
            get => _a;
            set
            {
                _a = value;
                OnPropertyChanged();
            }
        }

        private double _b;
        public double B
        {
            get => _b;
            set
            {
                _b = value;
                OnPropertyChanged();
            }
        }

        private double _c;
        public double C
        {
            get => _c;
            set
            {
                _c = value;
                OnPropertyChanged();
            }
        }

        private double _r;
        public double R
        {
            get => _r;
            set
            {
                _r = value;
                OnPropertyChanged();
            }
        }
    }
}
