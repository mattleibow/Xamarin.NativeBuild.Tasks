using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Build.Framework;
using Xamarin.Messaging;
using Xamarin.Messaging.Diagnostics;
using Xamarin.VisualStudio.Build;

namespace Xamarin.iOS.NativeBuild.Tasks
{
    public class CocoaPodBuildTask : SshBasedTask
    {
        // build properties

        public string PodToolPath { get; set; }

        public string XCodeBuildToolPath { get; set; }

        // pod properties

        [Required]
        public string PodName { get; set; }

        [Required]
        public string PodVersion { get; set; }

        [Required]
        public string PlatformName { get; set; }

        [Required]
        public string PlatformVersion { get; set; }

        public bool UseFrameworks { get; set; }

        // communication properties

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

            // make sure we have POD available
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

            var podfileRoot = PlatformPath.GetPathForMac(Path.Combine(BuildIntermediateOutputPath, PodName));

            // create the podfile for the bundling build
            if (!CreatePodfileXCodeProject(podfileRoot, true))
            {
                return false;
            }

            // build Pod-CocoaPodBuildTask as a framework
            if (!BuildPodfileXcodeProject(podfileRoot, true, XCodeBuildSettings.iOS.SimulatorSingle))
            {
                return false;
            }

            //// copy build/*.framework/*.bundle

            // create the podfile for the real build
            if (!CreatePodfileXCodeProject(podfileRoot, UseFrameworks, noRepoUpdate: true))
            {
                return false;
            }

            // build Pod-CocoaPodBuildTask as requested
            if (!BuildPodfileXcodeProject(podfileRoot, UseFrameworks, XCodeBuildSettings.iOS))
            {
                return false;
            }


            // in Release-iPhoneSimulator
            //     - PodName.framework
            //           lipo PodName (archive)
            //           copy others
            //     - lipo libPodName.a

            Log.LogError("Finished Execute");
            return false;

            //messagingService.SshCommands.Runner.ExecuteBashCommand();

            //var args = new CommandLineBuilder();
            //args.AppendSwitch("-convert");
            //args.AppendSwitch("binary1");
            //args.AppendSwitch("-o");
            //args.AppendFileNameIfNotNull(this.Output.ItemSpec);
            //args.AppendFileNameIfNotNull(this.Input.ItemSpec);
            //return args.ToString();

            //Log.LogError(messagingService.Fingerprint);
            //Log.LogError(buildserverPath);


            //return (new TaskRunner(SessionId)).Run(this);

            return true;
        }

        protected bool CreatePodfileXCodeProject(string podfileRoot, bool framework, bool? noRepoUpdate = null)
        {
            if (IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            var podfilePath = PlatformPath.GetPathForMac(Path.Combine(podfileRoot, "Podfile"));
            var podfileLockPath = PlatformPath.GetPathForMac(Path.Combine(podfileRoot, "Podfile.lock"));

            // see if we can avoid updating the master repo
            noRepoUpdate = noRepoUpdate == true || (noRepoUpdate == null && FileExists(podfileLockPath));

            // create and restore a Podfile
            return CreatePodfile(framework, podfilePath) && RestorePodfile(podfileRoot, noRepoUpdate);
        }

        protected bool CreatePodfile(bool framework, string podfilePath)
        {
            if (IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            var podfile =
                $"{(framework ? "use_frameworks!" : "")}\n" +
                $"platform :{PlatformName}, '{PlatformVersion}'\n" +
                $"target 'CocoaPodBuildTask' do\n" +
                $"  pod '{PodName}', '{PodVersion}'\n" +
                $"end";
            CreateDirectory(PlatformPath.GetDirectoryNameForMac(podfilePath));
            using (var stream = GetStreamFromText(podfile))
            {
                UploadFile(stream, podfilePath);
            }

            return true;
        }

        protected bool RestorePodfile(string podfileRoot, bool? noRepoUpdate)
        {
            if (IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            var restore =
                $@"""{PodToolPath}"" install" +
                $@"  --no-integrate" +
                $@"  --project-directory=""{podfileRoot}""" +
                $@"  {(noRepoUpdate == true ? "--no-repo-update" : "")}";
            var restorePods = ExecuteCommandStream(restore);
            if (!WasSuccess(restorePods))
            {
                Log.LogError("Error installing the podfile: " + restorePods.Result);
                return false;
            }

            return true;
        }

        protected bool BuildPodfileXcodeProject(string podfileRoot, bool framework, XCodeBuildSettings.XCodeArcitecture buildSettings)
        {
            var projectFilePath = PlatformPath.GetPathForMac(Path.Combine(podfileRoot, "Pods/Pods.xcodeproj"));

            if (UseFrameworks)
            {

            }
            else
            {

            }

            return BuildXCodeProject(projectFilePath, framework, buildSettings);
        }

        protected bool BuildXCodeProject(string projectFilePath, bool framework, XCodeBuildSettings.XCodeArcitecture buildSettings)
        {
            foreach (var build in buildSettings.Builds)
            {
                if (IsCancellationRequested)
                {
                    Log.LogError("Task was canceled.");
                    return false;
                }

                var arch = build.Key;
                var sdk = build.Value;

                // build the project
                var xcodebuild =
                    $@"""{XCodeBuildToolPath}""" +
                    $@"  -project ""{projectFilePath}""" +
                    $@"  -target ""Pods-CocoaPodBuildTask""" +
                    $@"  -configuration ""Release""" +
                    $@"  -arch ""{arch}""" +
                    $@"  -sdk ""{sdk}""" +
                    $@"  build";
                //var cd = $@"(cd ""{PlatformPath.GetDirectoryNameForMac(projectFilePath)}"" && {xcodebuild})";
                var result = ExecuteCommandStream(xcodebuild);
                if (!WasSuccess(result))
                {
                    Log.LogError($"Error building the XCode project: {result.Result}");
                    return false;
                }

                if (IsCancellationRequested)
                {
                    Log.LogError("Task was canceled.");
                    return false;
                }

                if (framework)
                {
                }
                else
                {
                    // copy the output from static archives
                    //var staticArchive = workingDirectory.Combine("build").Combine(os == TargetOS.Mac ? "Release" : ("Release-" + sdk)).CombineWithFilePath(output);
                }
            }

            return true;
        }

        protected bool RunLipo(string podfileRoot, string output, params string[] inputs)
        {
            if (IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            output = PlatformPath.GetPathForMac(Path.Combine(podfileRoot, output));
            inputs = inputs.Select(i => PlatformPath.GetPathForMac(Path.Combine(podfileRoot, i))).ToArray();

            var lipo =
                $@"lipo -create" +
                $@"  -output ""{output}""" +
                $@"  {inputs.Select(i => $@"""{i}"" ")}";
            var runLipo = ExecuteCommand(lipo);
            if (!WasSuccess(runLipo))
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
    }
}

