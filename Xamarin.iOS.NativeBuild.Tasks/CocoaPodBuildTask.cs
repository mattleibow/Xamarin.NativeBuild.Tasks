using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.iOS.Tasks;
using Xamarin.Messaging;
using Xamarin.Messaging.Build.Contracts;
using Xamarin.Messaging.Client;
using Xamarin.Messaging.Client.Ssh;
using Xamarin.Messaging.Diagnostics;
using Xamarin.Messaging.VisualStudio;
using Xamarin.VisualStudio.Build;
using Renci.SshNet;
using System.Collections.Generic;
using System.Threading;

namespace Xamarin.iOS.NativeBuild.Tasks
{
    public class CocoaPodBuildTask : Task
    {
        private static string[] SearchPaths = { "/usr/local/bin", "/usr/local/sbin", "/usr/bin", "/usr/sbin", "/bin", "/sbin" };

        private static class XCodeBuild
        {
            public static iOSArcitecture iOS = new iOSArcitecture();
            public static MacOSXArcitecture MacOSX = new MacOSXArcitecture();
            public static tvOSArcitecture tvOS = new tvOSArcitecture();

            public abstract class XCodeArcitecture
            {
                public string SimulatorSdk;

                public string DeviceSdk;

                public string[] SimulatorArchitectures;

                public string[] DeviceArchitectures;

                public string[] Architectures => SimulatorArchitectures.Union(DeviceArchitectures).ToArray();

                public SingleArcitecture SimulatorSingle => new SingleArcitecture(SimulatorSdk, SimulatorArchitectures.First());

                public SingleArcitecture DeviceSingle => new SingleArcitecture(DeviceSdk, DeviceArchitectures.First());

                public Dictionary<string, string> Builds
                {
                    get
                    {
                        var sims = SimulatorArchitectures.Select(a => new KeyValuePair<string, string>(a, SimulatorSdk));
                        var devs = DeviceArchitectures.Select(a => new KeyValuePair<string, string>(a, DeviceSdk));
                        return sims.Union(devs).ToDictionary(i => i.Key, i => i.Value);
                    }
                }
            }

            public class SingleArcitecture : XCodeArcitecture
            {
                public SingleArcitecture(string sdk, string architecture)
                {
                    SimulatorSdk = sdk;
                    DeviceSdk = sdk;

                    SimulatorArchitectures = new[] { architecture };
                    DeviceArchitectures = new[] { architecture };
                }
            }

            public class iOSArcitecture : XCodeArcitecture
            {
                public iOSArcitecture()
                {
                    SimulatorSdk = "iphonesimulator";
                    DeviceSdk = "iphoneos";

                    SimulatorArchitectures = new[] { "x86_64", "i386" };
                    DeviceArchitectures = new[] { "arm64", "armv7", "armv7s" };
                }
            }

            public class MacOSXArcitecture : XCodeArcitecture
            {
            }

            public class tvOSArcitecture : XCodeArcitecture
            {
            }
        }

        private IBuildClient buildClient;
        private MessagingService messagingService;

        // build properties

        [Required]
        public string SessionId { get; set; }

        [Required]
        public string AppName { get; set; }

        [Required]
        public string IntermediateOutputPath { get; set; }

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

        protected IBuildClient BuildClient
        {
            get
            {
                if (buildClient == null)
                {
                    buildClient = BuildClients.Instance.Get(SessionId);
                }
                return buildClient;
            }
        }

        protected MessagingService MessagingService
        {
            get
            {
                if (messagingService == null)
                {
                    var type = BuildClient.GetType().GetTypeInfo();
                    var property = type.GetDeclaredProperty("MessagingService");
                    messagingService = property.GetValue(buildClient) as MessagingService;
                }
                return messagingService;
            }
        }

        protected IMessagingClient MessagingClient
        {
            get { return MessagingService.MessagingClient; }
        }

        protected ISshCommands Commands
        {
            get { return MessagingService.SshCommands; }
        }

        protected string MacHomePath
        {
            get { return Commands.GetHomeDirectory(); }
        }

        protected string BuildRootPath
        {
            get { return PlatformPath.GetBuildPath(MacHomePath, AppName, SessionId, string.Empty); }
        }

        protected string BuildIntermediateOutputPath
        {
            get { return PlatformPath.GetBuildPath(MacHomePath, AppName, SessionId, IntermediateOutputPath); }
        }

        public override bool Execute()
        {
            Tracer.SetManager(BuildTracerManager.Instance);

            // make sure our tools are working
            if (BuildClient == null)
            {
                Log.LogError("The build client wasn't set up correctly.");
                return false;
            }
            if (MessagingService == null)
            {
                Log.LogError("The messaging service wasn't set up correctly.");
                return false;
            }
            if (string.IsNullOrEmpty(BuildRootPath))
            {
                Log.LogError("The mac build root wasn't set up correctly.");
                return false;
            }
            if (!BuildClient.IsConnected)
            {
                Log.LogError("A connection to the mac is required in order to use Cocoapods.");
                return false;
            }

            // make sure we have POD available
            PodToolPath = LocateToolPath(PodToolPath, "pod", "--version");
            if (string.IsNullOrEmpty(PodToolPath))
            {
                return false;
            }

            // make sure we have POD available
            XCodeBuildToolPath = LocateToolPath(XCodeBuildToolPath, "xcodebuild", "-version");
            if (string.IsNullOrEmpty(XCodeBuildToolPath))
            {
                return false;
            }

            var podfileRoot = PlatformPath.GetPathForMac(Path.Combine(BuildIntermediateOutputPath, PodName));

            // create the podfile for the bundling build
            if (!CreatePodfile(podfileRoot, true))
            {
                return false;
            }

            // build Pod-CocoaPodBuildTask as a framework
            if (!BuildPods(podfileRoot, true, XCodeBuild.iOS.SimulatorSingle))
            {
                return false;
            }

            // create the podfile for the real build
            if (!CreatePodfile(podfileRoot, UseFrameworks, noRepoUpdate: true))
            {
                return false;
            }

            // build Pod-CocoaPodBuildTask as requested
            if (!BuildPods(podfileRoot, UseFrameworks, XCodeBuild.iOS))
            {
                return false;
            }


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

        private bool CreatePodfile(string podfileRoot, bool framework, bool? noRepoUpdate = null)
        {
            var podfilePath = PlatformPath.GetPathForMac(Path.Combine(podfileRoot, "Podfile"));
            var podfileLockPath = PlatformPath.GetPathForMac(Path.Combine(podfileRoot, "Podfile.lock"));

            // see if we can avoid updating the master repo
            noRepoUpdate = noRepoUpdate == true || (noRepoUpdate == null && Commands.FileExists(podfileLockPath));

            // create a Podfile on the mac
            var podfile =
                $"{(framework ? "use_frameworks!" : "")}\n" +
                $"platform :{PlatformName}, '{PlatformVersion}'\n" +
                $"target 'CocoaPodBuildTask' do\n" +
                $"  pod '{PodName}', '{PodVersion}'\n" +
                $"end";
            Commands.CreateDirectory(podfileRoot);
            using (var stream = GetStreamFromText(podfile))
            {
                Commands.Runner.Upload(stream, podfilePath);
            }

            // restore those pods
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
            else
            {
                Log.LogVerbose($"pod result: {restorePods.Result}");
            }

            return true;
        }

        private bool BuildPods(string podfileRoot, bool framework, XCodeBuild.XCodeArcitecture buildSettings)
        {
            var projectFilePath = PlatformPath.GetPathForMac(Path.Combine(podfileRoot, "Pods/Pods.xcodeproj"));

            return BuildXcodeProject(projectFilePath, framework, buildSettings);
        }

        private bool BuildXcodeProject(string projectFilePath, bool framework, XCodeBuild.XCodeArcitecture buildSettings)
        {
            if (framework)
            {
            }
            else
            {
                foreach (var build in buildSettings.Builds)
                {
                    var arch = build.Key;
                    var sdk = build.Value;

                    var xcodebuild =
                        $@"""{XCodeBuildToolPath}""" +
                        $@"  -project ""{projectFilePath}""" +
                        $@"  -target ""Pods-CocoaPodBuildTask""" +
                        $@"  -configuration ""Release""" +
                        $@"  -arch ""{arch}""" +
                        $@"  -sdk ""{sdk}""" +
                        $@"  build";
                    var result = ExecuteCommandStream(xcodebuild);
                    if (!WasSuccess(result))
                    {
                        Log.LogError($"Error building the XCode project: {result.Result}");
                        return false;
                    }
                    else
                    {
                        Log.LogVerbose($"xcodebuild result: {result.Result}");
                    }

                }
            }

            return true;
        }

        private bool RunLipo(string podfileRoot, string output, params string[] inputs)
        {
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

        protected SshCommand ExecuteCommand(string commandText)
        {
            Log.LogVerbose($"Executing SSH command '{commandText}'...");
            Log.LogCommandLine(commandText);
            var command = Commands.Runner.ExecuteCommand(commandText);
            Log.LogVerbose($"SSH command exit code was '{command.ExitStatus}'...");
            return command;
        }

        protected SshCommand ExecuteBashCommandStream(string commandText)
        {
            var bash = $"/bin/bash -c '{commandText}'";
            return ExecuteCommandStream(bash);
        }

        protected SshCommand ExecuteCommandStream(string commandText)
        {
            Log.LogVerbose($"Executing SSH command '{commandText}'...");
            Log.LogCommandLine(commandText);
            var command = MessagingService.SshMessagingConnection.SshClient.CreateCommand(commandText);
            var asyncResult = command.BeginExecute(result =>
            {
                var text = command.EndExecute(result);
                if (!string.IsNullOrEmpty(text))
                {
                    Log.LogVerbose(text);
                }
            });
            var reader = new StreamReader(command.OutputStream);
            while (!asyncResult.IsCompleted)
            {
                if (!reader.EndOfStream)
                {
                    Log.LogVerbose(reader.ReadToEnd().Trim());
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
            Log.LogVerbose($"SSH command exit code was '{command.ExitStatus}'...");
            return command;
        }

        protected string GetCommandResult(string commandText, bool firstLineOnly = true)
        {
            var result = GetCommandResult(ExecuteCommand(commandText));
            return firstLineOnly ? GetFirstLine(result) : result;
        }

        protected static string GetCommandResult(SshCommand command)
        {
            if (!WasSuccess(command) || string.IsNullOrEmpty(command.Result))
            {
                return string.Empty;
            }
            return command.Result.Trim();
        }

        protected static bool WasSuccess(SshCommand command)
        {
            return command.ExitStatus == 0;
        }

        protected static string GetFirstLine(string multiline)
        {
            var split = multiline.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            return split.FirstOrDefault() ?? string.Empty;
        }

        protected Stream GetStreamFromText(string contents)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(contents);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        private string LocateToolPath(string toolPath, string tool, string versionOption)
        {
            string foundPath = null;

            if (!string.IsNullOrEmpty(toolPath))
            {
                // if it was explicitly set, bail if it wasn't found
                toolPath = PlatformPath.GetPathForMac(toolPath);
                if (Commands.FileExists(toolPath))
                {
                    foundPath = toolPath;
                }
            }
            else
            {
                // not set, so search
                var findTool = GetCommandResult($"which {tool}");
                if (!string.IsNullOrEmpty(findTool))
                {
                    foundPath = findTool.Trim();
                }
                else
                {
                    // we didn't find {tool} in the default places, so do a bit of research
                    var dirs = string.Join(" ", SearchPaths);
                    var command =
                        $@"for file in {dirs}; do " +
                        $@"  if [ -e ""$file/{tool}"" ]; then" +
                        $@"    echo ""$file/{tool}""; " +
                        $@"    exit 0; " +
                        $@"  fi; " +
                        $@"done; " +
                        $@"exit 1; ";
                    findTool = GetCommandResult(command);
                    if (!string.IsNullOrEmpty(findTool))
                    {
                        foundPath = findTool.Trim();
                    }
                }
            }

            if (string.IsNullOrEmpty(foundPath))
            {
                Log.LogError("Unable to find {tool}.");
            }
            else
            {
                foundPath = PlatformPath.GetPathForMac(foundPath);
                if (string.IsNullOrEmpty(versionOption))
                {
                    Log.LogVerbose($"Found {tool} at {foundPath}.");
                }
                else
                {
                    var version = GetCommandResult($"{foundPath} {versionOption}");
                    Log.LogVerbose($"Found {tool} version {version} at {foundPath}.");
                }
            }

            return foundPath;
        }
    }
}
