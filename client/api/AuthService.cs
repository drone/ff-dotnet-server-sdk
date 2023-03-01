﻿using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.HarnessOpenAPIService;
using Serilog;
using System.Threading;
using System.Threading.Tasks;

namespace io.harness.cfsdk.client.api
{
    interface IAuthCallback
    {
        void OnAuthenticationSuccess();
    }
    interface IAuthService
    {
        void Start();
        void Stop();
    }

    /// <summary>
    /// The class is in charge of initiating authentication requests and retry until successful authentication.
    /// </summary>
    internal class AuthService : IAuthService
    {
        private readonly IConnector connector;
        private readonly Config config;
        private readonly IAuthCallback callback;
        private readonly ILogger loggerWithContext;
        private Timer authTimer;
        private int retries = 0;

        public AuthService(IConnector connector, Config config, IAuthCallback callback)
        {
            this.connector = connector;
            this.config = config;
            this.callback = callback;
            loggerWithContext = Log.ForContext<AuthService>();
        }
        public void Start()
        {
            this.retries = 0;
            // initiate authentication
            authTimer = new Timer(OnTimedEvent, null, 0, config.PollIntervalInMiliSeconds);
        }
        public void Stop()
        {
            if (authTimer == null) return;
            authTimer.Dispose();
            authTimer = null;
        }
        private async void OnTimedEvent(object source)
        {
            try
            {
                await connector.Authenticate();
                callback.OnAuthenticationSuccess();
                Stop();
                loggerWithContext.Information("Stopping authentication service");
            }
            catch
            {
                // Exception thrown on Authentication. Timer will retry authentication.
                if (retries++ >= config.MaxAuthRetries)
                {
                    loggerWithContext.Error($"Max authentication retries reached {retries}");
                    Stop();
                }
                else
                {
                    loggerWithContext.Error($"Exception while authenticating, retry ({retries}) in {config.pollIntervalInSeconds}");
                }
            }
        }
    }
}
