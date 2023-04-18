[![Build status](https://ci.appveyor.com/api/projects/status/y4vy6qr9k0xj8e7y/branch/master?svg=true)](https://ci.appveyor.com/project/oldrev/sandwych-smartconfig/branch/master)
[![NuGet](https://img.shields.io/nuget/v/Sandwych.SmartConfig.svg)](https://www.nuget.org/packages/Sandwych.SmartConfig)

# CSharp.SmartConfig

Csharp.SmartConfig is fork of Sandwych.SmartConfig, a pure C# implementation of various WiFi SmartConfig protocols that build from scratch.

TD;LR: If you working on a Xamarin mobile app to deal with WiFi-capability IoT devices, you may need this library.

English | [简体中文](README.zh_cn.md)

## Getting Started

## Features

* A .NET Standard class library, works on both Xamarin and desktop.
* No third-party library referenced.
* Supported protocols: WeChat's AirKiss, Espressif's ESPTouch and ESPTouchV2.
* Clean architecture, easy to learn and add your own protocol.
* IoC container friendly.

## Getting Started

### Prerequisites

* Microsoft Visual Studio 2019 
* DocFX for API documents generation (Optional)

### Supported Platforms

* .NET Standard 2.1+

### Installation

Install Sandwych.SmartConfig to your project by [NuGet](https://www.nuget.org/packages/Sandwych.SmartConfig) then you're good to go.

## Examples

### Usage

```csharp

var provider = new EspSmartConfigProvider();
var ctx = provider.CreateContext();

ctx.DeviceDiscoveredEvent += (s, e) => {
	Console.WriteLine("Found device: IP={0}    MAC={1}", e.Device.IPAddress, e.Device.MacAddress);
};

var scArgs = new SmartConfigArguments()
{
	Ssid = "YourWiFiSSID",
	Bssid = PhysicalAddress.Parse("10-10-10-10-10-10"),
	Password = "YourWiFiPassword",

	// Your local IP address of WiFi network. It's important for using multiple network interfaces
	// See CliDemoApp for details.
	LocalAddress = IPAddress.Parse("192.168.1.10") 
};

// Do the SmartConfig job
using (var job = new SmartConfigJob(TimeSpan.FromSeconds(100))) // Set the timeout to 100 seconds
{
	await job.ExecuteAsync(ctx, scArgs);
}

```

Or much simpler if you perfer the callback style:

```csharp

await SmartConfigStarter.StartAsync<EspSmartConfigProvider>(args, 
	onDeviceDiscovered: (s, e) => Console.WriteLine("Found device: IP={0}    MAC={1}", e.Device.IPAddress, e.Device.MacAddress));

```

### The Demo Android App

APK Download: WIP

## Donation

If this project is useful to you, you can buy me a beer:

[![Support via PayPal.me](https://github.com/oldrev/sandwych-smartconfig/blob/master/assets/paypal_button.svg)](https://www.paypal.me/oldrev)

## Contributiors

* **Li "oldrev" Wei** - *Init work and the main maintainer* - [oldrev](https://github.com/oldrev)

## License

Licensed under the MIT License. Copyright &copy; Sandwych.SmartConfig Contributors.

See [LICENSE.md](LICENSE.md) for details.

## Credits

* Espressif EsptouchForAndroid: https://github.com/EspressifApp/EsptouchForAndroid
