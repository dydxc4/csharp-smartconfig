using Sandwych.SmartConfig.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Sandwych.SmartConfig.Networking
{
    public class DatagramBroadcaster : IDatagramBroadcaster
    {
        private IDatagramClient _broadcastingSocket;
        private bool _isStarted = false;

        public DatagramBroadcaster() : this(new DefaultDatagramClient())
        {
        }

        public DatagramBroadcaster(IDatagramClient client)
        {
            _broadcastingSocket = client;
        }

        public async Task BroadcastAsync(SmartConfigContext context, SmartConfigArguments args, CancellationToken cancelToken)
        {
            if (_isStarted)
            {
                throw new InvalidOperationException("Already started");
            }

            try
            {
                _isStarted = true;

                var targetPort = context.GetOption<int>(StandardOptionNames.BroadcastingTargetPort);
                _broadcastingSocket.SetDefaultTarget(new IPEndPoint(IPAddress.Broadcast, targetPort));                
                _broadcastingSocket.Bind(new IPEndPoint(args.LocalAddress, 0));
                
                var encoder = context.Provider.CreateProcedureEncoder();
                var segments = encoder.Encode(context, args);
                var broadcastBuffer = CreateBroadcastBuffer(segments.SelectMany(x => x.Frames));

                await BroadcastProcedureAsync(context, segments, broadcastBuffer, cancelToken);
            }
            finally
            {
                _isStarted = false;
            }
        }

        private async Task BroadcastProcedureAsync(
            SmartConfigContext context,
            IEnumerable<Segment> segments,
            byte[] broadcastBuffer,
            CancellationToken userCancelToken)
        {
            var segmentInterval = context.GetOption<TimeSpan>(StandardOptionNames.SegmentInterval);
            while (true)
            {
                userCancelToken.ThrowIfCancellationRequested();

                foreach (var segment in segments)
                {
                    userCancelToken.ThrowIfCancellationRequested();

                    if (segment.BroadcastingMaxTimes > 0)
                    {
                        await BroadcastSegmentByTimesAsync(context, segment, broadcastBuffer, userCancelToken);
                    }
                    else
                    {
                        await BroadcastSegmentUntilAsync(
                            context, segment, broadcastBuffer, userCancelToken);
                    }
                    if (segmentInterval > TimeSpan.Zero)
                    {
                        await Task.Delay(segmentInterval, userCancelToken);
                    }
                }

                if (segmentInterval > TimeSpan.Zero)
                {
                    await Task.Delay(segmentInterval, userCancelToken);
                }
            }
        }

        private async Task BroadcastSegmentUntilAsync(
            SmartConfigContext context, Segment segment, byte[] broadcastBuffer, CancellationToken token)
        {
            var segmentInterval = context.GetOption<TimeSpan>(StandardOptionNames.SegmentInterval);

            var endTime = segment.BroadcastingPeriod < TimeSpan.MaxValue ? // Overflow if BroadcastingPeriod is close to Timespan.MaxValue
                TimeSpan.FromMilliseconds(Environment.TickCount) + segment.BroadcastingPeriod :
                TimeSpan.MaxValue;

            while ((TimeSpan.FromMilliseconds(Environment.TickCount) <= endTime) && !token.IsCancellationRequested)
            {
                await BroadcastSingleSegmentAsync(segment, broadcastBuffer, segmentInterval, token);
            }
        }

        private async Task BroadcastSegmentByTimesAsync(
            SmartConfigContext context, Segment segment, byte[] broadcastBuffer, CancellationToken token)
        {
            var segmentInterval = context.GetOption<TimeSpan>(StandardOptionNames.FrameInterval);
            for (int i = 0; i < segment.BroadcastingMaxTimes; i++)
            {
                token.ThrowIfCancellationRequested();
                await BroadcastSingleSegmentAsync(segment, broadcastBuffer, segmentInterval, token);
            }
        }

        private async Task BroadcastSingleSegmentAsync(
            Segment segment, byte[] broadcastBuffer, TimeSpan segmentInterval, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            foreach (var frame in segment.Frames)
            {
                token.ThrowIfCancellationRequested();
                await _broadcastingSocket.SendAsync(broadcastBuffer, frame);
                if (segment.FrameInterval > TimeSpan.Zero)
                {
                    await Task.Delay(segment.FrameInterval, token);
                }
            }
            if (segmentInterval > TimeSpan.Zero)
            {
                await Task.Delay(segmentInterval, token);
            }
        }

        public byte[] CreateBroadcastBuffer(IEnumerable<ushort> frames)
        {
            var maxLength = frames.Max();
            var bytes = new byte[maxLength];
            
            return bytes.ToArray();
        }

        #region IDisposable Support
        private bool _isDisposed = false; // To detect redundant calls

        public void Close()
        {
            Dispose();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_isDisposed)
            {
                if (disposing)
                {
                    _broadcastingSocket.Dispose();
                }
                _isDisposed = true;
            }
        }

        ~DatagramBroadcaster()
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
