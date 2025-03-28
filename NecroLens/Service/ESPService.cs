using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using ImGuiNET;
using NecroLens.Model;
using NecroLens.util;
using static NecroLens.util.ESPUtils;

namespace NecroLens.Service;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public class ESPService : IDisposable
{
    private const ushort Tick = 250;

    private readonly List<ESPObject> mapObjects = new List<ESPObject>();
    private readonly Task mapScanner = Task.CompletedTask;
    private bool active;

    public ESPService()
    {
        try
        {
            NecroLens.PluginLog.Debug("ESP Service loading...");

            active = true;

            NecroLens.PluginInterface.UiBuilder.Draw += OnUpdate;
            NecroLens.ClientState.TerritoryChanged += OnCleanup;

            // Enable Scanner
            mapScanner = Task.Run(MapScanner);
        }
        catch (Exception e)
        {
            NecroLens.PluginLog.Error(e.ToString());
        }
    }

    public void Dispose()
    {
        try
        {
            NecroLens.PluginInterface.UiBuilder.Draw -= OnUpdate;
            NecroLens.ClientState.TerritoryChanged -= OnCleanup;
            active = false;
            while (!mapScanner.IsCompleted) NecroLens.PluginLog.Debug("wait till scanner is stopped...");
            mapObjects.Clear();
            NecroLens.PluginLog.Information("ESP Service unloaded");
        }
        catch (Exception e)
        {
            NecroLens.PluginLog.Error(e.ToString());
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
            NecroLens.PluginLog.Error(ex.ToString());
        }
    }

    /**
     * Main-Drawing method.
     */
    private void OnUpdate()
    {
        try
        {
            if (ShouldDraw())
            {
                if (!Monitor.TryEnter(mapObjects)) return;

                var drawList = ImGui.GetBackgroundDrawList();
                foreach (var gameObject in mapObjects) DrawEspObject(drawList, gameObject);

                Monitor.Exit(mapObjects);
            }
        }
        catch (Exception e)
        {
            NecroLens.PluginLog.Error(e.ToString());
        }
    }

    private bool DoDrawName(ESPObject espObject)
    {
        return espObject.Type switch
        {
            ESPObject.ESPType.Player => false,
            ESPObject.ESPType.Enemy => !espObject.InCombat(),
            ESPObject.ESPType.Mimic => !espObject.InCombat(),
            ESPObject.ESPType.FriendlyEnemy => !espObject.InCombat(),
            ESPObject.ESPType.BronzeChest => NecroLens.Config.ShowBronzeCoffers,
            ESPObject.ESPType.SilverChest => NecroLens.Config.ShowSilverCoffers,
            ESPObject.ESPType.GoldChest => NecroLens.Config.ShowGoldCoffers,
            ESPObject.ESPType.AccursedHoard => NecroLens.Config.ShowHoards && !NecroLens.DeepDungeonService.FloorDetails.HoardFound,
            ESPObject.ESPType.MimicChest => NecroLens.Config.ShowMimicCoffer,
            ESPObject.ESPType.Trap => NecroLens.Config.ShowTraps,
            ESPObject.ESPType.Return => NecroLens.Config.ShowReturn,
            ESPObject.ESPType.Passage => NecroLens.Config.ShowPassage,
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
            if (!espObject.GameObject.IsValid())
                return;

            var type = espObject.Type;
            var onScreen = NecroLens.GameGui.WorldToScreen(espObject.GameObject.Position, out var position2D);
            if (onScreen)
            {
                var distance = espObject.Distance();

                if (NecroLens.Config.ShowPlayerDot && type == ESPObject.ESPType.Player)
                    DrawPlayerDot(drawList, position2D);

                if (DoDrawName(espObject))
                    DrawName(drawList, espObject, position2D);

                if (espObject.Type == ESPObject.ESPType.AccursedHoard && NecroLens.Config.ShowHoards && !NecroLens.DeepDungeonService.FloorDetails.HoardFound)
                {
                    var chestRadius = type == ESPObject.ESPType.AccursedHoard ? 2.0f : 1f; // Make Hoards bigger

                    if (distance <= 35 && NecroLens.Config.HighlightCoffers)
                        DrawCircleFilled(drawList, espObject, chestRadius, espObject.RenderColor(), 1f);
                }

                if (espObject.IsChest())
                {
                    if (!NecroLens.Config.ShowBronzeCoffers && type == ESPObject.ESPType.BronzeChest) return;
                    if (!NecroLens.Config.ShowSilverCoffers && type == ESPObject.ESPType.SilverChest) return;
                    if (!NecroLens.Config.ShowGoldCoffers && type == ESPObject.ESPType.GoldChest) return;
                    if (!NecroLens.Config.ShowHoards && type == ESPObject.ESPType.AccursedHoardCoffer) return;

                    if (distance <= 35 && NecroLens.Config.HighlightCoffers)
                        DrawCircleFilled(drawList, espObject, 1f, espObject.RenderColor(), 1f);
                    if (distance <= 10 && NecroLens.Config.ShowCofferInteractionRange)
                        DrawInteractionCircle(drawList, espObject, espObject.InteractionDistance());
                }

                if (NecroLens.Config.ShowTraps && type == ESPObject.ESPType.Trap)
                    DrawCircleFilled(drawList, espObject, 1.7f, espObject.RenderColor());

                if (NecroLens.Config.ShowMimicCoffer && type == ESPObject.ESPType.MimicChest)
                    DrawCircleFilled(drawList, espObject, 1f, espObject.RenderColor());

                if (NecroLens.Config.HighlightPassage && type == ESPObject.ESPType.Passage)
                    DrawCircleFilled(drawList, espObject, 2f, espObject.RenderColor());
            }

            if (NecroLens.Config.ShowMobViews &&
                (type == ESPObject.ESPType.Enemy || type == ESPObject.ESPType.Mimic) &&
                BattleNpcSubKind.Enemy.Equals((BattleNpcSubKind)espObject.GameObject.SubKind) &&
                !espObject.InCombat())
            {
                if (NecroLens.Config.ShowPatrolArrow && espObject.IsPatrol())
                    DrawFacingDirectionArrow(drawList, espObject, Color.Red.ToUint(), 0.6f);

                if (espObject.Distance() <= 50)
                {
                    switch (espObject.AggroType())
                    {
                        case ESPObject.ESPAggroType.Proximity:
                            DrawCircle(drawList, espObject, espObject.AggroDistance(),
                                       NecroLens.Config.NormalAggroColor, DefaultFilledOpacity);
                            break;
                        case ESPObject.ESPAggroType.Sound:
                            DrawCircle(drawList, espObject, espObject.AggroDistance(),
                                       NecroLens.Config.SoundAggroColor, DefaultFilledOpacity);
                            DrawCircleFilled(drawList, espObject, espObject.GameObject.HitboxRadius,
                                             NecroLens.Config.SoundAggroColor, DefaultFilledOpacity);
                            break;
                        case ESPObject.ESPAggroType.Sight:
                            DrawConeFromCenterPoint(drawList, espObject, espObject.SightRadian,
                                                    espObject.AggroDistance(), NecroLens.Config.NormalAggroColor);
                            break;
                        default:
                            NecroLens.PluginLog.Error(
                                $"Unable to process AggroType {espObject.AggroType().ToString()}");
                            break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            NecroLens.PluginLog.Error(e.ToString());
        }
    }



    /**
     * Method returns true if the ESP is Enabled, In valid state and in DeepDungeon
     */
    private bool ShouldDraw()
    {
        return NecroLens.Config.EnableESP &&
               !(NecroLens.Condition[ConditionFlag.LoggingOut] ||
                 NecroLens.Condition[ConditionFlag.BetweenAreas] ||
                 NecroLens.Condition[ConditionFlag.BetweenAreas51]) &&
               DeepDungeonUtil.InDeepDungeon && NecroLens.ClientState.LocalPlayer != null &&
               NecroLens.ClientState.LocalContentId > 0 && NecroLens.ObjectTable.Length > 0 &&
               !NecroLens.DeepDungeonService.FloorDetails.FloorTransfer;
    }

    /**
     * Not-Drawing Scanner method updating mapObjects every Tick.
     */
    private void MapScanner()
    {
        NecroLens.PluginLog.Debug("ESP Background scan started");
        // Keep scanner alive till Dispose()
        while (active)
        {
            try
            {
                if (GetShouldDrawFromMainThread())
                {
                    var entityList = new List<ESPObject>();
                    var waitHandle = new ManualResetEventSlim();
                    NecroLens.Framework.RunOnFrameworkThread(() =>
                    {
                        try
                        {
                            foreach (var obj in NecroLens.ObjectTable)
                            {
                                // Ignore every player object
                                if (obj.IsValid() && !IsIgnoredObject(obj))
                                {
                                    MobInfo mobInfo = null!;
                                    if (obj is IBattleNpc npcObj)
                                        NecroLens.MobService.MobInfoDictionary.TryGetValue(npcObj.NameId, out mobInfo!);

                                    var espObj = new ESPObject(obj, mobInfo);

                                    if (obj.DataId == DataIds.GoldChest
                                        && NecroLens.DeepDungeonService.FloorDetails.DoubleChests.TryGetValue(obj.EntityId, out var value))
                                    {
                                        espObj.ContainingPomander = value;
                                    }

                                    NecroLens.DeepDungeonService.TryInteract(espObj);

                                    entityList.Add(espObj);
                                    NecroLens.DeepDungeonService.TrackFloorObjects(espObj);
                                }

                                if (NecroLens.ClientState.LocalPlayer != null &&
                                    NecroLens.ClientState.LocalPlayer.EntityId == obj.EntityId)
                                    entityList.Add(new ESPObject(obj));
                            }
                            waitHandle.Set();
                        }
                        catch (Exception e)
                        {
                            NecroLens.PluginLog.Error(e.ToString());
                        }
                    });
                    waitHandle.Wait();

                    Monitor.Enter(mapObjects);
                    mapObjects.Clear();
                    mapObjects.AddRange(entityList);
                    Monitor.Exit(mapObjects);
                }
            }
            catch (Exception e)
            {
                NecroLens.PluginLog.Error(e.ToString());
            }

            Thread.Sleep(Tick);
        }
    }

    private bool GetShouldDrawFromMainThread()
    {
        bool shouldDraw = false;
        var waitHandle = new ManualResetEventSlim();
        NecroLens.Framework.RunOnFrameworkThread(() =>
        {
            try
            {
                shouldDraw = ShouldDraw();
                waitHandle.Set();
            }
            catch (Exception e)
            {
                NecroLens.PluginLog.Error(e.ToString());
            }
        });
        waitHandle.Wait();
        return shouldDraw;
    }
}
