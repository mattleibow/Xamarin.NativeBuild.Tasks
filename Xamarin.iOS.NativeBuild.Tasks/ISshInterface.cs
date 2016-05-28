using System.IO;
using Renci.SshNet;

namespace Xamarin.iOS.NativeBuild.Tasks
{
    public interface ISshInterface
    {
        string AppName { get; set; }
        string SessionId { get; set; }

        void CopyPath(string source, string destination);
        void CreateDirectory(string directoryPath);
        void CreateFile(Stream stream, string remotePath);
        SshCommand ExecuteBashCommandStream(string commandText);
        SshCommand ExecuteCommand(string commandText);
        SshCommand ExecuteCommandStream(string commandText);
        bool ExecuteLaunchCtlCommand(string[] arguments, int checkInterval = 600, string workingDirectory = null);
        bool FileExists(string filePath);
        string GetCommandResult(SshCommand command);
        string GetCommandResult(string commandText, bool firstLineOnly = true);
        string LocateToolPath(string toolPath, string tool, string versionOption);
        bool WasSuccess(SshCommand command);
    }
}
