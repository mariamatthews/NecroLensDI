using Dalamud.Plugin.Services;
using NecroLens.Interface;

namespace NecroLens.Logging
{
    public class DalamudLoggingService : ILoggingService
    {
        private readonly IPluginLog pluginLog;
        public DalamudLoggingService(IPluginLog pluginLog)
        {
            this.pluginLog = pluginLog;
        }

        public void LogError(string message)
        {
            pluginLog.Error(message);
        }
        public void LogWarning(string message)
        {
            pluginLog.Warning(message);
        }

        public void LogDebug(string message)
        {
            pluginLog.Debug(message);
        }

        public void LogInformation(string message)
        {
            pluginLog.Information(message);
        }

        public void LogVerbose(string message)
        {
            pluginLog.Verbose(message);
        }


    }
}
