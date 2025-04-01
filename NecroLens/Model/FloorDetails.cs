using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Component.GUI;
using NecroLens.Interface;
using NecroLens.util;
using Newtonsoft.Json;
using static NecroLens.util.DeepDungeonUtil;

namespace NecroLens.Model;

public partial class FloorDetails
{
    private readonly Configuration configuration;
    private readonly ILoggingService logger;
    private readonly IGameGui gameGui;

    public readonly Dictionary<uint, Pomander> DoubleChests = new();
    private readonly List<Pomander> floorEffects = new();
    public readonly Dictionary<uint, FloorObject> FloorObjects = new();
    public readonly List<uint> InteractionList = new();

    private readonly List<Pomander> usedPomanders = new();
    public int CurrentFloor;
    public DateTime FloorStartTime;
    public bool FloorTransfer;
    public bool FloorVerified;
    public bool HoardFound;
    public DateTime NextRespawn;

    public int RespawnTime;

    public FloorDetails(ILoggingService logger, Configuration configuration, IGameGui gameGui)
    {
        this.logger = logger;
        this.configuration = configuration;
        this.gameGui = gameGui;
    }

    [GeneratedRegex("\\d+")]
    private static partial Regex FloorNumber();


    public void Clear()
    {
        usedPomanders.Clear();
        floorEffects.Clear();
        InteractionList.Clear();
        FloorObjects.Clear();
        DoubleChests.Clear();
        FloorVerified = false;
        CurrentFloor = 0;
        FloorTransfer = false;
    }

    public void NextFloor()
    {
        if (FloorTransfer)
        {
            logger.LogInformation($"NextFloor: {CurrentFloor + 1}");

            // Reset
            InteractionList.Clear();
            FloorObjects.Clear();
            DoubleChests.Clear();

            // Apply effects
            floorEffects.Clear();
            if (ContainsAny(usedPomanders, Pomander.Affluence, Pomander.AffluenceProtomander))
                floorEffects.Add(Pomander.Affluence);

            if (ContainsAny(usedPomanders, Pomander.Alteration, Pomander.AlterationProtomander))
                floorEffects.Add(Pomander.Alteration);

            if (ContainsAny(usedPomanders, Pomander.Flight, Pomander.FlightProtomander))
                floorEffects.Add(Pomander.Flight);

            usedPomanders.Clear();
            HoardFound = false;
            CurrentFloor++;
            FloorStartTime = DateTime.Now;
            NextRespawn = DateTime.Now.AddSeconds(RespawnTime);
            FloorTransfer = false;
        }
    }

    public unsafe void VerifyFloorNumber()
    {
        if (TryGetAddonByName<AtkUnitBase>("DeepDungeonMap", out var addon))
        {
            var floorText = addon->GetNodeById(17)->ChildNode->PrevSiblingNode->GetAsAtkTextNode()->NodeText.ToString();
            var floor = int.Parse(FloorNumber().Match(floorText).Value);
            if (CurrentFloor != floor)
            {
                logger.LogInformation("Floor number mismatch - adjusting");
                CurrentFloor = floor;
            }

            FloorVerified = true;
        }
    }

    public unsafe int PassageProgress()
    {
        if (TryGetAddonByName<AtkUnitBase>("DeepDungeonMap", out var addon))
        {
            var key = addon->GetNodeById(7)->ChildNode->PrevSiblingNode;
            var image = key->GetAsAtkComponentNode()->Component->UldManager.NodeList[1]->GetAsAtkImageNode();
            return image->PartId * 10;
        }

        return 0;
    }

    public void OnPomanderUsed(Pomander pomander)
    {
        logger.LogInformation($"Pomander ID: {pomander}");

        if (InEO)
        {
            if (pomander is >= Pomander.Safety and <= Pomander.Serenity) pomander -= 22;

            if (pomander is Pomander.Intuition or Pomander.Raising) pomander -= 20;
        }

        if (pomander is Pomander.Affluence or Pomander.Flight or Pomander.Alteration)
            usedPomanders.Add(pomander);
        else
        {
            floorEffects.Add(pomander);
            usedPomanders.Add(pomander);
        }
    }

    public DeepDungeonTrapStatus TrapStatus()
    {
        if (ContainsAny(floorEffects, Pomander.Safety, Pomander.SafetyProtomander))
            return DeepDungeonTrapStatus.Inactive;

        if (ContainsAny(floorEffects, Pomander.Sight, Pomander.SightProtomander)) return DeepDungeonTrapStatus.Visible;

        return DeepDungeonTrapStatus.Active;
    }

    public bool HasRespawn()
    {
        return !(CurrentFloor % 10 == 0 || (InEO && CurrentFloor == 99));
    }

    public int TimeTillRespawn()
    {
        return (int)(DateTime.Now - NextRespawn).TotalSeconds;
    }

    public int UpdateFloorTime()
    {
        var now = DateTime.Now;
        var time = (int)(now - FloorStartTime).TotalSeconds;
        if (now > NextRespawn) NextRespawn = now.AddSeconds(RespawnTime);
        return time;
    }

    private unsafe bool TryGetAddonByName<T>(string name, out T* addon) where T : unmanaged
    {
        addon = (T*)gameGui.GetAddonByName(name, 1);
        return addon != null;
    }

    public void TrackFloorObjects(ESPObject espObj, int currentContentId)
    {
        if (FloorTransfer
            || IsIgnored(espObj.GameObject.DataId)
            || FloorObjects.ContainsKey(espObj.GameObject.EntityId)) return;

        var obj = new FloorObject();
        obj.DataId = espObj.GameObject.DataId;
        if (espObj.GameObject is IBattleNpc npcObj)
        {
            obj.NameId = npcObj.NameId;
            obj.Name = npcObj.Name.TextValue;
        }

        obj.ContentId = currentContentId;
        obj.Floor = CurrentFloor;
        obj.HitboxRadius = espObj.GameObject.HitboxRadius;
        FloorObjects[espObj.GameObject.EntityId] = obj;
    }

    private bool IsIgnored(uint dataId)
    {
        return DataIds.ReturnIDs.Contains(dataId)
               || DataIds.PassageIDs.Contains(dataId)
               || DataIds.TrapIDs.ContainsKey(dataId)
               || DataIds.GoldChest == dataId
               || DataIds.SilverChest == dataId
               || DataIds.MimicChest == dataId
               || DataIds.BronzeChestIDs.Contains(dataId)
               || DataIds.AccursedHoard == dataId
               || DataIds.AccursedHoardCoffer == dataId;
    }

    public void DumpFloorObjects(int currentContentId)
    {
        if (configuration.OptInDataCollection)
        {
            var result = new Dictionary<uint, DataCollector.MobData>();

            foreach (var keyValuePair in FloorObjects)
            {
                DataCollector.MobData data = new()
                {
                    DataId = keyValuePair.Value.DataId,
                    NameId = keyValuePair.Value.NameId,
                    ContentId = currentContentId,
                    Floor = CurrentFloor,
                    HitboxRadius = keyValuePair.Value.HitboxRadius,
                    MoveTimes = new Collection<float>(),     // TODO
                    AggroDistances = new Collection<float>() // TODO
                };
                result.TryAdd(data.DataId, data);
            }

            var collector = new DataCollector
            {
                Sender = configuration.UniqueId!,
                Party = NecroLens.PartyList.PartyId.ToString(),
                Data = new Collection<DataCollector.MobData>(result.Values.ToList())
            };

            var json = JsonConvert.SerializeObject(collector,
                                                   Formatting.Indented,
                                                   new JsonSerializerSettings
                                                   {
                                                       NullValueHandling = NullValueHandling.Ignore
                                                   });

            Task.Factory.StartNew(async () =>
            {
                using var client = new HttpClient();
                try
                {
                    await client.PostAsync("https://necrolens.jusrv.de/api/import2",
                                           new StringContent(json, Encoding.UTF8, "application/json"));
                }
                catch (Exception e)
                {
                    logger.LogError($"Failed to send data to server: {e.Message}");
                }
            });
        }
    }

    public List<Pomander> GetFloorEffects()
    {
        return floorEffects.OrderBy(e => e.ToString()).ToList();
    }

    public bool IsNextFloorWith(Pomander pomander)
    {
        return usedPomanders.Contains(pomander);
    }

    private bool ContainsAny(List<Pomander> list, params Pomander[] values)
    {
        foreach (var value in values)
        {
            if (list.Contains(value))
            {
                return true;
            }
        }
        return false;
    }
}
