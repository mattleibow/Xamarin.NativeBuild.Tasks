using Microsoft.Build.Utilities;
using Xamarin.NativeBuild.Tasks.Common;

namespace Xamarin.NativeBuild.Tasks.Common
{
    public class ToolLogger : IToolLogger
    {
        private readonly TaskLoggingHelper log;

        public ToolLogger(TaskLoggingHelper log)
        {
            this.log = log;
        }

        public void LogError(string error)
        {
            log.LogError(error);
        }

        public void LogMessage(string message)
        {
            log.LogMessage(message);
        }

        public void LogVerbose(string verboseMessage)
        {
            log.LogVerbose(verboseMessage);
        }

        public void LogWarning(string warning)
        {
            log.LogWarning(warning);
        }
    }
}
