using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Unity.Build.Classic.Private
{
    internal struct ShellProcessArguments
    {
        public string Executable { get; set; }
        public string[] Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public Dictionary<string, string> EnvironmentVariables { get; set; }
        public DataReceivedEventHandler OutputDataReceived { get; set; }
        public DataReceivedEventHandler ErrorDataReceived { get; set; }
    }

    internal enum ProcessStatus
    {
        Running,
        Killed,
        Done
    }

    internal struct ShellProcessProgressInfo
    {
        public Process Process { get; set; }
        public float Progress { get; set; }
        public string Info { get; set; }
        public StringBuilder Output { get; set; }
        public int ExitCode { get; set; }
    }

    internal class ShellProcess
    {
        public Process Process { get; private set; }
        public DateTime TimeOfLastObservedOutput { get; private set; }

        ShellProcess(ProcessStartInfo startInfo)
        {
            Process = new Process { StartInfo = startInfo };
            TimeOfLastObservedOutput = DateTime.Now;
        }

        public static ShellProcess Start(ShellProcessArguments args)
        {
            var startInfo = new ProcessStartInfo()
            {
                FileName = args.Executable,
                Arguments = string.Join(" ", args.Arguments),
                WorkingDirectory = args.WorkingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
                RedirectStandardError = true,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
                UseShellExecute = false
            };

            if (args.EnvironmentVariables != null)
            {
                foreach (var pair in args.EnvironmentVariables)
                {
                    startInfo.EnvironmentVariables[pair.Key] = pair.Value;
                }
            }

            var shellProcess = new ShellProcess(startInfo);
            shellProcess.Process.OutputDataReceived += (sender, data) =>
            {
                shellProcess.TimeOfLastObservedOutput = DateTime.Now;
                args.OutputDataReceived?.Invoke(sender, data);
            };
            shellProcess.Process.ErrorDataReceived += (sender, data) =>
            {
                shellProcess.TimeOfLastObservedOutput = DateTime.Now;
                args.ErrorDataReceived?.Invoke(sender, data);
            };

            shellProcess.Process.Start();
            shellProcess.Process.BeginOutputReadLine();
            shellProcess.Process.BeginErrorReadLine();
            return shellProcess;
        }

        public IEnumerator<ProcessStatus> WaitForProcess(int maxIdleTimeInMs, int yieldFrequencyInMs = 100)
        {
            while (true)
            {
                if (Process.WaitForExit(yieldFrequencyInMs))
                {
                    // WaitForExit with a timeout will not wait for async event handling operations to finish.
                    // To ensure that async event handling has been completed, call WaitForExit that takes no parameters.
                    // See remarks: https://msdn.microsoft.com/en-us/library/ty0d8k56(v=vs.110)

                    Process.WaitForExit();
                    yield return ProcessStatus.Done;
                    break;
                }

                var IdleTimeInMs = (DateTime.Now - TimeOfLastObservedOutput).TotalMilliseconds;
                if (IdleTimeInMs < maxIdleTimeInMs || Debugger.IsAttached)
                {
                    yield return ProcessStatus.Running;
                    continue;
                }

                UnityEngine.Debug.LogError("Idle process detected. See console for more details.");
                Process.Kill();
                Process.WaitForExit();
                yield return ProcessStatus.Killed;
                break;
            }
        }
    }
}
