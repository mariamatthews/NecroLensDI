#undef DEBUG


using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Threading;
using Dalamud.Game;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using NecroLens.Model;
using NecroLens.Service;
using NecroLens.Windows;
using NecroLens.Interface;
using NecroLens.Logging;
using Dalamud.Game.Command;
using NecroLens.Data;
using System; // Ensure this is referenced

namespace NecroLens;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
[SuppressMessage("ReSharper", "UnusedType.Global")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public sealed class NecroLens : IDalamudPlugin, IMainUIManager
{
    //Dalamud services
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static ICondition Condition { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IGameGui GameGui { get; private set; } = null!;
    [PluginService] internal static IPluginLog PluginLog { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IPartyList PartyList { get; private set; } = null!;
    [PluginService] internal static IGameNetwork GameNetwork { get; private set; } = null!;
    [PluginService] internal static IChatGui Chat { get; private set; } = null!;


    // Custom services
    public Configuration Configuration { get; init; }
    public static DeepDungeonService DeepDungeonService { get; private set; } = null!;
    public static MobInfoService MobService { get; private set; } = null!;
    public static ESPService ESPService { get; private set; } = null!;
    public static ConfigWindow ConfigWindow { get; private set; } = null!;
    public static MainWindow MainWindow { get; private set; } = null!;

    private readonly MobInfoService mobService;
    private readonly DeepDungeonService deepDungeonService;
    private readonly ESPService espService;

    public readonly WindowSystem WindowSystem = new("NecroLens");

    // Commands
    private const string NecroLensCmd = "/necrolens";
    private const string NecroLensCfgCmd = "/necrolenscfg";
    private const string OpenChestCmd = "/openchest";
    private const string PomanderCmd = "/pomander";

    // Rate-limit the scans:
    private DateTime lastScanTime = DateTime.MinValue;
    private readonly TimeSpan scanInterval = TimeSpan.FromMilliseconds(1000);

    public NecroLens(IDalamudPluginInterface PluginInterface, IFramework framework)
    {
        ILoggingService logger = new DalamudLoggingService(PluginLog);
        Framework = framework;

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        //ECommonsMain.Init(PluginInterface, this, Module.DalamudReflector);

        // Initialize ESP service
        mobService = new MobInfoService(logger);
        deepDungeonService = new DeepDungeonService(logger, Configuration, this, GameNetwork, DataManager, ClientState, mobService, ObjectTable, GameGui);
        espService = new ESPService(logger, Configuration, this, ClientState, ObjectTable, deepDungeonService, Framework, mobService, GameGui );
////#if DEBUG
////        espTestService = new ESPTestService();
////#endif

        PluginInterface.UiBuilder.Draw += espService.OnDraw;
        PluginInterface.UiBuilder.Draw += DrawUI;
        Framework.Update += OnFrameworkUpdate;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        CultureInfo.DefaultThreadCurrentUICulture = ClientState.ClientLanguage switch
        {
            ClientLanguage.French => CultureInfo.GetCultureInfo("fr"),
            ClientLanguage.German => CultureInfo.GetCultureInfo("de"),
            ClientLanguage.Japanese => CultureInfo.GetCultureInfo("ja"),
            _ => CultureInfo.GetCultureInfo("en")
        };

        // Initialize commands
        InitialiseCommands();
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow?.Dispose();
        MainWindow?.Dispose();
        ESPService?.Dispose();
        DeepDungeonService?.Dispose();
        MobService?.Dispose();

        //ECommonsMain.Dispose();

//#if DEBUG
//        espTestService?.Dispose();
//#endif

        CommandManager.RemoveHandler("/necrolens");
        CommandManager.RemoveHandler("/necrolenscfg");
        CommandManager.RemoveHandler("/openchest");
        CommandManager.RemoveHandler("/pomander");
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        // Rate-limit the scans:
        if (DateTime.UtcNow - lastScanTime >= scanInterval)
        {
            lastScanTime = DateTime.UtcNow;
            PluginLog.Debug("Calling DoMapScan()");
            espService.DoMapScan();
            PluginLog.Debug("Finished DoMapScan()");
        }
    }

    private void DrawUI()
    {
        WindowSystem.Draw();
    }
    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();

    private void InitialiseCommands()
    {
        AddCommandHandler(NecroLensCmd, OnNecroLens, Strings.PluginCommands_OpenOverlay_Help);
        AddCommandHandler(NecroLensCfgCmd, OnNecroLensCfg, Strings.PluginCommands_OpenConfig_Help);
        AddCommandHandler(OpenChestCmd, OnOpenChest, Strings.PluginCommands_OpenChest_Help);
        AddCommandHandler(PomanderCmd, OnPomander, "Try to use the pomander with given name");
    }

    private void AddCommandHandler(string command, IReadOnlyCommandInfo.HandlerDelegate handler, string helpMessage)
    {
        CommandManager.AddHandler(command, new CommandInfo(handler)
        {
            HelpMessage = helpMessage,
            ShowInHelp = true
        });
    }

    private void OnNecroLens(string command, string args)
    {
        ToggleMainUI();
    }
    private void OnNecroLensCfg(string command, string args)
    {
        ToggleConfigUI();
    }
    private void OnOpenChest(string command, string args)
    {
        deepDungeonService.TryNearestOpenChest();
    }
    private void OnPomander(string command, string args)
    {
        deepDungeonService.OnPomanderCommand(args);
    }
}
