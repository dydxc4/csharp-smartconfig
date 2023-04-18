using System;
using System.Collections.Generic;

namespace Sandwych.SmartConfig
{
    public class SmartConfigContext
    {
        public IDictionary<string, object> Options { get; } = new Dictionary<string, object>();

        public T GetOption<T>(string name) => (T)Options[name];

        public void SetOption<T>(string name, T value)
        {
            Options[name] = value ?? throw new ArgumentNullException(nameof(value));
        }

        public ISmartConfigProvider Provider { get; internal set; }

        public SmartConfigContext(ISmartConfigProvider provider)
        {
            Provider = provider;
        }

        public event EventHandler<DeviceDiscoveredEventArgs>? DeviceDiscoveredEvent;

        public void ReportDevice(ISmartConfigDevice device)
        {
            DeviceDiscoveredEvent?.Invoke(this, new DeviceDiscoveredEventArgs(device));
        }
    }
}
