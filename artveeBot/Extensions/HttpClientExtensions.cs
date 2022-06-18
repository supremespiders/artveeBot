using artveeBot.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace artveeBot.Extensions
{
    public static class HttpClientExtensions
    {
        public static async Task<string> HandleAndRepeat(this HttpClient httpClient, HttpRequestMessage req, int maxAttempts = 1, CancellationToken ct = new CancellationToken())
        {
            int tries = 0;
            do
            {
                try
                {
                    var r = await httpClient.SendAsync(req, ct).ConfigureAwait(false);
                    var s = await r.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return (s);
                }
                catch (TaskCanceledException ex)
                {
                    if (ct.IsCancellationRequested)
                        throw;
                    throw new Exception("Timed Out");
                }
                catch (WebException ex)
                {
                    var errorMessage = "";
                    try
                    {
                        errorMessage = await new StreamReader(ex.Response.GetResponseStream()).ReadToEndAsync();
                    }
                    catch (Exception)
                    {
                        //
                    }

                    tries++;
                    if (tries == maxAttempts)
                    {
                        throw new KnownException($"Error calling : {req.RequestUri}\n{ex.Message} {errorMessage}");
                    }

                    await Task.Delay(2000, ct).ConfigureAwait(false);
                }
            } while (true);
        }

        public static async Task<string> PostJson(this HttpClient httpClient, string url, string json, int maxAttempts = 1, Dictionary<string, string> headers = null, CancellationToken ct = new CancellationToken())
        {
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            if (content.Headers.ContentType != null)
                content.Headers.ContentType.CharSet = "";
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

            if (headers == null)
                return await httpClient.HandleAndRepeat(req, maxAttempts, ct);

            foreach (var header in headers)
                req.Headers.Add(header.Key, header.Value);

            return await httpClient.HandleAndRepeat(req, maxAttempts, ct);
        }

        public static async Task<string> PostFormData(this HttpClient httpClient, string url, Dictionary<string, string> data, int maxAttempts = 1, Dictionary<string, string> headers = null, CancellationToken ct = new CancellationToken())
        {
            var content = new FormUrlEncodedContent(data);
            var req = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };

            if (headers == null)
                return await httpClient.HandleAndRepeat(req, maxAttempts, ct);

            foreach (var header in headers)
                req.Headers.Add(header.Key, header.Value);

            return await httpClient.HandleAndRepeat(req, maxAttempts, ct);
        }

        public static async Task<string> GetHtml(this HttpClient httpClient, string url, int maxAttempts = 1, Dictionary<string, string> headers = null, CancellationToken ct = new CancellationToken())
        {
            var tries = 0;
            do
            {
                try
                {
                    var req = new HttpRequestMessage(HttpMethod.Get, url);

                    if (headers != null)
                        foreach (var header in headers)
                            req.Headers.Add(header.Key, header.Value);

                    var r = await httpClient.SendAsync(req, ct).ConfigureAwait(false);
                    var s = await r.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return WebUtility.HtmlDecode(s);
                }
                catch (TaskCanceledException)
                {
                    if (ct.IsCancellationRequested) throw;
                    tries++;
                    if (tries == maxAttempts)
                        throw new KnownException($"Timed out on {url}");
                }
                catch (WebException ex)
                {
                    var errorMessage = "";
                    try
                    {
                        errorMessage = await new StreamReader(ex.Response.GetResponseStream() ?? throw new InvalidOperationException()).ReadToEndAsync();
                    }
                    catch (Exception)
                    {
                        //
                    }

                    tries++;
                    if (tries == maxAttempts)
                    {
                        throw new KnownException($"Error calling : {url}\n{ex.Message} {errorMessage}");
                    }
                }
                await Task.Delay(4000, ct).ConfigureAwait(false);
            } while (true);
        }

        public static async Task DownloadFile(this HttpClient client, string url, string localPath, CancellationToken ct)
        {
            var response = await client.GetAsync(url, ct);
            using (var fs = new FileStream(localPath, FileMode.Create))
            {
                await response.Content.CopyToAsync(fs).ConfigureAwait(false);
            }
        }
    }
}