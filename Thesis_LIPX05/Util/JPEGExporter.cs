using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.Win32;

namespace Thesis_LIPX05.Util
{
    internal class JPEGExporter
    {
        private const int DPI_VALUE = 384;
        
        // Exports a single Canvas as a JPEG image (solely used on S-Graphs)
        public static void ExportOneCanvas(Canvas cv, string ctx)
        {
            SaveFileDialog dlg = new()
            {
                Title = $"Export {ctx} as JPEG",
                Filter = "JPEG Image (*.jpg)|*.jpg",
                DefaultExt = "jpg",
                FileName = $"{ctx.ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
                AddExtension = true
            };

            RenderTargetBitmap rtb = new(
                Convert.ToInt32(cv.Width),
                Convert.ToInt32(cv.Height),
                DPI_VALUE, DPI_VALUE,
                PixelFormats.Pbgra32);

            rtb.Render(cv);
            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                $"{ctx} RenderTargetBitmap created with size {cv.Width}x{cv.Height} at {DPI_VALUE} DPI.");

            JpegBitmapEncoder enc = new();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                $"{ctx} JpegBitmapEncoder created and frame added.");

            if (dlg.ShowDialog() == true)
            {
                using FileStream stream = new(dlg.FileName, FileMode.Create);
                enc.Save(stream);

                MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                    $"{ctx} exported successfully as {dlg.FileName}.");
                MessageBox.Show($"Canvas exported as {dlg.FileName}",
                    "Export Successful!", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                    $"{ctx} export cancelled by user.");
                MessageBox.Show($"Export of {dlg.FileName} was cancelled",
                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Exports two Canvases as a single JPEG image (used for Gantt charts with time ruler)
        public static void ExportMultipleCanvases(Canvas rcv, Canvas gcv, string ctx)
        {
            StackPanel panel = new() { Orientation = Orientation.Vertical };

            foreach (var cv in new[] { rcv, gcv }) panel.Children.Add(CloneVisual(cv));
            
            panel.Measure(availableSize: new(double.PositiveInfinity, double.PositiveInfinity)); // measure the panel to get its desired size
            panel.Arrange(finalRect: new(panel.DesiredSize)); // arrange the panel to apply the measurements
            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO, $"{ctx} combined panel measured and arranged with size {panel.DesiredSize.Width}x{panel.DesiredSize.Height}.");

            RenderTargetBitmap rtb = new(
                Convert.ToInt32(panel.DesiredSize.Width),
                Convert.ToInt32(panel.DesiredSize.Height),
                DPI_VALUE, DPI_VALUE,
                PixelFormats.Pbgra32);
            rtb.Render(panel);
            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                $"{ctx} combined RenderTargetBitmap created with size {panel.DesiredSize.Width}x{panel.DesiredSize.Height} at {DPI_VALUE} DPI.");

            JpegBitmapEncoder enc = new();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                $"{ctx} JpegBitmapEncoder created and frame added.");

            SaveFileDialog dlg = new()
            {
                Title = $"Export {ctx} as JPEG",
                Filter = "JPEG Image (*.jpg)|*.jpg",
                DefaultExt = "jpg",
                FileName = $"{ctx.ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
                AddExtension = true
            };

            if (dlg.ShowDialog() == true)
            {
                using FileStream stream = new(dlg.FileName, FileMode.Create);
                enc.Save(stream);
                MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                    $"{ctx} exported successfully as {dlg.FileName}.");
                MessageBox.Show($"Canvas exported as {dlg.FileName}",
                    "Export Successful!", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                    $"{ctx} export cancelled by user.");
                MessageBox.Show($"Export of {dlg.FileName} was cancelled",
                    "Info", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        // Clones a Canvas and its children to avoid modifying the original
        // (it returns a Canvas object instead of one of UIElement in favor of performance-related improvements)
        private static Canvas CloneVisual(Canvas og)
        {
            Canvas clone = new()
            {
                Width = og.ActualWidth,
                Height = og.ActualHeight,
                Background = og.Background
            };

            foreach (var child in og.Children)
            {
                if (child is UIElement uie)
                {
                    string xaml = XamlWriter.Save(uie);
                    UIElement deepCopy = (UIElement)XamlReader.Parse(xaml);

                    double
                        left = Canvas.GetLeft(uie),
                        top = Canvas.GetTop(uie);

                    if (!double.IsNaN(left)) Canvas.SetLeft(deepCopy, left);
                    if (!double.IsNaN(top)) Canvas.SetTop(deepCopy, top);

                    clone.Children.Add(deepCopy);
                    MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                        $"Child element of type {uie.GetType().Name} cloned and added to canvas.");
                }
            }
            MainWindow.GetLogger().Log(LogManager.LogSeverity.INFO,
                $"Canvas cloned with {clone.Children.Count} children.");
            return clone;
        }
    }
}
