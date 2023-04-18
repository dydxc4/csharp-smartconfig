using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sandwych.SmartConfig.Protocol;

namespace Sandwych.SmartConfig.EspTouchV2.Protocol
{
    public sealed class EspV2ProcedureEncoder : IProcedureEncoder
    {
        public IEnumerable<Segment> Encode(SmartConfigContext context, SmartConfigArguments args)
        {
            EspV2DatumFrameEncoder encoder = new EspV2DatumFrameEncoder(context, args);
            var frameInterval = context.GetOption<TimeSpan>(StandardOptionNames.FrameInterval);
            var times = 0;
            var segments = new Segment[] 
            {
                new Segment(encoder.Encode(), frameInterval, times)
            };

            return segments;
        }
    }
}
