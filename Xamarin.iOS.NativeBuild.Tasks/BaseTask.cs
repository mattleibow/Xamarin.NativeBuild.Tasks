using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xamarin.Messaging.Diagnostics;
using Xamarin.VisualStudio.Build;

namespace Xamarin.NativeBuild.Tasks
{
    public abstract class BaseTask : Task, ICancelableTask
    {
        private readonly CancellationTokenSource tokenSource;

        public BaseTask()
        {
            // task stuff
            tokenSource = new CancellationTokenSource();
        }

        [Required]
        public string IntermediateOutputPath { get; set; }

        public bool IsCancellationRequested => tokenSource.IsCancellationRequested;

        public override bool Execute()
        {
            Tracer.SetManager(BuildTracerManager.Instance);

            if (IsCancellationRequested)
            {
                Log.LogError("Task was canceled.");
                return false;
            }

            return true;
        }

        public virtual void Cancel()
        {
            tokenSource.Cancel();
        }

        protected CancellationToken GetCancellationToken()
        {
            return tokenSource.Token;
        }
    }
}
