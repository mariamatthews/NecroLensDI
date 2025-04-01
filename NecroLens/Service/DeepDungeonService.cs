using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Timers;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.Network;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Lumina.Excel.Sheets;
using NecroLens.Interface;
using NecroLens.Model;
using NecroLens.util;
using static NecroLens.util.DeepDungeonUtil;
using GameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;
using System.Threading.Tasks;
using Task = System.Threading.Tasks.Task;
using static FFXIVClientStructs.FFXIV.Client.Graphics.Render.ModelRenderer;

namespace NecroLens.Service
{
    public class DeepDungeonService : IDisposable, IDeepDungeonService
    {
        private readonly ILoggingService logger;
        private readonly Configuration configuration;
        private readonly IMainUIManager mainUIManager;
        private readonly IClientState clientState;
        private readonly IMobInfoService mobService;
        private readonly IObjectTable objectTable;
        private readonly IGameNetwork gameNetwork;
        private readonly IGameGui gameGui;
        private readonly Dictionary<Pomander, string> pomanderNames = new();
        public IReadOnlyDictionary<Pomander, string> PomanderNames => pomanderNames;

        private readonly Dictionary<int, int> floorTimes = new();
        public Dictionary<int, int> FloorTimes => floorTimes;
        public FloorDetails FloorDetails { get; }

        private readonly Timer floorTimer;

        public int CurrentContentId { get; private set; }
        public DeepDungeonContentInfo.DeepDungeonFloorSetInfo? FloorSetInfo;

        public bool Ready;

        private readonly TaskManager taskManager;

        public DeepDungeonService(
            ILoggingService logger,
            Configuration configuration,
            IMainUIManager mainUIManager,
            IGameNetwork gameNetwork,
            IDataManager dataManager,
            IClientState clientState,
            IMobInfoService mobService,
            IObjectTable objectTable,
            IGameGui gameGui)
        {
            this.logger = logger;
            this.configuration = configuration;
            this.mainUIManager = mainUIManager;
            this.clientState = clientState;
            this.mobService = mobService;
            this.objectTable = objectTable;
            this.gameNetwork = gameNetwork;
            this.gameGui = gameGui;

            gameNetwork.NetworkMessage += NetworkMessage;

            floorTimer = new Timer
            {
                Interval = 1000
            };
            floorTimer.Elapsed += OnTimerUpdate;

            Ready = false;

            FloorDetails = new FloorDetails(logger, configuration, gameGui);

            taskManager = new TaskManager();
            // Remove the line causing the error as TaskManager does not have a DefaultConfiguration property
            // taskManager.DefaultConfiguration.TimeoutSilently = true;

            var sheet = dataManager.GetExcelSheet<DeepDungeonItem>(clientState.ClientLanguage);
            if (sheet != null)
            {
                foreach (var pomander in sheet.Skip(1))
                {
                    pomanderNames[(Pomander)pomander.RowId] = pomander.Name.ToString();
                }
            }
        }

        private void EnterDeepDungeon(int contentId, DeepDungeonContentInfo.DeepDungeonFloorSetInfo info)
        {
            FloorSetInfo = info;
            CurrentContentId = contentId;
            logger.LogInformation($"Entering ContentID {CurrentContentId}");

            FloorTimes.Clear();

            mobService.TryReloadIfEmpty();

            for (var i = info.StartFloor; i < info.StartFloor + 10; i++)
                FloorTimes[i] = 0;

            FloorDetails.CurrentFloor = info.StartFloor - 1; // NextFloor() adds 1
            FloorDetails.RespawnTime = info.RespawnTime;
            FloorDetails.FloorTransfer = true;
            FloorDetails.NextFloor();

            if (configuration.AutoOpenOnEnter)
                mainUIManager.ToggleMainUI();

            floorTimer.Start();
            Ready = true;
        }

        private void ExitDeepDungeon()
        {
            logger.LogInformation($"ContentID {CurrentContentId} - Exiting");

            FloorDetails.DumpFloorObjects(CurrentContentId);

            floorTimer.Stop();
            FloorSetInfo = null;
            FloorDetails.Clear();
            Ready = false;

            mainUIManager.ToggleMainUI();
        }

        private void OnTimerUpdate(object? sender, ElapsedEventArgs e)
        {
            if (!InDeepDungeon)
            {
                logger.LogInformation("Failsafe exit");
                ExitDeepDungeon();
            }

            if (!FloorDetails.FloorVerified)
                FloorDetails.VerifyFloorNumber();

            var time = FloorDetails.UpdateFloorTime();
            FloorTimes[FloorDetails.CurrentFloor] = time;
        }

        private void NetworkMessage(
            IntPtr dataPtr, ushort opCode, uint sourceActorId, uint targetActorId, NetworkMessageDirection direction)
        {
            if (direction == NetworkMessageDirection.ZoneDown)
            {
                switch (opCode)
                {
                    case (int)ServerZoneIpcType.SystemLogMessage:
                        OnSystemLogMessage(dataPtr, ReadNumber(dataPtr, 4, 4));
                        break;
                    case (int)ServerZoneIpcType.ActorControlSelf:
                        OnActorControlSelf(dataPtr);
                        break;
                }
            }
        }

        private void OnActorControlSelf(IntPtr dataPtr)
        {
            if (Marshal.ReadByte(dataPtr) == DataIds.ActorControlSelfDirectorUpdate)
            {
                switch (Marshal.ReadByte(dataPtr, 8))
                {
                    case DataIds.DirectorUpdateDutyCommenced:
                        {
                            var contentId = ReadNumber(dataPtr, 4, 2);
                            if (!Ready && DeepDungeonContentInfo.ContentInfo.TryGetValue(contentId, out var info))
                                EnterDeepDungeon(contentId, info);
                            break;
                        }
                    case DataIds.DirectorUpdateDutyRecommenced:
                        if (Ready && FloorDetails.FloorTransfer)
                        {
                            FloorDetails.NextFloor();
                        }

                        break;
                }
            }
        }

        private void OnSystemLogMessage(IntPtr dataPtr, int logId)
        {
            if (InDeepDungeon)
            {
                switch ((uint)logId)
                {
                    case DataIds.SystemLogPomanderUsed:
                        FloorDetails.OnPomanderUsed((Pomander)Marshal.ReadByte(dataPtr, 16));
                        break;
                    case DataIds.SystemLogDutyEnded:
                        ExitDeepDungeon();
                        break;
                    case DataIds.SystemLogTransferenceInitiated:
                        FloorDetails.FloorTransfer = true;
                        FloorDetails.DumpFloorObjects(CurrentContentId);
                        FloorDetails.FloorObjects.Clear();
                        break;
                    case 0x1C6A:
                    case 0x1C6B:
                    case 0x1C6C:
                        FloorDetails.HoardFound = true;
                        break;
                    case 0x1C36:
                    case 0x23F8:
                        var pomander = (Pomander)Marshal.ReadByte(dataPtr, 12);
                        if (pomander > 0)
                        {
                            var player = clientState.LocalPlayer;
                            if (player != null)
                            {
                                var chest = objectTable
                                            .Where(o => o.DataId == DataIds.GoldChest)
                                            .FirstOrDefault(o => o.Position.Distance2D(player.Position) <= 4.6f);
                                if (chest != null)
                                {
                                    FloorDetails.DoubleChests[chest.EntityId] = pomander;
                                }
                            }
                        }

                        break;
                }
            }
        }

        private static int ReadNumber(IntPtr dataPtr, int offset, int size)
        {
            var bytes = new byte[4];
            Marshal.Copy(dataPtr + offset, bytes, 0, size);
            return BitConverter.ToInt32(bytes);
        }

        private bool CheckChestOpenSafe(ESPObject.ESPType type)
        {
            var info = FloorSetInfo;
            var unsafeChest = false;
            if (info != null)
            {
                unsafeChest = (info.MimicChests == DeepDungeonContentInfo.MimicChests.Silver &&
                               type == ESPObject.ESPType.SilverChest) ||
                              (info.MimicChests == DeepDungeonContentInfo.MimicChests.Gold &&
                               type == ESPObject.ESPType.GoldChest);
            }

            return !unsafeChest || (unsafeChest && configuration.OpenUnsafeChests);
        }

        public unsafe void TryInteract(ESPObject espObj)
        {
            var player = clientState.LocalPlayer;
            if (player != null && (player.StatusFlags & StatusFlags.InCombat) == 0 && configuration.OpenChests && espObj.IsChest())
            {
                var type = espObj.Type;

                if (!configuration.OpenBronzeCoffers && type == ESPObject.ESPType.BronzeChest) return;
                if (!configuration.OpenSilverCoffers && type == ESPObject.ESPType.SilverChest) return;
                if (!configuration.OpenGoldCoffers && type == ESPObject.ESPType.GoldChest) return;
                if (!configuration.OpenHoards && type == ESPObject.ESPType.AccursedHoardCoffer) return;

                if (type == ESPObject.ESPType.SilverChest && player.CurrentHp <= player.MaxHp * 0.77) return;

                if (CheckChestOpenSafe(type) && espObj.Distance() <= espObj.InteractionDistance()
                                             && !FloorDetails.InteractionList.Contains(espObj.GameObject.EntityId))
                {
                    TargetSystem.Instance()->InteractWithObject((GameObject*)espObj.GameObject.Address);
                    FloorDetails.InteractionList.Add(espObj.GameObject.EntityId);
                }
            }
        }

        public unsafe void TryNearestOpenChest()
        {
            foreach (var obj in objectTable)
                if (obj.IsValid())
                {
                    var dataId = obj.DataId;
                    if (DataIds.BronzeChestIDs.Contains(dataId) || DataIds.SilverChest == dataId ||
                        DataIds.GoldChest == dataId || DataIds.AccursedHoardCoffer == dataId)
                    {
                        var espObj = new ESPObject(obj, clientState, configuration, this, logger, null);
                        if (CheckChestOpenSafe(espObj.Type) && espObj.Distance() <= espObj.InteractionDistance())
                        {
                            TargetSystem.Instance()->InteractWithObject((GameObject*)espObj.GameObject.Address);
                            break;
                        }
                    }
                }
        }

        private unsafe bool TryGetAddonByName<T>(string name, out T* addon) where T : unmanaged
        {
            addon = (T*)this.gameGui.GetAddonByName(name, 1);
            return addon != null;
        }

        private unsafe bool IsAddonReady(AtkUnitBase* addon)
        {
            return addon != null && addon->IsVisible;
        }

        public void OnPomanderCommand(string pomanderName)
        {
            if (pomanderName == "Flight")
            {
                if (TryFindPomanderByName("Flight", out var flight) && IsPomanderUsable(flight))
                {
                    PrintChatMessage("Using Flight Pomander");
                    unsafe
                    {
                        if (!TryGetAddonByName<AtkUnitBase>("DeepDungeonStatus", out _))
                        {
                            AgentDeepDungeonStatus.Instance()->AgentInterface.Show();
                        }

                        Task.Run(() =>
                        {
                            unsafe
                            {
                                TryGetAddonByName<AtkUnitBase>("DeepDungeonStatus", out var addon);
                                if (IsAddonReady(addon))
                                {
                                    // Replacing Callback.Fire with the correct method to use the addon
                                    Task.Run(() =>
                                    {
                                        unsafe
                                        {
                                            TryGetAddonByName<AtkUnitBase>("DeepDungeonStatus", out var addon);
                                            if (IsAddonReady(addon))
                                            {
                                                // Correcting the arguments for FireCallback
                                                var atkValues = stackalloc AtkValue[2];
                                                atkValues[0] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = 11 };
                                                atkValues[1] = new AtkValue { Type = FFXIVClientStructs.FFXIV.Component.GUI.ValueType.Int, Int = (int)flight };
                                                addon->FireCallback(1, atkValues);
                                            }
                                        }
                                    }).Wait();
                                }
                            }
                        }).Wait();
                    }
                }
            }
        }



        public void TrackFloorObjects(ESPObject espObj)
        {
            FloorDetails.TrackFloorObjects(espObj, CurrentContentId);
        }

        public void Dispose()
        {
            gameNetwork.NetworkMessage -= NetworkMessage;
            floorTimer?.Stop();
            floorTimer?.Dispose();
            mobService?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
