using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace DeejFeedback;

public sealed class AppConfig
{
    public string ComPort { get; set; } = "COM3";
    public int BaudRate { get; set; } = 115200;
    public bool StartWithWindows { get; set; }
    public bool StartMinimized { get; set; }
    public bool PrivacyMuteAllCaptureDevices { get; set; } = true;
    public int DisplaySleepSeconds { get; set; } = 30;
    public bool ContinuousFaderUpdates { get; set; }
    public int ControllerHeartbeatMs { get; set; } = 1000;
    public int FeedbackIntervalMs { get; set; } = 500;
    public List<ChannelConfig> Channels { get; set; } = DefaultChannels();

    public static List<ChannelConfig> DefaultChannels() =>
    [
        new() { Index = 0, Name = "Communication", Targets = ["discord.exe", "Zoom.exe", "ts3client_win64.exe", "ms-teams.exe"] },
        new() { Index = 1, Name = "Music", Targets = ["spotify.exe"], MaximumPercent = 100 },
        new() { Index = 2, Name = "Browser", Targets = ["msedge.exe", "chrome.exe", "firefox.exe"] },
        new() { Index = 3, Name = "Games", Targets = ["Anno1800.exe", "Anno117.exe", "League of Legends.exe", "LeagueClientUx.exe", "LeagueClient.exe", "LeagueClientUxRender.exe", "DRL Simulator.exe", "Trackmania.exe", "Diablo IV.exe"] }
    ];
}

public sealed class ChannelConfig
{
    public int Index { get; set; }
    public string Name { get; set; } = "Channel";
    public List<string> Targets { get; set; } = [];
    public int AdcMinimum { get; set; } = 0;
    public int AdcMaximum { get; set; } = 1023;
    public int MinimumPercent { get; set; } = 0;
    public int MaximumPercent { get; set; } = 100;
    public double CurveExponent { get; set; } = 2.0;
    public int DeadbandAdc { get; set; } = 3;
    public bool Inverted { get; set; } = true;
    public bool SoftTakeover { get; set; } = true;
    [JsonIgnore] public int RawValue { get; set; }
    [JsonIgnore] public float RequestedVolume { get; set; }
    [JsonIgnore] public float ActualVolume { get; set; }
    [JsonIgnore] public bool IsLatched { get; set; }
    [JsonIgnore] public bool HasRawValue { get; set; }

    public float MapRawToVolume(int raw)
    {
        var span = Math.Max(1, AdcMaximum - AdcMinimum);
        var normalized = Math.Clamp((raw - AdcMinimum) / (double)span, 0, 1);
        if (Inverted) normalized = 1 - normalized;
        normalized = Math.Pow(normalized, Math.Clamp(CurveExponent, 0.1, 5));
        return (float)((MinimumPercent + normalized * (MaximumPercent - MinimumPercent)) / 100.0);
    }
}

public enum PrivacyState { Active, MutedConfirmed, Uncertain, Disconnected }

public sealed record AudioSessionInfo(string ProcessName, string DisplayName, float Volume, bool Muted, string DeviceName);
public sealed record CaptureDeviceInfo(string Id, string Name, bool Muted, float Peak);
