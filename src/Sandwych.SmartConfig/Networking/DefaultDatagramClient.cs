using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Sandwych.SmartConfig.Networking
{
    public class DefaultDatagramClient : IDatagramClient
    {
        private readonly UdpClient _udp;
        private IPEndPoint? _targetEndPoint;

        public DefaultDatagramClient()
        {
            _udp = new UdpClient(AddressFamily.InterNetwork);
            _udp.EnableBroadcast = true;
        }

        public void Bind(IPEndPoint localEndPoint)
        {
            _udp.Client.Bind(localEndPoint);
        }

        public void SetDefaultTarget(IPEndPoint targetEndPoint)
        {
            _targetEndPoint = targetEndPoint;
        }

        public async Task SendAsync(byte[] datagram, int bytes, IPEndPoint? target = null)
        {
            if (target is null && _targetEndPoint is null)
            {
                throw new InvalidOperationException();
            }

            await _udp.SendAsync(datagram, bytes, target ?? _targetEndPoint);
        }

        public async Task<DatagramReceiveResult> ReceiveAsync()
        {
            var result = await _udp.ReceiveAsync();
            return new DatagramReceiveResult(result.Buffer, result.RemoteEndPoint);
        }

        public void Dispose()
        {
            _udp.Dispose();
        }
    }
}
