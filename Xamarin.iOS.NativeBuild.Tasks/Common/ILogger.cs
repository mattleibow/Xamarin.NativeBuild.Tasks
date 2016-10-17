namespace Xamarin.NativeBuild.Tasks.Common
{
    public interface IToolLogger
    {
        void LogError(string error);

        void LogWarning(string warning);

        void LogMessage(string message);

        void LogVerbose(string verboseMessage);
    }
}
