using NecroLensDI.Model;
using System.Collections.Generic;

namespace NecroLensDI.Interface
{
    public interface IDeepDungeonService
    {
        IReadOnlyDictionary<Pomander, string> PomanderNames { get; }
        Dictionary<int, int> FloorTimes { get; }
        int CurrentContentId { get; }
        FloorDetails FloorDetails { get; }
        void Dispose();
        void OnPomanderCommand(string pomanderName);
        void TrackFloorObjects(ESPObject espObj);
        void TryNearestOpenChest();
        void TryInteract(ESPObject espObj);
    }
}
