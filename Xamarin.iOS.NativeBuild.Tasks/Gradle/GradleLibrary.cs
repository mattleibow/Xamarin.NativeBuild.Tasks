namespace Xamarin.Android.NativeBuild.Tasks.Gradle
{
    public class GradleLibrary
    {
        public GradleDependency Request { get; set; }

        public GradleDependencyTypes Type { get; set; }

        public string Path { get; set; }

        public BuildAction BuildAction { get; set; }
    }
}
