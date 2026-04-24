using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using FCWheel.Windows;

namespace FCWheel;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static ITargetManager TargetManager { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;

    public Configuration Configuration { get; init; }
    public WindowSystem WindowSystem = new("FCWheel");
    private MainWindow MainWindow { get; init; }
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Initialize(PluginInterface);

        MainWindow = new MainWindow(this);
        ConfigWindow = new ConfigWindow(this);

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler("/fcwheel", new CommandInfo(OnCommand) { HelpMessage = "Opens the wheel." });
        CommandManager.AddHandler("/fcwheelconfig", new CommandInfo(OnConfigCommand) { HelpMessage = "Opens settings." });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler("/fcwheel");
        CommandManager.RemoveHandler("/fcwheelconfig");
    }

    private void OnCommand(string c, string a) => ToggleMainUi();
    private void OnConfigCommand(string c, string a) => ToggleConfigUi();

    public void ToggleMainUi() => MainWindow.IsOpen = !MainWindow.IsOpen;
    public void ToggleConfigUi() => ConfigWindow.IsOpen = !ConfigWindow.IsOpen;
    private void DrawUI() => WindowSystem.Draw();
}