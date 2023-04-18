using System;
using System.Collections.Generic;

namespace Sandwych.SmartConfig.Protocol
{
    public struct Segment
    {
        public IEnumerable<ushort> Frames { get; }
        public TimeSpan FrameInterval { get; }
        public TimeSpan BroadcastingPeriod { get; }
        public int BroadcastingMaxTimes { get; }

        public Segment(
            IEnumerable<ushort> frames,
            TimeSpan frameInterval,
            int broadcastingMaxTimes)
        {
            Frames = frames;
            FrameInterval = frameInterval;
            BroadcastingPeriod = TimeSpan.MaxValue;
            BroadcastingMaxTimes = broadcastingMaxTimes;
        }

        public Segment(
            IEnumerable<ushort> frames,
            TimeSpan frameInterval,
            TimeSpan broadcastingPeriod)
        {
            Frames = frames;
            FrameInterval = frameInterval;
            BroadcastingPeriod = broadcastingPeriod;
            BroadcastingMaxTimes = 0;
        }
    }
}
