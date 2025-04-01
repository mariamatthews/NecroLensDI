using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Plugin.Services;
using NecroLensDI.Model;

namespace NecroLensDI.util;

[SuppressMessage("ReSharper", "PatternIsRedundant")] // RSRP-492231
[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class DeepDungeonUtil
{
    private static IClientState ClientState => NecroLensDI.ClientState;
    private static ICondition Condition => NecroLensDI.Condition;
    private static IChatGui Chat => NecroLensDI.Chat;
    private static IDataManager DataManager => NecroLensDI.DataManager;

    public static ushort MapId => ClientState?.TerritoryType ?? 0;
    public static bool InDeepDungeon => InPotD || InHoH || InEO;
    public static bool InPotD => DataIds.PalaceOfTheDeadMapIds.Contains(MapId);
    public static bool InHoH => DataIds.HeavenOnHighMapIds.Contains(MapId);
    public static bool InEO => DataIds.EurekaOrthosMapIds.Contains(MapId);

    public static bool IsPomanderUsable(Pomander pomander)
    {
        // Only in Deep Dungeon of course :D
        var usable = InDeepDungeon;

        if (!usable)
        {
            PrintChatMessage($"Can only be used in DeepDungeon");
            return false;
        }

        // checking for item penalty if not serenity
        if (pomander != Pomander.Serenity && pomander != Pomander.SerenityProtomander)
        {
            var itemPenalty = ClientState.LocalPlayer?.StatusList?.Where(s => s.StatusId == DataIds.ItemPenaltyStatusId);
            usable = usable && (itemPenalty == null || !itemPenalty.Any());
        }

        if (!usable)
        {
            PrintChatMessage($"Unable to use: Item Penalty active");
            return false;
        }

        usable = usable && pomander switch
        {
            // Normal Pomander can be used in PotD and HoH
            >= Pomander.Safety and <= Pomander.Serenity or Pomander.Intuition or Pomander.Raising => InPotD || InHoH,

            // PotD exclusive Pomander
            Pomander.Rage or Pomander.Lust or Pomander.Resolution => InPotD,

            // Eureka exclusive Pomander
            Pomander.Frailty or Pomander.Concealment or Pomander.Petrification => InHoH,

            // Protomander can be used in EO only
            >= Pomander.LethargyProtomander and <= Pomander.RaisingProtomander => InEO,

            _ => false
        };

        if (!usable)
        {
            PrintChatMessage($"Unable to use: Pomander not usable in current Deep Dungeon");
            return false;
        }

        return usable;
    }

    public static bool TryFindPomanderByName(string name, out Pomander pomander)
    {
        pomander = default;
        if (string.IsNullOrEmpty(name))
        {
            PrintChatMessage($"Define a pomander name like '/pomander Safety' or even a part of the name like '/pomander saf'");
            return false;
        }

        var sheet = DataManager.GetExcelSheet<Lumina.Excel.Sheets.DeepDungeonItem>();
        if (sheet == null)
        {
            PrintChatMessage($"Failed to retrieve DeepDungeonItem sheet.");
            return false;
        }

        var matches = sheet.Where(e => e.RowId is > 0 and < 23)
                           .Where(e => e.Singular.ToString().Contains(name, StringComparison.OrdinalIgnoreCase))
                           .ToList();

        if (matches.Count > 1)
        {
            PrintChatMessage($"Multiple matches found for '{name}' please be more specific.");
        }
        else if (!matches.Any())
        {
            // Nothing found? Try match with enum
            if (!Enum.TryParse(name, true, out pomander))
            {
                PrintChatMessage($"No matches found for '{name}'.");
            }
        }
        else
        {
            pomander = (Pomander)matches.First().RowId;
        }

        // if we are in EO and use normal names we have to shift them
        if (InEO)
        {
            if (pomander is >= Pomander.Safety and <= Pomander.Serenity)
            {
                pomander += 22;
            }

            if (pomander is Pomander.Intuition or Pomander.Raising)
            {
                pomander += 20;
            }
        }

        return pomander != default;
    }

    public static void PrintChatMessage(string msg)
    {
        var message = new XivChatEntry
        {
            Message = new SeStringBuilder()
                      .AddUiForeground($"[NecroLensDI] ", 48)
                      .Append(msg).Build()
        };

        Chat?.Print(message);
    }
}
