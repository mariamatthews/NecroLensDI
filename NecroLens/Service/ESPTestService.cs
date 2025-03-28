using System;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using Dalamud.Game.ClientState.Conditions;
using ImGuiNET;
using NecroLens.Model;
using NecroLens.util;

namespace NecroLens.Service;

/**
 * Test Class for stuff drawing tests -not loaded by default
 */
[SuppressMessage("ReSharper", "InconsistentNaming")]
public class ESPTestService : IDisposable
{
    public ESPTestService()
    {
        NecroLens.PluginInterface.UiBuilder.Draw += OnUpdate;
    }

    public void Dispose()
    {
        NecroLens.PluginInterface.UiBuilder.Draw -= OnUpdate;
    }

    private void OnUpdate()
    {
        if (ShouldDraw())
        {
            var drawList = ImGui.GetBackgroundDrawList();
            var player = NecroLens.ClientState.LocalPlayer;
            var espObject = new ESPObject(player!);

            var onScreen = NecroLens.GameGui.WorldToScreen(player!.Position, out _);
            if (onScreen)
            {
                //drawList.AddCircleFilled(position2D, 3f, ColorUtils.ToUint(Color.Red, 0.8f), 100);

                // drawList.PathArcTo(position2D, 2f, 2f, 2f);
                // drawList.PathStroke(ColorUtils.ToUint(Color.Red, 0.8f), ImDrawFlags.RoundCornersDefault, 2f);
                // drawList.PathClear();

                ESPUtils.DrawFacingDirectionArrow(drawList, espObject, Color.Red.ToUint(), 1f, 4f);
            }
        }
    }

    private bool ShouldDraw()
    {
        return !(NecroLens.Condition[ConditionFlag.LoggingOut] ||
                 NecroLens.Condition[ConditionFlag.BetweenAreas] ||
                 NecroLens.Condition[ConditionFlag.BetweenAreas51]) &&
               NecroLens.ClientState.LocalPlayer != null &&
               NecroLens.ClientState.LocalContentId > 0 && NecroLens.ObjectTable.Length > 0;
    }
}
