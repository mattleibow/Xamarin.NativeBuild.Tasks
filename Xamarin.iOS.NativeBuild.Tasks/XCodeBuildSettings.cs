using System.Collections.Generic;
using System.Linq;

namespace Xamarin.iOS.NativeBuild.Tasks
{
    public static class XCodeBuildSettings
    {
        public static iOSArcitecture iOS = new iOSArcitecture();
        public static MacOSXArcitecture MacOSX = new MacOSXArcitecture();
        public static tvOSArcitecture tvOS = new tvOSArcitecture();

        public abstract class XCodeArcitecture
        {
            public string SimulatorSdk;

            public string DeviceSdk;

            public string[] SimulatorArchitectures;

            public string[] DeviceArchitectures;

            public string[] Architectures => SimulatorArchitectures.Union(DeviceArchitectures).ToArray();

            public SingleArcitecture SimulatorSingle => new SingleArcitecture(SimulatorSdk, SimulatorArchitectures.First());

            public SingleArcitecture DeviceSingle => new SingleArcitecture(DeviceSdk, DeviceArchitectures.First());

            public Dictionary<string, string> Builds
            {
                get
                {
                    var sims = SimulatorArchitectures.Select(a => new KeyValuePair<string, string>(a, SimulatorSdk));
                    var devs = DeviceArchitectures.Select(a => new KeyValuePair<string, string>(a, DeviceSdk));
                    return sims.Union(devs).ToDictionary(i => i.Key, i => i.Value);
                }
            }
        }

        public class SingleArcitecture : XCodeArcitecture
        {
            public SingleArcitecture(string sdk, string architecture)
            {
                SimulatorSdk = sdk;
                DeviceSdk = sdk;

                SimulatorArchitectures = new[] { architecture };
                DeviceArchitectures = new[] { architecture };
            }
        }

        public class iOSArcitecture : XCodeArcitecture
        {
            public iOSArcitecture()
            {
                SimulatorSdk = "iphonesimulator";
                DeviceSdk = "iphoneos";

                SimulatorArchitectures = new[] { "x86_64", "i386" };
                DeviceArchitectures = new[] { "arm64", "armv7", "armv7s" };
            }
        }

        public class MacOSXArcitecture : XCodeArcitecture
        {
        }

        public class tvOSArcitecture : XCodeArcitecture
        {
        }
    }
}
