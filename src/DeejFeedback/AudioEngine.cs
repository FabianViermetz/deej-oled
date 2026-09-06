using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NAudio.CoreAudioApi;

namespace DeejFeedback;

public sealed class AudioEngine : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly Dictionary<string, bool> _muteBeforeLock = new();
    public bool PrivacyLock { get; private set; }

    public IReadOnlyList<AudioSessionInfo> GetRenderSessions()
    {
        var result = new List<AudioSessionInfo>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            try
            {
                for (var i = 0; i < device.AudioSessionManager.Sessions.Count; i++)
                {
                    var session = device.AudioSessionManager.Sessions[i];
                    var processName = "system";
                    try { if (session.GetProcessID != 0) processName = Process.GetProcessById((int)session.GetProcessID).ProcessName + ".exe"; } catch { }
                    result.Add(new(processName, string.IsNullOrWhiteSpace(session.DisplayName) ? processName : session.DisplayName,
                        session.SimpleAudioVolume.Volume, session.SimpleAudioVolume.Mute, device.FriendlyName));
                }
            }
            catch { }
        }
        return result;
    }

    public IReadOnlyList<CaptureDeviceInfo> GetCaptureDevices()
    {
        var result = new List<CaptureDeviceInfo>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            try { result.Add(new(device.ID, device.FriendlyName, device.AudioEndpointVolume.Mute, device.AudioMeterInformation.MasterPeakValue)); }
            catch { result.Add(new(device.ID, device.FriendlyName, false, 0)); }
        }
        return result;
    }

    public float SetTargetsVolume(IEnumerable<string> targetNames, float requested)
    {
        var targets = new HashSet<string>(targetNames, StringComparer.OrdinalIgnoreCase);
        var actual = requested;
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            try
            {
                for (var i = 0; i < device.AudioSessionManager.Sessions.Count; i++)
                {
                    var session = device.AudioSessionManager.Sessions[i];
                    string name;
                    try { name = session.GetProcessID == 0 ? "system" : Process.GetProcessById((int)session.GetProcessID).ProcessName + ".exe"; }
                    catch { continue; }
                    if (!targets.Contains(name)) continue;
                    session.SimpleAudioVolume.Volume = Math.Clamp(requested, 0, 1);
                    actual = session.SimpleAudioVolume.Volume;
                }
            }
            catch { }
        }
        return actual;
    }

    public PrivacyState TogglePrivacyMute(bool allDevices)
    {
        if (PrivacyLock) return ReleasePrivacyMute();
        PrivacyLock = true;
        _muteBeforeLock.Clear();
        var devices = GetCaptureEndpoints(allDevices);
        var success = devices.Count > 0;
        foreach (var device in devices)
        {
            try { _muteBeforeLock[device.ID] = device.AudioEndpointVolume.Mute; device.AudioEndpointVolume.Mute = true; success &= device.AudioEndpointVolume.Mute; }
            catch { success = false; }
        }
        return success ? PrivacyState.MutedConfirmed : PrivacyState.Uncertain;
    }

    public PrivacyState EnforcePrivacyMute(bool allDevices)
    {
        if (!PrivacyLock) return PrivacyState.Active;
        var devices = GetCaptureEndpoints(allDevices);
        var success = devices.Count > 0;
        foreach (var device in devices)
        {
            try
            {
                if (!_muteBeforeLock.ContainsKey(device.ID)) _muteBeforeLock[device.ID] = device.AudioEndpointVolume.Mute;
                if (!device.AudioEndpointVolume.Mute) device.AudioEndpointVolume.Mute = true;
                success &= device.AudioEndpointVolume.Mute;
            }
            catch { success = false; }
        }
        return success ? PrivacyState.MutedConfirmed : PrivacyState.Uncertain;
    }

    private PrivacyState ReleasePrivacyMute()
    {
        var success = true;
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            if (!_muteBeforeLock.TryGetValue(device.ID, out var prior)) continue;
            try { device.AudioEndpointVolume.Mute = prior; success &= device.AudioEndpointVolume.Mute == prior; } catch { success = false; }
        }
        PrivacyLock = false;
        _muteBeforeLock.Clear();
        return success ? PrivacyState.Active : PrivacyState.Uncertain;
    }

    private List<MMDevice> GetCaptureEndpoints(bool allDevices)
    {
        if (allDevices) return _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
        try { return [_enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)]; }
        catch { return []; }
    }

    public void Dispose() => _enumerator.Dispose();
}
