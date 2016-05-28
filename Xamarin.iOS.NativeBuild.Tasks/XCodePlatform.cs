using System;

namespace Xamarin.iOS.NativeBuild.Tasks
{
    public enum XCodePlatforms
    {
        None = 0,

        iOS,
        tvOS,
        MacOSX,
        watchOS
    }

    public enum XCodeSDKs
    {
        None = 0,

        iPhoneSimulator,
        iPhoneOS,

        MacOSX,

        AppleTVSimulator,
        AppleTVOS,

        WatchSimulator,
        WatchOS,
    }

    [Flags]
    public enum XCodeArchitectures
    {
        None = 0,

        i386 = 1 << 0,
        x86_64 = 1 << 1,

        ARM64 = 1 << 2,
        ARMv7 = 1 << 3,
        ARMv7s = 1 << 4,

        Allx86 = (i386 | x86_64),
        AllARM = (ARM64 | ARMv7 | ARMv7s),

        All = (i386 | x86_64 | ARM64 | ARMv7 | ARMv7s)
    }
}
