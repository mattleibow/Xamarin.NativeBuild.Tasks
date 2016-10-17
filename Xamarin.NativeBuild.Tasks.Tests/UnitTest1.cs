using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.Android.NativeBuild.Tasks.Gradle;
using Xamarin.NativeBuild.Tasks.Common;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace Xamarin.NativeBuild.Tasks.Tests
{
    [TestClass]
    public class GradleToolTests
    {
        private string toolPath;
        private TestLogger logger;
        private GradleTool tool;
        private string intermediateDirectory;
        private string buildGradlePath;

        [TestInitialize]
        public void InitTest()
        {
            logger = new TestLogger();

            toolPath = ToolBase.LocateToolPath(
                tool: Utilities.IsWindows ? "gradle.bat" : "gradle",
                logger: logger,
                versionLine: 1);

            tool = new GradleTool(toolPath, logger);

            intermediateDirectory = Path.Combine(Path.GetTempPath(), "Xamarin.NativeBuild.Tasks.Tests", "GradleTest", Path.GetRandomFileName());
            if (!Directory.Exists(intermediateDirectory))
            {
                Directory.CreateDirectory(intermediateDirectory);
            }

            buildGradlePath = Path.Combine(intermediateDirectory, "build.gradle");
        }

        [TestMethod]
        public void CreateBuildGradle_FileCreated_Test()
        {
            var dependencies = new List<GradleDependency>
            {
                new GradleDependency
                {
                    GroupId ="com.squareup",
                    ArtifactId ="android-times-square",
                    Version = "1.6.5",
                    Type = GradleDependencyTypes.Aar
                },
                new GradleDependency
                {
                    GroupId ="com.squareup.okhttp3",
                    ArtifactId ="okhttp",
                    Version = "3.3.1",
                    Repository = GradleRepositories.MavenCentral
                },
                new GradleDependency
                {
                    GroupId ="com.microsoft.onedrivesdk",
                    ArtifactId ="onedrive-picker-android",
                    Version = "v2.0",
                    Repository = GradleRepositories.JCenter
                }
            };

            var result = tool.CreateBuildGradle(buildGradlePath, dependencies);
            Assert.IsTrue(result);

            Assert.IsTrue(File.Exists(buildGradlePath));

            var contents = @"apply plugin: 'java'
repositories {
    mavenCentral()
    jcenter()
}
task restore << {
    configurations.compile.each {
        File file -> println file.path
    }
}
dependencies {
    compile 'com.squareup:android-times-square:1.6.5@aar'
    compile 'com.squareup.okhttp3:okhttp:3.3.1'
    compile 'com.microsoft.onedrivesdk:onedrive-picker-android:v2.0'
}
";

            Assert.AreEqual(contents, File.ReadAllText(buildGradlePath));
        }

        [TestMethod]
        public void CreateBuildGradle_DefaultRepository_Test()
        {
            var dependencies = new List<GradleDependency>
            {
                new GradleDependency
                {
                    GroupId ="com.squareup.okhttp3",
                    ArtifactId ="okhttp",
                    Version = "3.3.1",
                }
            };

            var result = tool.CreateBuildGradle(buildGradlePath, dependencies);
            Assert.IsTrue(result);

            Assert.IsTrue(File.Exists(buildGradlePath));

            var contents = @"apply plugin: 'java'
repositories {
    mavenCentral()
}
task restore << {
    configurations.compile.each {
        File file -> println file.path
    }
}
dependencies {
    compile 'com.squareup.okhttp3:okhttp:3.3.1'
}
";

            Assert.AreEqual(contents, File.ReadAllText(buildGradlePath));
        }

        [TestMethod]
        public void GetLibraries_FetchesDependencies_Test()
        {
            var dependencies = new List<GradleDependency>
            {
                new GradleDependency
                {
                    GroupId ="com.squareup.okhttp3",
                    ArtifactId ="okhttp",
                    Version = "3.3.1",
                }
            };

            tool.CreateBuildGradle(buildGradlePath, dependencies);

            var libraries = tool.GetLibraries(buildGradlePath);
            Assert.IsNotNull(libraries);

            foreach (var lib in libraries)
            {
                Assert.IsTrue(File.Exists(lib));
            }

            var filenames = libraries.Select(l => Path.GetFileName(l)).ToArray();
            var folders = libraries.Select(l => Path.GetFileName(Path.GetFullPath(Path.Combine(l, "..", "..", "..", "..")))).ToArray();

            Assert.AreEqual("com.squareup.okhttp3", folders[0]);
            Assert.AreEqual("com.squareup.okio", folders[1]);

            Assert.AreEqual("okhttp-3.3.1.jar", filenames[0]);
            Assert.IsTrue(filenames[1].StartsWith("okio-") && filenames[1].EndsWith(".jar"));
        }

        [TestMethod]
        public void CreateBuildGradle_InvalidVersion_Test()
        {
            logger.ExpectError = true;

            var dependencies = new List<GradleDependency>
            {
                new GradleDependency
                {
                    GroupId ="com.squareup",
                    ArtifactId ="android-times-square",
                    Version = "0.0.0.1",
                    Type = GradleDependencyTypes.Aar
                }
            };
            var result = tool.CreateBuildGradle(buildGradlePath, dependencies);

            Assert.IsFalse(result);
        }

        [TestMethod]
        public void CreateBuildGradleTest()
        {
            var dependencies = new List<GradleDependency>
            {
                new GradleDependency
                {
                    GroupId ="com.squareup",
                    ArtifactId ="android-times-square",
                    Version = "1.6.5",
                    Type = GradleDependencyTypes.Aar
                }
            };
            var result = tool.CreateBuildGradle(intermediateDirectory, dependencies);

            Assert.IsTrue(result);
        }
    }
}
