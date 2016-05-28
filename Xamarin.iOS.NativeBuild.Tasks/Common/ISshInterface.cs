using System;
using System.IO;
using Renci.SshNet;

namespace Xamarin.iOS.NativeBuild.Tasks.Common
{
    public interface ISshInterface
    {
        string AppName { get; set; }
        string SessionId { get; set; }

        void CopyPath(string source, string destination);
        void CreateDirectory(string directoryPath);
        void CreateFile(Stream stream, string remotePath);
        SshCommand ExecuteBashCommandStream(string commandText, Action<string> processStream = null);
        SshCommand ExecuteCommand(string commandText);
        SshCommand ExecuteCommandStream(string commandText, Action<string> processStream = null);
        bool ExecuteLaunchCtlCommand(string[] arguments, int checkInterval = 600, string workingDirectory = null);
        bool FileExists(string filePath);
        string GetCommandResult(SshCommand command);
        string GetCommandResult(string commandText, bool firstLineOnly = true);
        string LocateToolPath(string toolPath, string tool, string versionOption);
        bool WasSuccess(SshCommand command);
    }
}
