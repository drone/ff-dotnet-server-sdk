.NET SDK For Harness Feature Flags
========================

## Table of Contents
**[Intro](#Intro)**<br>
**[Requirements](#Requirements)**<br>
**[Quickstart](#Quickstart)**<br>
**[Further Reading](docs/further_reading.md)**<br>
**[Build Instructions](docs/build.md)**<br>


## Intro
Use this README to get started with our Feature Flags (FF) SDK for .NET. This guide outlines the basics of getting started with the SDK and provides a full code sample for you to try out.
This sample doesn’t include configuration options, for in depth steps and configuring the SDK, for example, disabling streaming or using our Relay Proxy, see the  [.NET SDK Reference](https://ngdocs.harness.io/article/c86rasy39v-net-sdk-reference).

For a sample FF .NET SDK project, see our [test .NET project](examples/getting_started/).

![FeatureFlags](https://github.com/harness/ff-python-server-sdk/raw/main/docs/images/ff-gui.png)

## Requirements
[.NET Framework >= 4.8](https://dotnet.microsoft.com/en-us/download/dotnet-framework/net48)<br> 
or<br>
[.Net 5.0.104](https://docs.microsoft.com/en-us/nuget/quickstart/install-and-use-a-package-using-the-dotnet-cli) or newer (dotnet --version)<br>
The library is packaged as multi-target supporting `netstandard2.0` set of API's and additionaly targets `net461` for older frameworks.


## Quickstart
To follow along with our test code sample, make sure you’ve:

- [Created a Feature Flag on the Harness Platform](https://ngdocs.harness.io/article/1j7pdkqh7j-create-a-feature-flag) called harnessappdemodarkmode
- [Created a server SDK key and made a copy of it](https://ngdocs.harness.io/article/1j7pdkqh7j-create-a-feature-flag#step_3_create_an_sdk_key)



### Install the SDK
Add the sdk using dotnet
```bash
dotnet add package ff-netF48-server-sdk .
```

### Code Sample
Here is a complete example that will connect to the feature flag service and report the flag value every 10 seconds until the connection is closed.  
Any time a flag is toggled from the feature flag service you will receive the updated value.

```c#
using System;
using System.Collections.Generic;
using io.harness.cfsdk.client.dto;
using io.harness.cfsdk.client.api;
using System.Threading;

namespace getting_started
{
    class Program
    {
        public static String apiKey = Environment.GetEnvironmentVariable("FF_API_KEY");
        public static String flagName = Environment.GetEnvironmentVariable("FF_FLAG_NAME") is string v && v.Length > 0 ? v : "harnessappdemodarkmode";
        
        static void Main(string[] args)
        {
            // Create a feature flag client
            CfClient.Instance.Initialize(apiKey, Config.Builder().Build());
            
            // Create a target (different targets can get different results based on rules)
            Target target = Target.builder()
                            .Name("DotNetSDK") 
                            .Identifier("dotnetsdk")
                            .Attributes(new Dictionary<string, string>(){{"location", "emea"}})
                            .build();

           // Loop forever reporting the state of the flag
            while (true)
            {
                bool resultBool = CfClient.Instance.boolVariation(flagName, target, false);
                Console.WriteLine("Flag variation " + resultBool);
                Thread.Sleep(10 * 1000);
            }
        }
    }
}

```

### Running the example

```bash
$ export FF_API_KEY=<your key here>
$ dotnet run --project examples/getting_started/
```

### Running the example with Docker
If you dont have the right version of dotnet installed locally, or dont want to install the dependancies you can
use docker to quicky get started

```bash
docker run -v $(pwd):/app -w /app -e FF_API_KEY=$FF_API_KEY mcr.microsoft.com/dotnet/sdk:5.0 dotnet run --project examples/getting_started/
```

### Additional Reading


For further examples and config options, see the [.NET SDK Reference](https://ngdocs.harness.io/article/c86rasy39v-net-sdk-reference#).

For more information about Feature Flags, see our [Feature Flags documentation](https://ngdocs.harness.io/article/0a2u2ppp8s-getting-started-with-feature-flags).


-------------------------
[Harness](https://www.harness.io/) is a feature management platform that helps teams to build better software and to
test features quicker.

-------------------------
