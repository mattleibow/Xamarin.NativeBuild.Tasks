namespace Xamarin.Android.NativeBuild.Tasks.Gradle
{
    public abstract class GradleRepository
    {
        public static MavenCentralRepository MavenCentral = new MavenCentralRepository();
        public static JCenterRepository JCenter = new JCenterRepository();

        public static GradleRepository GetRepository(GradleRepositories repository)
        {
            switch (repository)
            {
                case GradleRepositories.MavenCentral:
                    return MavenCentral;
                case GradleRepositories.JCenter:
                    return JCenter;
            }
            return null;
        }

        public abstract string PluginName { get; }

        public class MavenCentralRepository : GradleRepository
        {
            public override string PluginName { get { return "mavenCentral()"; } }
        }

        public class JCenterRepository : GradleRepository
        {
            public override string PluginName { get { return "jcenter()"; } }
        }
    }
}
