using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace PrettyOnionNameFinderRole
{
    internal class ScallionManager : IDisposable
    {
        private static DateTime lastLoopIteration = DateTime.Now.AddMinutes(5.0); // 1st at 5 min
        private static void OutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(e.Data))
            {
                if (e.Data.StartsWith("LoopIteration:", StringComparison.Ordinal))
                {
                    if (lastLoopIteration < DateTime.Now)
                    {
                        Trace.TraceInformation(e.Data);
                        lastLoopIteration = DateTime.Now.AddHours(1.0);
                    }
                }
                else
                    Trace.TraceInformation(e.Data);
            }
        }

        private static void ErrorOutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(e.Data))
            {
                Trace.TraceError(e.Data);
#if DEBUG
                if (Debugger.IsAttached) { Debugger.Break(); }
#endif
            }
        }

        private Process process;
        private CancellationToken token;

        public ScallionManager(CancellationToken cancellationToken)
        {
            token = cancellationToken;
            process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                    FileName = @"scallion\scallion.exe",
                    Arguments = Settings.Default.Searched,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            process.OutputDataReceived += new DataReceivedEventHandler(OutputHandler);
            process.ErrorDataReceived += new DataReceivedEventHandler(ErrorOutputHandler);
            process.Start();
            // after start
            process.PriorityClass = ProcessPriorityClass.Normal;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public void WaitForExit()
        {
            process.WaitForExit();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (process != null)
                    {
                        if (!process.HasExited)
                        {
                            try
                            {
                                process.Kill();
                                while (!process.HasExited)
                                {
                                    Task.Delay(10).Wait(token);
                                }
                            }
                            catch (Exception ex)
                            {
                                Trace.TraceWarning("ScallionManager.Close Exception : " + ex.GetBaseException().ToString());
                            }
                        }
                        process.Dispose();
                        process = null;
                    }
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
