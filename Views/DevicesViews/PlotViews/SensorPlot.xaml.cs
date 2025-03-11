using OxyPlot;
using OxyPlot.Wpf;
using System.Windows.Controls;

namespace CapnoAnalyzer.Views.DevicesViews.PlotViews
{
    public partial class SensorPlot : UserControl
    {
        public SensorPlot()
        {
            InitializeComponent();

            #region Plot için tüm varsayılan fare eylemlerini kaldır
            var customController = new PlotController();
            customController.UnbindAll();
            customController.BindMouseDown(OxyMouseButton.Left, OxyPlot.PlotCommands.Track);
            Chart.Controller = customController;
            #endregion

        }
    }
}
