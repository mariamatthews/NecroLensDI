namespace NecroLensDI.Interface
{
    public interface ILoggingService
    {
        void LogError(string message);
        void LogWarning(string message);
        void LogDebug(string message);
        void LogInformation(string message);
        void LogVerbose(string message);
    }
}
