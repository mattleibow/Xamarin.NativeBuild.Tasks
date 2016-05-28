namespace Xamarin.iOS.NativeBuild.Tasks.CocoaPods
{
    public class Pod
    {
        public string Id { get; set; }

        public string Version { get; set; }

        public override string ToString()
        {
            return $"pod '{Id}', '{Version}'";
        }
    }
}
