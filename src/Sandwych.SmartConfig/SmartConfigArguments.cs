using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace Sandwych.SmartConfig
{
    public class SmartConfigArguments
    {
        public string? Password { get; set; }

        public string? Ssid { get; set; }

        public PhysicalAddress Bssid { get; set; } = PhysicalAddress.None;

        public IPAddress LocalAddress { get; set; } = IPAddress.Any;
        
        public byte[]? AesKey { get; set; }

        public byte[]? ReservedData { get; set; }
    }
}
