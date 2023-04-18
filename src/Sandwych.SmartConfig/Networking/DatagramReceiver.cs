using Sandwych.SmartConfig.Esptouch;
using Sandwych.SmartConfig.Util;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Sandwych.SmartConfig.Networking
{
    public class DatagramReceiver : IDatagramReceiver
    {
        private readonly IDatagramClient _listeningSocket;
        private readonly ConcurrentDictionary<IPAddress, byte> _reportedDeviceIPAddresses = new ConcurrentDictionary<IPAddress, byte>();

        private bool _isStarted = false;

        public DatagramReceiver()
        {
            _listeningSocket = new DefaultDatagramClient();
        }

        public DatagramReceiver(IDatagramClient client)
        {
            _listeningSocket = client;
        }

        public async Task ListenAsync(
            SmartConfigContext context, IPAddress localAddress, CancellationToken cancelToken)
        {
            if (_isStarted)
            {
                throw new InvalidOperationException("Already started.");
            }

            try
            {
                _isStarted = true;
                _reportedDeviceIPAddresses.Clear();
                await ListenUntilCancelledAsync(context).WithCancellation(cancelToken);
            }
            finally
            {
                _isStarted = false;
            }
        }

        public void SetupSocket(IPAddress localAddress, int listeningPort)
        {
            _listeningSocket.Bind(new IPEndPoint(localAddress, listeningPort));
        }

        private async Task ListenUntilCancelledAsync(SmartConfigContext context)
        {
            var interpreter = context.Provider.CreateDevicePacketInterpreter();
            while (true)
            {
                var result = await _listeningSocket.ReceiveAsync();
                if (result.Buffer != null && result.Buffer.Length > 0)
                {
                    var remoteAddress = result.RemoteEndPoint.Address;
                    if (interpreter.Validate(context, result.Buffer) && _reportedDeviceIPAddresses.TryAdd(remoteAddress, 0))
                    {
                        var mac = interpreter.ParseMacAddress(result.Buffer);
                        context.ReportDevice(new SmartConfigDevice(mac, remoteAddress));
                    }
                }
            }
        }

        public void Close()
        {
            Dispose();
        }

        #region IDisposable Support

        private bool _isDisposed = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _listeningSocket.Dispose();
                }
                _isDisposed = true;
            }
        }

        ~DatagramReceiver()
        {
            Dispose(false);
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            if (_isStarted)
            {
                throw new InvalidOperationException("Already started.");
            }
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }
}
