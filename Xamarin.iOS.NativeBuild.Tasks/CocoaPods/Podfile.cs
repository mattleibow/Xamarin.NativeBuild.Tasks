namespace Xamarin.iOS.NativeBuild.Tasks.CocoaPods
{
    public class Podfile
    {
        public string PlatformName => Platform.ToString().ToLowerInvariant();

        public PodfilePlatform Platform { get; set; }

        public string PlatformVersion { get; set; }

        public string TargetName { get; set; }

        public bool UseFrameworks { get; set; }

        public Pod[] Pods { get; set; }

        public override string ToString()
        {
            return $"platform :{PlatformName}, '{PlatformVersion}'";
        }
    }
}
