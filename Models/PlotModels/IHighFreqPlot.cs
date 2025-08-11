using System;

namespace CapnoAnalyzer.Models.PlotModels
{
    /// <summary>Yüksek frekanslı çizim için basit sözleşme.</summary>
    public interface IHighFreqPlot
    {
        /// <summary>Her örnek için lock-free enqueue.</summary>
        void Enqueue(double time, double gas, double reference);

        /// <summary>Zaman penceresi (saniye).</summary>
        double TimeWindowSeconds { get; set; }

        /// <summary>Maksimum UI refresh FPS (5–120).</summary>
        int MaxFps { get; set; }
    }
}
