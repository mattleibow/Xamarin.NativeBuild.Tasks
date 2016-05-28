using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Utilities;

namespace Xamarin.iOS.NativeBuild.Tasks
{
    public class XCodeBuild
    {
        private readonly Regex InvalidCharactersRegex = new Regex("[^a-zA-Z0-9]");
        private readonly Regex InvalidPrefixRegex = new Regex("(^[0-9])");

        public XCodeBuild(string xcodeBuildPath, TaskLoggingHelper log, CancellationToken cancellation, ISshInterface sshInterface)
        {
            XCodeBuildToolPath = xcodeBuildPath;
            Log = log;
            CancellationToken = cancellation;
            Ssh = sshInterface;
        }

        public CancellationToken CancellationToken { get; private set; }

        public TaskLoggingHelper Log { get; private set; }

        public ISshInterface Ssh { get; private set; }

        public string XCodeBuildToolPath { get; private set; }

        public bool BuildXCodeProject(XCodeBuildParameters parameters)
        {
            var projectDirectory = SshPath.GetDirectoryName(parameters.ProjectFilePath);
            var outputDirectory = parameters.OutputDirectory ?? SshPath.Combine(projectDirectory, "out");
            var artifactsDirectory = parameters.ArtifactsDirectory ?? SshPath.Combine(projectDirectory, "build");

            var architectures = parameters.SplitArchitectures;

            foreach (var arch in architectures)
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    Log.LogError("Task was canceled.");
                    return false;
                }

                var artifactsPath = SshPath.Combine(artifactsDirectory, parameters.ArchitectureSettings.GetArtifactDirectoryName("Release", arch: arch));

                // build the project
                var result = Ssh.ExecuteLaunchCtlCommand(
                    workingDirectory: projectDirectory,
                    arguments: new[]
                    {
                        XCodeBuildToolPath,
                        "-project", parameters.ProjectFilePath,
                        "-target", "Pods-CocoaPodBuildTask",
                        "-configuration", "Release",
                        "-arch", arch.ToString().ToLowerInvariant(),
                        "-sdk", parameters.ArchitectureSettings.GetSdk(arch).ToString().ToLowerInvariant(),
                        "build"
                    });
                if (!result)
                {
                    Log.LogError($"Error building the XCode project.");
                    return false;
                }

                if (CancellationToken.IsCancellationRequested)
                {
                    Log.LogError("Task was canceled.");
                    return false;
                }

                // copy the atrifacts to the output obj directory, naming them per platform
                foreach (var target in parameters.Targets)
                {
                    Ssh.CreateDirectory(SshPath.Combine(outputDirectory, target, "obj"));
                    if (parameters.IsFrameworks)
                    {
                        var cleanTarget = CleanFrameworkName(target);
                        Ssh.CopyPath(
                            SshPath.Combine(artifactsPath, $"{cleanTarget}.framework"),
                            SshPath.Combine(outputDirectory, target, "obj", $"{cleanTarget}-{arch}.framework"));
                    }
                    else
                    {
                        Ssh.CopyPath(
                            SshPath.Combine(artifactsPath, $"lib{target}.a"),
                            SshPath.Combine(outputDirectory, target, "obj", $"lib{target}-{arch}.a"));
                    }
                }
            }

            // run lipo on the outputs, from the obj to the out
            foreach (var target in parameters.Targets)
            {
                if (parameters.IsFrameworks)
                {
                    var cleanTarget = CleanFrameworkName(target);

                    // copy the first arch as we need the other files
                    var firstArch = architectures[0];
                    Ssh.CopyPath(
                        SshPath.Combine(outputDirectory, target, "obj", $"{cleanTarget}-{firstArch}.framework"),
                        SshPath.Combine(outputDirectory, target, $"{cleanTarget}.framework"));

                    // just lipo the archives
                    var inputs = architectures.Select(arch => SshPath.Combine(outputDirectory, target, "obj", $"{cleanTarget}-{arch}.framework", cleanTarget)).ToArray();
                    this.RunLipo(SshPath.Combine(outputDirectory, target, $"{cleanTarget}.framework", cleanTarget), inputs);
                }
                else
                {
                    var inputs = architectures.Select(arch => SshPath.Combine(outputDirectory, target, "obj", $"lib{target}-{arch}.a")).ToArray();
                    this.RunLipo(SshPath.Combine(outputDirectory, target, $"lib{target}.a"), inputs);
                }
            }

            return true;
        }

        public bool RunLipo(string output, params string[] inputs)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            inputs = inputs.Select(i => $@"""{i}""").ToArray();

            var lipo = $@"lipo -create -output ""{output}"" {string.Join(" ", inputs)}";
            var runLipo = Ssh.ExecuteCommand(lipo);
            if (!Ssh.WasSuccess(runLipo))
            {
                Log.LogError("Error running lipo: " + runLipo.Result);
                return false;
            }
            else
            {
                Log.LogVerbose($"lipo result: {runLipo.Result}");
            }

            return true;
        }

        private string CleanFrameworkName(string target)
        {
            return InvalidPrefixRegex.Replace(InvalidCharactersRegex.Replace(target, "_"), "_$1");
        }
    }
}
