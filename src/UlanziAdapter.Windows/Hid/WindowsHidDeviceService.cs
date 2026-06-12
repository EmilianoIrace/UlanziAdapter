using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UlanziAdapter.Core.Configuration;

namespace UlanziAdapter.Windows.Hid;

public sealed class WindowsHidDeviceService
{
    public IReadOnlyList<HidDeviceInfo> EnumerateDevices()
    {
        var devices = new List<HidDeviceInfo>();
        NativeMethods.HidD_GetHidGuid(out var hidGuid);

        var infoSet = NativeMethods.SetupDiGetClassDevs(
            ref hidGuid,
            IntPtr.Zero,
            IntPtr.Zero,
            NativeMethods.DIGCF_PRESENT | NativeMethods.DIGCF_DEVICEINTERFACE);

        if (infoSet == NativeMethods.InvalidHandleValue)
        {
            return devices;
        }

        try
        {
            var index = 0u;
            while (true)
            {
                var interfaceData = new NativeMethods.SP_DEVICE_INTERFACE_DATA
                {
                    cbSize = Marshal.SizeOf<NativeMethods.SP_DEVICE_INTERFACE_DATA>()
                };

                if (!NativeMethods.SetupDiEnumDeviceInterfaces(infoSet, IntPtr.Zero, ref hidGuid, index, ref interfaceData))
                {
                    break;
                }

                var path = GetDevicePath(infoSet, interfaceData);
                if (path is not null && TryReadDeviceInfo(path, out var device))
                {
                    devices.Add(device);
                }

                index++;
            }
        }
        finally
        {
            NativeMethods.SetupDiDestroyDeviceInfoList(infoSet);
        }

        return devices;
    }

    public HidDeviceInfo? FindDevice(HidDeviceSelectorConfig selector)
    {
        var devices = EnumerateDevices();
        return devices.FirstOrDefault(device => Matches(device, selector));
    }

    public IReadOnlyList<HidReportApplyResult> ApplyReports(HidConfig config)
    {
        var results = new List<HidReportApplyResult>();
        var device = FindDevice(config.Selector);
        if (device is null)
        {
            results.Add(new HidReportApplyResult(false, "No HID device matched the configured selector."));
            return results;
        }

        using var handle = OpenDevice(device.DevicePath, writeAccess: true);
        if (handle.IsInvalid)
        {
            results.Add(new HidReportApplyResult(false, $"Unable to open HID device for writing: {device.DisplayName}"));
            return results;
        }

        foreach (var report in config.Reports.Where(report => report.Enabled))
        {
            var bytes = HexByteParser.Parse(report.Bytes);
            var type = report.Type.Trim().ToLowerInvariant();
            var payload = PadReport(bytes, type == "feature" ? device.FeatureReportLength : device.OutputReportLength);
            var success = type == "feature"
                ? NativeMethods.HidD_SetFeature(handle, payload, payload.Length)
                : NativeMethods.WriteFile(handle, payload, payload.Length, out _, IntPtr.Zero);

            results.Add(new HidReportApplyResult(
                success,
                success
                    ? $"Applied HID {type} report to {device.DisplayName}: {report.Description ?? report.Bytes}"
                    : $"Failed to apply HID {type} report to {device.DisplayName}: {report.Description ?? report.Bytes}"));

            if (report.DelayAfterMs > 0)
            {
                Thread.Sleep(report.DelayAfterMs);
            }
        }

        return results;
    }

    private static bool Matches(HidDeviceInfo device, HidDeviceSelectorConfig selector)
    {
        if (!string.IsNullOrWhiteSpace(selector.DevicePath) &&
            !string.Equals(device.DevicePath, selector.DevicePath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (selector.VendorId is not null && device.VendorId != selector.VendorId)
        {
            return false;
        }

        if (selector.ProductId is not null && device.ProductId != selector.ProductId)
        {
            return false;
        }

        if (selector.UsagePage is not null && device.UsagePage != selector.UsagePage)
        {
            return false;
        }

        if (selector.Usage is not null && device.Usage != selector.Usage)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.ProductContains) &&
            !device.ProductName.Contains(selector.ProductContains, StringComparison.OrdinalIgnoreCase) &&
            !device.DevicePath.Contains(selector.ProductContains, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static byte[] PadReport(byte[] bytes, ushort expectedLength)
    {
        if (expectedLength == 0 || bytes.Length >= expectedLength)
        {
            return bytes;
        }

        var padded = new byte[expectedLength];
        Array.Copy(bytes, padded, bytes.Length);
        return padded;
    }

    private static string? GetDevicePath(IntPtr infoSet, NativeMethods.SP_DEVICE_INTERFACE_DATA interfaceData)
    {
        NativeMethods.SetupDiGetDeviceInterfaceDetail(
            infoSet,
            ref interfaceData,
            IntPtr.Zero,
            0,
            out var requiredSize,
            IntPtr.Zero);

        if (requiredSize == 0)
        {
            return null;
        }

        var detailDataBuffer = Marshal.AllocHGlobal((int)requiredSize);
        try
        {
            Marshal.WriteInt32(detailDataBuffer, IntPtr.Size == 8 ? 8 : 6);
            var success = NativeMethods.SetupDiGetDeviceInterfaceDetail(
                infoSet,
                ref interfaceData,
                detailDataBuffer,
                requiredSize,
                out _,
                IntPtr.Zero);

            return success ? Marshal.PtrToStringAuto(IntPtr.Add(detailDataBuffer, 4)) : null;
        }
        finally
        {
            Marshal.FreeHGlobal(detailDataBuffer);
        }
    }

    private static bool TryReadDeviceInfo(string path, out HidDeviceInfo device)
    {
        device = null!;
        using var handle = OpenDevice(path, writeAccess: false);
        if (handle.IsInvalid)
        {
            return false;
        }

        var attributes = new NativeMethods.HIDD_ATTRIBUTES
        {
            Size = Marshal.SizeOf<NativeMethods.HIDD_ATTRIBUTES>()
        };

        if (!NativeMethods.HidD_GetAttributes(handle, ref attributes))
        {
            return false;
        }

        var usage = (ushort)0;
        var usagePage = (ushort)0;
        var inputLength = (ushort)0;
        var outputLength = (ushort)0;
        var featureLength = (ushort)0;

        if (NativeMethods.HidD_GetPreparsedData(handle, out var preparsedData))
        {
            try
            {
                if (NativeMethods.HidP_GetCaps(preparsedData, out var caps) == NativeMethods.HIDP_STATUS_SUCCESS)
                {
                    usage = caps.Usage;
                    usagePage = caps.UsagePage;
                    inputLength = caps.InputReportByteLength;
                    outputLength = caps.OutputReportByteLength;
                    featureLength = caps.FeatureReportByteLength;
                }
            }
            finally
            {
                NativeMethods.HidD_FreePreparsedData(preparsedData);
            }
        }

        device = new HidDeviceInfo(
            path,
            attributes.VendorID,
            attributes.ProductID,
            attributes.VersionNumber,
            usagePage,
            usage,
            inputLength,
            outputLength,
            featureLength,
            ReadProductString(handle));

        return true;
    }

    private static string ReadProductString(SafeFileHandle handle)
    {
        var buffer = new byte[256];
        if (!NativeMethods.HidD_GetProductString(handle, buffer, buffer.Length))
        {
            return string.Empty;
        }

        return System.Text.Encoding.Unicode.GetString(buffer).TrimEnd('\0');
    }

    private static SafeFileHandle OpenDevice(string path, bool writeAccess)
    {
        var desiredAccess = writeAccess
            ? NativeMethods.GENERIC_READ | NativeMethods.GENERIC_WRITE
            : 0u;

        return NativeMethods.CreateFile(
            path,
            desiredAccess,
            NativeMethods.FILE_SHARE_READ | NativeMethods.FILE_SHARE_WRITE,
            IntPtr.Zero,
            NativeMethods.OPEN_EXISTING,
            NativeMethods.FILE_ATTRIBUTE_NORMAL,
            IntPtr.Zero);
    }

    private static class NativeMethods
    {
        internal static readonly IntPtr InvalidHandleValue = new(-1);

        internal const uint DIGCF_PRESENT = 0x00000002;
        internal const uint DIGCF_DEVICEINTERFACE = 0x00000010;
        internal const uint GENERIC_READ = 0x80000000;
        internal const uint GENERIC_WRITE = 0x40000000;
        internal const uint FILE_SHARE_READ = 0x00000001;
        internal const uint FILE_SHARE_WRITE = 0x00000002;
        internal const uint OPEN_EXISTING = 3;
        internal const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;
        internal const int HIDP_STATUS_SUCCESS = 0x00110000;

        [DllImport("hid.dll")]
        internal static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool HidD_GetAttributes(SafeFileHandle hidDeviceObject, ref HIDD_ATTRIBUTES attributes);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool HidD_GetProductString(SafeFileHandle hidDeviceObject, byte[] buffer, int bufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool HidD_SetFeature(SafeFileHandle hidDeviceObject, byte[] reportBuffer, int reportBufferLength);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool HidD_GetPreparsedData(SafeFileHandle hidDeviceObject, out IntPtr preparsedData);

        [DllImport("hid.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool HidD_FreePreparsedData(IntPtr preparsedData);

        [DllImport("hid.dll")]
        internal static extern int HidP_GetCaps(IntPtr preparsedData, out HIDP_CAPS capabilities);

        [DllImport("setupapi.dll", SetLastError = true)]
        internal static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            IntPtr enumerator,
            IntPtr hwndParent,
            uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            uint memberIndex,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize,
            out uint requiredSize,
            IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(
            string fileName,
            uint desiredAccess,
            uint shareMode,
            IntPtr securityAttributes,
            uint creationDisposition,
            uint flagsAndAttributes,
            IntPtr templateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool WriteFile(
            SafeFileHandle file,
            byte[] buffer,
            int numberOfBytesToWrite,
            out int numberOfBytesWritten,
            IntPtr overlapped);

        [StructLayout(LayoutKind.Sequential)]
        internal struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public int Flags;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HIDD_ATTRIBUTES
        {
            public int Size;
            public ushort VendorID;
            public ushort ProductID;
            public ushort VersionNumber;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct HIDP_CAPS
        {
            public ushort Usage;
            public ushort UsagePage;
            public ushort InputReportByteLength;
            public ushort OutputReportByteLength;
            public ushort FeatureReportByteLength;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 17)]
            public ushort[] Reserved;

            public ushort NumberLinkCollectionNodes;
            public ushort NumberInputButtonCaps;
            public ushort NumberInputValueCaps;
            public ushort NumberInputDataIndices;
            public ushort NumberOutputButtonCaps;
            public ushort NumberOutputValueCaps;
            public ushort NumberOutputDataIndices;
            public ushort NumberFeatureButtonCaps;
            public ushort NumberFeatureValueCaps;
            public ushort NumberFeatureDataIndices;
        }
    }
}
