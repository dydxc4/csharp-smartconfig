using System;
using System.Net.NetworkInformation;
using Sandwych.SmartConfig.Protocol;

namespace Sandwych.SmartConfig.EspTouchV2.Protocol
{
    public class EspV2DevicePacketInterpreter : IDevicePacketInterpreter
    {
        public PhysicalAddress ParseMacAddress(byte[] response)
        {
            var macSpan = new ArraySegment<byte>(response, 1, 6);
            return new PhysicalAddress(macSpan.ToArray());
        }

        public bool Validate(SmartConfigContext context, byte[] response)
        {
            return response.Length == EspV2Constants.DeviceResponseLength &&
                response[0] == EspV2Constants.DeviceResponseMagic;
        }
    }
}