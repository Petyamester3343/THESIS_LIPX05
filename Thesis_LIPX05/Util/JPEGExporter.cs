using Microsoft.Win32;

using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using static Thesis_LIPX05.Util.LogManager;

namespace Thesis_LIPX05.Util
{
    internal class JPEGExporter
    {
        // DPI value for high-resolution export
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

            RenderTargetBitmap rtb = RenderedTargetBitmap(cv, (int)cv.ActualWidth, (int)cv.ActualHeight, DPI_VALUE, PixelFormats.Pbgra32);
            LogGeneralActivity(LogSeverity.INFO,
                $"{ctx} RenderTargetBitmap created with size {cv.Width}x{cv.Height} at {DPI_VALUE} DPI.", GeneralLogContext.EXPORT);

            JpegBitmapEncoder enc = EncodedBitmap(rtb);
            LogGeneralActivity(LogSeverity.INFO,
                $"{ctx} JpegBitmapEncoder created and frame added.", GeneralLogContext.EXPORT);

            ExportPicutres(dlg, enc, ctx);
        }

        // Exports two Canvases as a single JPEG image (used for Gantt charts with time ruler)
        public static void ExportMultipleCanvases(Canvas rcv, Canvas gcv, string ctx)
        {
            StackPanel panel = CreateStackPanel(rcv, gcv);

            LogGeneralActivity(LogSeverity.INFO,
                $"{ctx} combined panel measured and arranged with size {panel.DesiredSize.Width}x{panel.DesiredSize.Height}.", GeneralLogContext.EXPORT);

            SaveFileDialog dlg = new()
            {
                Title = $"Export {ctx} as JPEG",
                Filter = "JPEG Image (*.jpg)|*.jpg",
                DefaultExt = "jpg",
                FileName = $"{ctx.ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
                AddExtension = true
            };

            RenderTargetBitmap rtb = RenderedTargetBitmap(panel, (int)panel.DesiredSize.Width, (int)panel.DesiredSize.Height, DPI_VALUE, PixelFormats.Pbgra32);
            LogGeneralActivity(LogSeverity.INFO,
                $"{ctx} combined RenderTargetBitmap created with size {panel.DesiredSize.Width}x{panel.DesiredSize.Height} at {DPI_VALUE} DPI.", GeneralLogContext.EXPORT);

            JpegBitmapEncoder enc = EncodedBitmap(rtb);
            LogGeneralActivity(LogSeverity.INFO,
                $"{ctx} JpegBitmapEncoder created and frame added.", GeneralLogContext.EXPORT);

            ExportPicutres(dlg, enc, ctx);
        }

        // Helper method to create a stack panel
        private static StackPanel CreateStackPanel(Canvas rcv, Canvas gcv)
        {
            StackPanel panel = new() { Orientation = Orientation.Vertical };

            foreach (var cv in new[] { rcv, gcv })
                panel.Children.Add(CloneVisual(cv));

            panel.Measure(availableSize: new(double.PositiveInfinity, double.PositiveInfinity)); // measure the panel to get its desired size
            panel.Arrange(finalRect: new(panel.DesiredSize)); // arrange the panel to apply the measurements

            return panel;
        }

        // Helper method to create an encoded render target bitmap
        private static JpegBitmapEncoder EncodedBitmap(RenderTargetBitmap rtb)
        {
            JpegBitmapEncoder enc = new() { QualityLevel = 100 };
            enc.Frames.Add(BitmapFrame.Create(rtb));
            return enc;
        }
        
        // Helper method to create a raw render target bitmap
        private static RenderTargetBitmap RenderedTargetBitmap(Visual visual, int w, int h, int dpi, PixelFormat pf)
        {
            RenderTargetBitmap rtb = new(w, h, dpi, dpi, pf);
            rtb.Render(visual);
            return rtb;
        }

        // Helper method to export the picture(s)
        private static void ExportPicutres(SaveFileDialog dlg, JpegBitmapEncoder enc, string ctx)
        {
            if (dlg.ShowDialog() is true)
            {
                using FileStream stream = new(dlg.FileName, FileMode.Create);
                enc.Save(stream);
                LogGeneralActivity(LogSeverity.INFO,
                    $"{ctx} exported successfully as {dlg.FileName}.", GeneralLogContext.EXPORT);
                MessageBox.Show($"Canvas exported as {dlg.FileName}",
                    "Export Successful!", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                LogGeneralActivity(LogSeverity.INFO,
                    $"{ctx} export cancelled by user.", GeneralLogContext.EXPORT);
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
                    LogGeneralActivity(LogSeverity.INFO,
                        $"Child element of type {uie.GetType().Name} cloned and added to canvas.", GeneralLogContext.EXPORT);
                }
            }
            LogGeneralActivity(LogSeverity.INFO,
                $"Canvas cloned with {clone.Children.Count} children.", GeneralLogContext.EXPORT);
            return clone;
        }
    }
}
