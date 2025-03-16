using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using CapnoAnalyzer.ViewModels.CalibrationViewModels;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.Optimization;
using OxyPlot;
using OxyPlot.Series;

namespace CapnoAnalyzer.Views.CalibrationViews
{
    /// <summary>
    /// SampleGasConcentrationTable.xaml etkileşim mantığı
    /// </summary>
    public partial class SampleGasConcentrationTable : UserControl
    {
        public SampleGasConcentrationTable()
        {
            InitializeComponent();
            // ViewModel'i oluştur ve DataContext'e ata
            this.DataContext = new SampleGasConcentrationTableViewModel();
        }
    }
}
