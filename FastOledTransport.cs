using System;
using System.Drawing;
using System.Linq;
using HidSharp;

namespace SteelseriesDockCustomiser;

/// <summary>
/// Experimental, opt-in OLED frame transport for the secret mini FPS only.
/// No firmware writes, brightness changes, device-info reads, or background
/// activity. It merely sends the same bounded draw feature reports used by
/// the independently documented Nova Pro display protocol. The existing
/// SHA-verified ggoled CLI remains responsible for restoring the OEM screen.
/// </summary>
internal sealed class FastOledTransport : IDisposable
{
    private const int VendorId = 0x1038;
    private const int PacketLength = 1024;
    private const byte ScreenReportId = 0x06;
    private readonly object gate = new();
    private HidStream? stream;

    private static HidDevice? FindOledDevice()
    {
        // Explicitly limit support to the Nova Pro Wireless dock family.
        // The OLED collection has a 1024-byte feature report. Other SteelSeries
        // devices or HID collections are never opened or modified.
        foreach (var device in DeviceList.Local.GetHidDevices(VendorId))
        {
            if (device.ProductID is not (0x12e0 or 0x12e5 or 0x225d))
                continue;
            try
            {
                if (device.GetMaxFeatureReportLength() < PacketLength) continue;
                byte[] descriptor = device.GetRawReportDescriptor();
                if (descriptor.Length < 2 || descriptor[1] != 0xc0) continue;
                return device;
            }
            catch { /* Unsupported HID collection: never guess. */ }
        }
        return null;
    }

    public static string ValidateAvailable()
    {
        try
        {
            return FindOledDevice() is null
                ? "Desteklenen Nova Pro Wireless OLED HID koleksiyonu bulunamadı."
                : "";
        }
        catch (Exception e)
        {
            return "OLED algılama hatası: " + e.Message;
        }
    }

    public void Open()
    {
        lock (gate)
        {
            if (stream != null) throw new InvalidOperationException("OLED zaten açık.");
            var device = FindOledDevice() ??
                throw new InvalidOperationException(
                    "Doğrulanmış Nova Pro OLED koleksiyonu bulunamadı.");
            stream = device.Open();
            stream.WriteTimeout = 600;
        }
    }

    /// <summary>Build one of the two column-major 64x64 OLED feature reports.</summary>
    public static byte[] EncodeHalf(Bitmap image, int startX)
    {
        if (image.Width != 128 || image.Height != 64 ||
            startX is not (0 or 64))
            throw new ArgumentException("Only 128×64 images, halves 0/64, are supported.");
        var report = new byte[PacketLength];
        report[0] = ScreenReportId;
        report[1] = 0x93; // OLED draw command; never changes firmware.
        report[2] = (byte)startX;
        report[3] = 0;
        report[4] = 64;
        report[5] = 64;
        for (int x = 0; x < 64; x++)
        for (int y = 0; y < 64; y++)
        {
            var color = image.GetPixel(startX + x, y);
            if (color.R < 128 && color.G < 128 && color.B < 128) continue;
            int offset = 6 + x * 8 + y / 8;
            report[offset] |= (byte)(1 << (y % 8));
        }
        return report;
    }

    public void SendFrame(Bitmap image)
    {
        // Build packets before acquiring the device lock. Neither this
        // operation nor testing EncodeHalf requires hardware access.
        var left = EncodeHalf(image, 0);
        var right = EncodeHalf(image, 64);
        lock (gate)
        {
            if (stream is null) throw new InvalidOperationException("OLED bağlantısı kapalı.");
            stream.SetFeature(left);
            stream.SetFeature(right);
        }
    }

    public void Dispose()
    {
        lock (gate)
        {
            stream?.Dispose();
            stream = null;
        }
    }
}
