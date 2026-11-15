using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using DressSaver.Core;
using DressSaver.Windows;

namespace DressSaver;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;

    private const string CommandName = "/dresssaver";

    public Configuration Configuration { get; }
    public ItemCatalog Items { get; }
    public Extractor Extractor { get; }
    public Remapper Remapper { get; }

    private readonly WindowSystem windowSystem = new("DressSaver");
    private readonly MainWindow mainWindow;

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Items = new ItemCatalog(DataManager);
        Extractor = new Extractor(PluginInterface, Items, Log);
        Remapper = new Remapper(DataManager, Items);

        mainWindow = new MainWindow(this);
        windowSystem.AddWindow(mainWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Ouvre DressSaver pour extraire la tenue portée en mod Penumbra.",
        });

        PluginInterface.UiBuilder.Draw += windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;
    }

    public void SaveConfiguration() => PluginInterface.SavePluginConfig(Configuration);

    private void OnCommand(string command, string args) => ToggleMainUi();

    private void ToggleMainUi() => mainWindow.Toggle();

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= windowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        windowSystem.RemoveAllWindows();
        CommandManager.RemoveHandler(CommandName);
    }
}
