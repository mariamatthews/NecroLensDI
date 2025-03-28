using System;
using System.Diagnostics;
using System.Drawing;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using NecroLens.Data;
using NecroLens.Model;
using NecroLens.util;

namespace NecroLens.Windows;

public class ConfigWindow : Window, IDisposable
{
    private readonly Configuration configuration;

    public ConfigWindow(NecroLens plugin) : base(Strings.ConfigWindow_Title, ImGuiWindowFlags.AlwaysAutoResize)
    {
        configuration = plugin.Configuration;
    }

    public void Dispose() { }

    public override void Draw()
    {
        if (ImGui.Button("Want to help with localization?"))
            Process.Start(new ProcessStartInfo
                              { FileName = "https://crowdin.com/project/necrolens", UseShellExecute = true });
        if (ImGui.BeginTabBar("MyTabBar", ImGuiTabBarFlags.None))
        {
            if (ImGui.BeginTabItem(Strings.ConfigWindow_Tab_General))
            {
                DrawGeneralTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Strings.ConfigWindow_Tab_ESPSettings))
            {
                DrawEspTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Strings.ConfigWindow_Tab_Chests))
            {
                DrawChestsTab();
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem(Strings.ConfigWindow_Tab_Extras))
            {
                DrawDebugTab();
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }
    }

    private void DrawDebugTab()
    {
        var optInCollection = configuration.OptInDataCollection;
        if (ImGui.Checkbox("Opt-In Data Collection", ref optInCollection))
        {
            configuration.OptInDataCollection = optInCollection;
            if (configuration.OptInDataCollection && configuration.UniqueId == null)
            {
                configuration.UniqueId = Guid.NewGuid().ToString();
            }
            configuration.Save();
        }

        ImGui.Indent(15);
        ImGui.Text("Help me improve NecroLens by enabling data collection.\n" +
                   "This will send information about every enemy and some other objects anonymously to my server.\n" +
                   "It contains only enemy and object id's and names per floor and a \'party-id\' for separation.\n\n" +
                   "Absolutely no information linking to any players or accounts will be collected.");
        ImGui.Unindent(15);
        
        ImGui.Separator();
        var showDebugInformation = configuration.ShowDebugInformation;
        if (ImGui.Checkbox(Strings.ConfigWindow_ExtrasTab_ShowDebugInformation, ref showDebugInformation))
        {
            configuration.ShowDebugInformation = showDebugInformation;
            configuration.Save();
        }

        ImGui.Indent(15);
        ImGui.Text(Strings.ConfigWindow_ExtrasTab_ShowDebugInformation_Details);
        ImGui.Unindent(15);
        
    }

    private void DrawChestsTab()
    {
        var openChests = configuration.OpenChests;
        if (ImGui.Checkbox(Strings.ConfigWindow_ChestsTab_OpenChests, ref openChests))
        {
            configuration.OpenChests = openChests;
           configuration.Save();
        }

        ImGui.SameLine();
        ImGui.TextColored(Color.Red.ToV4(), Strings.ConfigWindow_ChestsTab_OpenChests_EXPERIMENTAL);
        ImGui.Indent(15);
        ImGui.Text(Strings.ConfigWindow_ChestsTab_OpenChests_Details);
        ImGui.Unindent(15);
        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text(Strings.ConfigWindow_ChestsTab_OpenFollowingChests);
        ImGui.Indent(15);

        var openBronzeCoffers = configuration.OpenBronzeCoffers;
        if (ImGui.Checkbox(Strings.ConfigWindow_ChestsTab_OpenFollowingChests_Bronze, ref openBronzeCoffers))
        {
            configuration.OpenBronzeCoffers = openBronzeCoffers;
           configuration.Save();
        }

        ImGui.SameLine();
        var openSilverCoffers = configuration.OpenSilverCoffers;
        if (ImGui.Checkbox(Strings.ConfigWindow_ChestsTab_OpenFollowingChests_Silver, ref openSilverCoffers))
        {
            configuration.OpenSilverCoffers = openSilverCoffers;
           configuration.Save();
        }

        ImGui.SameLine();
        var openGoldCoffers = configuration.OpenGoldCoffers;
        if (ImGui.Checkbox(Strings.ConfigWindow_ChestsTab_OpenFollowingChests_Gold, ref openGoldCoffers))
        {
            configuration.OpenGoldCoffers = openGoldCoffers;
           configuration.Save();
        }

        ImGui.SameLine();
        var openHoards = configuration.OpenHoards;
        if (ImGui.Checkbox(Strings.ConfigWindow_ChestsTab_OpenFollowingChests_Hoards, ref openHoards))
        {
            configuration.OpenHoards = openHoards;
           configuration.Save();
        }

        ImGui.Unindent(15);
        ImGui.Separator();
        ImGui.Spacing();

        var openUnsafeChests = configuration.OpenUnsafeChests;
        if (ImGui.Checkbox(Strings.ConfigWindow_ChestsTab_OpenUnsafeChests, ref openUnsafeChests))
        {
            configuration.OpenUnsafeChests = openUnsafeChests;
           configuration.Save();
        }

        ImGui.Indent(15);
        ImGui.Text(Strings.ConfigWindow_ChestsTab_OpenUnsafeChests_Details);
        ImGui.Unindent(15);
    }

    private void DrawEspTab()
    {
        var playerDotColor = ImGui.ColorConvertU32ToFloat4(configuration.PlayerDotColor).WithoutAlpha();
        if (ImGui.ColorEdit3("##playerDot", ref playerDotColor, ImGuiColorEditFlags.NoInputs))
        {
            configuration.PlayerDotColor = ImGui.ColorConvertFloat4ToU32(playerDotColor.WithAlpha(0xCC));
            configuration.Save();
        }

        ImGui.SameLine();

        var showPlayerDot = configuration.ShowPlayerDot;
        if (ImGui.Checkbox(Strings.ConfigWindow_ESPTab_ShowPlayerDot, ref showPlayerDot))
        {
            configuration.ShowPlayerDot = showPlayerDot;
           configuration.Save();
        }

        ImGui.Indent(15);
        ImGui.Text(Strings.ConfigWindow_ESPTab_ShowPlayerDot_Details);
        ImGui.Unindent(15);
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.BeginGroup();
        var showMobViews = configuration.ShowMobViews;
        if (ImGui.Checkbox(Strings.ConfigWindow_ESPTab_ShowAggroRange, ref showMobViews))
        {
            configuration.ShowMobViews = showMobViews;
           configuration.Save();
        }

        ImGui.Indent(15);
        ImGui.Text(Strings.ConfigWindow_ESPTab_ShowAggroRange_Details);
        ImGui.Unindent(15);

        var normalAggroColor = ImGui.ColorConvertU32ToFloat4(configuration.NormalAggroColor).WithoutAlpha();
        if (ImGui.ColorEdit3(Strings.ConfigWindow_ESPTab_ShowAggroRange_Proximity_and_Sight, ref normalAggroColor,
                             ImGuiColorEditFlags.NoInputs))
        {
            configuration.NormalAggroColor = ImGui.ColorConvertFloat4ToU32(normalAggroColor.WithAlpha(0xFF));
            configuration.Save();
        }

        ImGui.SameLine();
        var soundAggroColor = ImGui.ColorConvertU32ToFloat4(configuration.SoundAggroColor).WithoutAlpha();
        if (ImGui.ColorEdit3(Strings.ConfigWindow_ESPTab_ShowAggroRange_Sound, ref soundAggroColor,
                             ImGuiColorEditFlags.NoInputs))
        {
            configuration.SoundAggroColor = ImGui.ColorConvertFloat4ToU32(soundAggroColor.WithAlpha(0xFF));
            configuration.Save();
        }

        ImGui.EndGroup();

        ImGui.SameLine();

        ImGui.BeginGroup();
        var showPatrolArrow = configuration.ShowPatrolArrow;
        if (ImGui.Checkbox(Strings.ConfigWindow_ESPTab_ShowPatrolArrow, ref showPatrolArrow))
        {
            configuration.ShowPatrolArrow = showPatrolArrow;
           configuration.Save();
        }

        ImGui.Indent(15);
        ImGui.Text(Strings.ConfigWindow_ESPTab_ShowPatrolArrow_Details);
        ImGui.Unindent(15);
        ImGui.EndGroup();

        ImGui.Separator();
        ImGui.Spacing();

        var showCofferInteractionRange = configuration.ShowCofferInteractionRange;
        if (ImGui.Checkbox(Strings.ConfigWindow_ESPTab_ShowCofferInteractionRange, ref showCofferInteractionRange))
        {
            configuration.ShowCofferInteractionRange = showCofferInteractionRange;
           configuration.Save();
        }

        ImGui.Indent(15);
        ImGui.Text(Strings.ConfigWindow_ESPTab_ShowCofferInteractionRange_Details);
        ImGui.Unindent(15);

        ImGui.Separator();
        ImGui.Spacing();

        ImGui.Text(Strings.ConfigWindow_ESPTab_HighlightObjects);

        ImGui.Indent(15);
        var highlightCoffers = configuration.HighlightCoffers;
        if (ImGui.Checkbox(Strings.ConfigWindow_ESPTab_HighlightObjects_TreasureChests, ref highlightCoffers))
        {
            configuration.HighlightCoffers = highlightCoffers;
           configuration.Save();
        }

        var passageColor = ImGui.ColorConvertU32ToFloat4(configuration.PassageColor).WithoutAlpha();
        if (ImGui.ColorEdit3("##passage", ref passageColor, ImGuiColorEditFlags.NoInputs))
        {
            configuration.PassageColor = ImGui.ColorConvertFloat4ToU32(passageColor.WithAlpha(0xFF));
            configuration.Save();
        }

        ImGui.SameLine();
        var highlightPassage = configuration.HighlightPassage;
        if (ImGui.Checkbox(Strings.ConfigWindow_ESPTab_HighlightObjects_Passage, ref highlightPassage))
        {
            configuration.HighlightPassage = highlightPassage;
           configuration.Save();
        }

        ImGui.Unindent(15);

        ImGui.Separator();
        ImGui.Spacing();
        ImGui.Text(Strings.ConfigWindow_ESPTab_HighlightTreasureChests);
        ImGui.Indent(15);

        var bronzeCofferColor = ImGui.ColorConvertU32ToFloat4(configuration.BronzeCofferColor).WithoutAlpha();
        if (ImGui.ColorEdit3("##bronzeCoffer", ref bronzeCofferColor, ImGuiColorEditFlags.NoInputs))
        {
            configuration.BronzeCofferColor = ImGui.ColorConvertFloat4ToU32(bronzeCofferColor.WithAlpha(0xFF));
            configuration.Save();
        }

        ImGui.SameLine();
        var showBronzeCoffers = configuration.ShowBronzeCoffers;
        if (ImGui.Checkbox(Strings.ConfigWindow_ESPTab_HighlightTreasureChests_Bronze, ref showBronzeCoffers))
        {
            configuration.ShowBronzeCoffers = showBronzeCoffers;
           configuration.Save();
        }

        var silverCofferColor = ImGui.ColorConvertU32ToFloat4(configuration.SilverCofferColor).WithoutAlpha();
        if (ImGui.ColorEdit3("##silverCoffer", ref silverCofferColor, ImGuiColorEditFlags.NoInputs))
        {
            configuration.SilverCofferColor = ImGui.ColorConvertFloat4ToU32(silverCofferColor.WithAlpha(0xFF));
            configuration.Save();
        }

        ImGui.SameLine();
        var showSilverCoffers = configuration.ShowSilverCoffers;
        if (ImGui.Checkbox(Strings.ConfigWindow_ESPTab_HighlightTreasureChests_Silver, ref showSilverCoffers))
        {
            configuration.ShowSilverCoffers = showSilverCoffers;
           configuration.Save();
        }

        var goldCofferColor = ImGui.ColorConvertU32ToFloat4(configuration.GoldCofferColor).WithoutAlpha();
        if (ImGui.ColorEdit3("##goldCoffer", ref goldCofferColor, ImGuiColorEditFlags.NoInputs))
        {
            configuration.GoldCofferColor = ImGui.ColorConvertFloat4ToU32(goldCofferColor.WithAlpha(0xFF));
            configuration.Save();
        }

        ImGui.SameLine();
        var showGoldCoffers = configuration.ShowGoldCoffers;
        if (ImGui.Checkbox(Strings.ConfigWindow_ESPTab_HighlightTreasureChests_Gold, ref showGoldCoffers))
        {
            configuration.ShowGoldCoffers = showGoldCoffers;
           configuration.Save();
        }

        var hoardColor = ImGui.ColorConvertU32ToFloat4(configuration.HoardColor).WithoutAlpha();
        if (ImGui.ColorEdit3("##hoard", ref hoardColor, ImGuiColorEditFlags.NoInputs))
        {
            configuration.HoardColor = ImGui.ColorConvertFloat4ToU32(hoardColor.WithAlpha(0xFF));
            configuration.Save();
        }

        ImGui.SameLine();
        var showHoards = configuration.ShowHoards;
        if (ImGui.Checkbox(Strings.ConfigWindow_ESPTab_HighlightTreasureChests_Hoards, ref showHoards))
        {
            configuration.ShowHoards = showHoards;
           configuration.Save();
        }

        ImGui.Unindent(15);
    }


    private void DrawGeneralTab()
    {
        var autoOpen = configuration.AutoOpenOnEnter;
        if (ImGui.Checkbox(Strings.ConfigWindow_GeneralTab_AutomaticallyOpen, ref autoOpen))
        {
            configuration.AutoOpenOnEnter = autoOpen;
           configuration.Save();
        }

        ImGui.Indent(15);
        ImGui.Text(Strings.ConfigWindow_GeneralTab_AutomaticallyOpen_Details);
        ImGui.Unindent(15);
        ImGui.Separator();

        var enableEsp = configuration.EnableESP;
        if (ImGui.Checkbox(Strings.ConfigWindow_GeneralTab_EnableOverlay, ref enableEsp))
        {
            configuration.EnableESP = enableEsp;
           configuration.Save();
        }

        ImGui.Indent(15);
        ImGui.Text(Strings.ConfigWindow_GeneralTab_EnableOverlay_Details);
        ImGui.Unindent(15);
        ImGui.Separator();

        var openChests = configuration.OpenChests;
        if (ImGui.Checkbox(Strings.ConfigWindow_GeneralTab_OpenChests, ref openChests))
        {
            configuration.OpenChests = openChests;
           configuration.Save();
        }

        ImGui.Indent(15);
        ImGui.Text(Strings.ConfigWindow_GeneralTab_OpenChests_Details);
        ImGui.Unindent(15);
    }
}
