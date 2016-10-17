using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Xamarin.NativeBuild.Tasks.Common;

namespace Xamarin.Android.NativeBuild.Tasks.Gradle
{
    public class GradleTool : ToolBase
    {
        public GradleTool(string tool, IToolLogger log, CancellationToken cancellation = default(CancellationToken))
            : base(tool, log, cancellation)
        {
        }

        public bool RestoreDependencies(string buildGradleRoot, IEnumerable<GradleDependency> dependencies, ICollection<GradleLibrary> libraries)
        {
            var buildGradlePath = Path.Combine(CrossPath.ToCurrent(buildGradleRoot), "build.gradle");
            if (!CreateBuildGradle(buildGradlePath, dependencies))
            {
                return false;
            }

            var libs = GetLibraries(buildGradlePath);
            if (libs == null)
            {
                Log.LogError("Unable to restore gradle packages.");
                return false;
            }

            if (!ProcessLibraries(dependencies, libs, libraries))
            {
                return false;
            }

            return true;
        }

        public string[] GetLibraries(string buildGradlePath)
        {
            var libs = Utilities.GetProcessResult(ToolPath, $"--build-file {buildGradlePath} --quiet restore", false);
            if (string.IsNullOrEmpty(libs))
            {
                return null;
            }

            return Utilities.SplitLines(libs);
        }

        private bool ProcessLibraries(IEnumerable<GradleDependency> dependencies, IEnumerable<string> results, ICollection<GradleLibrary> libraries)
        {
            foreach (var result in results)
            {
                var artifactIdVersion = Path.GetFileName(result);
                var idIdx = artifactIdVersion.LastIndexOf("-");
                if (idIdx == -1 || idIdx == artifactIdVersion.Length)
                {
                    Log.LogError($"Unable to determine Artifact ID and version from: {artifactIdVersion} with path {result}.");
                    return false;
                }
                var artifactId = artifactIdVersion.Substring(0, idIdx);
                var artifactVersion = artifactIdVersion.Substring(idIdx + 1);
                var extension = Path.GetExtension(result);
                extension = extension.Substring(1, extension.Length - 1);
                if (string.IsNullOrEmpty(extension))
                {
                    Log.LogError($"Unable to determine Artifact type from {result}.");
                    return false;
                }
                var type = Utilities.ParseEnum<GradleDependencyTypes>(extension);
                if (type == GradleDependencyTypes.Default)
                {
                    Log.LogError($"Unable to determine Artifact type from {result}.");
                    return false;
                }

                var dependency = dependencies.FirstOrDefault(d => d.ArtifactId.Equals(artifactId, StringComparison.OrdinalIgnoreCase));
                var library = new GradleLibrary
                {
                    Path = result,
                    Request = dependency,
                    Type = type,
                    BuildAction = DetermineBuildAction(dependency, type)
                };

                if (library.BuildAction == BuildAction.Unknown)
                {
                    Log.LogError($"Unable to determine BuildAction type from {type} with path {result}.");
                    return false;
                }

                // right now we skip dependencies we didn't ask for, but we might not later
                if (dependency != null)
                {
                    libraries.Add(library);
                }
            }

            return true;
        }

        private BuildAction DetermineBuildAction(GradleDependency dependency, GradleDependencyTypes type)
        {
            switch (type)
            {
                case GradleDependencyTypes.Jar:
                    return BuildAction.EmbeddedJar;
                case GradleDependencyTypes.Aar:
                    return BuildAction.LibraryProjectZip;
            }

            return BuildAction.Unknown;
        }

        public bool CreateBuildGradle(string buildGradlePath, IEnumerable<GradleDependency> dependencies)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            var gradleContents =
                @"apply plugin: 'java'" + Environment.NewLine +
                @"repositories {" + Environment.NewLine;
            foreach (var repo in dependencies.GroupBy(d => d.Repository).Select(g => g.Key))
            {
                var plugin = GradleRepository.GetRepository(repo);
                gradleContents +=
                $"    {plugin.PluginName}" + Environment.NewLine;
            }
            gradleContents +=
                @"}" + Environment.NewLine +
                @"task restore << {" + Environment.NewLine +
                @"    configurations.compile.each {" + Environment.NewLine +
                @"        File file -> println file.path" + Environment.NewLine +
                @"    }" + Environment.NewLine +
                @"}" + Environment.NewLine +
                @"dependencies {" + Environment.NewLine;
            foreach (var dep in dependencies)
            {
                gradleContents +=
                $"    compile '{dep.MavenResourcePath}'" + Environment.NewLine;
            }
            gradleContents +=
                @"}" + Environment.NewLine;

            var root = Path.GetDirectoryName(buildGradlePath);
            if (!Directory.Exists(root))
            {
                Directory.CreateDirectory(root);
            }
            File.WriteAllText(buildGradlePath, gradleContents);

            return true;
        }
    }
}
