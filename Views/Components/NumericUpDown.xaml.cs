using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CapnoAnalyzer.Views.Components;

public partial class NumericUpDown : UserControl
{
    public NumericUpDown()
    {
        InitializeComponent();
    }

    // ── Value ────────────────────────────────────────────────────────────────
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumericUpDown),
            new FrameworkPropertyMetadata(0.0,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnValueChanged, CoerceValue));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // TextBox güncelleme zaten binding ile yapılıyor.
    }

    private static object CoerceValue(DependencyObject d, object baseValue)
    {
        var ctrl = (NumericUpDown)d;
        double v = (double)baseValue;
        v = Math.Clamp(v, ctrl.Minimum, ctrl.Maximum);
        // Adıma yuvarla
        if (ctrl.Step > 0)
            v = Math.Round(v / ctrl.Step) * ctrl.Step;
        return v;
    }

    // ── Minimum ──────────────────────────────────────────────────────────────
    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(NumericUpDown),
            new PropertyMetadata(0.0));

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    // ── Maximum ──────────────────────────────────────────────────────────────
    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NumericUpDown),
            new PropertyMetadata(100.0));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    // ── Step ─────────────────────────────────────────────────────────────────
    public static readonly DependencyProperty StepProperty =
        DependencyProperty.Register(nameof(Step), typeof(double), typeof(NumericUpDown),
            new PropertyMetadata(1.0));

    public double Step
    {
        get => (double)GetValue(StepProperty);
        set => SetValue(StepProperty, value);
    }

    // ── StringFormat ─────────────────────────────────────────────────────────
    public static readonly DependencyProperty StringFormatProperty =
        DependencyProperty.Register(nameof(StringFormat), typeof(string), typeof(NumericUpDown),
            new PropertyMetadata("{0:G}"));

    public string StringFormat
    {
        get => (string)GetValue(StringFormatProperty);
        set => SetValue(StringFormatProperty, value);
    }

    // ── BoxWidth ─────────────────────────────────────────────────────────────
    public static readonly DependencyProperty BoxWidthProperty =
        DependencyProperty.Register(nameof(BoxWidth), typeof(double), typeof(NumericUpDown),
            new PropertyMetadata(50.0));

    public double BoxWidth
    {
        get => (double)GetValue(BoxWidthProperty);
        set => SetValue(BoxWidthProperty, value);
    }

    // ── Button handlers ──────────────────────────────────────────────────────
    private void Increment_Click(object sender, RoutedEventArgs e)
    {
        Value = Math.Clamp(Value + Step, Minimum, Maximum);
    }

    private void Decrement_Click(object sender, RoutedEventArgs e)
    {
        Value = Math.Clamp(Value - Step, Minimum, Maximum);
    }

    // ── TextBox handlers ─────────────────────────────────────────────────────
    private void ValueBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        // Rakam, nokta ve eksi işaretine izin ver
        foreach (char c in e.Text)
        {
            if (!char.IsDigit(c) && c != '.' && c != ',')
            {
                e.Handled = true;
                return;
            }
        }
    }

    private void ValueBox_LostFocus(object sender, RoutedEventArgs e)
    {
        CommitText();
    }

    private void ValueBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            CommitText();
        else if (e.Key == Key.Up)
            Increment_Click(sender, e);
        else if (e.Key == Key.Down)
            Decrement_Click(sender, e);
    }

    private void CommitText()
    {
        if (double.TryParse(ValueBox.Text,
                NumberStyles.Any,
                CultureInfo.InvariantCulture,
                out double parsed))
        {
            Value = parsed; // CoerceValue clamp + round yapacak
        }
        else
        {
            // Geçersiz metin → mevcut değeri yeniden yaz
            UpdateTextBox();
        }
    }

    private void UpdateTextBox()
    {
        ValueBox.Text = string.Format(CultureInfo.InvariantCulture,
            StringFormat, Value);
    }
}
