using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Messaging;
using Xamarin.Messaging.Client;
using Xamarin.Messaging.Client.Ssh;
using Xamarin.Messaging.Diagnostics;
using Xamarin.Messaging.VisualStudio;
using Xamarin.VisualStudio.Build;
using Renci.SshNet;
using System.Diagnostics;
using System.Text;

namespace Xamarin.iOS.NativeBuild.Tasks
{
    public abstract class SshBasedTask : Task, ICancelableTask, ISshInterface
    {
        protected static string[] ToolSearchPaths = { "/usr/local/bin", "/usr/local/sbin", "/usr/bin", "/usr/sbin", "/bin", "/sbin" };

        private readonly CancellationTokenSource tokenSource;

        private readonly Lazy<IBuildClient> buildClient;
        private readonly Lazy<MessagingService> messagingService;
        private readonly Lazy<string> macHomePath;

        public SshBasedTask()
        {
            // task stuff
            tokenSource = new CancellationTokenSource();

            // ssh stuff
            buildClient = new Lazy<IBuildClient>(() => BuildClients.Instance.Get(SessionId));
            messagingService = new Lazy<MessagingService>(() =>
            {
                var type = BuildClient.GetType().GetTypeInfo();
                var property = type.GetDeclaredProperty("MessagingService");
                return property.GetValue(BuildClient) as MessagingService;
            });
            macHomePath = new Lazy<string>(() => Commands.GetHomeDirectory());
        }

        [Required]
        public string SessionId { get; set; }

        [Required]
        public string AppName { get; set; }

        [Required]
        public string IntermediateOutputPath { get; set; }

        public bool IsCancellationRequested => tokenSource.IsCancellationRequested;

        private IBuildClient BuildClient => buildClient.Value;

        private MessagingService MessagingService => messagingService.Value;

        private IMessagingClient MessagingClient => MessagingService.MessagingClient;

        private ISshCommands Commands => MessagingService.SshCommands;

        protected string MacHomePath => macHomePath.Value;

        protected string BuildRootPath => PlatformPath.GetBuildPath(MacHomePath, AppName, SessionId, string.Empty);

        protected string BuildIntermediateOutputPath => PlatformPath.GetBuildPath(MacHomePath, AppName, SessionId, IntermediateOutputPath);

        public override bool Execute()
        {
            Tracer.SetManager(BuildTracerManager.Instance);

            if (IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

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

            return true;
        }

        public virtual void Cancel()
        {
            tokenSource.Cancel();
        }

        protected CancellationToken GetCancellationToken()
        {
            return tokenSource.Token;
        }

        public void CreateFile(Stream stream, string remotePath)
        {
            Commands.Runner.Upload(stream, SshPath.ToSsh(remotePath));
        }

        public void CopyPath(string source, string destination)
        {
            ExecuteCommand($@"cp -rf ""{SshPath.ToSsh(source)}"" ""{SshPath.ToSsh(destination)}""");
        }

        public void CreateDirectory(string directoryPath)
        {
            Commands.CreateDirectory(SshPath.ToSsh(directoryPath));
        }

        public bool FileExists(string filePath)
        {
            return Commands.FileExists(SshPath.ToSsh(filePath));
        }

        private string CreatePList(string labelName, string outputPath, string errorPath, string[] arguments, string workingDirectory = null)
        {
            var plist =
                $"<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n" +
                $"<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">\n" +
                $"<plist version=\"1.0\">\n" +
                $"<dict>\n" +
                $"    <key>Label</key>\n" +
                $"    <string>{labelName}</string>\n" +
                $"    <key>RunAtLoad</key>\n" +
                $"    <true/>\n" +
                $"    <key>LaunchOnlyOnce</key>\n" +
                $"    <true/>\n" +
                $"    <key>LimitLoadToSessionType</key>\n" +
                $"    <string>Aqua</string>\n";
            if (!string.IsNullOrEmpty(workingDirectory))
            {
                plist +=
                $"    <key>WorkingDirectory</key>\n" +
                $"    <string>{workingDirectory}</string>\n";
            }
            plist +=
                $"    <key>StandardOutPath</key>\n" +
                $"    <string>{outputPath}</string>\n" +
                $"    <key>StandardErrorPath</key>\n" +
                $"    <string>{errorPath}</string>\n" +
                $"    <key>ProgramArguments</key>\n" +
                $"    <array>\n";
            foreach (var arg in arguments)
            {
                plist +=
                $"        <string>{arg}</string>\n";
            }
            plist +=
                $"    </array>\n" +
                $"</dict>\n" +
                $"</plist>\n";
            return plist;
        }

        public bool ExecuteLaunchCtlCommand(string[] arguments, int checkInterval = 600, string workingDirectory = null)
        {
            var binary = arguments.FirstOrDefault();
            var options = new StartOptions
            {
                Id = Process.GetCurrentProcess().Id.ToString(),
                DateTime = DateTime.Now
            };
            var labelName = $"com.xamarin.nativebuild.tasks.{options.GetFormattedDateTime()}.{options.Id}.{Path.GetFileNameWithoutExtension(binary)}".ToLowerInvariant();
            var root = $"/tmp/{labelName}";

            var outputLog = SshPath.Combine(root, "output.log");
            var errorLog = SshPath.Combine(root, "error.log");
            var appPList = SshPath.Combine(root, "app.plist");

            // upload the plist
            var plist = CreatePList(labelName, outputLog, errorLog, arguments, workingDirectory);
            CreateDirectory(root);
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(plist)))
            {
                CreateFile(stream, appPList);
            }

            // create the logs and start the process
            var launchctl = ExecuteCommand($"touch {outputLog}; touch {errorLog}; launchctl load -S Aqua {appPList}");
            if (!WasSuccess(launchctl))
            {
                Log.LogError($"Unable to starting {binary}: {launchctl.Result}");
                return false;
            }

            // tail and stream the output
            var tailOutput = ExecuteCommandStream($"tail -f {outputLog} & while launchctl list {labelName} &> /dev/null; do sleep {checkInterval / 1000}; done; kill $!;");
            if (!WasSuccess(tailOutput))
            {
                Log.LogError($"There was an error reading the output: {tailOutput.Result}");
                return false;
            }

            // get the errors
            var tailError = ExecuteCommand($"cat {errorLog}");
            if (!WasSuccess(tailError))
            {
                Log.LogError($"There was an error reading the error log: {tailError.Result}");
                return false;
            }
            else if (!string.IsNullOrEmpty(tailError.Result))
            {
                Log.LogError($"There was an error: {tailError.Result}");
                return false;
            }

            return true;
        }

        public SshCommand ExecuteCommand(string commandText)
        {
            Log.LogVerbose($"Executing SSH command '{commandText}'...");
            Log.LogCommandLine(commandText);
            var command = Commands.Runner.ExecuteCommand(commandText);
            Log.LogVerbose($"SSH command exit code was '{command.ExitStatus}'...");
            return command;
        }

        public SshCommand ExecuteBashCommandStream(string commandText)
        {
            var bash = $"/bin/bash -c '{commandText}'";
            return ExecuteCommandStream(bash);
        }

        public SshCommand ExecuteCommandStream(string commandText)
        {
            Log.LogVerbose($"Executing SSH command '{commandText}'...");
            Log.LogCommandLine(commandText);

            var cancelled = false;
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
                if (IsCancellationRequested && !cancelled)
                {
                    Log.LogVerbose($"SSH cancellation was requested...");
                    command.CancelAsync();
                    cancelled = false;
                }

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

        public string GetCommandResult(string commandText, bool firstLineOnly = true)
        {
            var result = GetCommandResult(ExecuteCommand(commandText));
            return firstLineOnly ? Utilities.GetFirstLine(result) : result;
        }

        public string GetCommandResult(SshCommand command)
        {
            if (!WasSuccess(command) || string.IsNullOrEmpty(command.Result))
            {
                return string.Empty;
            }
            return command.Result.Trim();
        }

        public bool WasSuccess(SshCommand command)
        {
            return command.ExitStatus == 0;
        }

        public string LocateToolPath(string toolPath, string tool, string versionOption)
        {
            string foundPath = null;

            if (!string.IsNullOrEmpty(toolPath))
            {
                // if it was explicitly set, bail if it wasn't found
                toolPath = SshPath.ToSsh(toolPath);
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
                    var dirs = string.Join(" ", ToolSearchPaths);
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
                foundPath = SshPath.ToSsh(foundPath);
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
