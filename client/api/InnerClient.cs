﻿using io.harness.cfsdk.client.api.analytics;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.connector;
using io.harness.cfsdk.client.polling;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.IdentityModel.Tokens;
using Newtonsoft.Json.Linq;
using Serilog;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace io.harness.cfsdk.client.api
{
    internal class InnerClient :
        IAuthCallback,
        IRepositoryCallback,
        IPollCallback,
        IUpdateCallback,
        IEvaluatorCallback,
        IMetricCallback,
        IConnectionCallback
    {
        // Services
        private IAuthService authService;
        private IRepository repository;
        private IPollingProcessor polling;
        private IUpdateProcessor update;
        private IEvaluator evaluator;
        private IMetricsProcessor metric;
        private IConnector connector;

        public event EventHandler InitializationCompleted;
        public event EventHandler<string> EvaluationChanged;

        private CfClient parent;
        private readonly ILogger logger;

        public InnerClient(CfClient parent, ILogger logger = null)
        {
            this.parent = parent;
            this.logger = logger ?? Log.Logger;
        }
        public InnerClient(string apiKey, Config config, CfClient parent, ILogger logger = null)
            : this(parent, logger)
        {
            Initialize(apiKey, config);
        }

        public InnerClient(IConnector connector, Config config, CfClient parent, ILogger logger = null)
            : this(parent, logger)
        {
            Initialize(connector, config);
        }

        public void Initialize(string apiKey, Config config)
        {
            Initialize(new HarnessConnector(apiKey, config, this), config);
        }

        public void Initialize(IConnector connector, Config config)
        {
            this.connector = connector;
            this.authService = new AuthService(connector, config, this);
            this.repository = new StorageRepository(config.Cache, config.Store, this);
            this.polling = new PollingProcessor(connector, this.repository, config, this);
            this.update = new UpdateProcessor(connector, this.repository, config, this);
            this.evaluator = new Evaluator(this.repository, this);
            this.metric = new MetricsProcessor(connector, config, this);
        }
        public void Start()
        {
            logger.Information("Initialize authentication");
            // Start Authentication flow
            this.authService.Start();
        }
        public async Task WaitToInitialize()
        {
            var initWork = new[] {
                this.polling.ReadyAsync()
            };

            // We finished with initialization when Polling processor returns.
            await Task.WhenAll(initWork);

            OnNotifyInitializationCompleted();
        }
        public async Task StartAsync()
        {
            Start();
            await WaitToInitialize();
        }
        #region Stream callback

        public void OnStreamConnected()
        {
            this.polling.Stop();
        }
        public void OnStreamDisconnected()
        {
            this.polling.Start();
        }
        #endregion



        #region Authentication callback
        public void OnAuthenticationSuccess()
        {
            // after successfull authentication, start
            polling.Start();
            update.Start();
            metric.Start();
        }

        #endregion

        #region Reauthentication callback
        public void OnReauthenticateRequested()
        {
            polling.Stop();
            update.Stop();
            metric.Stop();

            authService.Start();
        }
        #endregion

        #region Poller Callback
        public void OnPollerReady()
        {

        }
        public void OnPollError(string message)
        {

        }
        #endregion

        #region Repository callback

        public void OnFlagStored(string identifier)
        {
            OnNotifyEvaluationChanged(identifier);
        }

        public void OnFlagDeleted(string identifier)
        {
            OnNotifyEvaluationChanged(identifier);
        }

        public void OnSegmentStored(string identifier)
        {
            repository.FindFlagsBySegment(identifier).ToList().ForEach(i =>
            {
                OnNotifyEvaluationChanged(i);
            });
        }

        public void OnSegmentDeleted(string identifier)
        {
            repository.FindFlagsBySegment(identifier).ToList().ForEach(i =>
            {
                OnNotifyEvaluationChanged(i);
            });
        }
        #endregion

        private void OnNotifyInitializationCompleted()
        {
            InitializationCompleted?.Invoke(parent, EventArgs.Empty);
        }
        private void OnNotifyEvaluationChanged(string identifier)
        {
            EvaluationChanged?.Invoke(parent, identifier);
        }

        public bool BoolVariation(string key, dto.Target target, bool defaultValue)
        {
            return evaluator.BoolVariation(key, target, defaultValue);
        }
        public string StringVariation(string key, dto.Target target, string defaultValue)
        {
            return evaluator.StringVariation(key, target, defaultValue);
        }
        public double NumberVariation(string key, dto.Target target, double defaultValue)
        {
            return evaluator.NumberVariation(key, target, defaultValue);
        }
        public JObject JsonVariation(string key, dto.Target target, JObject defaultValue)
        {
            return evaluator.JsonVariation(key, target, defaultValue);
        }

        public void Close()
        {
            this.connector.Close();
            this.authService.Stop();
            this.repository.Close();
            this.polling.Stop();
            this.update.Stop();
            this.metric.Stop();
        }

        public void Update(Message message, bool manual)
        {
            this.update.Update(message, manual);
        }
        public void evaluationProcessed(FeatureConfig featureConfig, dto.Target target, Variation variation)
        {
            this.metric.PushToQueue(target, featureConfig, variation);
        }
    }
}
