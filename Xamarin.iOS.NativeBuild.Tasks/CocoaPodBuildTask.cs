using System.Linq;
using Microsoft.Build.Framework;
using Xamarin.iOS.NativeBuild.Tasks.XCodeBuild;
using Xamarin.iOS.NativeBuild.Tasks.CocoaPods;
using Xamarin.iOS.NativeBuild.Tasks.Common;

namespace Xamarin.iOS.NativeBuild.Tasks
{
    public class CocoaPodBuildTask : SshBasedTask
    {
        // build properties

        public string PodToolPath { get; set; }

        public CocoaPodsTool CocoaPods { get; set; }

        public string XCodeBuildToolPath { get; set; }

        public XCodeBuildTool XCodeBuild { get; set; }

        [Output]
        public string NewMtouchExtraArgs { get; set; }

        // pod properties

        [Required]
        public ITaskItem[] Pods { get; set; }

        [Required]
        public string PlatformVersion { get; set; }

        [Required]
        public string PlatformName { get; set; }

        public bool UseFrameworks { get; set; }

        public string Architectures { get; set; }

        public PodfilePlatform PodfilePlatform => Utilities.ParseEnum<PodfilePlatform>(PlatformName);

        // XCode

        public XCodeArchitectures XCodeArchitectures => Utilities.ParseEnum<XCodeArchitectures>(Architectures);

        public XCodePlatforms XCodePlatform => Utilities.ParseEnum<XCodePlatforms>(PlatformName);

        public XCodeBuildParameters.XCodeArcitecture XCodeBuildArchitecture => XCodeBuildParameters.GetArchitecture(XCodePlatform);

        public override bool Execute()
        {
            if (!base.Execute())
            {
                return false;
            }

            // make sure we have POD available
            PodToolPath = LocateToolPath(PodToolPath, "pod", "--version");
            if (string.IsNullOrEmpty(PodToolPath))
            {
                return false;
            }

            if (IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            // make sure we have xcodebuild available
            XCodeBuildToolPath = LocateToolPath(XCodeBuildToolPath, "xcodebuild", "-version");
            if (string.IsNullOrEmpty(XCodeBuildToolPath))
            {
                return false;
            }

            if (IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            XCodeBuild = new XCodeBuildTool(XCodeBuildToolPath, Log, GetCancellationToken(), this);
            CocoaPods = new CocoaPodsTool(PodToolPath, Log, GetCancellationToken(), this);

            var podfile = new Podfile
            {
                Platform = PodfilePlatform,
                PlatformVersion = PlatformVersion,
                UseFrameworks = UseFrameworks,
                TargetName = "MSBuildTask",
                Pods = Pods.Select(p =>
                    new Pod
                    {
                        Id = p.ItemSpec,
                        Version = p.GetMetadata("Version")
                    }).ToArray()
            };

            var podfileRoot = BuildIntermediateOutputPath;
            var includeTargets = Pods.Select(p => p.ItemSpec).ToArray();

            if (!UseFrameworks)
            {
                // this extra step when building static archives is needed
                // in order to collect the resources that should be added
                //
                // here we just use the first architecture

                // create the podfile for the bundling build
                podfile.UseFrameworks = true;
                if (!CocoaPods.CreatePodfileXCodeProject(podfileRoot, podfile, true))
                {
                    return false;
                }
                podfile.UseFrameworks = UseFrameworks;

                // build Pod-CocoaPodBuildTask as a framework
                var framework = BuildPodfileXCodeProject(podfileRoot, includeTargets, XCodeBuildParameters.SplitArchitecture(XCodeArchitectures).FirstOrDefault(), true);
                if (framework == null)
                {
                    return false;
                }

                // TODO: if building as a static archive,
                // we need to manually include the resources.
            }

            // create the podfile for the real build
            if (!CocoaPods.CreatePodfileXCodeProject(podfileRoot, podfile, noRepoUpdate: true))
            {
                return false;
            }

            // build Pod-CocoaPodBuildTask as requested
            var outputs = BuildPodfileXCodeProject(podfileRoot, includeTargets, XCodeArchitectures, UseFrameworks);
            if (outputs == null)
            {
                return false;
            }

            // TODO this needs work - testing only
            var staticArchives = string.Join(" ", outputs.Select(to => $"'{to.ArchiveOutput.Path}'"));
            NewMtouchExtraArgs += $@" -gcc_flags "" {staticArchives} "" ";

            return true;
        }

        private XCodeBuildOutputs BuildPodfileXCodeProject(string podfileRoot, string[] targets, XCodeArchitectures architectures, bool framework)
        {
            var parameters = new XCodeBuildParameters
            {
                ArchitectureSettings = XCodeBuildArchitecture,
                ArtifactsDirectory = SshPath.Combine(podfileRoot, "build"),
                OutputDirectory = SshPath.Combine(podfileRoot, "out"),
                IsFrameworks = framework,
                ProjectFilePath = SshPath.Combine(podfileRoot, "Pods/Pods.xcodeproj"),
                BuildTargets = new[] { "Pods-MSBuildTask" },
                OutputTargets = targets,
                ArchitectureOverride = architectures
            };
            var outputs = new XCodeBuildOutputs();

            if (XCodeBuild.BuildXCodeProject(parameters, outputs))
            {
                return outputs;
            }
            else
            {
                return null;
            }
        }
    }
}
