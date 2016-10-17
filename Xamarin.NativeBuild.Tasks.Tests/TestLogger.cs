using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Xamarin.NativeBuild.Tasks.Common;

namespace Xamarin.NativeBuild.Tasks.Tests
{
    public class TestLogger : IToolLogger
    {
        public bool ExpectError = false;

        public void LogError(string error)
        {
            if (!ExpectError)
            {
                Assert.Fail("error: " + error);
            }
            else
            {
                Console.WriteLine("error: " + error);
            }
        }

        public void LogMessage(string message)
        {
            Console.WriteLine("message: " + message);
        }

        public void LogVerbose(string verboseMessage)
        {
            Console.WriteLine("verbose: " + verboseMessage);
        }

        public void LogWarning(string warning)
        {
            if (!ExpectError)
            {
                Assert.Fail("warning: " + warning);
            }
            else
            {
                Console.WriteLine("warning: " + warning);
            }
        }
    }
}
