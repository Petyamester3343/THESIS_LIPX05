using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

using Microsoft.Win32;

namespace Thesis_LIPX05.Util
{
    internal class JPEGExporter
    {
        public static void ExportCanvas(Canvas cv, string ctx, int dpi = 96)
        {
            var saveDialog = new SaveFileDialog
            {
                Title = $"Export {ctx} as JPEG",
                Filter = "JPEG Image (*.jpg)|*.jpg",
                DefaultExt = "jpg",
                FileName = $"{ctx.ToLower()}_{DateTime.Now:yyyyMMdd_HHmmss}.jpg",
                AddExtension = true
            };

            if (saveDialog.ShowDialog() == true)
            {
                var size = new Size(Convert.ToInt32(cv.Width), Convert.ToInt32(cv.Height));
                cv.Measure(size);
                cv.Arrange(new(size));

                var rtb = new RenderTargetBitmap((int)size.Width, (int)size.Height, dpi, dpi, PixelFormats.Pbgra32);

                rtb.Render(cv);

                var encoder = new JpegBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using var stream = new FileStream(saveDialog.FileName, FileMode.Create);
                encoder.Save(stream);

                MessageBox.Show($"Canvas exported as {saveDialog.FileName}", "Export Successful!", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else MessageBox.Show($"Export of {saveDialog.FileName} was cancelled", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
