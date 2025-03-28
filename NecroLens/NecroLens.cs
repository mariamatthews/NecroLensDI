#undef DEBUG


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using Dalamud.Game;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using NecroLens.Model;
using NecroLens.Service;
using NecroLens.Windows;
using ECommons.DalamudServices; // Ensure this is referenced

namespace NecroLens;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public sealed class NecroLens : IDalamudPlugin
{
    // 1) Dalamud services
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static IPartyList PartyList { get; private set; } = null!;
    [PluginService] public static IGameNetwork GameNetwork { get; private set; } = null!;



    // ...and any other IPartyList, ITargetManager, etc. you need.

    // 2) Your custom services or references
    public static Configuration Config { get; private set; } = null!;
    public static PluginCommands PluginCommands { get; private set; } = null!;
    public static DeepDungeonService DeepDungeonService { get; private set; } = null!;
    public static MobInfoService MobService { get; private set; } = null!;
    public static ESPService ESPService { get; private set; } = null!;
    public static ConfigWindow ConfigWindow { get; private set; } = null!;
    public static MainWindow MainWindow { get; private set; } = null!;


    public readonly WindowSystem WindowSystem = new("NecroLens");

    public NecroLens(IDalamudPluginInterface? PluginInterface)
    {
        //Plugin = this;

        ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);

        // Load NecroLens.Config
        NecroLens.Config = NecroLens.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        // CommandManager = new ICommandManager(); // Remove this line

        // Initialize custom services once:
        MobService = new MobInfoService();
        DeepDungeonService = new DeepDungeonService();
        //GameNetwork = new GameNetwork();

        // Initialize other parts (windows, commands, etc.)
        PluginCommands = new PluginCommands();
        ConfigWindow = new ConfigWindow();
        MainWindow = new MainWindow();

        WindowSystem.AddWindow(MainWindow);
        WindowSystem.AddWindow(ConfigWindow);

        // Initialize ESP service
        ESPService = new ESPService();
#if DEBUG
        espTestService = new ESPTestService();
#endif
        NecroLens.PluginInterface.UiBuilder.Draw += DrawUI;
        NecroLens.PluginInterface.UiBuilder.OpenConfigUi += ShowConfigWindow;

        CultureInfo.DefaultThreadCurrentUICulture = NecroLens.ClientState.ClientLanguage switch
        {
            ClientLanguage.French => CultureInfo.GetCultureInfo("fr"),
            ClientLanguage.German => CultureInfo.GetCultureInfo("de"),
            ClientLanguage.Japanese => CultureInfo.GetCultureInfo("ja"),
            _ => CultureInfo.GetCultureInfo("en")
        };
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        NecroLens.PluginInterface.UiBuilder.Draw -= DrawUI;
        NecroLens.PluginInterface.UiBuilder.OpenConfigUi -= ShowConfigWindow;

        ConfigWindow.Dispose();
        PluginCommands.Dispose();
        MainWindow.Dispose();
        ESPService.Dispose();
        DeepDungeonService.Dispose();
#if DEBUG
        espTestService.Dispose();
#endif
        MobService.Dispose();
        
        ECommonsMain.Dispose();
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }

    public static void ShowMainWindow()
    {
        MainWindow.IsOpen = true;
    }

    public static void CloseMainWindow()
    {
        MainWindow.IsOpen = false;
    }

    public static void ShowConfigWindow()
    {
        ConfigWindow.IsOpen = true;
    }
}
