using System;

namespace NetworkWatcher.Models
{
    public class NetEvent
    {
        public string Type { get; set; }        // "network.changed"
        public string Adapter { get; set; }     // "Wi-Fi"
        public string Ssid { get; set; }        // "OfficeNet"
        public string Ipv4 { get; set; }        // "10.0.0.25"
        public bool Internet { get; set; }      // true/false
    }
}
