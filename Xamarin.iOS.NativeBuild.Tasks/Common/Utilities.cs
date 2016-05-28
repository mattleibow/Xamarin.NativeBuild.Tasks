using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Xamarin.iOS.NativeBuild.Tasks.Common
{
    public static class Utilities
    {
        public static string GetFirstLine(string multiline)
        {
            var split = multiline.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
            return split.FirstOrDefault() ?? string.Empty;
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
    }
}
