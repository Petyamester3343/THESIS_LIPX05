using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

using static Thesis_LIPX05.Util.LogManager;

namespace Thesis_LIPX05
{
    /// <summary>
    /// Interaction logic for AboutWindow.xaml
    /// </summary>
    public partial class AboutWindow : Window
    {
        public AboutWindow() => InitializeComponent();
        
        // Handles the click event of the Close button to close the About window
        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
            Log(LogSeverity.INFO, "About window closed.");
        }

        // Handles the mouse left button down event on the YOKAI text block to invert its colors
        private void TextBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) => InvertColor();

        private static Color Invert(Color og) => 
            Color.FromArgb(og.A, (byte)(255 - og.R), (byte)(255 - og.G), (byte)(255 - og.B));
        
        // Inverts the foreground and background colors of the YOKAI text block (a little extra)
        private void InvertColor()
        {
            if (YOKAI.Background is SolidColorBrush backBrush && YOKAI.Foreground is SolidColorBrush foreBrush)
            {
                Color
                    currFGC = foreBrush.Color,
                    currBGC = backBrush.Color;

                YOKAI.Foreground = new SolidColorBrush(Invert(currFGC));
                YOKAI.Background = new SolidColorBrush(Invert(currBGC));
                
                Log(LogSeverity.INFO, "YOKAI colors inverted.");
            }
        }
    }
}
