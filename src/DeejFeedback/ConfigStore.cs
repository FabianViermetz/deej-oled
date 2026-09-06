using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using YamlDotNet.RepresentationModel;

namespace DeejFeedback;

public sealed class ConfigStore
{
    public string DirectoryPath { get; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeejFeedback");
    public string ConfigPath => Path.Combine(DirectoryPath, "config.json");

    public AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new AppConfig();
            return JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigPath), JsonOptions()) ?? new AppConfig();
        }
        catch { return new AppConfig(); }
    }

    public void Save(AppConfig config)
    {
        Directory.CreateDirectory(DirectoryPath);
        var temporary = ConfigPath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(config, JsonOptions()));
        File.Move(temporary, ConfigPath, true);
    }

    public AppConfig ImportLegacyYaml(string path, AppConfig basis)
    {
        using var reader = File.OpenText(path);
        var yaml = new YamlStream();
        yaml.Load(reader);
        var root = (YamlMappingNode)yaml.Documents[0].RootNode;
        if (root.Children.TryGetValue("com_port", out var port)) basis.ComPort = port.ToString();
        if (root.Children.TryGetValue("baud_rate", out var baud) && int.TryParse(baud.ToString(), out var b)) basis.BaudRate = b;
        // Not copied: the legacy firmware inverted and squared ADC values
        // before deej received them. The new firmware sends raw values, so the
        // equivalent mapping is expressed by the channel defaults.
        if (root.Children.TryGetValue("slider_mapping", out var mappingNode) && mappingNode is YamlMappingNode mapping)
        {
            foreach (var entry in mapping.Children)
            {
                if (!int.TryParse(entry.Key.ToString(), out var index) || index is < 0 or > 3) continue;
                var channel = basis.Channels.FirstOrDefault(c => c.Index == index) ?? new ChannelConfig { Index = index };
                channel.Targets = entry.Value switch
                {
                    YamlSequenceNode seq => seq.Children.Select(x => x.ToString()).Where(x => !IsSpecial(x)).ToList(),
                    _ when !IsSpecial(entry.Value.ToString()) => [entry.Value.ToString()],
                    _ => []
                };
                if (!basis.Channels.Contains(channel)) basis.Channels.Add(channel);
            }
        }
        return basis;
    }

    private static bool IsSpecial(string value) => value.Equals("mic", StringComparison.OrdinalIgnoreCase);
    private static JsonSerializerOptions JsonOptions() => new() { WriteIndented = true, PropertyNameCaseInsensitive = true };
}
