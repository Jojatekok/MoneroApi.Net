﻿using Jojatekok.MoneroAPI.RpcManagers;
using Jojatekok.MoneroAPI.RpcManagers.Daemon.Http.Responses;
using Jojatekok.MoneroAPI.RpcManagers.Daemon.Json.Requests;
using Jojatekok.MoneroAPI.RpcManagers.Daemon.Json.Responses;
using Jojatekok.MoneroAPI.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace Jojatekok.MoneroAPI.ProcessManagers
{
    public class DaemonManager : BaseRpcProcessManager
    {
        public event EventHandler BlockchainSynced;
        public event EventHandler<NetworkInformationChangingEventArgs> NetworkInformationChanging;

        private static readonly string[] ProcessArgumentsDefault = { "--log-level 0" };
        private List<string> ProcessArgumentsExtra { get; set; }

        private Timer TimerQueryNetworkInformation { get; set; }

        private RpcWebClient RpcWebClient { get; set; }
        private TimerSettings TimerSettings { get; set; }

        private bool _isBlockchainSynced;
        public bool IsBlockchainSynced {
            get { return _isBlockchainSynced; }

            private set {
                _isBlockchainSynced = value;
                if (BlockchainSynced != null && value) BlockchainSynced(this, EventArgs.Empty);
            }
        }

        private NetworkInformation _networkInformation;
        public NetworkInformation NetworkInformation {
            get { return _networkInformation; }

            private set {
                if (value == NetworkInformation) return;

                if (NetworkInformationChanging != null) {
                    NetworkInformationChanging(this, new NetworkInformationChangingEventArgs(value));
                }
                _networkInformation = value;
            }
        }

        internal DaemonManager(RpcWebClient rpcWebClient, PathSettings pathSettings, TimerSettings timerSettings) : base(pathSettings.SoftwareDaemon, rpcWebClient, timerSettings, true)
        {
            RpcAvailabilityChanged += Process_RpcAvailabilityChanged;

            TimerQueryNetworkInformation = new Timer(delegate { QueryNetworkInformation(); });

            RpcWebClient = rpcWebClient;
            TimerSettings = timerSettings;

            var rpcSettings = RpcWebClient.RpcSettings;
            if (rpcSettings.IsDaemonRemote) {
                IsRpcAvailable = true;
                return;
            }

            ProcessArgumentsExtra = new List<string>(3) {
                //"--data-dir \"" + paths.DirectoryDaemonData + "\"",
                "--rpc-bind-port " + rpcSettings.UrlPortDaemon
            };

            if (rpcSettings.UrlHostDaemon != Utilities.DefaultRpcUrlHostDaemon) {
                ProcessArgumentsExtra.Add("--rpc-bind-ip " + rpcSettings.UrlHostDaemon);
            }

            // TODO: Remove this temporary fix
            ProcessArgumentsExtra.Add("--data-dir \"" + pathSettings.DirectoryDaemonData);
        }

        public void Start()
        {
            if (!RpcWebClient.RpcSettings.IsDaemonRemote) {
                StartProcess(ProcessArgumentsDefault.Concat(ProcessArgumentsExtra).ToArray());
            }
        }

        public void Stop()
        {
            if (!RpcWebClient.RpcSettings.IsDaemonRemote) {
                KillBaseProcess();
            }
        }

        public void Restart()
        {
            Stop();
            Start();
        }

        private void QueryNetworkInformation()
        {
            TimerQueryNetworkInformation.Stop();

            var output = HttpPostData<NetworkInformation>(HttpRpcCommands.DaemonGetInformation);
            if (output != null && output.BlockHeightTotal != 0) {
                var blockHeaderLast = QueryBlockHeaderLast();
                if (blockHeaderLast != null) {
                    output.BlockTimeCurrent = blockHeaderLast.Timestamp;

                    NetworkInformation = output;

                    if (output.BlockHeightRemaining == 0 && !IsBlockchainSynced) {
                        IsBlockchainSynced = true;
                    }
                }
            }

            TimerQueryNetworkInformation.StartOnce(TimerSettings.DaemonQueryNetworkInformationPeriod);
        }

        public BlockHeader QueryBlockHeaderLast()
        {
            var blockHeaderValueContainer = JsonPostData<BlockHeaderValueContainer>(new QueryBlockHeaderLast()).Result;
            if (blockHeaderValueContainer != null) {
                return blockHeaderValueContainer.Value;
            }

            return null;
        }

        private void Process_RpcAvailabilityChanged(object sender, EventArgs e)
        {
            if (IsRpcAvailable) {
                TimerQueryNetworkInformation.StartImmediately(TimerSettings.DaemonQueryNetworkInformationPeriod);

            } else {
                TimerQueryNetworkInformation.Stop();
            }
        }

        public new void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing) {
                TimerQueryNetworkInformation.Dispose();
                TimerQueryNetworkInformation = null;

                if (!RpcWebClient.RpcSettings.IsDaemonRemote) {
                    // Safe shutdown
                    SendConsoleCommand("exit");
                }

                base.Dispose(false);
            }
        }
    }
}
