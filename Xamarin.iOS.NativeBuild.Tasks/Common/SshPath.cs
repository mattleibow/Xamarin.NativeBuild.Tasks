using System;
using System.IO;
using System.Linq;

namespace Xamarin.iOS.NativeBuild.Tasks.Common
{
    public static class SshPath
    {
        public static string ToUnix(string path)
        {
            return path.Replace("\\", "/");
        }

        public static string ToWindows(string path)
        {
            return path.Replace("/", "\\");
        }

        public static string ToSsh(string path)
        {
            return ToUnix(path);
        }

        public static string ToCurrent(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }
            if (Environment.OSVersion.Platform != PlatformID.Unix && Environment.OSVersion.Platform != PlatformID.MacOSX)
            {
                return ToWindows(path);
            }
            return ToUnix(path);
        }

        public static string Combine(params string[] paths)
        {
            return ToUnix(Path.Combine(paths.Select(ToCurrent).ToArray()));
        }

        public static string GetDirectoryName(string path)
        {
            return ToUnix(Path.GetDirectoryName(ToCurrent(path)));
        }

        public static string GetFileName(string path)
        {
            return ToUnix(Path.GetFileName(ToCurrent(path)));
        }

        public static string GetFileNameWithoutExtension(string path)
        {
            return ToUnix(Path.GetFileNameWithoutExtension(ToCurrent(path)));
        }
    }
}
