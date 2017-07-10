using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.ServiceRuntime;

namespace PrettyOnionNameFinderRole
{
    public class WorkerRole : RoleEntryPoint, IDisposable
    {
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

        public override bool OnStart()
        {
            bool result = base.OnStart(); // will init azure trace

            Trace.TraceWarning("WorkerRole is starting");

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

        private List<Task> taskPool;

        protected async Task RunAsync(CancellationToken cancellationToken)
        {
            Trace.TraceInformation("WorkerRole.RunAsync : Start");
            try
            {
                await Task.Delay(5000, cancellationToken); // let time to Azure to know that we are OK before taking 100% CPU...
                RotManager.TryKillTorIfRequired();

                // pools init
                taskPool = new List<Task>(Settings.Default.TaskNumber);
                for (int i = 0; i < taskPool.Capacity; i++)
                    taskPool.Add(null);

                // main loop
                while (!cancellationToken.IsCancellationRequested)
                {
                    for (int i = 0; !cancellationToken.IsCancellationRequested && i < taskPool.Count; i++)
                    {
                        Task task = taskPool[i];
                        if (task != null && (task.IsCanceled || task.IsCompleted || task.IsFaulted))
                        {
                            task.Dispose();
                            taskPool[i] = null;
                        }
                        if (taskPool[i] == null && !cancellationToken.IsCancellationRequested)
                        {
                            int iVarRequiredForLambda = i; // <!> else the i may be changed by next for iteration in this multi task app !!!
                            taskPool[i] = Task.Run(() =>
                            {
                                RunTask(iVarRequiredForLambda, cancellationToken).Wait(cancellationToken);
                            }, cancellationToken);
                            Task.Delay(5000, cancellationToken).Wait(cancellationToken); // avoid violent startup by x tor started in same instant
                        }
                    }

                    await Task.Delay(30000, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (AggregateException) { }
            catch (Exception ex)
            {
                Trace.TraceError("WorkerRole.RunAsync Exception : " + ex.GetBaseException().ToString());
#if DEBUG
                if (Debugger.IsAttached) { Debugger.Break(); }
#endif
            }

            Trace.TraceInformation("WorkerRole.RunAsync : End");
        }

        private static async Task RunTask(int i, CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    //using (RotManager rot = new RotManager(i)) // keep same proxy for a few time
                    //{
                    //    do
                    //    {
                    //        await Task.Delay(50, cancellationToken);
                    //    } while (!rot.IsOnionReady() && !cancellationToken.IsCancellationRequested);
                    //    await Task.Delay(50, cancellationToken); // let tor finish it s write and limit FileNotFoundException
                    //    if (!cancellationToken.IsCancellationRequested)
                    //        rot.TraceOnion();
                    //}
                    await Task.Delay(50*5, cancellationToken);
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Trace.TraceError("WorkerRole.RunTask Exception : " + ex.GetBaseException().ToString());
#if DEBUG
                if (Debugger.IsAttached) { Debugger.Break(); }
#endif
            }
        }

        public override void OnStop()
        {
            cancellationTokenSource.Cancel();

            Trace.TraceWarning("WorkerRole is stoping");

            if (taskPool != null)
            {
                for (int i = 0; i < taskPool.Count; i++)
                {
                    if (taskPool[i] != null)
                    {
                        if (!taskPool[i].IsCompleted || !taskPool[i].IsCanceled || !taskPool[i].IsFaulted)
                            try
                            {
                                taskPool[i].Wait(100);
                            }
                            catch (OperationCanceledException) { }
                            catch (AggregateException) { }
                        try
                        {
                            taskPool[i].Dispose();
                        }
                        catch (InvalidOperationException) { }
                        taskPool[i] = null;
                    }
                }
                taskPool = null; // may raise exception on the i < taskPool.Count who may continue in parrallele
            }

            base.OnStop(); // raise the cancellationToken before the taskPool cleanup

            RotManager.TryKillTorIfRequired();
        }
        
        #region IDisposable Support

        private bool disposedValue = false;
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
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
