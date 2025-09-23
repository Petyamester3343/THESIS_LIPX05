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
        // Exports a single Canvas as a JPEG image (solely used on S-Graphs)
        public static void ExportOneCanvas(Canvas cv, string ctx, int dpi = 96)
        {
            var saveDialog = new SaveFileDialog
            {
                Title = $"Export {ctx} as JPEG",
                Filter = "JPEG Image (*.jpg)|*.jpg",
                DefaultExt = "jpg",
                FileName = $"{ctx.ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
                AddExtension = true
            };

            Size size = new(cv.Width, cv.Height);
            cv.Measure(availableSize: size);
            cv.Arrange(finalRect: new(size));

            var rtb = new RenderTargetBitmap(Convert.ToInt32(size.Width), Convert.ToInt32(size.Height), dpi, dpi, PixelFormats.Pbgra32);

            rtb.Render(cv);

            JpegBitmapEncoder encoder = new();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            
            if (saveDialog.ShowDialog() == true)
            {
                using var stream = new FileStream(saveDialog.FileName, FileMode.Create);
                encoder.Save(stream);

                MessageBox.Show($"Canvas exported as {saveDialog.FileName}", "Export Successful!", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else MessageBox.Show($"Export of {saveDialog.FileName} was cancelled", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Exports two Canvases as a single JPEG image (used for Gantt charts with time ruler)
        public static void ExportMultipleCanvases(Canvas rcv, Canvas gcv, string ctx)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            panel.Children.Add(CloneVisual(rcv));
            panel.Children.Add(CloneVisual(gcv));

            panel.Measure(availableSize: new(double.PositiveInfinity, double.PositiveInfinity)); // measure the panel to get its desired size
            panel.Arrange(finalRect: new(panel.DesiredSize)); // arrange the panel to apply the measurements

            var rtb = new RenderTargetBitmap(Convert.ToInt32(panel.DesiredSize.Width), Convert.ToInt32(panel.DesiredSize.Height), 96, 96, PixelFormats.Pbgra32);
            rtb.Render(panel);

            JpegBitmapEncoder enc = new();
            enc.Frames.Add(BitmapFrame.Create(rtb));

            var dlg = new SaveFileDialog
            {
                Title = $"Export {ctx} as JPEG",
                Filter = "JPEG Image (*.jpg)|*.jpg",
                DefaultExt = "jpg",
                FileName = $"{ctx.ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
                AddExtension = true
            };

            if (dlg.ShowDialog() == true)
            {
                using var stream = new FileStream(dlg.FileName, FileMode.Create);
                enc.Save(stream);
                MessageBox.Show($"Canvas exported as {dlg.FileName}", "Export Successful!", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else MessageBox.Show($"Export of {dlg.FileName} was cancelled", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Clones a Canvas and its children to avoid modifying the original
        // (it returns a Canvas object instead of one of UIElement in favor of performance-related improvements)
        private static Canvas CloneVisual(Canvas og)
        {
            var clone = new Canvas
            {
                Width = og.ActualWidth,
                Height = og.ActualHeight,
                Background = og.Background
            };

            foreach (var child in og.Children)
            {
                if (child is UIElement uie)
                {
                    var xaml = XamlWriter.Save(uie);
                    var deepCopy = (UIElement)XamlReader.Parse(xaml);
                    clone.Children.Add(deepCopy);
                }
            }
            return clone;
        }
    }
}
