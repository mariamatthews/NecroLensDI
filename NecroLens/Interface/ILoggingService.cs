namespace NecroLens.Interface
{
    public interface ILoggingService
    {
        void LogError(string message);
        void LogDebug(string message);
        void LogInformation(string message);
        void LogVerbose(string message);
    }
}
