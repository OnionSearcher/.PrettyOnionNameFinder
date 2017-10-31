using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace PrettyOnionNameFinderRole
{
    public class WorkerRole : RoleEntryPoint, IDisposable
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Trace.TraceError("WorkerRole.CurrentDomain_UnhandledException : " + ex.GetBaseException().Message, ex);
            }
            else
            {
                Trace.TraceError("WorkerRole.CurrentDomain_UnhandledException : NULL");
            }
#if DEBUG
            if (Debugger.IsAttached) { Debugger.Break(); }
#endif
        }

        public override bool OnStart()
        {
            bool result = base.OnStart(); // will init azure trace

            Trace.TraceWarning("WorkerRole is starting");
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;

            return result;
        }

        public override void Run()
        {
            Trace.TraceWarning("WorkerRole is running");

            try
            {
                RunAsync(cancellationTokenSource.Token).Wait(cancellationTokenSource.Token); // RunAsync will be override
            }
            catch (OperationCanceledException)
            {
                Trace.TraceInformation("WorkerRole.Run OperationCanceled");
            }
            catch (Exception ex)
            {
                Trace.TraceError("WorkerRole.Run Exception : " + ex.GetBaseException().ToString());
#if DEBUG
                if (Debugger.IsAttached) { Debugger.Break(); }
#endif
            }
        }
        
        protected async Task RunAsync(CancellationToken cancellationToken)
        {
            Trace.TraceInformation("WorkerRole.RunAsync : Start");
            try
            {
                await Task.Delay(5000, cancellationToken); // let time to Azure to know that we are OK before taking 100% CPU...
                using (ScallionManager scallion = new ScallionManager(cancellationToken))
                {
                    scallion.WaitForExit();
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Trace.TraceError("WorkerRole.RunAsync Exception : " + ex.GetBaseException().ToString());
#if DEBUG
                if (Debugger.IsAttached) { Debugger.Break(); }
#endif
            }

            Trace.TraceInformation("WorkerRole.RunAsync : End");
        }

        public override void OnStop()
        {
            Trace.TraceWarning("WorkerRole is stoping");

            cancellationTokenSource.Cancel();
            
            base.OnStop(); // raise the cancellationToken before the taskPool cleanup
        }

        #region IDisposable Support

        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                }
                disposedValue = true;
            }
        }
        public void Dispose()
        {
            Dispose(true);
        }
        #endregion

    }
}
