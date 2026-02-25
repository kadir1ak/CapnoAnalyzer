using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CapnoAnalyzer.Views.Components
{
    public partial class KnobControl : UserControl
    {
        public static readonly DependencyProperty ValueProperty =
            DependencyProperty.Register("Value", typeof(double), typeof(KnobControl),
                new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

        public static readonly DependencyProperty MinimumProperty =
            DependencyProperty.Register("Minimum", typeof(double), typeof(KnobControl), new PropertyMetadata(0.0));

        public static readonly DependencyProperty MaximumProperty =
            DependencyProperty.Register("Maximum", typeof(double), typeof(KnobControl), new PropertyMetadata(100.0));

        public static readonly DependencyProperty StepProperty =
            DependencyProperty.Register("Step", typeof(double), typeof(KnobControl), new PropertyMetadata(1.0));

        public static readonly DependencyProperty TitleProperty =
            DependencyProperty.Register("Title", typeof(string), typeof(KnobControl), new PropertyMetadata("Knob"));

        public static readonly DependencyProperty StringFormatProperty =
            DependencyProperty.Register("StringFormat", typeof(string), typeof(KnobControl), new PropertyMetadata("N1"));

        public static readonly DependencyProperty AngleProperty =
            DependencyProperty.Register("Angle", typeof(double), typeof(KnobControl), new PropertyMetadata(-135.0));

        public static readonly DependencyProperty FormatValueProperty =
            DependencyProperty.Register("FormatValue", typeof(string), typeof(KnobControl), new PropertyMetadata("AUTO"));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }
        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }
        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }
        public double Step
        {
            get => (double)GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }
        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        public string StringFormat
        {
            get => (string)GetValue(StringFormatProperty);
            set => SetValue(StringFormatProperty, value);
        }
        public double Angle
        {
            get => (double)GetValue(AngleProperty);
            private set => SetValue(AngleProperty, value);
        }
        public string FormatValue
        {
            get => (string)GetValue(FormatValueProperty);
            private set => SetValue(FormatValueProperty, value);
        }

        private bool _isDragging = false;
        private double _lastAngleAtan;

        public KnobControl()
        {
            InitializeComponent();
            UpdateVisuals();
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KnobControl control)
            {
                control.UpdateVisuals();
            }
        }

        private void UpdateVisuals()
        {
            double val = Value;
            if (val < Minimum) val = Minimum;
            if (val > Maximum) val = Maximum;

            double ratio = (Maximum > Minimum) ? (val - Minimum) / (Maximum - Minimum) : 0;
            Angle = -135 + (ratio * 270);

            if (val == 0)
            {
                FormatValue = "AUTO";
            }
            else
            {
                int decimalPlaces = 0;
                double range = Maximum - Minimum;
                
                if (range <= 0.05) decimalPlaces = 4;
                else if (range <= 0.5) decimalPlaces = 3;
                else if (range <= 5.0) decimalPlaces = 2;
                else if (range <= 20.0) decimalPlaces = 1;
                else decimalPlaces = 0;

                int stepDecimals = 0;
                double currentStep = Step;
                if (currentStep > 0)
                {
                    while (Math.Abs(Math.Round(currentStep, stepDecimals) - currentStep) > 1e-6 && stepDecimals < 6)
                    {
                        stepDecimals++;
                    }
                }
                
                decimalPlaces = Math.Max(decimalPlaces, stepDecimals);
                string dynamicFormat = "N" + decimalPlaces;
                FormatValue = val.ToString(dynamicFormat);
            }
        }

        private void OnMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double delta = e.Delta > 0 ? Step : -Step;
            SetValueSafe(Value + delta);
            e.Handled = true;
        }

        private void OnMouseLeftDown(object sender, MouseButtonEventArgs e)
        {
            _isDragging = true;
            Point currentPosition = e.GetPosition(this);
            
            double centerX = ActualWidth / 2.0;
            double centerY = ActualHeight / 2.0;
            double dx = currentPosition.X - centerX;
            double dy = currentPosition.Y - centerY;

            _lastAngleAtan = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            Mouse.Capture(this);
        }

        private void OnMouseLeftUp(object sender, MouseButtonEventArgs e)
        {
            _isDragging = false;
            Mouse.Capture(null);
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (!_isDragging) return;

            Point currentPosition = e.GetPosition(this);
            double centerX = ActualWidth / 2.0;
            double centerY = ActualHeight / 2.0;
            double dx = currentPosition.X - centerX;
            double dy = currentPosition.Y - centerY;

            // Eğer fare tam merkezdeyse açı hesaplamak titreşime sebep olur
            if (Math.Sqrt(dx * dx + dy * dy) < 5) return;

            double currentAngleAtan = Math.Atan2(dy, dx) * 180.0 / Math.PI;
            double deltaAngle = currentAngleAtan - _lastAngleAtan;

            // -180 ve +180 derece geçişlerini yumuşat
            if (deltaAngle > 180) deltaAngle -= 360;
            if (deltaAngle < -180) deltaAngle += 360;

            if (Math.Abs(deltaAngle) > 0)
            {
                double range = Maximum - Minimum;
                if (range > 0)
                {
                    // Arayüzde tuş 270 derece dönüyor
                    double valueChange = (deltaAngle / 270.0) * range;
                    SetValueSafe(Value + valueChange);
                }
                
                _lastAngleAtan = currentAngleAtan;
            }
        }

        private void SetValueSafe(double newValue)
        {
            if (newValue < Minimum) newValue = Minimum;
            if (newValue > Maximum) newValue = Maximum;
            Value = newValue;
        }
        private void OnMouseRightDown(object sender, MouseButtonEventArgs e)
        {
            SettingsPopup.IsOpen = true;
            e.Handled = true;
        }

        private void OnClosePopupClick(object sender, RoutedEventArgs e)
        {
            SettingsPopup.IsOpen = false;
        }
    }
}
