using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace FCWheel.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;

    public ConfigWindow(Plugin plugin) : base("FCWheel Configuration")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(300, 250),
            MaximumSize = new Vector2(400, 400)
        };
        this.Configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.Text("Custom Flavor Text");
        ImGui.Separator();

        var spinMsg = Configuration.SpinMessage;
        if (ImGui.InputText("Spinning Message", ref spinMsg, 128))
        {
            Configuration.SpinMessage = spinMsg;
            Configuration.Save();
        }

        var winMsg = Configuration.WinMessage;
        if (ImGui.InputText("Winner Message", ref winMsg, 128))
        {
            Configuration.WinMessage = winMsg;
            Configuration.Save();
        }

        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.5f, 0.5f, 0.5f, 1.0f));
        ImGui.TextWrapped("Instructions:");
        ImGui.TextWrapped("Use {name} in the Winner Message to automatically insert the winner's name.");
        ImGui.TextWrapped("Example: {name} has won the jackpot!");
        ImGui.PopStyleColor();
    }
}