using System;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.iOS.NativeBuild.Tasks.XCodeBuild
{
    public class XCodeBuildParameters
    {
        public static iOSArcitecture iOS = new iOSArcitecture();
        public static MacOSXArcitecture MacOSX = new MacOSXArcitecture();
        public static tvOSArcitecture tvOS = new tvOSArcitecture();

        public string ProjectFilePath { get; set; }

        public string ArtifactsDirectory { get; set; }

        public string OutputDirectory { get; set; }

        public XCodeArcitecture ArchitectureSettings { get; set; }

        public string[] BuildTargets { get; set; }

        public string[] OutputTargets { get; set; }

        public XCodeArchitectures ArchitectureOverride { get; set; }

        public bool IsFrameworks { get; set; }

        public XCodeArchitectures Architectures
        {
            get { return ArchitectureOverride != XCodeArchitectures.None ? ArchitectureOverride : ArchitectureSettings.Architectures; }
        }

        public XCodeArchitectures[] SplitArchitectures
        {
            get { return SplitArchitecture(Architectures).ToArray(); }
        }

        public static XCodeArcitecture GetArchitecture(XCodePlatforms platforms)
        {
            switch (platforms)
            {
                case XCodePlatforms.iOS:
                    return iOS;
                case XCodePlatforms.tvOS:
                    return tvOS;
                case XCodePlatforms.MacOSX:
                    return MacOSX;
            }

            throw new NotSupportedException($"The platform'{platforms}' is not yet supported.");
        }

        public static IEnumerable<XCodeArchitectures> SplitArchitecture(XCodeArchitectures architectures)
        {
            if (architectures.HasFlag(XCodeArchitectures.i386))
                yield return XCodeArchitectures.i386;
            if (architectures.HasFlag(XCodeArchitectures.x86_64))
                yield return XCodeArchitectures.x86_64;
            if (architectures.HasFlag(XCodeArchitectures.ARM64))
                yield return XCodeArchitectures.ARM64;
            if (architectures.HasFlag(XCodeArchitectures.ARMv7))
                yield return XCodeArchitectures.ARMv7;
            if (architectures.HasFlag(XCodeArchitectures.ARMv7s))
                yield return XCodeArchitectures.ARMv7s;
        }

        public abstract class XCodeArcitecture
        {
            public XCodeSDKs SimulatorSdk { get; set; }

            public XCodeSDKs DeviceSdk { get; set; }

            public XCodeArchitectures SimulatorArchitectures { get; set; }

            public XCodeArchitectures DeviceArchitectures { get; set; }

            public XCodePlatforms Platform { get; set; }

            public XCodeArchitectures Architectures => SimulatorArchitectures | DeviceArchitectures;

            public virtual string GetArtifactDirectoryName(string configuration, XCodeSDKs sdk)
            {
                return $"{configuration}-{sdk}";
            }

            public string GetArtifactDirectoryName(string configuration, XCodeArchitectures arch)
            {
                return GetArtifactDirectoryName(configuration, GetSdk(arch));
            }

            public virtual XCodeSDKs GetSdk(XCodeArchitectures arch)
            {
                return XCodeArchitectures.Allx86.HasFlag(arch) ? SimulatorSdk : DeviceSdk;
            }

            public virtual XCodeArchitectures GetArchitecture(XCodeSDKs sdk)
            {
                return sdk == SimulatorSdk ? SimulatorArchitectures : DeviceArchitectures;
            }
        }

        public class SingleArchitecture : XCodeArcitecture
        {
            public SingleArchitecture(XCodePlatforms platform, XCodeSDKs sdk, XCodeArchitectures architecture)
            {
                Platform = platform;
                SimulatorSdk = sdk;
                DeviceSdk = sdk;

                SimulatorArchitectures = architecture;
                DeviceArchitectures = architecture;
            }
        }

        public class iOSArcitecture : XCodeArcitecture
        {
            public iOSArcitecture()
            {
                Platform = XCodePlatforms.iOS;
                SimulatorSdk = XCodeSDKs.iPhoneSimulator;
                DeviceSdk = XCodeSDKs.iPhoneOS;

                SimulatorArchitectures = XCodeArchitectures.Allx86;
                DeviceArchitectures = XCodeArchitectures.AllARM;
            }
        }

        public class MacOSXArcitecture : XCodeArcitecture
        {
            public MacOSXArcitecture()
            {
                Platform = XCodePlatforms.MacOSX;
                SimulatorSdk = XCodeSDKs.MacOSX;
                DeviceSdk = XCodeSDKs.MacOSX;

                // i386 is no longer supported on OS X
                SimulatorArchitectures = XCodeArchitectures.x86_64;
                DeviceArchitectures = XCodeArchitectures.x86_64;
            }

            public override string GetArtifactDirectoryName(string configuration, XCodeSDKs sdk)
            {
                // Mac OS X does not have a simulator (only one SDK - the device)
                return configuration;
            }
        }

        public class tvOSArcitecture : XCodeArcitecture
        {
            public tvOSArcitecture()
            {
                Platform = XCodePlatforms.tvOS;
                SimulatorSdk = XCodeSDKs.AppleTVSimulator;
                DeviceSdk = XCodeSDKs.AppleTVOS;

                SimulatorArchitectures = XCodeArchitectures.x86_64;
                DeviceArchitectures = XCodeArchitectures.ARM64;
            }
        }
    }
}
