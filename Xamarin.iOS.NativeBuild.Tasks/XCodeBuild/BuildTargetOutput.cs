namespace Xamarin.iOS.NativeBuild.Tasks.XCodeBuild
{
    public class BuildTargetOutput
    {
        private readonly BuildIntermediates intermediates;

        public BuildTargetOutput()
        {
            intermediates = new BuildIntermediates(this);
        }

        public string Target { get; set; }

        public string IntermediateDirectory { get; set; }

        public BuildIntermediates Intermediates => intermediates;

        public string Directory { get; set; }

        public BuildArchitectureOutput FrameworkOutput { get; set; }

        public BuildArchitectureOutput ArchiveOutput { get; set; }
    }
}
