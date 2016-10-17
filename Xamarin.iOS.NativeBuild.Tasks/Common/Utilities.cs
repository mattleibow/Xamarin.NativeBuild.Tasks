using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Xamarin.NativeBuild.Tasks.Common
{
    public static class Utilities
    {
        public static readonly bool IsWindows;
        public static readonly bool IsUnix;

        static Utilities()
        {
            IsWindows = Environment.OSVersion.Platform != PlatformID.Unix &&
                        Environment.OSVersion.Platform != PlatformID.MacOSX;
            IsUnix = !IsWindows;
        }

        public static string GetFirstLine(string multiline)
        {
            return GetLine(multiline, 0);
        }

        public static string GetLine(string multiline, int line)
        {
            var split = multiline.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            return split.Skip(line).FirstOrDefault() ?? string.Empty;
        }

        public static string[] SplitLines(string multiline)
        {
            return multiline.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
        }

        public static Stream GetStreamFromText(string contents)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(contents);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        public static T ParseEnum<T>(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return default(T);
            }

            var split = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            value = string.Join(",", split.Select(s => s.Trim()).Where(s => !string.IsNullOrWhiteSpace(s)));

            if (string.IsNullOrWhiteSpace(value))
            {
                return default(T);
            }

            return (T)Enum.Parse(typeof(T), value, true);
        }

        public static T CreateEnum<T>(IEnumerable<T> flags)
        {
            return (T)(object)flags.Cast<int>().Aggregate((aggr, next) => aggr | next);
        }

        public static string GetProcessResult(string tool, string args, bool firstLineOnly = true)
        {
            var info = new ProcessStartInfo
            {
                FileName = tool,
                Arguments = args,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            using (var process = Process.Start(info))
            using (StreamReader reader = process.StandardOutput)
            {
                string result;
                if (firstLineOnly)
                {
                    result = reader.ReadLine();
                    reader.ReadToEnd(); // wait untile the stream ends
                }
                else
                {
                    result = reader.ReadToEnd();
                }
                if (process.ExitCode != 0)
                {
                    return null;
                }
                return result;
            }
        }
    }
}
