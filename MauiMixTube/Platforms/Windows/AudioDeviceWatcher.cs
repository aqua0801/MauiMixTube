#if WINDOWS
using CommunityToolkit.Mvvm.Messaging;
using MauiMixTube.Messages;
using System.Runtime.InteropServices;

namespace MauiMixTube.Audio;

public sealed class AudioDeviceWatcher : IAudioDeviceWatcher, IDisposable
{
    public enum DataFlow
    {
        Render = 0,
        Capture = 1,
        All = 2
    }

    public enum Role
    {
        Console = 0,
        Multimedia = 1,
        Communications = 2
    }
    private readonly IMMDeviceEnumeratorNative _enumerator;
    private readonly DefaultDeviceChangedCallback _callback;
    private bool _isWatching;

    public AudioDeviceWatcher()
    {
        var clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
        var iid = typeof(IMMDeviceEnumeratorNative).GUID;

        int hr = CoCreateInstance(ref clsid, IntPtr.Zero, 1, ref iid, out var ptr);
        Marshal.ThrowExceptionForHR(hr);

        _enumerator = (IMMDeviceEnumeratorNative)Marshal.GetObjectForIUnknown(ptr);
        _callback = new DefaultDeviceChangedCallback();
    }

    public void StartWatching()
    {
        if (_isWatching) return;
        _enumerator.RegisterEndpointNotificationCallback(_callback);
        _isWatching = true;
    }

    public void StopWatching()
    {
        if (!_isWatching) return;
        _enumerator.UnregisterEndpointNotificationCallback(_callback);
        _isWatching = false;
    }

    public void Dispose() => StopWatching();

    [DllImport("ole32.dll")]
    private static extern int CoCreateInstance(
        ref Guid clsid, IntPtr pUnkOuter, uint dwClsContext,
        ref Guid riid, out IntPtr ppv);
}

[ComImport]
[Guid("7991EEC9-7E89-4D85-8390-6C703CEC60C0")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
public interface IMyNotificationClient
{
    [PreserveSig] int OnDeviceStateChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, int dwNewState);
    [PreserveSig] int OnDeviceAdded([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
    [PreserveSig] int OnDeviceRemoved([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId);
    [PreserveSig] int OnDefaultDeviceChanged(int flow, int role, [MarshalAs(UnmanagedType.LPWStr)] string pwstrDefaultDeviceId);
    [PreserveSig] int OnPropertyValueChanged([MarshalAs(UnmanagedType.LPWStr)] string pwstrDeviceId, IntPtr key);
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumeratorNative
{
    int EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr ppDevices);
    int GetDefaultAudioEndpoint(int dataFlow, int role, out IntPtr ppDevice);
    int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IntPtr ppDevice);
    int RegisterEndpointNotificationCallback(IMyNotificationClient pClient);
    int UnregisterEndpointNotificationCallback(IMyNotificationClient pClient);
}

public class DefaultDeviceChangedCallback : IMyNotificationClient
{
    public int OnDefaultDeviceChanged(int flow, int role, string defaultDeviceId)
    {
        if (!Enum.IsDefined(typeof(AudioDeviceWatcher.DataFlow), flow) ||
            !Enum.IsDefined(typeof(AudioDeviceWatcher.Role), role))
            return 0;

        if ((AudioDeviceWatcher.DataFlow)flow == AudioDeviceWatcher.DataFlow.Render &&
            (AudioDeviceWatcher.Role)role == AudioDeviceWatcher.Role.Multimedia)
        {
            WeakReferenceMessenger.Default
                .Send(new AudioDeviceChangedMessage(string.Empty));
        }
        return 0;
    }

    public int OnDeviceStateChanged(string id, int state) => 0;
    public int OnDeviceAdded(string id) => 0;
    public int OnDeviceRemoved(string id) => 0;
    public int OnPropertyValueChanged(string id, IntPtr key) => 0;
}
#endif