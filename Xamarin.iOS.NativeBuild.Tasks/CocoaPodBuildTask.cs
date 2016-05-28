using System;
using System.Linq;
using Microsoft.Build.Framework;

namespace Xamarin.iOS.NativeBuild.Tasks
{
    public class CocoaPodBuildTask : SshBasedTask
    {
        // build properties

        public string PodToolPath { get; set; }

        public CocoaPods CocoaPods { get; set; }

        public string XCodeBuildToolPath { get; set; }

        public XCodeBuild XCodeBuild { get; set; }

        // pod properties

        [Required]
        public ITaskItem[] Pods { get; set; }

        [Required]
        public string PlatformVersion { get; set; }

        [Required]
        public string PlatformName { get; set; }

        public bool UseFrameworks { get; set; }

        public string Architectures { get; set; }

        public CocoaPods.PodfilePlatform PodfilePlatform => (CocoaPods.PodfilePlatform)Enum.Parse(typeof(CocoaPods.PodfilePlatform), PlatformName, true);

        // XCode

        public XCodeArchitectures XCodeArchitectures => (XCodeArchitectures)Enum.Parse(typeof(XCodeArchitectures), Architectures, true);

        public XCodePlatforms XCodePlatform => (XCodePlatforms)Enum.Parse(typeof(XCodePlatforms), PlatformName, true);

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

            XCodeBuild = new XCodeBuild(XCodeBuildToolPath, Log, GetCancellationToken(), this);
            CocoaPods = new CocoaPods(PodToolPath, Log, GetCancellationToken(), this);

            var podfile = new CocoaPods.Podfile
            {
                Platform = PodfilePlatform,
                PlatformVersion = PlatformVersion,
                UseFrameworks = UseFrameworks,
                Pods = Pods.Select(p =>
                    new CocoaPods.Pod
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
                if (!BuildPodfileXCodeProject(podfileRoot, includeTargets, XCodeBuildParameters.SplitArchitecture(XCodeArchitectures).FirstOrDefault(), true))
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
            if (!BuildPodfileXCodeProject(podfileRoot, includeTargets, XCodeArchitectures, UseFrameworks))
            {
                return false;
            }

            return true;
        }

        private bool BuildPodfileXCodeProject(string podfileRoot, string[] targets, XCodeArchitectures architectures, bool framework)
        {
            return XCodeBuild.BuildXCodeProject(new XCodeBuildParameters
            {
                ArchitectureSettings = XCodeBuildArchitecture,
                ArtifactsDirectory = SshPath.Combine(podfileRoot, "build"),
                OutputDirectory = SshPath.Combine(podfileRoot, "out"),
                IsFrameworks = framework,
                ProjectFilePath = SshPath.Combine(podfileRoot, "Pods/Pods.xcodeproj"),
                Targets = targets,
                ArchitectureOverride = architectures
            });
        }
    }
}
