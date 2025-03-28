using System;
using System.Drawing;
using System.Linq;
using System.Numerics;
using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using NecroLens.Data;
using NecroLens.Interface;
using NecroLens.util;

namespace NecroLens.Model;

[Serializable]
public class ESPObject
{
    public enum ESPAggroType
    {
        Sight,
        Sound,
        Proximity
    }

    public enum ESPDangerLevel
    {
        Easy,
        Caution,
        Danger
    }

    public enum ESPType
    {
        Player,
        Enemy,
        Mimic,
        FriendlyEnemy,
        BronzeChest,
        SilverChest,
        GoldChest,
        AccursedHoard,
        AccursedHoardCoffer,
        MimicChest,
        Trap,
        Return,
        Passage
    }

    private readonly IClientState? clientState;
    private readonly Configuration? configuration;
    private readonly IDeepDungeonService? deepDungeonService;
    private readonly ILoggingService logger;
    private readonly MobInfo? mobInfo;

    public Pomander? ContainingPomander { get; set; } = null;
    public ESPType Type { get; set; } = ESPType.Enemy;

    // For "sight" mobs, ~90° angle
    public float SightRadian { get; set; } = 1.571f;

    public IGameObject GameObject { get; }
    public bool InCombatCached { get; internal set; }

    public ESPObject(
        IGameObject gameObject,
        IClientState clientState,
        Configuration configuration,
        IDeepDungeonService deepDungeonService,
        ILoggingService logger,
        MobInfo? mobInfo)
    {
        this.GameObject = gameObject;
        this.clientState = clientState;
        this.configuration = configuration;
        this.deepDungeonService = deepDungeonService;
        this.logger = logger;
        this.mobInfo = mobInfo;

        ContainingPomander = null;

        if (this.mobInfo != null)
        {
            if (this.deepDungeonService != null &&
                DeepDungeonContentInfo.ContentMobInfoChanges
                   .TryGetValue(this.deepDungeonService.CurrentContentId, out var overrideInfos))
            {
                // Safe read of NameId
                var nameId = SafelyReadGameObject<uint>(
                    g => ((IBattleNpc)g).NameId,
                    0,
                    "Constructor-NameId"
                );

                var mobOverride = overrideInfos.FirstOrDefault(m => m.Id == nameId);
                if (mobOverride != null)
                {
                    this.mobInfo.Patrol = mobOverride.Patrol ?? this.mobInfo.Patrol;
                    this.mobInfo.AggroType = mobOverride.AggroType ?? this.mobInfo.AggroType;
                }
            }
        }
        else
        {
            // Safely read memory for DataId and EntityId
            var dataId = SafelyReadGameObject<uint>(g => g.DataId, 0, "Constructor-DataId");
            var entityId = SafelyReadGameObject<uint>(g => g.EntityId, 0, "Constructor-EntityId");

            if (clientState.LocalPlayer != null && clientState.LocalPlayer.EntityId == entityId)
                Type = ESPType.Player;
            else if (DataIds.BronzeChestIDs.Contains(dataId))
                Type = ESPType.BronzeChest;
            else if (DataIds.SilverChest == dataId)
                Type = ESPType.SilverChest;
            else if (DataIds.GoldChest == dataId)
                Type = ESPType.GoldChest;
            else if (DataIds.MimicChest == dataId)
                Type = ESPType.MimicChest;
            else if (DataIds.AccursedHoard == dataId)
                Type = ESPType.AccursedHoard;
            else if (DataIds.AccursedHoardCoffer == dataId)
                Type = ESPType.AccursedHoardCoffer;
            else if (DataIds.PassageIDs.Contains(dataId))
                Type = ESPType.Passage;
            else if (DataIds.ReturnIDs.Contains(dataId))
                Type = ESPType.Return;
            else if (DataIds.TrapIDs.ContainsKey(dataId))
                Type = ESPType.Trap;
            else if (DataIds.FriendlyIDs.Contains(dataId))
                Type = ESPType.FriendlyEnemy;
            else if (DataIds.MimicIDs.Contains(dataId))
                Type = ESPType.Mimic;
            else
                Type = ESPType.Enemy;
        }
    }
    // -----------------------
    // Safe read helpers
    // -----------------------
    private T SafelyReadGameObject<T>(
        Func<IGameObject, T> readFunc,
        T defaultValue,
        string operationName)
    {
        if (GameObject == null || !GameObject.IsValid())
            return defaultValue;

        try
        {
            return readFunc(GameObject);
        }
        catch (AccessViolationException ave)
        {
            logger.LogError($"{operationName}: AccessViolationException.\n{ave}");
            return defaultValue;
        }
        catch (Exception ex)
        {
            logger.LogError($"{operationName}: Exception.\n{ex}");
            return defaultValue;
        }
    }

    private T SafelyReadCharacter<T>(
        Func<ICharacter, T> readFunc,
        T defaultValue,
        string operationName)
    {
        // Check if it's an IBattleNpc + ICharacter, etc.
        if (GameObject == null || !GameObject.IsValid() || GameObject is not IBattleNpc npc)
            return defaultValue;

        if (npc is not ICharacter character)
            return defaultValue;

        if (character.Address == IntPtr.Zero)
        {
            logger.LogDebug($"{operationName}: character.Address is zero.");
            return defaultValue;
        }

        try
        {
            return readFunc(character);
        }
        catch (AccessViolationException ave)
        {
            logger.LogError($"{operationName}: AccessViolationException occurred.\n{ave}");
            return defaultValue;
        }
        catch (Exception ex)
        {
            logger.LogError($"{operationName}: Exception occurred.\n{ex}");
            return defaultValue;
        }
    }

    private Vector3 SafelyReadObjectPosition(IGameObject? obj, string operationName)
    {
        if (obj == null || !obj.IsValid())
            return Vector3.Zero;

        try
        {
            return obj.Position;
        }
        catch (AccessViolationException ave)
        {
            logger.LogError($"{operationName}: AccessViolationException.\n{ave}");
            return Vector3.Zero;
        }
        catch (Exception ex)
        {
            logger.LogError($"{operationName}: Exception.\n{ex}");
            return Vector3.Zero;
        }
    }


    /**
     * Most monsters have different aggro distances. 10.8y is roughly a safe value. Expect PotD Mimics ... 14.6 ._.
     */
    public float AggroDistance()
    {
        return Type == ESPType.Mimic && DeepDungeonUtil.InPotD ? 14.6f : 10.8f;
    }

    public ESPAggroType AggroType()
    {
        return mobInfo?.AggroType ?? ESPAggroType.Proximity;
    }

    public ESPDangerLevel DangerLevel()
    {
        return mobInfo?.DangerLevel ?? ESPDangerLevel.Easy;
    }

    public bool IsBossOrAdd()
    {
        return mobInfo?.BossOrAdd ?? false;
    }

    public bool IsSpecialMob()
    {
        return mobInfo?.Special ?? false;
    }

    public bool IsPatrol()
    {
        // If special override
        if (mobInfo != null && mobInfo.Id == 7305)
        {
            // Safely read DataId
            var dataId = SafelyReadGameObject<uint>(g => g.DataId, 0, "IsPatrol");
            return dataId == 8922;
        }

        return mobInfo?.Patrol ?? false;
    }

    public float InteractionDistance()
    {
        return Type switch
        {
            ESPType.BronzeChest => 3.1f,
            ESPType.SilverChest => 4.4f,
            ESPType.GoldChest => 4.4f,
            ESPType.AccursedHoardCoffer => 4.4f,
            _ => 2f
        };
    }

    // Example: a memory-read method using SafelyReadCharacter
    public bool InCombat()
    {
        return SafelyReadCharacter(
            c => (c.StatusFlags & StatusFlags.InCombat) != 0,
            false,
            "InCombat"
        );
    }

    public float Distance()
    {
        // If no clientState, can't get local player
        if (clientState == null)
            return 0f;

        var localPlayer = clientState.LocalPlayer;
        if (localPlayer == null)
            return 0f;

        // Safely read local player’s position
        var playerPos = SafelyReadObjectPosition(localPlayer, "Distance-LocalPlayerPos");

        // Safely read this GameObject’s position
        var objPos = SafelyReadObjectPosition(GameObject, "Distance-GameObjectPos");

        // If either is zero, distance is 0
        if (playerPos == Vector3.Zero || objPos == Vector3.Zero)
            return 0f;

        // Do your 2D distance
        return objPos.Distance2D(playerPos);
    }
    public bool IsChest()
    {
        return Type is ESPType.BronzeChest or ESPType.SilverChest or ESPType.GoldChest or ESPType.AccursedHoardCoffer;
    }

    public uint RenderColor()
    {
        switch (Type)
        {
            case ESPType.Enemy:
                return DangerLevel() switch
                {
                    ESPDangerLevel.Danger => Color.Red.ToUint(),
                    ESPDangerLevel.Caution => Color.OrangeRed.ToUint(),
                    _ => Color.White.ToUint()
                };
            case ESPType.FriendlyEnemy:
                return Color.LightGreen.ToUint();
            case ESPType.Mimic:
            case ESPType.MimicChest:
            case ESPType.Trap:
                return Color.Red.ToUint();
            case ESPType.Return:
                return Color.LightBlue.ToUint();
            case ESPType.Passage:
                return configuration?.PassageColor ?? Color.White.ToUint();
            case ESPType.AccursedHoard:
            case ESPType.AccursedHoardCoffer:
                return configuration?.HoardColor ?? Color.White.ToUint();
            case ESPType.GoldChest:
                return configuration?.GoldCofferColor ?? Color.White.ToUint();
            case ESPType.SilverChest:
                return configuration?.SilverCofferColor ?? Color.White.ToUint();
            case ESPType.BronzeChest:
                return configuration?.BronzeCofferColor ?? Color.White.ToUint();
            default:
                return Color.White.ToUint();
        }
    }

    public string? NameSymbol()
    {
        if (IsSpecialMob()) return "\uE0C0";
        if (IsPatrol()) return "\uE05E";

        return Type switch
        {
            ESPType.Trap => "\uE0BF",
            ESPType.AccursedHoard => "\uE03C",
            ESPType.BronzeChest => "\uE03D",
            ESPType.SilverChest => "\uE03D",
            ESPType.GoldChest => "\uE03D",
            ESPType.Return => "\uE03B",
            ESPType.Passage => "\uE035",
            ESPType.FriendlyEnemy => "\uE034",
            _ => null
        };
    }

    public string Name()
    {
        // We don't want to see Bosses and Adds
        if (IsBossOrAdd())
            return "";

        // Safely read SubKind from the game object
        var subKind = SafelyReadGameObject(
            g => g.SubKind,
            (byte)0,
            "Name-SubKind"
        );

        // If Type=Enemy but SubKind != Enemy, skip name
        if (Type == ESPType.Enemy && subKind != (byte)BattleNpcSubKind.Enemy)
            return "";

        // Begin building the name string
        var name = "";

        // Symbol from NameSymbol() (safe, no direct memory read)
        var symbol = NameSymbol();
        if (symbol != null)
            name += symbol + " ";

        // Safely read DataId if needed
        var dataId = SafelyReadGameObject(
            g => g.DataId,
            0u,
            "Name-DataId"
        );

        // Decide how to get the display name
        string mainText;
        switch (Type)
        {
            case ESPType.Trap:
                if (DataIds.TrapIDs.TryGetValue(dataId, out var trapValue))
                    mainText = trapValue;
                else
                    mainText = Strings.Traps_Unknown;
                break;

            case ESPType.AccursedHoard:
                mainText = Strings.Chest_Accursed_Hoard;
                break;

            case ESPType.BronzeChest:
                mainText = Strings.Chest_Bronze_Chest;
                break;

            case ESPType.SilverChest:
                mainText = Strings.Chest_Silver_Chest;
                break;

            case ESPType.GoldChest:
                mainText = Strings.Chest_Gold_Chest;
                break;

            case ESPType.MimicChest:
                mainText = Strings.Chest_Mimic;
                break;

            default:
                // Safely read the Name text
                mainText = SafelyReadGameObject(
                    g => g.Name.TextValue,
                    string.Empty,
                    "Name-TextValue"
                );
                break;
        }

        name += mainText;

        // If Type == Passage, append distance
        if (Type == ESPType.Passage)
        {
            name += " - " + Distance().ToString("0.0");
        }

        // Debug info
        if (configuration.ShowDebugInformation)
        {
            name += "\nD:" + dataId;

            // If cast to IBattleNpc is valid, read NameId
            var npcNameId = SafelyReadGameObject(
                g => ((IBattleNpc)g).NameId,
                0u,
                "Name-NameId"
            );

            if (npcNameId != 0)
            {
                name += " N:" + npcNameId;
            }
        }

        return name;
    }
}
