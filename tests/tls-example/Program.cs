﻿
using System.Security.Cryptography.X509Certificates;
using io.harness.cfsdk.client.api;
using Serilog;
using Serilog.Extensions.Logging;

namespace io.harness.tls_example
{
    class Program
    {
        private static string certAuthority1 =
            "-----BEGIN CERTIFICATE-----\n<<ADD YOUR CA CERTS HERE>>\n-----END CERTIFICATE-----";
        
        
        static async Task Main(string[] args)
        {
            var apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
            if (apiKey == null) throw new ArgumentNullException("FF_API_KEY","FF_API_KEY env variable is not set");
            var flagName = Environment.GetEnvironmentVariable("FF_FLAG_NAME");
            if (flagName == null) flagName = "test";
            
            var loggerFactory = new SerilogLoggerFactory(
                new LoggerConfiguration()
                    .MinimumLevel.Verbose()
                    .WriteTo.Console()
                    .CreateLogger());

            var cert1 = pemToX509Cert(certAuthority1);

            var trustedCerts = new List<X509Certificate2> { cert1 };
            
            var config = Config.Builder()
                .ConfigUrl("https://ffserver:8001/api/1.0")
                .EventUrl("https://ffserver:8000/api/1.0")
                .TlsTrustedCAs(trustedCerts)
                .LoggerFactory(loggerFactory).Build();
            var client = new CfClient(apiKey, config);

            await client.InitializeAndWait();

            while (true)
            {
                var resultBool = client.boolVariation(flagName, null, false);
                Console.WriteLine($"Flag '{flagName}' = " + resultBool);
                Thread.Sleep(2 * 1000);
            }
            
        }

        static X509Certificate2 pemToX509Cert(string pem)
        {
            pem = pem
                .Replace("-----BEGIN CERTIFICATE-----",null)
                .Replace("-----END CERTIFICATE-----",null);

            return new X509Certificate2(Convert.FromBase64String(pem));
        }
    }
}