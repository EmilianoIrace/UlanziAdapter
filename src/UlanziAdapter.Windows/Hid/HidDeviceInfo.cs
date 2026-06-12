namespace UlanziAdapter.Windows.Hid;

public sealed record HidDeviceInfo(
    string DevicePath,
    ushort VendorId,
    ushort ProductId,
    ushort VersionNumber,
    ushort UsagePage,
    ushort Usage,
    ushort InputReportLength,
    ushort OutputReportLength,
    ushort FeatureReportLength,
    string ProductName)
{
    public string DisplayName =>
        $"{ProductNameOrFallback} VID_{VendorId:X4} PID_{ProductId:X4} usage 0x{UsagePage:X4}/0x{Usage:X4}";

    private string ProductNameOrFallback => string.IsNullOrWhiteSpace(ProductName) ? "HID device" : ProductName;
}
