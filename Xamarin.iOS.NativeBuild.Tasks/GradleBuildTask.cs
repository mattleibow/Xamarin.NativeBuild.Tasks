using System.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.NativeBuild.Tasks;
using Xamarin.NativeBuild.Tasks.Common;
using Xamarin.Android.NativeBuild.Tasks.Gradle;
using System.IO;
using System.Diagnostics;
using Xamarin.iOS.NativeBuild.Tasks.Common;
using System.Collections.Generic;

namespace Xamarin.Android.NativeBuild.Tasks
{
    public class GradleBuildTask : BaseTask
    {
        // build properties

        public GradleTool Gradle { get; set; }

        public string GradleToolPath { get; set; }

        [Output]
        public ITaskItem[] NewAndroidJavaLibraries { get; set; }

        // gradle properties

        [Required]
        public ITaskItem[] Dependencies { get; set; }

        public override bool Execute()
        {
            if (!base.Execute())
            {
                return false;
            }

            var gradleLogger = new ToolLogger(Log);

            // make sure we have GRADLE available
            GradleToolPath = ToolBase.LocateToolPath(
                tool: Utilities.IsWindows ? "gradle.bat" : "gradle",
                logger: gradleLogger,
                specifiedToolPath: GradleToolPath,
                versionLine: 1);
            if (string.IsNullOrEmpty(GradleToolPath))
            {
                return false;
            }

            if (IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            Gradle = new GradleTool(GradleToolPath, gradleLogger, GetCancellationToken());

            var deps = Dependencies.Select(item => new GradleDependency
            {
                ArtifactId = item.ItemSpec,
                GroupId = item.GetMetadata("GroupId"),
                Version = item.GetMetadata("Version"),
                Type = Utilities.ParseEnum<GradleDependencyTypes>(item.GetMetadata("Type") ?? "MavenCentral"),
                Repository = Utilities.ParseEnum<GradleRepositories>(item.GetMetadata("Repository") ?? "Default"),
            });

            var buildGradleRoot = IntermediateOutputPath;

            List<GradleLibrary> libraries = new List<GradleLibrary>();
            if (!Gradle.RestoreDependencies(buildGradleRoot, deps, libraries))
            {
                return false;
            }

            return false;
        }
    }
}
