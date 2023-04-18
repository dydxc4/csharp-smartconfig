using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.ComponentModel;
using Sandwych.SmartConfig.Networking;
using Sandwych.SmartConfig.Util;

#if DEBUG
using System.Diagnostics;
#endif

namespace Sandwych.SmartConfig
{
    public class SmartConfigJob : ISmartConfigJob
    {
        public static TimeSpan TimeInterval { get; } = TimeSpan.FromSeconds(1);

        private bool _isStarted = false;
        private readonly IDatagramBroadcaster _broadcaster = new DatagramBroadcaster();
        private readonly IDatagramReceiver _receiver = new DatagramReceiver();

        private readonly System.Timers.Timer _timer = new System.Timers.Timer(TimeInterval.TotalMilliseconds);

        private CancellationTokenSource? _timerCts;

        public event SmartConfigTimerEventHandler? Elapsed;

        public TimeSpan Timeout { get; }
        public TimeSpan ExecutedTime { get; private set; } = TimeSpan.Zero;
        public TimeSpan LeftTime => Timeout - ExecutedTime;

        public SmartConfigJob() : this(TimeSpan.FromSeconds(60))
        {
        }

        public SmartConfigJob(TimeSpan timeout)
        {
            Timeout = timeout;

            _timer.Elapsed += Timer_Elapsed;
        }

        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            ExecutedTime = ExecutedTime.Add(TimeSpan.FromMilliseconds(_timer.Interval));
            Elapsed?.Invoke(this, new SmartConfigTimerEventArgs(Timeout, ExecutedTime));
            if (LeftTime <= TimeSpan.Zero)
            {
                if (_timer.Enabled)
                {
                    _timer.Stop();
                }

                _timerCts?.Cancel();
            }
        }

        private void SetupReceiver(SmartConfigContext context, SmartConfigArguments args)
        {
            var listeningPorts = context.GetOption<IReadOnlyList<int>>(StandardOptionNames.ListeningPorts);
            SocketException? exception = null;
            bool success = false;
            
            for (int i = 0; i < listeningPorts.Count; i++)
            {
                try
                {
                    _receiver.SetupSocket(args.LocalAddress, listeningPorts[i]);
                    context.SetOption(StandardOptionNames.SelectedListeningPortIndex, i);
                    success = true;
                    break;
                }
                catch (SocketException ex)
                {
                    exception = ex;
                }
            }
            
            if (!success && exception != null)
            {
                throw exception;
            }
        }

        public async Task ExecuteAsync(SmartConfigContext context, SmartConfigArguments args, CancellationToken externalCancelToken)
        {
            if (_isStarted)
            {
                throw new InvalidOperationException("Already started");
            }

            ExecutedTime = TimeSpan.Zero;
            _isStarted = true;
            _timerCts = new CancellationTokenSource();
            var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(externalCancelToken, _timerCts.Token);
            
            try
            {
                SetupReceiver(context, args);

                _timer.Start();
                Elapsed?.Invoke(this, new SmartConfigTimerEventArgs(Timeout, ExecutedTime));

                var broadcastingTask = _broadcaster.BroadcastAsync(context, args, linkedCts.Token).CancelOnFaulted(linkedCts);
                var receivingTask = _receiver.ListenAsync(context, args.LocalAddress, linkedCts.Token).CancelOnFaulted(linkedCts);
                await Task.WhenAll(broadcastingTask, receivingTask);
            }
            catch (OperationCanceledException ocex)
            {
                if (externalCancelToken.IsCancellationRequested)
                {
                    throw ocex;
                }
            }
            catch
            {
                linkedCts.Cancel();
                throw;
            }
            finally
            {
                if (_timer.Enabled)
                {
                    _timer.Stop();
                }

                linkedCts.Dispose();

                _timerCts.Dispose();
                _timerCts = null;

                ExecutedTime = TimeSpan.Zero;
                _isStarted = false;
            }
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
                    _timer.Dispose();
                    _receiver.Dispose();
                    _broadcaster.Dispose();
                }
                _isDisposed = true;
            }
        }

        ~SmartConfigJob()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            if (_isStarted)
            {
                throw new InvalidOperationException("Already started");
            }
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }

}
