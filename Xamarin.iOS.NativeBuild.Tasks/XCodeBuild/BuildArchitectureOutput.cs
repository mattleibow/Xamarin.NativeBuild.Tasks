namespace Xamarin.iOS.NativeBuild.Tasks.XCodeBuild
{
    public class BuildArchitectureOutput
    {
        public BuildArchitectureOutput(BuildTargetOutput targetOutput)
        {
            TargetOutput = targetOutput;
        }

        public BuildTargetOutput TargetOutput { get; private set; }

        public XCodeArchitectures Architecture { get; set; }

        public bool IsFrameworks { get; set; }

        public string Path { get; set; }
    }
}
