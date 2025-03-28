using System;
using System.Collections.Generic;
using System.Drawing;
using SystemTask = System.Threading.Tasks.Task;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ImGuiNET;
using NecroLens.Interface;
using NecroLens.Model;
using NecroLens.util;
using static NecroLens.util.ESPUtils;


namespace NecroLens.Service;

public class ESPService : IDisposable
{
    private readonly ILoggingService logger;
    private readonly IClientState clientState;
    private readonly Configuration configuration;
    private readonly IObjectTable objectTable;
    private readonly IDeepDungeonService deepDungeonService;
    private readonly IFramework framework;
    private readonly IMobInfoService mobInfoService;
    private readonly IGameGui gameGui;

    private readonly List<ESPObject> mapObjects = new List<ESPObject>();
    private readonly SystemTask mapScanner;

    public ESPService(ILoggingService logger,
        Configuration configuration,
        IMainUIManager mainUIManager,
        IClientState clientState,
        IObjectTable objectTable,
        IDeepDungeonService deepDungeonService,
        IFramework framework,
        IMobInfoService mobInfoService,
        IGameGui gameGui)
    {
        try
        {
            this.logger = logger;
            logger.LogDebug("ESP Service loading...");
            this.clientState = clientState;
            this.objectTable = objectTable;
            this.deepDungeonService = deepDungeonService;
            this.configuration = configuration;
            this.framework = framework;
            this.mobInfoService = mobInfoService;
            this.gameGui = gameGui;

            //NecroLens.PluginInterface.UiBuilder.Draw += OnDraw;
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }
    }

    public void Dispose()
    {
        try
        {
            NecroLens.PluginInterface.UiBuilder.Draw -= OnDraw;
            clientState.TerritoryChanged -= OnCleanup;
            while (!mapScanner.IsCompleted) logger.LogDebug("wait till scanner is stopped...");
            mapObjects.Clear();

            // Dispose of the services
            deepDungeonService?.Dispose();
            mobInfoService?.Dispose();

            logger.LogInformation("ESP Service unloaded");
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }
        finally
        {
            GC.SuppressFinalize(this);
        }
    }

    /**
     * Clears the drawable GameObjects on MapChange.
     */
    private void OnCleanup(ushort e)
    {
        try
        {
            // Example inside your scanning or drawing loop:
            lock (mapObjects)
            {
                mapObjects.RemoveAll(obj => !obj.GameObject.IsValid());
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex.ToString());
        }
    }

    /**
     * Main-Drawing method.
     */
    public void OnDraw()
    {
        try
        {
            // Always draw every frame:
            if (ShouldDraw())
            {
                lock (mapObjects)
                {
                    // For debugging
                    // logger.LogVerbose($"OnDraw(): Rendering {mapObjects.Count} objects.");

                    var drawList = ImGui.GetBackgroundDrawList();
                    foreach (var gameObject in mapObjects)
                    {
                        DrawEspObject(drawList, gameObject);
                    }
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }
    }

    private bool DoDrawName(ESPObject espObject, bool inCombat)
    {
        return espObject.Type switch
        {
            ESPObject.ESPType.Player => false,
            ESPObject.ESPType.Enemy => !inCombat,
            ESPObject.ESPType.Mimic => !inCombat,
            ESPObject.ESPType.FriendlyEnemy => !inCombat,
            ESPObject.ESPType.BronzeChest => configuration.ShowBronzeCoffers,
            ESPObject.ESPType.SilverChest => configuration.ShowSilverCoffers,
            ESPObject.ESPType.GoldChest => configuration.ShowGoldCoffers,
            ESPObject.ESPType.AccursedHoard => configuration.ShowHoards && !deepDungeonService.FloorDetails.HoardFound,
            ESPObject.ESPType.MimicChest => configuration.ShowMimicCoffer,
            ESPObject.ESPType.Trap => configuration.ShowTraps,
            ESPObject.ESPType.Return => configuration.ShowReturn,
            ESPObject.ESPType.Passage => configuration.ShowPassage,
            _ => false
        };
    }

    /**
     * Draws every Object for the ESP-Overlay.
     */
    private void DrawEspObject(ImDrawListPtr drawList, ESPObject espObject)
    {
        try
        {
            // Check the game object validity before doing any work.
            if (espObject.GameObject == null || !espObject.GameObject.IsValid())
                return;

            // Get the screen position.
            var onScreen = gameGui.WorldToScreen(espObject.GameObject.Position, out var position2D);
            if (!onScreen)
                return;

            // Cache the combat status
            bool inCombat = espObject.InCombatCached;

            var distance = 0.0;
            lock (espObject)
            {
                distance = espObject.Distance();
            }

            if (configuration.ShowPlayerDot && espObject.Type == ESPObject.ESPType.Player)
                DrawPlayerDot(drawList, position2D, configuration);

            // Pass the cached inCombat value to DoDrawName.
            if (DoDrawName(espObject, inCombat))
                DrawName(drawList, espObject, position2D, deepDungeonService);

            // Continue drawing based on type and distance...
            if (espObject.Type == ESPObject.ESPType.AccursedHoard
                && configuration.ShowHoards && !deepDungeonService.FloorDetails.HoardFound)
            {
                var chestRadius = espObject.Type == ESPObject.ESPType.AccursedHoard ? 2.0f : 1f;
                if (distance <= 35 && configuration.HighlightCoffers)
                    DrawCircleFilled(drawList, espObject, chestRadius, espObject.RenderColor(), 1f);
            }

            if (espObject.IsChest())
            {
                if (!configuration.ShowBronzeCoffers && espObject.Type == ESPObject.ESPType.BronzeChest) return;
                if (!configuration.ShowSilverCoffers && espObject.Type == ESPObject.ESPType.SilverChest) return;
                if (!configuration.ShowGoldCoffers && espObject.Type == ESPObject.ESPType.GoldChest) return;
                if (!configuration.ShowHoards && espObject.Type == ESPObject.ESPType.AccursedHoardCoffer) return;

                if (distance <= 35 && configuration.HighlightCoffers)
                    DrawCircleFilled(drawList, espObject, 1f, espObject.RenderColor(), 1f);
                if (distance <= 10 && configuration.ShowCofferInteractionRange)
                    DrawInteractionCircle(drawList, espObject, espObject.InteractionDistance());
            }

            if (configuration.ShowTraps && espObject.Type == ESPObject.ESPType.Trap)
                DrawCircleFilled(drawList, espObject, 1.7f, espObject.RenderColor());

            if (configuration.ShowMimicCoffer && espObject.Type == ESPObject.ESPType.MimicChest)
                DrawCircleFilled(drawList, espObject, 1f, espObject.RenderColor());

            if (configuration.HighlightPassage && espObject.Type == ESPObject.ESPType.Passage)
                DrawCircleFilled(drawList, espObject, 2f, espObject.RenderColor());

            if (configuration.ShowMobViews &&
                (espObject.Type == ESPObject.ESPType.Enemy || espObject.Type == ESPObject.ESPType.Mimic) &&
                BattleNpcSubKind.Enemy.Equals((BattleNpcSubKind)espObject.GameObject.SubKind) &&
                !inCombat)
            {
                if (configuration.ShowPatrolArrow && espObject.IsPatrol())
                    DrawFacingDirectionArrow(drawList, espObject, Color.Red.ToUint(), 0.6f);

                if (distance <= 50)
                {
                    switch (espObject.AggroType())
                    {
                        case ESPObject.ESPAggroType.Proximity:
                            DrawCircle(drawList, espObject, espObject.AggroDistance(),
                                       configuration.NormalAggroColor, ESPUtils.DefaultFilledOpacity);
                            break;
                        case ESPObject.ESPAggroType.Sound:
                            DrawCircle(drawList, espObject, espObject.AggroDistance(),
                                       configuration.SoundAggroColor, ESPUtils.DefaultFilledOpacity);
                            DrawCircleFilled(drawList, espObject, espObject.GameObject.HitboxRadius,
                                             configuration.SoundAggroColor, ESPUtils.DefaultFilledOpacity);
                            break;
                        case ESPObject.ESPAggroType.Sight:
                            DrawConeFromCenterPoint(drawList, espObject, espObject.SightRadian,
                                                    espObject.AggroDistance(), configuration.NormalAggroColor);
                            break;
                        default:
                            logger.LogError($"Unable to process AggroType {espObject.AggroType()}");
                            break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError(e.ToString());
        }
    }

    /**
     * Method returns true if the ESP is Enabled, In valid state and in DeepDungeon
     */
    private bool ShouldDraw()
    {
        return configuration.EnableESP &&
               !(NecroLens.Condition[ConditionFlag.LoggingOut] ||
                 NecroLens.Condition[ConditionFlag.BetweenAreas] ||
                 NecroLens.Condition[ConditionFlag.BetweenAreas51]) &&
               DeepDungeonUtil.InDeepDungeon && clientState.LocalPlayer != null &&
               clientState.LocalContentId > 0 && objectTable.Length > 0 &&
               !deepDungeonService.FloorDetails.FloorTransfer;
    }

    public void DoMapScan()
    {
        // If your existing “ShouldDraw()” is also in ESPService, just call it:
        if (!ShouldDraw())
        {
            logger.LogDebug("DoMapScan(): ShouldDraw() returned false, skipping scan.");
            return;
        }

        var entityList = new List<ESPObject>();
        logger.LogDebug($"DoMapScan(): Starting scan of objectTable (length={objectTable.Length}).");

        int count = 0;
        // The rest is basically your old scanning logic:
        foreach (var obj in objectTable)
        {
            if (obj.IsValid() && !IsIgnoredObject(obj))
            {
                MobInfo? mobInfo = null;
                if (obj is IBattleNpc npcObj)
                {
                    mobInfoService.MobInfoDictionary.TryGetValue(npcObj.NameId, out mobInfo);
                }

                var espObj = new ESPObject(obj, clientState, configuration, deepDungeonService, logger, mobInfo);
                espObj.InCombatCached = espObj.InCombat();

                if (obj.DataId == DataIds.GoldChest &&
                    deepDungeonService.FloorDetails.DoubleChests.TryGetValue(obj.EntityId, out var value))
                {
                    espObj.ContainingPomander = value;
                }

                deepDungeonService.TryInteract(espObj);
                entityList.Add(espObj);
                deepDungeonService.TrackFloorObjects(espObj);
                count++;
            }

            if (clientState.LocalPlayer != null &&
                clientState.LocalPlayer.EntityId == obj.EntityId)
            {
                entityList.Add(new ESPObject(obj, clientState, configuration, deepDungeonService, logger, null));
            }
        }

        logger.LogDebug($"DoMapScan(): Found {count} valid objects. Updating mapObjects list.");
        lock (mapObjects)
        {
            mapObjects.Clear();
            mapObjects.AddRange(entityList);
        }
    }
}

