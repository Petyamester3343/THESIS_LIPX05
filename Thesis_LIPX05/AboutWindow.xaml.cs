using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Thesis_LIPX05.Util;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow()
        {
            InitializeComponent();
        }

        // Handles the click event of the Close button to close the About window
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, "About window closed.");
        }

        // Handles the mouse left button down event on the YOKAI text block to invert its colors
        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => InvertColor();

        // Inverts the foreground and background colors of the YOKAI text block (a little extra)
        private void InvertColor()
        {
            if (YOKAI.Background is SolidColorBrush backBrush && YOKAI.Foreground is SolidColorBrush foreBrush)
            {
                var foreColor = foreBrush.Color;
                var backColor = backBrush.Color;
                YOKAI.Foreground = new SolidColorBrush(Color.FromArgb(foreColor.A, Convert.ToByte(255 - foreColor.R), Convert.ToByte(255 - foreColor.G), Convert.ToByte(255 - foreColor.B)));
                YOKAI.Background = new SolidColorBrush(Color.FromArgb(backColor.A, Convert.ToByte(255 - backColor.R), Convert.ToByte(255 - backColor.G), Convert.ToByte(255 - backColor.B)));
                MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, "YOKAI colors inverted.");
            }
        }
    }
}
