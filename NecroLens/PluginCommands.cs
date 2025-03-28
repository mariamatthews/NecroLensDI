using System;
using Dalamud.Game.Command;
using NecroLens.Data;

namespace NecroLens;

public class PluginCommands : IDisposable
{
    public PluginCommands()
    {
        NecroLens.CommandManager.AddHandler("/necrolens",
            new CommandInfo((_, _) => NecroLens.ShowMainWindow())
            {
                HelpMessage = Strings.PluginCommands_OpenOverlay_Help,
                ShowInHelp = true
            });

        NecroLens.CommandManager.AddHandler("/necrolenscfg",
            new CommandInfo((_, _) => NecroLens.ShowConfigWindow())
            {
                HelpMessage = Strings.PluginCommands_OpenConfig_Help,
                ShowInHelp = true
            });

        NecroLens.CommandManager.AddHandler("/openchest",
            new CommandInfo((_, _) => NecroLens.DeepDungeonService.TryNearestOpenChest())
            {
                HelpMessage = Strings.PluginCommands_OpenChest_Help,
                ShowInHelp = true
            });

        NecroLens.CommandManager.AddHandler("/pomander",
            new CommandInfo((_, args) => NecroLens.DeepDungeonService.OnPomanderCommand(args))
            {
                HelpMessage = "Try to use the pomander with given name",
                ShowInHelp = true
            });
    }

    public void Dispose()
    {
        NecroLens.CommandManager.RemoveHandler("/necrolens");
        NecroLens.CommandManager.RemoveHandler("/necrolenscfg");
        NecroLens.CommandManager.RemoveHandler("/openchest");
        NecroLens.CommandManager.RemoveHandler("/pomander");
        GC.SuppressFinalize(this);
    }
}
