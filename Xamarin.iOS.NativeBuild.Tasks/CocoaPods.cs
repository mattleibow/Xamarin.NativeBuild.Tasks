using System.Threading;
using Microsoft.Build.Utilities;

namespace Xamarin.iOS.NativeBuild.Tasks
{
    public class CocoaPods
    {
        public CocoaPods(string podToolPath, TaskLoggingHelper log, CancellationToken cancellation, ISshInterface sshInterface)
        {
            PodToolPath = podToolPath;
            Log = log;
            CancellationToken = cancellation;
            Ssh = sshInterface;
        }

        public CancellationToken CancellationToken { get; private set; }

        public TaskLoggingHelper Log { get; private set; }

        public ISshInterface Ssh { get; private set; }

        public string PodToolPath { get; private set; }

        public bool CreatePodfileXCodeProject(string podfileRoot, Podfile podfile, bool? noRepoUpdate = null)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            var podfilePath = SshPath.Combine(podfileRoot, "Podfile");
            var podfileLockPath = SshPath.Combine(podfileRoot, "Podfile.lock");

            // see if we can avoid updating the master repo
            noRepoUpdate = noRepoUpdate == true || (noRepoUpdate == null && Ssh.FileExists(podfileLockPath));

            // create and restore a Podfile
            return CreatePodfile(podfile, podfilePath) && RestorePodfile(podfileRoot, noRepoUpdate);
        }

        public bool CreatePodfile(Podfile podfile, string podfilePath)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            var podfileContents =
                $"{(podfile.UseFrameworks ? "use_frameworks!" : "")}\n" +
                $"platform :{podfile.PlatformName}, '{podfile.PlatformVersion}'\n" +
                $"target 'CocoaPodBuildTask' do\n";
            foreach (var pod in podfile.Pods)
            {
                podfileContents +=
                $"    pod '{pod.Id}', '{pod.Version}'\n";
            }
            podfileContents +=
                $"end";
            Ssh.CreateDirectory(SshPath.GetDirectoryName(podfilePath));
            using (var stream = Utilities.GetStreamFromText(podfileContents))
            {
                Ssh.CreateFile(stream, podfilePath);
            }

            return true;
        }

        public bool RestorePodfile(string podfileRoot, bool? noRepoUpdate)
        {
            if (CancellationToken.IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            var restore =
                $@"""{PodToolPath}"" install" +
                $@"  --no-integrate" +
                $@"  --project-directory=""{podfileRoot}""" +
                $@"  {(noRepoUpdate == true ? "--no-repo-update" : "")}";
            var restorePods = Ssh.ExecuteCommandStream(restore);
            if (!Ssh.WasSuccess(restorePods))
            {
                Log.LogError("Error installing the podfile: " + restorePods.Result);
                return false;
            }

            return true;
        }

        public enum PodfilePlatform
        {
            iOS,
            tvOS,
            OSX,
            watchOS
        }

        public class Podfile
        {
            public string PlatformName => Platform.ToString().ToLowerInvariant();

            public PodfilePlatform Platform { get; set; }

            public string PlatformVersion { get; set; }

            public bool UseFrameworks { get; set; }

            public Pod[] Pods { get; set; }
        }

        public class Pod
        {
            public string Id { get; set; }

            public string Version { get; set; }
        }
    }
}
