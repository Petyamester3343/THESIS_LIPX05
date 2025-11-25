using Microsoft.Win32;
using System.Globalization;
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
        private const int
            DPI_VALUE = 384,
            DPI_STD = 96;

        // Exports a single Canvas as a JPEG image (solely used on S-Graphs)
        public static void ExportOneCanvas(Canvas cv, string ctx)
        {
            SaveFileDialog dlg = new()
            {
                Title = $"Export {ctx} as JPEG",
                Filter = "JPEG Image (*.jpg)|*.jpg",
                DefaultExt = "jpg",
                FileName = $"{ctx.Replace(" ", "_").ToLower(CultureInfo.InvariantCulture)}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
                AddExtension = true
            };

            Grid wrapper = new() { Background = Brushes.White };
            Canvas clone = CloneVisual(cv);
            wrapper.Children.Add(clone);

            wrapper.Measure(availableSize: new(cv.ActualWidth, cv.ActualHeight));
            wrapper.Arrange(finalRect: new(0, 0, cv.ActualWidth, cv.ActualHeight));

            int
                renderW = (int)Math.Ceiling(cv.Width * ((double)DPI_VALUE / DPI_STD)),
                renderH = (int)Math.Ceiling(cv.Height * ((double)DPI_VALUE / DPI_STD));

            RenderTargetBitmap rtb = RenderedTargetBitmap(
                wrapper, renderW, renderH, DPI_VALUE, PixelFormats.Pbgra32
                );
            LogGeneralActivity(LogSeverity.INFO,
                $"{ctx} RenderTargetBitmap created with size {cv.Width}x{cv.Height} at {DPI_VALUE} DPI.", GeneralLogContext.EXPORT);

            JpegBitmapEncoder enc = EncodedBitmap(rtb);
            LogGeneralActivity(LogSeverity.INFO,
                $"{ctx} JpegBitmapEncoder created and frame added.", GeneralLogContext.EXPORT);

            ExportPicutres(dlg, enc, ctx);
        }

        // Exports two Canvases as a single JPEG image (used for Gantt charts with time ruler)
        public static void ExportMultipleCanvases(Canvas rcv, Canvas gcv, Canvas lcv, string ctx)
        {
            Grid panel = CreateExportGrid(rcv, gcv, lcv);

            LogGeneralActivity(LogSeverity.INFO,
                $"{ctx} combined panel measured and arranged with size {panel.DesiredSize.Width}x{panel.DesiredSize.Height}.", GeneralLogContext.EXPORT);

            SaveFileDialog dlg = new()
            {
                Title = $"Export {ctx} as JPEG",
                Filter = "JPEG Image (*.jpg)|*.jpg",
                DefaultExt = "jpg",
                FileName = $"{ctx.Replace(" ", "_").ToLower(CultureInfo.InvariantCulture)}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
                AddExtension = true
            };

            int
                renderW = (int)Math.Ceiling(panel.DesiredSize.Width * ((double)DPI_VALUE / DPI_STD)),
                renderH = (int)Math.Ceiling(panel.DesiredSize.Height * ((double)DPI_VALUE / DPI_STD));

            RenderTargetBitmap rtb = RenderedTargetBitmap(panel, renderW, renderH, DPI_VALUE, PixelFormats.Pbgra32);
            LogGeneralActivity(LogSeverity.INFO,
                $"{ctx} combined RenderTargetBitmap created with size {panel.DesiredSize.Width}x{panel.DesiredSize.Height} at {DPI_VALUE} DPI.", GeneralLogContext.EXPORT);

            JpegBitmapEncoder enc = EncodedBitmap(rtb);
            LogGeneralActivity(LogSeverity.INFO,
                $"{ctx} JpegBitmapEncoder created and frame added.", GeneralLogContext.EXPORT);

            ExportPicutres(dlg, enc, ctx);
        }

        // Helper method to create a stack panel
        private static Grid CreateExportGrid(Canvas rcv, Canvas gcv, Canvas lcv)
        {
            lcv.Width = 60;
            lcv.Height = rcv.ActualHeight + gcv.ActualHeight;

            // defining the grid container for all three canvases
            Grid exportGrid = new() { Background = Brushes.White };

            exportGrid.ColumnDefinitions.Add(new() { Width = new(60) });
            exportGrid.ColumnDefinitions.Add(new() { Width = new(1, GridUnitType.Star) });

            // measuring and arranging the grid with all canvases as the Gantt diagram
            StackPanel scrollingStack = new() { Orientation = Orientation.Vertical };

            foreach (Canvas cv in new[] { rcv, gcv })
                scrollingStack.Children.Add(CloneVisual(cv));

            scrollingStack.Measure(availableSize: new(double.PositiveInfinity, double.PositiveInfinity)); // measure the panel to get its desired size
            scrollingStack.Arrange(finalRect: new(scrollingStack.DesiredSize)); // arrange the panel to apply the measurements

            Canvas lblClone = CloneVisual(lcv);
            Grid.SetColumn(lblClone, 0);
            exportGrid.Children.Add(lblClone);

            Grid.SetColumn(scrollingStack, 1);
            exportGrid.Children.Add(scrollingStack);

            exportGrid.Measure(availableSize: new(double.PositiveInfinity, double.PositiveInfinity));
            exportGrid.Arrange(finalRect: new(exportGrid.DesiredSize));

            return exportGrid;
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

            foreach (object child in og.Children)
            {
                if (child is UIElement uie)
                {
                    UIElement deepCopy = (UIElement)XamlReader.Parse(XamlWriter.Save(uie));

                    if (!double.IsNaN(Canvas.GetLeft(uie))) Canvas.SetLeft(deepCopy, Canvas.GetLeft(uie));
                    if (!double.IsNaN(Canvas.GetTop(uie))) Canvas.SetTop(deepCopy, Canvas.GetTop(uie));

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