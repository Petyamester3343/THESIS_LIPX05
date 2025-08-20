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

            var size = new Size(Convert.ToInt32(cv.Width), Convert.ToInt32(cv.Height));
            cv.Measure(size);
            cv.Arrange(new(size));

            var rtb = new RenderTargetBitmap((int)size.Width, (int)size.Height, dpi, dpi, PixelFormats.Pbgra32);

            rtb.Render(cv);

            var encoder = new JpegBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(rtb));
            
            if (saveDialog.ShowDialog() == true)
            {
                using var stream = new FileStream(saveDialog.FileName, FileMode.Create);
                encoder.Save(stream);

                MessageBox.Show($"Canvas exported as {saveDialog.FileName}", "Export Successful!", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else MessageBox.Show($"Export of {saveDialog.FileName} was cancelled", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public static void ExportMultipleCanvases(Canvas rcv, Canvas gcv, string ctx) // for the Gantt chart with the time ruler
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical
            };

            panel.Children.Add(CloneVisual(rcv));
            panel.Children.Add(CloneVisual(gcv));

            panel.Measure(new(double.PositiveInfinity, double.PositiveInfinity)); // measure the panel to get its desired size
            panel.Arrange(new(panel.DesiredSize)); // arrange the panel to apply the measurements

            var rtb = new RenderTargetBitmap((int)panel.DesiredSize.Width, (int)panel.DesiredSize.Height, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(panel);

            var enc = new JpegBitmapEncoder();
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
            else MessageBox.Show($"Export of {dlg.FileName} was cancelled", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

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
