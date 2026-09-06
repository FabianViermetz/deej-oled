using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using NAudio.CoreAudioApi;

namespace DeejFeedback;

public sealed class AudioEngine : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly List<MMDevice> _renderDevices = new();
    private readonly Dictionary<string, List<CachedSession>> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Stopwatch _inventoryAge = new();
    private sealed record CachedSession(AudioSessionControl Control, string Name, string DeviceName);

    // Called on the UI thread. Discovery is independent of the feedback interval.
    public void RefreshRenderInventory(bool force = false)
    {
        if (!force && _inventoryAge.IsRunning && _inventoryAge.ElapsedMilliseconds < 3000) return;
        ClearRenderInventory();
        _inventoryAge.Restart();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            _renderDevices.Add(device);
            try
            {
                var sessions = device.AudioSessionManager.Sessions;
                for (var i = 0; i < sessions.Count; i++)
                {
                    var session = sessions[i];
                    try
                    {
                        var processName = "system";
                        if (session.GetProcessID != 0)
                        {
                            using var process = Process.GetProcessById((int)session.GetProcessID);
                            processName = process.ProcessName + ".exe";
                        }
                        var name = string.IsNullOrWhiteSpace(session.DisplayName) ? processName : session.DisplayName;
                        if (!_sessions.TryGetValue(processName, out var group))
                            _sessions[processName] = group = new();
                        group.Add(new(session, name, device.FriendlyName));
                    }
                    catch { session.Dispose(); }
                }
            }
            catch { /* Retry unavailable devices at the next inventory refresh. */ }
        }
    }

    private void ClearRenderInventory()
    {
        foreach (var group in _sessions.Values)
            foreach (var session in group) session.Control.Dispose();
        _sessions.Clear();
        foreach (var device in _renderDevices) device.Dispose();
        _renderDevices.Clear();
    }

    public IReadOnlyList<AudioSessionInfo> GetRenderSessions()
    {
        var result = new List<AudioSessionInfo>();
        foreach (var group in _sessions)
        {
            foreach (var session in group.Value)
            {
                try
                {
                    var volume = session.Control.SimpleAudioVolume;
                    result.Add(new(group.Key, session.Name, volume.Volume, volume.Mute, session.DeviceName));
                }
                catch { }
            }
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
            finally { device.Dispose(); }
        }
        return result;
    }

    public float SetTargetsVolume(IEnumerable<string> targetNames, float requested)
    {
        var actual = requested;
        foreach (var target in targetNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_sessions.TryGetValue(target, out var sessions)) continue;
            foreach (var session in sessions)
            {
                try
                {
                    session.Control.SimpleAudioVolume.Volume = Math.Clamp(requested, 0, 1);
                    actual = session.Control.SimpleAudioVolume.Volume;
                }
                catch { }
            }
        }
        return actual;
    }

    public PrivacyState TogglePrivacyMute(bool allDevices)
    {
        var devices = GetCaptureEndpoints(allDevices);
        try
        {
            // Only a fully verified muted group may be unmuted by a toggle.
            // Mixed or unreadable groups receive a mute request instead.
            var mute = ReadPrivacyState(devices) != PrivacyState.MutedConfirmed;
            var success = devices.Count > 0;
            foreach (var device in devices)
            {
                try { device.AudioEndpointVolume.Mute = mute; }
                catch { success = false; }
            }
            var actual = ReadPrivacyState(devices);
            return success && actual == (mute ? PrivacyState.MutedConfirmed : PrivacyState.Active)
                ? actual : PrivacyState.Uncertain;
        }
        finally { foreach (var device in devices) device.Dispose(); }
    }

    public PrivacyState GetPrivacyState(bool allDevices)
    {
        var devices = GetCaptureEndpoints(allDevices);
        try { return ReadPrivacyState(devices); }
        finally { foreach (var device in devices) device.Dispose(); }
    }

    private static PrivacyState ReadPrivacyState(IReadOnlyList<MMDevice> devices)
    {
        if (devices.Count == 0) return PrivacyState.Uncertain;
        var muted = 0;
        var failed = false;
        foreach (var device in devices)
        {
            try { if (device.AudioEndpointVolume.Mute) muted++; }
            catch { failed = true; }
        }
        if (failed) return PrivacyState.Uncertain;
        if (muted == devices.Count) return PrivacyState.MutedConfirmed;
        return muted == 0 ? PrivacyState.Active : PrivacyState.Uncertain;
    }

    private List<MMDevice> GetCaptureEndpoints(bool allDevices)
    {
        if (allDevices) return _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).ToList();
        try { return [_enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)]; }
        catch { return []; }
    }

    public void Dispose()
    {
        ClearRenderInventory();
        _enumerator.Dispose();
    }
}
