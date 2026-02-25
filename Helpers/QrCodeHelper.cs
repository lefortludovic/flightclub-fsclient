using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace FlightClub.FsClient.Helpers;

public static class QrCodeHelper
{
    public static BitmapImage GenerateQrCode(string content)
    {
        using var qrGenerator = new QRCodeGenerator();
        using var qrCodeData = qrGenerator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var qrCode = new PngByteQRCode(qrCodeData);
        var pngBytes = qrCode.GetGraphic(10);

        var bitmap = new BitmapImage();
        using var stream = new MemoryStream(pngBytes);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = stream;
        bitmap.EndInit();
        bitmap.Freeze();

        return bitmap;
    }
}
