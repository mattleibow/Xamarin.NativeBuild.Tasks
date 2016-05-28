using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Xamarin.iOS.NativeBuild.Tasks.Common
{
    public static class LogExtensions
    {
        public static void LogVerbose(this TaskLoggingHelper logger, string message)
        {
            logger.LogMessage(MessageImportance.Low, message);
        }
    }
}
