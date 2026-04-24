using Dalamud.Configuration;
using Dalamud.Plugin;
using System;
using System.Collections.Generic;

namespace FCWheel;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public List<string> Names { get; set; } = new();
    public bool AnnounceWinner { get; set; } = true;
    public string[] Channels { get; set; } = { "/p", "/fc", "/sh", "/s", "/y" };
    public int SelectedChannelIndex { get; set; } = 0;

    // New Flavor Text Settings
    public string SpinMessage { get; set; } = "The wheel is spinning...";
    public string WinMessage { get; set; } = "Congratulations to {name}!";

    [NonSerialized]
    private IDalamudPluginInterface? pluginInterface;

    public void Initialize(IDalamudPluginInterface pluginInterface)
    {
        this.pluginInterface = pluginInterface;
    }

    public void Save()
    {
        this.pluginInterface!.SavePluginConfig(this);
    }
}