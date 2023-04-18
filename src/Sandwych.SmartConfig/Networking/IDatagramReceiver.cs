using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Sandwych.SmartConfig.Networking
{
    public interface IDatagramReceiver : IDisposable
    {
        void SetupSocket(IPAddress localAddress, int port);

        Task ListenAsync(
            SmartConfigContext context, IPAddress localAddress, CancellationToken cancelToken);
    }
}
