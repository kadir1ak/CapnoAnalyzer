using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FontAwesome.WPF;

namespace CapnoAnalyzer.Views.Components.Buttons
{
    public partial class ExportButton : UserControl
    {
        public ExportButton()
        {
            InitializeComponent();
        }

        // ButtonContent Özelliği
        public static readonly DependencyProperty ButtonContentProperty =
            DependencyProperty.Register("ButtonContent", typeof(string), typeof(ExportButton), new PropertyMetadata("Export"));

        public string ButtonContent
        {
            get { return (string)GetValue(ButtonContentProperty); }
            set { SetValue(ButtonContentProperty, value); }
        }

        // ButtonBackground Özelliği
        public static readonly DependencyProperty ButtonBackgroundProperty =
            DependencyProperty.Register("ButtonBackground", typeof(Brush), typeof(ExportButton), new PropertyMetadata(new SolidColorBrush(Color.FromRgb(128, 193, 255))));

        public Brush ButtonBackground
        {
            get { return (Brush)GetValue(ButtonBackgroundProperty); }
            set { SetValue(ButtonBackgroundProperty, value); }
        }

        // ButtonForeground Özelliği
        public static readonly DependencyProperty ButtonForegroundProperty =
            DependencyProperty.Register("ButtonForeground", typeof(Brush), typeof(ExportButton), new PropertyMetadata(Brushes.Black));

        public Brush ButtonForeground
        {
            get { return (Brush)GetValue(ButtonForegroundProperty); }
            set { SetValue(ButtonForegroundProperty, value); }
        }

        // ButtonIcon Özelliği
        public static readonly DependencyProperty ButtonIconProperty =
            DependencyProperty.Register("ButtonIcon", typeof(FontAwesomeIcon), typeof(ExportButton), new PropertyMetadata(FontAwesomeIcon.Upload));

        public FontAwesomeIcon ButtonIcon
        {
            get { return (FontAwesomeIcon)GetValue(ButtonIconProperty); }
            set { SetValue(ButtonIconProperty, value); }
        }

        // **Command Özelliği**
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(ExportButton), new PropertyMetadata(null));

        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        // **CommandParameter Özelliği**
        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register("CommandParameter", typeof(object), typeof(ExportButton), new PropertyMetadata(null));

        public object CommandParameter
        {
            get { return GetValue(CommandParameterProperty); }
            set { SetValue(CommandParameterProperty, value); }
        }
    }
}