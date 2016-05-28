using System.Collections;
using System.Collections.Generic;

namespace Xamarin.iOS.NativeBuild.Tasks.XCodeBuild
{
    public class XCodeBuildOutputs : IEnumerable<BuildTargetOutput>
    {
        private readonly Dictionary<string, BuildTargetOutput> outputs = new Dictionary<string, BuildTargetOutput>();

        public BuildTargetOutput this[string target]
        {
            get
            {
                if (!outputs.ContainsKey(target))
                {
                    outputs.Add(target, new BuildTargetOutput { Target = target });
                }
                return outputs[target];
            }
        }

        public string ArtifactsDirectory { get; set; }

        public string OutputDirectory { get; set; }

        public string ProjectDirectory { get; set; }

        public IEnumerator<BuildTargetOutput> GetEnumerator()
        {
            return outputs.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
