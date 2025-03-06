using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using FontAwesome.WPF;

namespace CapnoAnalyzer.Views.Components.Buttons
{
    public partial class PageButton : UserControl
    {
        public PageButton()
        {
            InitializeComponent();
        }

        // ButtonContent Özelliği
        public static readonly DependencyProperty ButtonContentProperty =
            DependencyProperty.Register("ButtonContent", typeof(string), typeof(PageButton), new PropertyMetadata("Button"));

        public string ButtonContent
        {
            get { return (string)GetValue(ButtonContentProperty); }
            set { SetValue(ButtonContentProperty, value); }
        }

        // ButtonBackground Özelliği
        public static readonly DependencyProperty ButtonBackgroundProperty =
            DependencyProperty.Register("ButtonBackground", typeof(Brush), typeof(PageButton), new PropertyMetadata(new SolidColorBrush(Color.FromRgb(92, 109, 140))));

        public Brush ButtonBackground
        {
            get { return (Brush)GetValue(ButtonBackgroundProperty); }
            set { SetValue(ButtonBackgroundProperty, value); }
        }

        // ButtonForeground Özelliği
        public static readonly DependencyProperty ButtonForegroundProperty =
            DependencyProperty.Register("ButtonForeground", typeof(Brush), typeof(PageButton), new PropertyMetadata(Brushes.White));

        public Brush ButtonForeground
        {
            get { return (Brush)GetValue(ButtonForegroundProperty); }
            set { SetValue(ButtonForegroundProperty, value); }
        }

        // ButtonIcon Özelliği
        public static readonly DependencyProperty ButtonIconProperty =
            DependencyProperty.Register("ButtonIcon", typeof(FontAwesomeIcon), typeof(PageButton), new PropertyMetadata(FontAwesomeIcon.QuestionCircle));

        public FontAwesomeIcon ButtonIcon
        {
            get { return (FontAwesomeIcon)GetValue(ButtonIconProperty); }
            set { SetValue(ButtonIconProperty, value); }
        }

        // **Command Özelliği**
        public static readonly DependencyProperty CommandProperty =
            DependencyProperty.Register("Command", typeof(ICommand), typeof(PageButton), new PropertyMetadata(null));

        public ICommand Command
        {
            get { return (ICommand)GetValue(CommandProperty); }
            set { SetValue(CommandProperty, value); }
        }

        // **CommandParameter Özelliği**
        public static readonly DependencyProperty CommandParameterProperty =
            DependencyProperty.Register("CommandParameter", typeof(object), typeof(PageButton), new PropertyMetadata(null));

        public object CommandParameter
        {
            get { return GetValue(CommandParameterProperty); }
            set { SetValue(CommandParameterProperty, value); }
        }

        // **Butona Tıklandığında Komutu Çalıştır**
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (Command != null && Command.CanExecute(CommandParameter))
            {
                Command.Execute(CommandParameter);
            }
        }
    }
}
