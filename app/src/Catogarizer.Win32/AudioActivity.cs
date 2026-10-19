using System.Runtime.InteropServices;
using Catogarizer.Core.Resources;

namespace Catogarizer.Win32;

/// <summary>
/// Which processes are playing sound right now, read from the Windows audio session list of the default
/// output device. Hand-written COM interop (no package, like the rest of the Win32 layer): only the
/// few calls this needs are declared, in vtable order.
/// </summary>
public sealed class AudioActivity : IAudioActivity
{
    private const int AudioSessionStateActive = 1;

    public bool IsReliable { get; private set; } = true;

    public IReadOnlySet<int> PlayingProcessIds()
    {
        var playing = new HashSet<int>();
        object? enumeratorObject = null, device = null, managerObject = null, sessionsObject = null;
        try
        {
            enumeratorObject = new MMDeviceEnumeratorClass();
            var enumerator = (IMMDeviceEnumerator)enumeratorObject;
            Marshal.ThrowExceptionForHR(enumerator.GetDefaultAudioEndpoint(0 /* eRender */, 1 /* eMultimedia */, out var endpoint));
            device = endpoint;

            var iid = typeof(IAudioSessionManager2).GUID;
            Marshal.ThrowExceptionForHR(endpoint.Activate(ref iid, 23 /* CLSCTX_ALL */, IntPtr.Zero, out managerObject));
            var manager = (IAudioSessionManager2)managerObject;
            Marshal.ThrowExceptionForHR(manager.GetSessionEnumerator(out var sessions));
            sessionsObject = sessions;

            Marshal.ThrowExceptionForHR(sessions.GetCount(out var count));
            for (var i = 0; i < count; i++)
            {
                Marshal.ThrowExceptionForHR(sessions.GetSession(i, out var control));
                try
                {
                    if (control is not IAudioSessionControl2 control2) continue;
                    if (control2.GetState(out var state) != 0 || state != AudioSessionStateActive) continue;
                    if (control2.GetProcessId(out var pid) == 0 && pid != 0) playing.Add((int)pid);
                }
                finally
                {
                    Marshal.ReleaseComObject(control);
                }
            }
            IsReliable = true;
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or InvalidComObjectException or UnauthorizedAccessException)
        {
            // No output device, a service restart, an odd driver: freezing then treats "unknown" as "might play".
            IsReliable = false;
        }
        finally
        {
            foreach (var o in new[] { sessionsObject, managerObject, device, enumeratorObject })
                if (o is not null) Marshal.ReleaseComObject(o);
        }
        return playing;
    }

    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumeratorClass;

    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
    }

    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
    }

    [ComImport, Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        // IAudioSessionManager
        [PreserveSig] int GetAudioSessionControl(IntPtr audioSessionGuid, uint streamFlags, out IntPtr sessionControl);
        [PreserveSig] int GetSimpleAudioVolume(IntPtr audioSessionGuid, uint streamFlags, out IntPtr audioVolume);
        // IAudioSessionManager2
        [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator sessionEnum);
    }

    [ComImport, Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig] int GetCount(out int sessionCount);
        [PreserveSig] int GetSession(int sessionCount, out IAudioSessionControl session);
    }

    [ComImport, Guid("F4B1A599-7266-4319-A8CA-E70ACB11E8CD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        [PreserveSig] int GetState(out int state);
    }

    [ComImport, Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        // IAudioSessionControl (all nine, in order)
        [PreserveSig] int GetState(out int state);
        [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);
        [PreserveSig] int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);
        [PreserveSig] int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);
        [PreserveSig] int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);
        [PreserveSig] int GetGroupingParam(out Guid groupingId);
        [PreserveSig] int SetGroupingParam(ref Guid groupingId, ref Guid eventContext);
        [PreserveSig] int RegisterAudioSessionNotification(IntPtr newNotifications);
        [PreserveSig] int UnregisterAudioSessionNotification(IntPtr newNotifications);
        // IAudioSessionControl2
        [PreserveSig] int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetProcessId(out uint processId);
    }
}
