using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;
using UlanziAdapter.Core.Configuration;

namespace UlanziAdapter.Windows.Driver;

public sealed class DriverFilterClient
{
    private const string DevicePath = @"\\.\UlanziAdapterFilter";
    private const uint FileDeviceUnknown = 0x00000022;
    private const uint MethodBuffered = 0;
    private const uint FileAnyAccess = 0;
    private const uint IoctlClearRules = (FileDeviceUnknown << 16) | (FileAnyAccess << 14) | (0x800 << 2) | MethodBuffered;
    private const uint IoctlAddRule = (FileDeviceUnknown << 16) | (FileAnyAccess << 14) | (0x801 << 2) | MethodBuffered;
    private const uint IoctlGetStatus = (FileDeviceUnknown << 16) | (FileAnyAccess << 14) | (0x802 << 2) | MethodBuffered;
    private const uint GenericRead = 0x80000000;
    private const uint GenericWrite = 0x40000000;
    private const uint FileShareRead = 0x00000001;
    private const uint FileShareWrite = 0x00000002;
    private const uint OpenExisting = 3;
    private const int MaxReportBytes = 64;
    private const int MaxRuleName = 64;
    private const uint SuppressFlag = 0x00000001;

    public bool IsAvailable()
    {
        using var handle = Open();
        return !handle.IsInvalid;
    }

    public void ClearRules()
    {
        using var handle = OpenOrThrow();
        DeviceIoControlOrThrow(handle, IoctlClearRules, null, 0, null, 0);
    }

    public void AddRule(DriverReportRuleConfig config)
    {
        var match = HexByteParser.Parse(config.Match);
        var replacement = string.IsNullOrWhiteSpace(config.Replacement)
            ? Array.Empty<byte>()
            : HexByteParser.Parse(config.Replacement);

        if (match.Length == 0 || match.Length > MaxReportBytes)
        {
            throw new InvalidOperationException($"Driver rule '{config.Name}' has invalid match length.");
        }

        if (replacement.Length > MaxReportBytes)
        {
            throw new InvalidOperationException($"Driver rule '{config.Name}' has invalid replacement length.");
        }

        var rule = new NativeRule
        {
            Flags = config.Suppress ? SuppressFlag : 0,
            MatchLength = (uint)match.Length,
            ReplacementLength = (uint)replacement.Length
        };

        Array.Copy(match, rule.Match, match.Length);
        Array.Copy(replacement, rule.Replacement, replacement.Length);
        WriteAscii(config.Name ?? "unnamed", rule.Name);

        using var handle = OpenOrThrow();
        var bytes = StructureToBytes(rule);
        DeviceIoControlOrThrow(handle, IoctlAddRule, bytes, bytes.Length, null, 0);
    }

    public DriverFilterStatus GetStatus()
    {
        using var handle = OpenOrThrow();
        var output = new byte[Marshal.SizeOf<NativeStatus>()];
        DeviceIoControlOrThrow(handle, IoctlGetStatus, null, 0, output, output.Length);
        var status = BytesToStructure<NativeStatus>(output);
        return new DriverFilterStatus(status.RuleCount, status.MatchedReports, status.RewrittenReports, status.SuppressedReports);
    }

    private static SafeFileHandle OpenOrThrow()
    {
        var handle = Open();
        if (handle.IsInvalid)
        {
            throw new InvalidOperationException("UlanziAdapterFilter driver is not available. Install and start the KMDF filter driver first.");
        }

        return handle;
    }

    private static SafeFileHandle Open()
    {
        return CreateFile(
            DevicePath,
            GenericRead | GenericWrite,
            FileShareRead | FileShareWrite,
            IntPtr.Zero,
            OpenExisting,
            0,
            IntPtr.Zero);
    }

    private static void DeviceIoControlOrThrow(
        SafeFileHandle handle,
        uint ioctl,
        byte[]? input,
        int inputLength,
        byte[]? output,
        int outputLength)
    {
        var success = DeviceIoControl(
            handle,
            ioctl,
            input,
            inputLength,
            output,
            outputLength,
            out _,
            IntPtr.Zero);

        if (!success)
        {
            throw new InvalidOperationException($"Driver IOCTL 0x{ioctl:X8} failed with Win32 error {Marshal.GetLastWin32Error()}.");
        }
    }

    private static byte[] StructureToBytes<T>(T value) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        var bytes = new byte[size];
        var ptr = Marshal.AllocHGlobal(size);
        try
        {
            Marshal.StructureToPtr(value, ptr, false);
            Marshal.Copy(ptr, bytes, 0, size);
            return bytes;
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static T BytesToStructure<T>(byte[] bytes) where T : struct
    {
        var ptr = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return Marshal.PtrToStructure<T>(ptr);
        }
        finally
        {
            Marshal.FreeHGlobal(ptr);
        }
    }

    private static void WriteAscii(string value, byte[] destination)
    {
        var bytes = System.Text.Encoding.ASCII.GetBytes(value);
        Array.Copy(bytes, destination, Math.Min(bytes.Length, destination.Length - 1));
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern SafeFileHandle CreateFile(
        string fileName,
        uint desiredAccess,
        uint shareMode,
        IntPtr securityAttributes,
        uint creationDisposition,
        uint flagsAndAttributes,
        IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeviceIoControl(
        SafeFileHandle device,
        uint ioControlCode,
        byte[]? inBuffer,
        int inBufferSize,
        byte[]? outBuffer,
        int outBufferSize,
        out int bytesReturned,
        IntPtr overlapped);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct NativeRule
    {
        public uint Flags;
        public uint MatchLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxReportBytes)]
        public byte[] Match;

        public uint ReplacementLength;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxReportBytes)]
        public byte[] Replacement;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = MaxRuleName)]
        public byte[] Name;

        public NativeRule()
        {
            Flags = 0;
            MatchLength = 0;
            Match = new byte[MaxReportBytes];
            ReplacementLength = 0;
            Replacement = new byte[MaxReportBytes];
            Name = new byte[MaxRuleName];
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private struct NativeStatus
    {
        public uint RuleCount;
        public uint MatchedReports;
        public uint RewrittenReports;
        public uint SuppressedReports;
    }
}

public sealed record DriverFilterStatus(uint RuleCount, uint MatchedReports, uint RewrittenReports, uint SuppressedReports);
