﻿using System.Timers;
using io.harness.cfsdk.client.cache;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.HarnessOpenAPIService;
using Microsoft.Extensions.Logging;
using Target = io.harness.cfsdk.client.dto.Target;

namespace io.harness.cfsdk.client.api.analytics
{
    internal class MetricsProcessor
    {
        private readonly AnalyticsCache analyticsCache;
        private readonly AnalyticsPublisherService analyticsPublisherService;
        private readonly Config config;
        private readonly ILogger<MetricsProcessor> logger;
        private Timer timer;
        
        private bool isGlobalTargetEnabled;

        public MetricsProcessor(Config config, AnalyticsCache analyticsCache,
            AnalyticsPublisherService analyticsPublisherService, ILoggerFactory loggerFactory, bool globalTargetEnabled)
        {
            this.analyticsCache = analyticsCache;
            this.config = config;
            this.analyticsPublisherService = analyticsPublisherService;
            logger = loggerFactory.CreateLogger<MetricsProcessor>();
            isGlobalTargetEnabled = globalTargetEnabled;
        }

        public void Start()
        {
            if (config.analyticsEnabled)
            {
                timer = new Timer((long)config.Frequency * 1000);
                timer.Elapsed += Timer_Elapsed;
                timer.AutoReset = true;
                timer.Enabled = true;
                timer.Start();
                logger.LogInformation("SDKCODE(metric:7000): Metrics thread started");
            }
        }


        public void Stop()
        {
            if (config.analyticsEnabled && timer != null)
            {
                timer.Stop();
                timer = null;
                logger.LogInformation("SDKCODE(metric:7001): Metrics thread exited");
            }
        }

        public void PushToCache(Target target, FeatureConfig featureConfig, Variation variation)
        {
            var cacheSize = analyticsCache.GetAllElements().Count;
            var bufferSize = config.getBufferSize();

            if (cacheSize > bufferSize)
            {
                logger.LogWarning(
                    "Metric frequency map exceeded buffer size ({cacheSize} > {bufferSize}), force flushing", cacheSize,
                    bufferSize);

                // If the map is starting to grow too much then push the metrics now and reset the counters
                SendMetrics();
            }
            else
            {
                if (isGlobalTargetEnabled)
                {
                    var globalTarget = new Target(EvaluationAnalytics.GlobalTargetIdentifier,
                        EvaluationAnalytics.GlobalTargetName, null);
                    PushToEvaluationAnalyticsCache(featureConfig, variation, globalTarget);
                }
                else
                {
                    PushToEvaluationAnalyticsCache(featureConfig, variation, target);
                }

                // Create target metrics 
                PushToTargetAnalyticsCache(target);

            }
        }

        private void PushToEvaluationAnalyticsCache(FeatureConfig featureConfig, Variation variation, Target target)
        {
            Analytics evaluationAnalytics = new EvaluationAnalytics(featureConfig, variation, target);
            var evaluationCount = analyticsCache.getIfPresent(evaluationAnalytics);
            analyticsCache.Put(evaluationAnalytics, evaluationCount + 1);
        }
        
        private void PushToTargetAnalyticsCache(Target target)
        {
            Analytics targetAnalytics = new TargetAnalytics(target);
            
            // We don't need to keep count of targets, so use a constant value, 1, for the count. 
            // Since 1.4.2, the analytics cache was refactored to separate out Evaluation and Target metrics, but the 
            // change did not go as far as to maintain two caches (due to effort involved), but differentiate them based on subclassing, so 
            // the counter used for target metrics isn't needed, but causes no issue. 
            analyticsCache.Put(targetAnalytics, 1);
        }

        internal void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            logger.LogDebug("Timer Elapsed - Processing/Sending analytics data");
            SendMetrics();
        }

        internal void SendMetrics()
        {
            try
            {
                analyticsPublisherService.SendDataAndResetCache();
            }
            catch (CfClientException ex)
            {
                logger.LogError(ex, "Failed to send analytics data to server");
            }
        }
    }
}