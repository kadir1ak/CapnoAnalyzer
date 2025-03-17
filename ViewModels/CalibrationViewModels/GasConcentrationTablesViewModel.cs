using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using CapnoAnalyzer.Helpers;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using OxyPlot;
using OxyPlot.Series;

namespace CapnoAnalyzer.ViewModels.CalibrationViewModels
{
    public class GasConcentrationTablesViewModel : BindableBase
    {
        private readonly Func<Vector<double>, double, double> model = (parameters, xVal) =>
        {
            double a = parameters[0];
            double b = parameters[1];
            double c = parameters[2];
            return a * (1 - Math.Exp(-b * Math.Pow(xVal, c)));
        };

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

        public ICommand CalculateCommand { get; }

        public string DeviceName { get; }

        public GasConcentrationTablesViewModel(string deviceName)
        {
            DeviceName = deviceName;
            DeviceData = new ObservableCollection<DeviceData>();
            Coefficients = new Coefficients();
            PlotModel = new PlotModel { Title = $"Nonlineer Model Fit: {deviceName}" };
            CalculateCommand = new RelayCommand(_ => CalculateCoefficients(), _ => CanCalculate());
        }

        public void AddDeviceData(DeviceData data)
        {
            DeviceData.Add(data);
        }

        private void CalculateCoefficients()
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

            double meanGasConc = DeviceData.Average(d => d.GasConcentration);
            double sst = DeviceData.Sum(d => Math.Pow(d.GasConcentration - meanGasConc, 2));
            double sse = DeviceData
                .Where(d => !double.IsNaN(d.PredictedGasConcentration ?? double.NaN))
                .Sum(d => Math.Pow(d.GasConcentration - (d.PredictedGasConcentration ?? 0), 2));

            Coefficients.R = 1 - (sse / sst);
        }

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

        private bool CanCalculate()
        {
            return DeviceData != null && DeviceData.Any() &&
                   DeviceData.All(d => d.Ref > 0 && d.Gas > 0);
        }
    }
}
