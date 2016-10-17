namespace Xamarin.Android.NativeBuild.Tasks.Gradle
{
    public class GradleDependency
    {
        public string ArtifactId { get; set; }

        public string GroupId { get; set; }

        public GradleRepositories Repository { get; set; }

        public GradleDependencyTypes Type { get; set; }

        public string Version { get; set; }

        public string MavenResourcePath
        {
            get
            {
                var path = $"{GroupId.Replace("\\", ":").Replace("/", ":")}:{ArtifactId}";
                if (!string.IsNullOrEmpty(Version))
                {
                    path += $":{Version}";
                }
                if (Type != GradleDependencyTypes.Default)
                {
                    path += $"@{Type.ToString().ToLowerInvariant()}";
                }
                return path;
            }
        }
    }
}
