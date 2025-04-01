using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ImGuiNET;
using NecroLens.Interface;
using NecroLens.Model;
using NecroLens.util;

namespace NecroLens.Service;

/**
 * Test Class for stuff drawing tests -not loaded by default
 */
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class ESPTestService : IDisposable
{
    private readonly IClientState clientState;
    private readonly Configuration configuration;
    private readonly IDeepDungeonService deepDungeonService;
    private readonly ILoggingService logger;
    private readonly IGameGui gameGui;

    public ESPTestService(IClientState clientState, IGameGui gameGui, Configuration configuration, IDeepDungeonService deepDungeonService, ILoggingService logger)
    {
        this.clientState = clientState;
        this.configuration = configuration;
        this.deepDungeonService = deepDungeonService;
        this.gameGui = gameGui;
        this.logger = logger;

        NecroLens.PluginInterface.UiBuilder.Draw += OnDraw;

    }

    public void Dispose()
    {
        NecroLens.PluginInterface.UiBuilder.Draw -= OnDraw;
        deepDungeonService?.Dispose();
        GC.SuppressFinalize(this);
    }

    private void OnDraw()
    {
        if (ShouldDraw())
        {
            var drawList = ImGui.GetBackgroundDrawList();
            var player = clientState.LocalPlayer;
            var espObject = new ESPObject(player!, clientState, configuration, deepDungeonService, logger, null);

            var onScreen = gameGui.WorldToScreen(player!.Position, out _);
            if (onScreen)
            {
                ESPUtils.DrawFacingDirectionArrow(drawList, espObject, Color.Red.ToUint(), 1f, 4f);
            }
        }
    }

    private bool ShouldDraw()
    {
        return !(NecroLens.Condition[ConditionFlag.LoggingOut] ||
                 NecroLens.Condition[ConditionFlag.BetweenAreas] ||
                 NecroLens.Condition[ConditionFlag.BetweenAreas51]) &&
               clientState.LocalPlayer != null &&
               clientState.LocalContentId > 0 && NecroLens.ObjectTable.Length > 0;
    }


}
