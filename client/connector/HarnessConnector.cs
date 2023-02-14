using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using io.harness.cfsdk.client.api;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace io.harness.cfsdk.client.connector
{
    interface IConnectionCallback
    {
        void OnReauthenticateRequested();
    }

    internal class HarnessConnector : IConnector
    {
        private string token;
        private string environment;
        private string cluster;

        public HttpClient apiHttpClient { get; set; }
        public HttpClient metricHttpClient { get; set; }
        public HttpClient sseHttpClient { get; set; }

        private string apiKey;
        private Config config;
        private IConnectionCallback callback;
        private Client harnessClient;

        private IService currentStream;
        private CancellationTokenSource cancelToken = new CancellationTokenSource();

        private static HttpClient ApiHttpClient(Config config)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(config.ConfigUrl);
            client.Timeout = TimeSpan.FromSeconds(config.ConnectionTimeout);
            return client;
        }
        private static HttpClient MetricHttpClient(Config config)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(config.EventUrl);
            client.Timeout = TimeSpan.FromSeconds(config.ConnectionTimeout);
            return client;
        }
        private static HttpClient SseHttpClient(Config config, string apiKey)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(config.ConfigUrl.EndsWith("/") ? config.ConfigUrl : config.ConfigUrl + "/" );
            client.DefaultRequestHeaders.Add("API-Key", apiKey);
            client.DefaultRequestHeaders.Add("Accept", "text /event-stream");
            client.Timeout = TimeSpan.FromMinutes(1);
            return client;
        }
        
        public HarnessConnector(string apiKey, Config config, IConnectionCallback callback)
        : this(apiKey, config, callback, ApiHttpClient(config), MetricHttpClient(config), SseHttpClient(config, apiKey))
        {
           
        }

        private static Client HarnessClient(Config config, HttpClient httpClient)
        {
            Client client = new Client(httpClient);
            client.BaseUrl = config.ConfigUrl;
            return client;
        }

        public HarnessConnector(
            string apiKey,
            Config config,
            IConnectionCallback callback,
            HttpClient apiHttpClient,
            HttpClient metricHttpClient,
            HttpClient sseHttpClient
        ) : this(apiKey, config, callback, apiHttpClient, metricHttpClient, sseHttpClient, HarnessClient(config, apiHttpClient))
        {
        }

        public HarnessConnector(
            string apiKey,
            Config config,
            IConnectionCallback callback,
            HttpClient apiHttpClient,
            HttpClient metricHttpClient,
            HttpClient sseHttpClient,
            Client harnessClient)
        {
            this.config = config;
            this.apiKey = apiKey;
            this.callback = callback;
            this.apiHttpClient = apiHttpClient;
            this.metricHttpClient = metricHttpClient;
            this.sseHttpClient = sseHttpClient;
            this.harnessClient = harnessClient;
        }

        private async Task<T> ReauthenticateIfNeeded<T>(Func<Task<T>> task)
        {
            try
            {
                return await task();
            }
            catch (ApiException ex)
            {
                if (ex.StatusCode == (int)HttpStatusCode.Forbidden)
                {
                    Log.Error("Initiate reauthentication");
                    callback.OnReauthenticateRequested();
                }
                throw new CfClientException(ex.Message);
            }
            catch (Exception ex)
            {
                throw new CfClientException(ex.Message);
            }
        }
        public async Task<IEnumerable<FeatureConfig>> GetFlags()
        {
            return await ReauthenticateIfNeeded(() => harnessClient.ClientEnvFeatureConfigsGetAsync(environment, cluster, cancelToken.Token));
        }
        
        public async Task<IEnumerable<Segment>> GetSegments()
        {
            return await ReauthenticateIfNeeded(() => harnessClient.ClientEnvTargetSegmentsGetAsync(environment, cluster, cancelToken.Token));
        }
        public Task<FeatureConfig> GetFlag(string identifier)
        {
            return ReauthenticateIfNeeded(() => harnessClient.ClientEnvFeatureConfigsGetAsync(identifier, environment, cluster, cancelToken.Token));
        }
        public Task<Segment> GetSegment(string identifier)
        {
            return ReauthenticateIfNeeded(() => harnessClient.ClientEnvTargetSegmentsGetAsync(identifier, environment, cluster, cancelToken.Token));
        }
        public IService Stream(IUpdateCallback updater)
        {
            currentStream?.Close();
            var url = $"stream?cluster={cluster}";
            currentStream = new EventSource(sseHttpClient, url, config, updater);
            return currentStream;
        }
        public async Task PostMetrics(HarnessOpenMetricsAPIService.Metrics metrics)
        {
            await ReauthenticateIfNeeded<Task>(async () =>
            {
                var startTime = DateTime.Now;
                var client = new HarnessOpenMetricsAPIService.Client(metricHttpClient)
                {
                    BaseUrl = config.EventUrl
                };
                await client.MetricsAsync(environment, cluster, metrics, cancelToken.Token);
                var endTime = DateTime.Now;
                if ((endTime - startTime).TotalMilliseconds > config.MetricsServiceAcceptableDuration)
                {
                    Log.Warning($"Metrics service API duration=[{endTime - startTime}]");
                }

                return null;
            });
        }
        public async Task<string> Authenticate()
        {
            try
            {
                var authenticationRequest = new AuthenticationRequest
                {
                    ApiKey = apiKey,
                    Target = new Target2 { Identifier = "" }
                };
                var response = await harnessClient.ClientAuthAsync(authenticationRequest, cancelToken.Token);
                token = response.AuthToken;

                var handler = new JwtSecurityTokenHandler();
                var jsonToken = handler.ReadToken(token);
                var jwtToken = (JwtSecurityToken)jsonToken;

                environment = jwtToken.Payload["environment"].ToString();
                cluster = jwtToken.Payload["clusterIdentifier"].ToString();

                apiHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                metricHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                sseHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

                return token;

            }
            catch (ApiException ex)
            {
                Log.Error($"Failed to get auth token {ex.Message}");
                if (ex.StatusCode == (int)HttpStatusCode.Unauthorized || ex.StatusCode == (int)HttpStatusCode.Forbidden)
                {
                    var errorMsg = $"Invalid apiKey {apiKey}. Serving default value.";
                    Log.Error(errorMsg);
                    throw new CfClientException(errorMsg);
                }
                throw new CfClientException(ex.Message);
            }
        }

        public void Close()
        {
            cancelToken.Cancel();
            currentStream.Close();
        }
    }
}
