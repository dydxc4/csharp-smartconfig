using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Sandwych.SmartConfig.Networking
{
    public interface IDatagramClient : IDisposable
    {
        void Bind(IPEndPoint localEndPoint);
        void SetDefaultTarget(IPEndPoint targetEndPoint);
        Task SendAsync(byte[] datagram, int bytes, IPEndPoint? target = null);

        Task<DatagramReceiveResult> ReceiveAsync();
    }
}
