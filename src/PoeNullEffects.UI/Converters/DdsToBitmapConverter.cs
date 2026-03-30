using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Pfim;

namespace PoeNullEffects.UI.Converters;

public static class DdsToBitmapConverter
{
    public static BitmapSource? Convert(byte[] ddsData)
    {
        try
        {
            using var ms = new MemoryStream(ddsData);
            using var image = Pfimage.FromStream(ms);

            var wpfFormat = image.Format switch
            {
                ImageFormat.Rgba32 => PixelFormats.Bgra32,
                ImageFormat.Rgb24 => PixelFormats.Bgr24,
                ImageFormat.Rgb8 => PixelFormats.Gray8,
                _ => PixelFormats.Bgra32
            };

            var pinnedData = GCHandle.Alloc(image.Data, GCHandleType.Pinned);
            try
            {
                var bitmap = BitmapSource.Create(
                    image.Width,
                    image.Height,
                    96.0, 96.0,
                    wpfFormat,
                    null,
                    pinnedData.AddrOfPinnedObject(),
                    image.DataLen,
                    image.Stride);

                bitmap.Freeze();
                return bitmap;
            }
            finally
            {
                pinnedData.Free();
            }
        }
        catch
        {
            return null;
        }
    }
}
