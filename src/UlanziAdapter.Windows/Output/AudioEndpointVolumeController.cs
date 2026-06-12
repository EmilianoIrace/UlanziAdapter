using System.Runtime.InteropServices;

namespace UlanziAdapter.Windows.Output;

public sealed class AudioEndpointVolumeController
{
    public AudioVolumeSnapshot? TryCapture()
    {
        try
        {
            using var endpoint = AudioEndpointVolumeHandle.Create();
            endpoint.Value.GetMasterVolumeLevelScalar(out var volume);
            endpoint.Value.GetMute(out var mute);
            return new AudioVolumeSnapshot(volume, mute);
        }
        catch
        {
            return null;
        }
    }

    public async void RestoreSoon(AudioVolumeSnapshot snapshot)
    {
        try
        {
            await Task.Delay(80).ConfigureAwait(false);
            using var endpoint = AudioEndpointVolumeHandle.Create();
            endpoint.Value.SetMasterVolumeLevelScalar(snapshot.Volume, Guid.Empty);
            endpoint.Value.SetMute(snapshot.Muted, Guid.Empty);
        }
        catch
        {
            // Best-effort guard for leaked system volume events.
        }
    }

    private sealed class AudioEndpointVolumeHandle : IDisposable
    {
        private readonly object _enumerator;
        private readonly object _device;
        private readonly object _endpoint;

        private AudioEndpointVolumeHandle(object enumerator, object device, object endpoint, IAudioEndpointVolume value)
        {
            _enumerator = enumerator;
            _device = device;
            _endpoint = endpoint;
            Value = value;
        }

        public IAudioEndpointVolume Value { get; }

        public static AudioEndpointVolumeHandle Create()
        {
            object enumerator = new MMDeviceEnumerator();
            var deviceEnumerator = (IMMDeviceEnumerator)enumerator;
            deviceEnumerator.GetDefaultAudioEndpoint(EDataFlow.Render, ERole.Multimedia, out var device);

            var iid = typeof(IAudioEndpointVolume).GUID;
            device.Activate(ref iid, ClsCtx.All, IntPtr.Zero, out var endpoint);

            return new AudioEndpointVolumeHandle(enumerator, device, endpoint, (IAudioEndpointVolume)endpoint);
        }

        public void Dispose()
        {
            ReleaseComObject(_endpoint);
            ReleaseComObject(_device);
            ReleaseComObject(_enumerator);
        }

        private static void ReleaseComObject(object? value)
        {
            if (value is not null && Marshal.IsComObject(value))
            {
                Marshal.ReleaseComObject(value);
            }
        }
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private sealed class MMDeviceEnumerator
    {
    }

    private enum EDataFlow
    {
        Render = 0,
        Capture = 1,
        All = 2
    }

    private enum ERole
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }

    [Flags]
    private enum ClsCtx : uint
    {
        InprocServer = 0x1,
        InprocHandler = 0x2,
        LocalServer = 0x4,
        RemoteServer = 0x10,
        All = InprocServer | InprocHandler | LocalServer | RemoteServer
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(EDataFlow dataFlow, uint dwStateMask, out IntPtr ppDevices);

        void GetDefaultAudioEndpoint(EDataFlow dataFlow, ERole role, out IMMDevice ppEndpoint);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(ref Guid iid, ClsCtx dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify(IntPtr pNotify);

        void UnregisterControlChangeNotify(IntPtr pNotify);

        void GetChannelCount(out uint pnChannelCount);

        void SetMasterVolumeLevel(float fLevelDb, Guid pguidEventContext);

        void SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);

        void GetMasterVolumeLevel(out float pfLevelDb);

        void GetMasterVolumeLevelScalar(out float pfLevel);

        void SetChannelVolumeLevel(uint nChannel, float fLevelDb, Guid pguidEventContext);

        void SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);

        void GetChannelVolumeLevel(uint nChannel, out float pfLevelDb);

        void GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);

        void SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, Guid pguidEventContext);

        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);

        void GetVolumeStepInfo(out uint pnStep, out uint pnStepCount);

        void VolumeStepUp(Guid pguidEventContext);

        void VolumeStepDown(Guid pguidEventContext);

        void QueryHardwareSupport(out uint pdwHardwareSupportMask);

        void GetVolumeRange(out float pflVolumeMindB, out float pflVolumeMaxdB, out float pflVolumeIncrementdB);
    }
}

public sealed record AudioVolumeSnapshot(float Volume, bool Muted);
