using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace Sandwych.SmartConfig.Networking
{
    public struct DatagramReceiveResult
    {
        public byte[] Buffer { get; }
        public IPEndPoint RemoteEndPoint { get; }

        public DatagramReceiveResult(byte[] buffer, IPEndPoint remote)
        {
            Buffer = buffer;
            RemoteEndPoint = remote;
        }
    }
}
