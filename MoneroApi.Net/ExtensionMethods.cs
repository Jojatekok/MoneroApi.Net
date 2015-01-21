﻿using Newtonsoft.Json;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Threading;

namespace Jojatekok.MoneroAPI
{
    static class ExtensionMethods
    {
        public static string GetResponseString(this HttpWebRequest request)
        {
            // Ensure a little of backwards compatibility in order to avoid RPC failures
            try {
                using (var response = request.GetResponse()) {
                    using (var stream = response.GetResponseStream()) {
                        Debug.Assert(stream != null, "stream != null");

                        using (var reader = new StreamReader(stream)) {
                            return reader.ReadToEnd();
                        }
                    }
                }

            } catch {
                return null;
            }
        }

        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public static T DeserializeObject<T>(this JsonSerializer serializer, string value)
        {
            if (value == null) return default(T);

            using (var stringReader = new StringReader(value)) {
                using (var jsonTextReader = new JsonTextReader(stringReader)) {
                    return (T)serializer.Deserialize(jsonTextReader, typeof(T));
                }
            }
        }

        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public static string SerializeObject<T>(this JsonSerializer serializer, T value)
        {
            using (var stringWriter = new StringWriter(Utilities.InvariantCulture)) {
                using (var jsonTextWriter = new JsonTextWriter(stringWriter)) {
                    serializer.Serialize(jsonTextWriter, value);
                }

                return stringWriter.ToString();
            }
        }

        public static void StartImmediately(this Timer timer, int period)
        {
            if (timer == null) return;
            timer.Change(0, period);
        }

        public static void StartOnce(this Timer timer, int dueTime)
        {
            if (timer == null) return;
            timer.Change(dueTime, Timeout.Infinite);
        }

        public static void Stop(this Timer timer)
        {
            if (timer == null) return;
            timer.Change(Timeout.Infinite, Timeout.Infinite);
        }
    }
}
