using System;
using System.IO;
using System.Threading;

namespace Xamarin.NativeBuild.Tasks.Common
{
    public abstract class ToolBase
    {
        public ToolBase(string toolPath, IToolLogger log, CancellationToken cancellation = default(CancellationToken))
        {
            ToolPath = toolPath;
            Log = log;
            CancellationToken = cancellation;
        }

        public CancellationToken CancellationToken { get; private set; }

        public IToolLogger Log { get; private set; }

        public string ToolPath { get; private set; }

        public static string LocateToolPath(string tool, IToolLogger logger = null, string specifiedToolPath = null, string versionOption = "--version", int versionLine = 0)
        {
            string foundPath = null;

            if (!string.IsNullOrEmpty(specifiedToolPath))
            {
                // if it was explicitly set, bail if it wasn't found
                if (File.Exists(specifiedToolPath))
                {
                    foundPath = specifiedToolPath;
                }
            }
            else
            {
                // not set, so search
                var findTool = Utilities.GetProcessResult(Utilities.IsWindows ? "where" : "which", tool);
                if (!string.IsNullOrEmpty(findTool))
                {
                    foundPath = findTool.Trim();
                }
                else
                {
                    // we didn't find {tool} in the default places, so do a bit of research
                    // TODO
                }
            }

            if (logger != null)
            {
                if (string.IsNullOrEmpty(foundPath))
                {
                    logger.LogError($"Unable to find {tool}.");
                }
                else
                {
                    foundPath = CrossPath.ToCurrent(foundPath);
                    if (string.IsNullOrEmpty(versionOption))
                    {
                        logger.LogVerbose($"Found {tool} at {foundPath}.");
                    }
                    else
                    {
                        var version = Utilities.GetProcessResult(foundPath, versionOption, false);
                        version = Utilities.GetLine(version, versionLine);
                        logger.LogVerbose($"Found {tool} version {version} at {foundPath}.");
                    }
                }
            }

            return foundPath;
        }
    }
}
