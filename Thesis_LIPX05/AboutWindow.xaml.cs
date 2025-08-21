using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

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
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // Handles the mouse left button down event on the YOKAI text block to invert its colors
        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => InvertColor();

        // Inverts the foreground and background colors of the YOKAI text block (a little extra)
        private void InvertColor()
        {
            if (YOKAI.Background is SolidColorBrush backBrush && YOKAI.Foreground is SolidColorBrush foreBrush)
            {
                var foreColor = foreBrush.Color;
                var backColor = backBrush.Color;
                YOKAI.Foreground = new SolidColorBrush(Color.FromArgb(foreColor.A, (byte)(255 - foreColor.R), (byte)(255 - foreColor.G), (byte)(255 - foreColor.B)));
                YOKAI.Background = new SolidColorBrush(Color.FromArgb(backColor.A, (byte)(255 - backColor.R), (byte)(255 - backColor.G), (byte)(255 - backColor.B)));
            }
        }
    }
}
