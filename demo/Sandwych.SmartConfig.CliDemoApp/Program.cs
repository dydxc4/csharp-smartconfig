using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Text;

using Sandwych.SmartConfig.Esptouch;
using Sandwych.SmartConfig.EspTouchV2;
using Sandwych.SmartConfig.Airkiss;

namespace Sandwych.SmartConfig.CliDemoApp
{
    class Program
    {
        class ArgumentsInfo
        {
            public SmartConfigArguments SCArguments { get; } = new SmartConfigArguments();

            public Type? ProtocolProvider { get; set; }

            public int Timeout { get; set; } = 30;
        }

        private static bool TryReadArguments(string[] args, out ArgumentsInfo? argumentsInfo)
        {
            argumentsInfo = new ArgumentsInfo();

            if (args.Length < 2)
            {
                Console.WriteLine("No arguments were received");
                return false;
            }

            // Protocol check
            if (StringCompare("esptouch", args[0]))
            {
                argumentsInfo.ProtocolProvider = typeof(EspSmartConfigProvider);
            }
            else if (StringCompare("esptouchv2", args[0]))
            {
                argumentsInfo.ProtocolProvider = typeof(EspV2SmartConfigProvider);
            }
            else if (StringCompare("airkiss", args[0]))
            {
                argumentsInfo.ProtocolProvider = typeof(AirkissSmartConfigProvider);
            }
            else
            {
                Console.WriteLine("Invalid protocol name");
                return false;
            }

            // BSSID check

            if (!PhysicalAddress.TryParse(args[1].Replace(':', '-'), out PhysicalAddress? bssid))
            {
                Console.WriteLine("Invalid BSSID format");
                return false;
            }

            argumentsInfo.SCArguments.Bssid = bssid;

            // Options check
            for (int i = 2; i < args.Length; i += 2)
            {
                int nextArgIdx = i + 1;
                string argName = args[i];

                // Check for ssid arg
                if (nextArgIdx >= args.Length)
                {
                    Console.WriteLine($"Missing value for '{argName}'!");
                    return false;
                }

                string argValue = args[nextArgIdx];

                if (StringCompare("-s", argName))
                {
                    argumentsInfo.SCArguments.Ssid = argValue;
                }
                else if (StringCompare("-p", argName))
                {
                    argumentsInfo.SCArguments.Password = argValue;
                }
                else if (StringCompare("-d", argName))
                {
                    argumentsInfo.SCArguments.ReservedData = Encoding.UTF8.GetBytes(argValue);
                }
                else if (StringCompare("-k", argName))
                {
                    argumentsInfo.SCArguments.AesKey = Encoding.UTF8.GetBytes(argValue);
                }
                else if (StringCompare("-t", argName) && 
                    int.TryParse(argValue, out int timeout))
                {
                    argumentsInfo.Timeout = timeout;
                }
                else
                {
                    Console.WriteLine($"Argument '{argName}' is not valid");
                    return false;
                }
            }

            return true;

            bool StringCompare(string str1, string str2)
                => str1.Equals(str2, StringComparison.OrdinalIgnoreCase);
        }

        private static NetworkInterface? FindFirstWifiInterfaceOrDefault()
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                // For some reason, wireless interfaces in Linux are identified as ethernet
                return adapters.Where(
                    x => x.Name.StartsWith("wl", StringComparison.OrdinalIgnoreCase)
                    && x.OperationalStatus == OperationalStatus.Up
                    && !x.IsReceiveOnly
                ).FirstOrDefault();
            }
            else
            {
                return adapters.Where(
                    x => x.NetworkInterfaceType == NetworkInterfaceType.Wireless80211
                    && x.OperationalStatus == OperationalStatus.Up
                    && !x.IsReceiveOnly
                ).FirstOrDefault();
            }
        }

        private static IPAddress? GetIPv4AddressOrDefault(NetworkInterface ni)
        {
            return ni.GetIPProperties()
                    .UnicastAddresses
                    .Where(x => x.Address.AddressFamily == AddressFamily.InterNetwork)
                    .Select(x => x.Address)
                    .FirstOrDefault();
        }

        static async Task<int> Main(string[] args)
        {
            Console.WriteLine("ESPTouch SmartConfig Demo/Utility");
            ShowOsInfo();
            ArgumentsInfo? argumentsInfo = null;

            if (!TryReadArguments(args, out argumentsInfo) || argumentsInfo is null || argumentsInfo.ProtocolProvider is null)
            {
                ShowUsage();
                return -1;
            }

            var wifiInterface = FindFirstWifiInterfaceOrDefault();
            if (wifiInterface == null)
            {
                Console.WriteLine("Cannot find any available WiFi adapter.");
                return -2;
            }
            Console.WriteLine("WiFi interface: {0}", wifiInterface.Name);

            var localAddress = GetIPv4AddressOrDefault(wifiInterface);
            if(localAddress == null)
            {
                Console.WriteLine("Cannot find IPv4 address for WiFi interface: {0}", wifiInterface.Name);
                return -3;
            }
            Console.WriteLine("Local address: {0}", localAddress);

            var provider = (ISmartConfigProvider?)Activator.CreateInstance(argumentsInfo.ProtocolProvider);
            if (provider is null)
            {
                Console.WriteLine("Failed to create an instance of the selected protocol");
                return -4;
            }
            
            var ctx = provider.CreateContext();

            ctx.DeviceDiscoveredEvent += (s, e) =>
            {
                Console.WriteLine("Found device: IP={0}, MAC={1}", e.Device.IPAddress, e.Device.MacAddress);
            };

            // Do the SmartConfig job
            using (var job = new SmartConfigJob(TimeSpan.FromSeconds(argumentsInfo.Timeout))) // Set the timeout to 45 seconds
            {
                job.Elapsed += Job_Elapsed;

                await job.ExecuteAsync(ctx, argumentsInfo.SCArguments);
            }

            Console.WriteLine("SmartConfig finished.");
            return 0;
        }

        private static void ShowUsage()
        {
            Console.WriteLine("Usage: sccli PROTOCOL BSSID [OPTIONS]");
            Console.WriteLine("\nAvailable Protocols: EspTouch, EspTouchV2 and AirKiss");
            Console.WriteLine("\nBSSID: The BSSID(MAC) of your WiFi AP, like '10-10-10-10-10-10' or '10:10:10:10:10:10'");
            Console.WriteLine("\nOptions:");
            Console.WriteLine("  -s AP_SSID\t\tThe SSID of your WiFi Access Point.");
            Console.WriteLine("  -p AP_PASSWORD\tWiFi access point password if required.");
            Console.WriteLine("  -d DATA\t\tReserved data to be sent to the device. Only available when EspTouchV2 is used.");
            Console.WriteLine("  -k KEY\t\tData encryption key. Must be 16 characters and only available when EspTouchV2 is used.");
            Console.WriteLine("  -t TIMEOUT\tTimeout, default 30 secs.");
            Console.WriteLine("\nTips:");
            Console.WriteLine("  On Windows you can get BSSID by using command 'netsh wlan show interfaces'");
        }

        private static void ShowOsInfo()
        {
            var description = RuntimeInformation.OSDescription;
            var arch = RuntimeInformation.OSArchitecture;

            Console.WriteLine($"Running in {description}, arch: {arch}");
        }

        private static void Job_Elapsed(object sender, SmartConfigTimerEventArgs e)
        {
            Console.WriteLine("Doing SmartConfig, Time remaining: {0}", e.LeftTime);
        }
    }
}
