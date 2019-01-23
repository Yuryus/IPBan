﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace IPBan
{
    /// <summary>
    /// Simple interface that can make http requests
    /// </summary>
    public interface IHttpRequestMaker
    {
        /// <summary>
        /// Make a GET or POST http request
        /// </summary>
        /// <param name="Uri">Uri</param>
        /// <param name="postJson">Optional json to post for a POST request, else GET is used</param>
        /// <param name="headers">Optional http headers</param>
        /// <returns>Task of response byte[]</returns>
        Task<byte[]> MakeRequestAsync(Uri uri, string postJson = null, IEnumerable<KeyValuePair<string, object>> headers = null);

        /// <summary>
        /// Web proxy (optional)
        /// </summary>
        IWebProxy Proxy { get; set; }
    }

    /// <summary>
    /// Default implementation of IHttpRequestMaker
    /// </summary>
    public class DefaultHttpRequestMaker : IHttpRequestMaker
    {
        /// <summary>
        /// Singleton of DefaultHttpRequestMaker
        /// </summary>
        public static DefaultHttpRequestMaker Instance { get; } = new DefaultHttpRequestMaker();

        /// <summary>
        /// Whether live requests should be disabled (unit tests)
        /// </summary>
        public static bool DisableLiveRequests { get; set; }

        private static long liveRequestCount;
        /// <summary>
        /// Global counter of live requests made
        /// </summary>
        public static long LiveRequestCount { get { return liveRequestCount; } }

        private static long localRequestCount;
        /// <summary>
        /// Global counter of local requests made
        /// </summary>
        public static long LocalRequestCount { get { return localRequestCount; } }

        public Task<byte[]> MakeRequestAsync(Uri uri, string postJson = null, IEnumerable<KeyValuePair<string, object>> headers = null)
        {
            if (uri.Host.Contains("localhost", StringComparison.OrdinalIgnoreCase) || uri.Host.Contains("127.0.0.1") || uri.Host.Contains("::1"))
            {
                Interlocked.Increment(ref localRequestCount);
            }
            else if (DisableLiveRequests)
            {
                throw new InvalidOperationException("Live requests have been disabled, cannot process url " + uri.ToString());
            }
            else
            {
                Interlocked.Increment(ref liveRequestCount);
            }
            using (WebClient client = new WebClient())
            {
                Assembly versionAssembly = Assembly.GetEntryAssembly();
                if (versionAssembly == null)
                {
                    versionAssembly = Assembly.GetAssembly(Type.GetType("IPBanService"));
                    if (versionAssembly == null)
                    {
                        versionAssembly = GetType().Assembly;
                    }
                }
                client.UseDefaultCredentials = true;
                client.Headers["User-Agent"] = versionAssembly.GetName().Name;
                client.Proxy = Proxy ?? client.Proxy;
                if (headers != null)
                {
                    foreach (KeyValuePair<string, object> header in headers)
                    {
                        client.Headers[header.Key] = header.Value.ToHttpHeaderString();
                    }
                }
                if (string.IsNullOrWhiteSpace(postJson))
                {
                    return client.DownloadDataTaskAsync(uri);
                }
                client.Headers["Content-Type"] = "application/json";
                return client.UploadDataTaskAsync(uri, "POST", Encoding.UTF8.GetBytes(postJson));
            }
        }

        public IWebProxy Proxy { get; set; }
    }
}
