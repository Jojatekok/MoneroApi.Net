﻿using Newtonsoft.Json;
using System.Collections.Generic;

namespace Jojatekok.MoneroAPI.RpcUtilities.AccountManager.Json.Responses
{
    class TransactionIdListValueContainer : IValueContainer<List<string>>
    {
        [JsonProperty("tx_hash_list")]
        public List<string> Value { get; private set; }
    }
}
