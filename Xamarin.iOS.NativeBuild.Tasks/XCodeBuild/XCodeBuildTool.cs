using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Utilities;
using Xamarin.iOS.NativeBuild.Tasks.Common;
using Xamarin.NativeBuild.Tasks.Common;

namespace Xamarin.iOS.NativeBuild.Tasks.XCodeBuild
{
    public class XCodeBuildTool
    {
        private readonly Regex InvalidCharactersRegex = new Regex("[^a-zA-Z0-9]");
        private readonly Regex InvalidPrefixRegex = new Regex("(^[0-9])");

        public XCodeBuildTool(string xcodeBuildPath, TaskLoggingHelper log, CancellationToken cancellation, ISshInterface sshInterface)
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

        public bool BuildXCodeProject(XCodeBuildParameters parameters, XCodeBuildOutputs outputs)
        {
            outputs.ProjectDirectory = CrossPath.GetDirectoryNameSsh(parameters.ProjectFilePath);
            outputs.OutputDirectory = parameters.OutputDirectory ?? CrossPath.CombineSsh(outputs.ProjectDirectory, "out");
            outputs.ArtifactsDirectory = parameters.ArtifactsDirectory ?? CrossPath.CombineSsh(outputs.ProjectDirectory, "build");

            foreach (var arch in parameters.SplitArchitectures)
            {
                if (CancellationToken.IsCancellationRequested)
                {
                    Log.LogError("Task was canceled.");
                    return false;
                }

                var artifactsPath = CrossPath.CombineSsh(outputs.ArtifactsDirectory, parameters.ArchitectureSettings.GetArtifactDirectoryName("Release", arch: arch));

                // build the project
                var result = Ssh.ExecuteLaunchCtlCommand(
                    workingDirectory: outputs.ProjectDirectory,
                    arguments: new[]
                    {
                        XCodeBuildToolPath,
                        "-project", parameters.ProjectFilePath,
                        "-target", parameters.BuildTargets[0], // TODO
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

                // copy the artifacts to the intermediates directory, naming them per architecture
                foreach (var target in parameters.OutputTargets)
                {
                    // this might be set multiple times as this is target-based,
                    // but we are building the same target multiple times for each arch
                    outputs[target].IntermediateDirectory = CrossPath.CombineSsh(outputs.OutputDirectory, target, "obj");
                    Ssh.CreateDirectory(outputs[target].IntermediateDirectory);

                    var currentIntermediate = outputs[target].Intermediates[arch];
                    currentIntermediate.IsFrameworks = parameters.IsFrameworks;

                    if (parameters.IsFrameworks)
                    {
                        var cleanTarget = CleanFrameworkName(target);
                        currentIntermediate.Path = CrossPath.CombineSsh(outputs[target].IntermediateDirectory, $"{cleanTarget}-{arch}.framework");
                        Ssh.CopyPath(CrossPath.CombineSsh(artifactsPath, $"{cleanTarget}.framework"), currentIntermediate.Path);
                    }
                    else
                    {
                        currentIntermediate.Path = CrossPath.CombineSsh(outputs.OutputDirectory, target, "obj", $"lib{target}-{arch}.a");
                        Ssh.CopyPath(CrossPath.CombineSsh(artifactsPath, $"lib{target}.a"), currentIntermediate.Path);
                    }
                }
            }

            // run lipo on the outputs, from the obj to the out
            foreach (var targetOutput in outputs)
            {
                targetOutput.Directory = CrossPath.CombineSsh(parameters.OutputDirectory, targetOutput.Target);

                // lipo the .a
                var staticIntermediates = targetOutput.Intermediates.Where(i => !i.IsFrameworks);
                if (staticIntermediates.Any())
                {
                    targetOutput.ArchiveOutput = new BuildArchitectureOutput(targetOutput)
                    {
                        Architecture = Utilities.CreateEnum(staticIntermediates.Select(i => i.Architecture)),
                        IsFrameworks = false,
                        Path = CrossPath.CombineSsh(parameters.OutputDirectory, targetOutput.Target, $"lib{targetOutput.Target}.a")
                    };
                    if (!RunLipo(targetOutput.ArchiveOutput.Path, staticIntermediates.Select(i => i.Path)))
                    {
                        return false;
                    }
                }

                // lipo the .framework
                var frameworkIntermediates = targetOutput.Intermediates.Where(i => i.IsFrameworks);
                if (frameworkIntermediates.Any())
                {
                    var cleanTarget = CleanFrameworkName(targetOutput.Target);

                    targetOutput.FrameworkOutput = new BuildArchitectureOutput(targetOutput)
                    {
                        Architecture = Utilities.CreateEnum(frameworkIntermediates.Select(i => i.Architecture)),
                        IsFrameworks = true,
                        Path = CrossPath.CombineSsh(parameters.OutputDirectory, targetOutput.Target, $"{cleanTarget}.framework")
                    };

                    // copy the first arch as we need the other files
                    var firstArch = frameworkIntermediates.First();
                    Ssh.CopyPath(firstArch.Path, targetOutput.FrameworkOutput.Path);

                    // now lipo the archive
                    if (!RunLipo(CrossPath.CombineSsh(targetOutput.FrameworkOutput.Path, cleanTarget), frameworkIntermediates.Select(i => CrossPath.CombineSsh(i.Path, cleanTarget))))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool RunLipo(string output, IEnumerable<string> inputs)
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
