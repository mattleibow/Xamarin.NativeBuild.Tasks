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

namespace Xamarin.iOS.NativeBuild.Tasks
{
    public abstract class SshBasedTask : Task, ICancelableTask
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

        protected void UploadFile(Stream stream, string remotePath)
        {
            Commands.Runner.Upload(stream, remotePath);
        }

        protected void CreateDirectory(string directoryPath)
        {
            Commands.CreateDirectory(directoryPath);
        }

        protected bool FileExists(string filePath)
        {
            return Commands.FileExists(filePath);
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

        protected string GetCommandResult(string commandText, bool firstLineOnly = true)
        {
            var result = GetCommandResult(ExecuteCommand(commandText));
            return firstLineOnly ? GetFirstLine(result) : result;
        }

        protected string GetCommandResult(SshCommand command)
        {
            if (!WasSuccess(command) || string.IsNullOrEmpty(command.Result))
            {
                return string.Empty;
            }
            return command.Result.Trim();
        }

        protected bool WasSuccess(SshCommand command)
        {
            return command.ExitStatus == 0;
        }

        protected string GetFirstLine(string multiline)
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

        protected string LocateToolPath(string toolPath, string tool, string versionOption)
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
