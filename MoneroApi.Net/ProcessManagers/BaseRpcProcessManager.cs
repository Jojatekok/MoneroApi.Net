﻿using Jojatekok.MoneroAPI.RpcManagers;

namespace Jojatekok.MoneroAPI.ProcessManagers
{
    public abstract class BaseRpcProcessManager
    {
        private RpcWebClient RpcWebClient { get; set; }

        private string RpcHost { get; set; }
        private ushort RpcPort { get; set; }

        internal BaseRpcProcessManager(RpcWebClient rpcWebClient, bool isDaemon) {
            RpcWebClient = rpcWebClient;

            var rpcSettings = rpcWebClient.RpcSettings;

            if (isDaemon) {
                RpcHost = rpcSettings.UrlHostDaemon;
                RpcPort = rpcSettings.UrlPortDaemon;
            } else {
                RpcHost = rpcSettings.UrlHostAccountManager;
                RpcPort = rpcSettings.UrlPortAccountManager;
            }
        }

        protected T HttpPostData<T>(string command) where T : HttpRpcResponse
        {
            var output = RpcWebClient.HttpPostData<T>(RpcHost, RpcPort, command);
            if (output != null && output.Status == RpcResponseStatus.Ok) {
                return output;
            }

            return null;
        }

        protected JsonRpcResponse<T> JsonPostData<T>(JsonRpcRequest jsonRpcRequest) where T : class
        {
            return RpcWebClient.JsonPostData<T>(RpcHost, RpcPort, jsonRpcRequest);
        }

        protected void JsonPostData(JsonRpcRequest jsonRpcRequest)
        {
            JsonPostData<object>(jsonRpcRequest);
        }
    }
}
