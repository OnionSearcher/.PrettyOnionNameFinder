using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PrettyOnionNameFinderRole
{
    internal class RotManager : IDisposable
    {

        private static readonly string basePath = AppDomain.CurrentDomain.BaseDirectory;
        private static readonly StringCollection searched = Settings.Default.Searched;
        public static void TryKillTorIfRequired()
        {
            try
            {
                // sometime Tor is not well killed (at last in dev mode)
                foreach (var oldProcess in Process.GetProcessesByName("rot"))
                    oldProcess.Kill(); //permission issue on Azure may occur
            }
            catch (Exception ex)
            {
                Trace.TraceWarning("RotManager.killTorIfRequired Exception : " + ex.GetBaseException().Message);  // No right usualy, simple message to keep.
            }
        }

        private void OutputHandler(object sender, DataReceivedEventArgs e)
        {

            if (!String.IsNullOrWhiteSpace(e.Data))
            {

                if (e.Data.Contains("[err]"))
                {
                    Trace.TraceError("RotManager : " + e.Data);
#if DEBUG
                    if (Debugger.IsAttached) { Debugger.Break(); }
#endif
                }
#if DEBUG
                else
                {
                    Trace.TraceInformation("RotManager : " + e.Data);
                }
#endif
            }
        }

        private void ErrorOutputHandler(object sender, DataReceivedEventArgs e)
        {
            if (!String.IsNullOrWhiteSpace(e.Data))
            {
                Trace.TraceError("RotManager : " + e.Data);
                // TOFIX Trace don't work on WebRole, use it if require : StorageManager.Contact("RotManager : " + e.Data);
#if DEBUG
                if (Debugger.IsAttached) { Debugger.Break(); }
#endif
            }
        }

        private Process process;
        private string pathHostname;
        private string pathPrivate_key;

        public RotManager(int i)
        {
            process = new Process()
            {
                StartInfo = new ProcessStartInfo
                {
                    WorkingDirectory = basePath, // changing that doesn't seems to work well with azure emulator
                    FileName = @"ExpertBundle\Rot\rot.exe",
                    Arguments = "-f \"" + Path.Combine(basePath, @"ExpertBundle\Data\rotrc" + i.ToString()) + "\" --defaults-torrc \"" + Path.Combine(basePath, @"ExpertBundle\Data\rotrc-defaults") + "\"", // full path not mandatory but avoid a warning...
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
            process.PriorityClass = ProcessPriorityClass.AboveNormal;
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            pathHostname = Path.Combine(basePath, @"ExpertBundle\Data\" + i.ToString() + @"\hostname");
            pathPrivate_key = Path.Combine(basePath, @"ExpertBundle\Data\" + i.ToString() + @"\private_key");
        }

        public bool IsOnionReady()
        {
            return File.Exists(pathHostname) && File.Exists(pathPrivate_key);
        }

        public bool TraceOnion()
        {
            try
            {
                string hostname = File.ReadAllText(pathHostname).TrimEnd();
#if DEBUG
                Trace.TraceInformation(hostname);
#endif
                foreach (string str in searched)
                {
                    if (hostname.StartsWith(str))
                    {
                        Trace.TraceError(hostname + ":" + File.ReadAllText(pathPrivate_key).TrimEnd()); // log as max level
                        return true;
                    }
                }
            }
            catch (FileNotFoundException)
            {
                Trace.TraceWarning("FileNotFoundException");
            }
            return false;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

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
                                    Task.Delay(10).Wait();
                                }
                            }
                            catch (Exception ex)
                            {
                                Trace.TraceWarning("RotManager.Close Exception : " + ex.GetBaseException().ToString());
                            }
                        }
                        process.Dispose();
                        process = null;
                    }
                    if (File.Exists(pathHostname)) File.Delete(pathHostname);
                    if (File.Exists(pathPrivate_key)) File.Delete(pathPrivate_key);
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
