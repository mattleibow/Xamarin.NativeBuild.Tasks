using System;
using System.IO;
using System.Linq;
using Xamarin.NativeBuild.Tasks.Common;

namespace Xamarin.NativeBuild.Tasks.Common
{
    public static class CrossPath
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
            return Utilities.IsWindows ? ToWindows(path) : ToUnix(path);
        }

        public static string CombineSsh(params string[] paths)
        {
            return ToSsh(Path.Combine(paths.Select(ToCurrent).ToArray()));
        }

        public static string GetDirectoryNameSsh(string path)
        {
            return ToSsh(Path.GetDirectoryName(ToCurrent(path)));
        }

        public static string GetFileNameSsh(string path)
        {
            return ToSsh(Path.GetFileName(ToCurrent(path)));
        }

        public static string GetFileNameWithoutExtensionSsh(string path)
        {
            return ToSsh(Path.GetFileNameWithoutExtension(ToCurrent(path)));
        }
    }
}
