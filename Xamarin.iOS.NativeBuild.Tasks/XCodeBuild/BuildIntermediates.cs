using System.Collections;
using System.Collections.Generic;

namespace Xamarin.iOS.NativeBuild.Tasks.XCodeBuild
{
    public class BuildIntermediates : IEnumerable<BuildArchitectureOutput>
    {
        private readonly Dictionary<XCodeArchitectures, BuildArchitectureOutput> intermediates = new Dictionary<XCodeArchitectures, BuildArchitectureOutput>();

        public BuildIntermediates(BuildTargetOutput targetOutput)
        {
            TargetOutput = targetOutput;
        }

        public BuildArchitectureOutput this[XCodeArchitectures arch]
        {
            get
            {
                if (!intermediates.ContainsKey(arch))
                {
                    intermediates.Add(arch, new BuildArchitectureOutput(TargetOutput) { Architecture = arch });
                }
                return intermediates[arch];
            }
        }

        public BuildTargetOutput TargetOutput { get; private set; }

        public IEnumerator<BuildArchitectureOutput> GetEnumerator()
        {
            return intermediates.Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
