﻿using Jojatekok.MoneroAPI.RpcManagers;
using System;
using System.Diagnostics;
using System.Threading;

namespace Jojatekok.MoneroAPI.ProcessManagers
{
    public abstract class BaseRpcProcessManager : IDisposable
    {
        public event EventHandler RpcAvailabilityChanged;
        public event EventHandler<string> OnLogMessage;

        protected event EventHandler<ProcessExitedEventArgs> Exited;

        private string Path { get; set; }
        private Process Process { get; set; }
        private RpcWebClient RpcWebClient { get; set; }
        private string RpcHost { get; set; }
        private ushort RpcPort { get; set; }

        private Timer TimerCheckRpcAvailability { get; set; }

        private bool _isRpcAvailable;
        public bool IsRpcAvailable {
            get { return _isRpcAvailable; }

            protected set {
                if (value == _isRpcAvailable) return;

                _isRpcAvailable = value;
                if (value) TimerCheckRpcAvailability.Stop();
                if (RpcAvailabilityChanged != null) RpcAvailabilityChanged(this, EventArgs.Empty);
            }
        }

        private bool IsDisposing { get; set; }
        private bool IsProcessAlive {
            get { return Process != null && !Process.HasExited; }
        }

        protected BaseRpcProcessManager(string path, RpcWebClient rpcWebClient, bool isDaemon) {
            Path = path;
            RpcWebClient = rpcWebClient;

            TimerCheckRpcAvailability = new Timer(delegate { CheckRpcAvailability(); });

            var rpcSettings = rpcWebClient.RpcSettings;

            if (isDaemon) {
                RpcHost = rpcSettings.UrlHostDaemon;
                RpcPort = rpcSettings.UrlPortDaemon;

            } else {
                RpcHost = rpcSettings.UrlHostAccountManager;
                RpcPort = rpcSettings.UrlPortAccountManager;
            }
        }

        protected void StartProcess(params string[] arguments)
        {
            if (Process != null) Process.Dispose();

            Process = new Process {
                EnableRaisingEvents = true,
                StartInfo = new ProcessStartInfo(Path) {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true
                }
            };

            if (arguments != null) {
                Process.StartInfo.Arguments = string.Join(" ", arguments);
            }

            Process.OutputDataReceived += Process_OutputDataReceived;
            Process.Exited += Process_Exited;

            Process.Start();
            Utilities.JobManager.AddProcess(Process);
            Process.BeginOutputReadLine();

            // Constantly check for the RPC port's activeness
            TimerCheckRpcAvailability.Change(TimerSettings.RpcCheckAvailabilityDueTime, TimerSettings.RpcCheckAvailabilityPeriod);
        }

        private void CheckRpcAvailability()
        {
            IsRpcAvailable = Utilities.IsPortInUse(RpcPort);
        }

        public void SendConsoleCommand(string input)
        {
            if (IsProcessAlive) {
                if (OnLogMessage != null) OnLogMessage(this, "> " + input);
                Process.StandardInput.WriteLine(input);
            }
        }

        protected void KillBaseProcess()
        {
            if (IsProcessAlive) {
                Process.Kill();
            }
        }

        protected T HttpPostData<T>(string command) where T : HttpRpcResponse
        {
            if (!IsRpcAvailable) return null;

            var output = RpcWebClient.HttpPostData<T>(RpcHost, RpcPort, command);
            if (output != null && output.Status == RpcResponseStatus.Ok) {
                return output;
            }

            return null;
        }

        protected JsonRpcResponse<T> JsonPostData<T>(JsonRpcRequest jsonRpcRequest) where T : class
        {
            if (!IsRpcAvailable) return new JsonRpcResponse<T>(new JsonError());
            return RpcWebClient.JsonPostData<T>(RpcHost, RpcPort, jsonRpcRequest);
        }

        protected void JsonPostData(JsonRpcRequest jsonRpcRequest)
        {
            JsonPostData<object>(jsonRpcRequest);
        }

        private void Process_OutputDataReceived(object sender, DataReceivedEventArgs e)
        {
            var line = e.Data;
            if (line == null) return;

            if (OnLogMessage != null) OnLogMessage(this, line);
        }

        private void Process_Exited(object sender, EventArgs e)
        {
            if (IsDisposing) return;

            IsRpcAvailable = false;
            Process.CancelOutputRead();
            if (Exited != null) Exited(this, new ProcessExitedEventArgs(Process.ExitCode));
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected void Dispose(bool isProcessKillNecessary)
        {
            Dispose(true, isProcessKillNecessary);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing, bool isProcessKillNecessary)
        {
            if (disposing && !IsDisposing) {
                IsDisposing = true;

                TimerCheckRpcAvailability.Dispose();
                TimerCheckRpcAvailability = null;

                if (Process == null) return;

                if (isProcessKillNecessary) {
                    if (!Process.HasExited) {
                        if (Process.Responding) {
                            if (!Process.WaitForExit(10000)) Process.Kill();
                        } else {
                            Process.Kill();
                        }
                    }

                    Process.Dispose();
                    Process = null;

                } else {
                    Process.WaitForExit();
                }
            }
        }
    }
}
