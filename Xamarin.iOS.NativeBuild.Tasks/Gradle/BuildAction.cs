namespace Xamarin.Android.NativeBuild.Tasks.Gradle
{
    public enum BuildAction
    {
        Unknown,

        // APP
        AndroidJavaLibrary,
        AndroidNativeLibrary,
        EmbeddedNativeLibrary,
        AndroidExternalJavaLibrary,

        // BINDING
        EmbeddedJar,
        InputJar,
        EmbeddedReferenceJar,
        ReferenceJar,
        LibraryProjectZip,
    }
}
