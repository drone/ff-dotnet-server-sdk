﻿using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using io.harness.cfsdk.client.cache;
using Microsoft.Extensions.Logging;

namespace io.harness.cfsdk.client.api
{
    public class Config
    {
        public static int MIN_FREQUENCY = 60;

        public string ConfigUrl { get => configUrl; }
        internal string configUrl = "https://config.ff.harness.io/api/1.0";
        public string EventUrl { get => eventUrl; }
        internal string eventUrl = "https://events.ff.harness.io/api/1.0";
        public bool StreamEnabled { get => streamEnabled; }
        internal bool streamEnabled = true;

        public int PollIntervalInMiliSeconds { get => pollIntervalInSeconds * 1000; }
        internal int pollIntervalInSeconds = 60;
        
        public int MaxAuthRetries { get => maxAuthRetries; }
        internal int maxAuthRetries = 10;

        // configurations for Analytics
        public bool AnalyticsEnabled { get => analyticsEnabled; }
        internal bool analyticsEnabled = true;

        public int Frequency { get => Math.Max(frequency, Config.MIN_FREQUENCY); }
        private int frequency = 60;

        public ICache Cache { get => cache; }
        internal ICache cache = new FeatureSegmentCache();

        public IStore Store { get => store;  }
        internal IStore store = null;

        //BufferSize must be a power of 2 for LMAX to work. This function vaidates
        //that. Source: https://stackoverflow.com/a/600306/1493480
        public int BufferSize => bufferSize;
        internal int bufferSize = 1024;


        /** timeout in milliseconds to connect to CF Server */
        public int ConnectionTimeout { get =>connectionTimeout;}
        internal int connectionTimeout = 10000;

        /** timeout in milliseconds for reading data from CF Server */
        public int ReadTimeout { get => readTimeout;  }
        internal int readTimeout { get; set; } = 30000;

        /** timeout in milliseconds for writing data to CF Server */
        public int WriteTimeout { get => writeTimeout;  }
        internal int writeTimeout { get; set; } = 10000;

        public bool Debug { get => debug;  }
        internal bool debug { get; set; } = false;

        /** If metrics service POST call is taking > this time, we need to know about it */

        public long MetricsServiceAcceptableDuration { get => metricsServiceAcceptableDuration;  }
        internal long metricsServiceAcceptableDuration = 10000;

        public ILoggerFactory LoggerFactory { get; set; }

        public List<X509Certificate2> TlsTrustedCAs { get; set; } = new();

        public Config(string configUrl, string eventUrl, bool streamEnabled, int pollIntervalInSeconds, bool analyticsEnabled, int frequency, int bufferSize,  int connectionTimeout, int readTimeout, int writeTimeout, bool debug, long metricsServiceAcceptableDuration)
        {
            this.configUrl = configUrl;
            this.eventUrl = eventUrl;
            this.streamEnabled = streamEnabled;
            this.pollIntervalInSeconds = pollIntervalInSeconds;
            this.analyticsEnabled = analyticsEnabled;
            this.frequency = frequency;
            this.bufferSize = bufferSize;
            this.connectionTimeout = connectionTimeout;
            this.readTimeout = readTimeout;
            this.writeTimeout = writeTimeout;
            this.debug = debug;
            this.metricsServiceAcceptableDuration = metricsServiceAcceptableDuration;
        }

        public Config()
        {
        }

        public static ConfigBuilder Builder()
        {
            return new ConfigBuilder();
        }
        
    }

    public class ConfigBuilder
    {
        Config configtobuild;

        public ConfigBuilder()
        {
            configtobuild = new Config();
        }

        public Config Build()
        {
            return configtobuild;
        }

        public ConfigBuilder SetPollingInterval(int pollIntervalInSeconds)
        {
            this.configtobuild.pollIntervalInSeconds = pollIntervalInSeconds;
            return this;
        }
        public ConfigBuilder SetCache(ICache cache)
        {
            this.configtobuild.cache = cache;
            return this;
        }
        public ConfigBuilder SetStore(IStore store)
        {
            this.configtobuild.store = store;
            return this;
        }
        public ConfigBuilder SetStreamEnabled(bool enabled = true)
        {
            configtobuild.streamEnabled = enabled;
            return this;
        }
        public ConfigBuilder MetricsServiceAcceptableDuration(long duration = 10000)
        {
            configtobuild.metricsServiceAcceptableDuration = duration;
            return this;
        }
        public ConfigBuilder SetAnalyticsEnabled(bool analyticsenabled = true)
        {
            this.configtobuild.analyticsEnabled = analyticsenabled;
            return this;
        }
        public ConfigBuilder ConfigUrl(string configUrl)
        {
            this.configtobuild.configUrl = configUrl;
            return this;
        }
        public ConfigBuilder EventUrl(string eventUrl)
        {
            this.configtobuild.eventUrl = eventUrl;
            return this;
        }
        public ConfigBuilder connectionTimeout(int connectionTimeout)
        {
            this.configtobuild.connectionTimeout = connectionTimeout;
            return this;
        }
        public ConfigBuilder readTimeout(int readTimeout)
        {
            this.configtobuild.readTimeout = readTimeout;
            return this;
        }

        public ConfigBuilder writeTimeout(int writeTimeout)
        {
            this.configtobuild.writeTimeout = writeTimeout;
            return this;
        }

        public ConfigBuilder debug(bool debug)
        {
            this.configtobuild.debug = debug;
            return this;
        }

        /*
        BufferSize must be a power of 2 for LMAX to work This function vaidates
        that. Source: https://stackoverflow.com/a/600306/1493480
        The max BufferSize that can be set is 4096. 
        Defaults to 2048 if not a power of 2 or over 4096.
        */
        public ConfigBuilder SetBufferSize(int bufferSize)
        {
            // Check if bufferSize is a power of two
            var isPowerOfTwo = bufferSize > 0 && (bufferSize & (bufferSize - 1)) == 0;

            if (!isPowerOfTwo || bufferSize > 4096)
            {
                // Log a warning if bufferSize is not a power of two or if it's greater than 4096
                bufferSize = 2048; // Set default value
            }

            configtobuild.bufferSize = bufferSize;
            return this;
        }

        /**
         * <summary>
         * Set an ILoggerFactory for the SDK. note: cannot be used in conjunction with getInstance()
         * <summary>
         */
        public ConfigBuilder LoggerFactory(ILoggerFactory loggerFactory)
        {
            this.configtobuild.LoggerFactory = loggerFactory;
            return this;
        }

        /**
         * <summary>
         * List of trusted CAs - for when the given config/event URLs are signed with a private CA. You
         * should include intermediate CAs too to allow the HTTP client to build a full trust chain.
         * </summary>
         */
        public ConfigBuilder TlsTrustedCAs( List<X509Certificate2> certs )
        {
            this.configtobuild.TlsTrustedCAs = certs;
            return this;
        }


    }
}
