using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Sandwych.SmartConfig.Protocol;
using Sandwych.SmartConfig.EspTouchV2.Protocol;

namespace Sandwych.SmartConfig.EspTouchV2
{
    public class EspV2SmartConfigProvider : AbstractSmartConfigProvider
    {
        public override string Name => "EspTouchV2";

        public override IDevicePacketInterpreter CreateDevicePacketInterpreter()
            => new EspV2DevicePacketInterpreter();

        public override IProcedureEncoder CreateProcedureEncoder()
            => new EspV2ProcedureEncoder();

        public override IEnumerable<(string key, object value)> GetDefaultOptions()
        {
            yield return (StandardOptionNames.BroadcastingTargetPort, 7001);
            yield return (StandardOptionNames.ListeningPorts, new int[] { 18266, 28266, 38266, 48266 });
            yield return (StandardOptionNames.SegmentInterval, TimeSpan.Zero);
            yield return (StandardOptionNames.FrameInterval, TimeSpan.FromMilliseconds(15));
            yield return (StandardOptionNames.GuideCodeTimeout, TimeSpan.Zero);
            yield return (EspV2OptionNames.SSIDLengthMax, 32);
            yield return (EspV2OptionNames.PasswordLengthMin, 8);
            yield return (EspV2OptionNames.PasswordLengthMax, 64);
            yield return (EspV2OptionNames.AESKeyLength, 16);
            yield return (EspV2OptionNames.ReservedDataLengthMax, 64);
        }
    }
}