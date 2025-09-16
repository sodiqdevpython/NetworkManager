using NetworkWatcher.Models;
using System;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace NetworkWatcher.Services
{
    public class NetworkWatcherService
    {
        public event EventHandler<NetEvent> NetworkChanged;

        private string _lastAdapterId;
        private string _lastAdapterType;
        private string _lastIpv4;
        private string _lastSsid;
        private bool _lastInternet;

        public void Start()
        {
            NetworkChange.NetworkAvailabilityChanged += OnAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += OnAddressChanged;
        }

        private void OnAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            PrintEvent("network." + (e.IsAvailable ? "up" : "down"));
        }

        private void OnAddressChanged(object sender, EventArgs e)
        {
            PrintEvent("network.changed");
        }

        private NetworkInterface GetActiveAdapter()
        {
            var adapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(a =>
                    a.OperationalStatus == OperationalStatus.Up &&
                    a.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    !a.Description.ToLower().Contains("vmware") &&
                    !a.Description.ToLower().Contains("virtual") &&
                    !a.Description.ToLower().Contains("hyper-v") &&
                    !a.Description.ToLower().Contains("bluetooth")
                );

            foreach (var adapter in adapters)
            {
                var props = adapter.GetIPProperties();
                if (props.GatewayAddresses.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork))
                {
                    return adapter;
                }
            }
            return adapters.FirstOrDefault();
        }

        private void PrintEvent(string type)
        {
            var adapter = GetActiveAdapter();

            if (adapter == null)
            {
                if (_lastAdapterId != null)
                {
                    var downEvent = new NetEvent
                    {
                        Type = "network.down",
                        Adapter = _lastAdapterType,
                        Ssid = _lastSsid,
                        Ipv4 = _lastIpv4,
                        Internet = false
                    };
                    NetworkChanged?.Invoke(this, downEvent);
                }

                _lastAdapterId = null;
                _lastAdapterType = null;
                _lastIpv4 = null;
                _lastSsid = null;
                _lastInternet = false;
                return;
            }

            var props = adapter.GetIPProperties();
            var ipv4 = props.UnicastAddresses
                .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetwork)?.Address.ToString();

            string ssid = null;
            if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                ssid = GetWifiSsid();
            else
                ssid = adapter.Name;

            bool internet = CheckInternet();
            if (_lastAdapterId != adapter.Id || ipv4 != _lastIpv4 || ssid != _lastSsid || internet != _lastInternet || type != "network.changed")
            {
                var netEvent = new NetEvent
                {
                    Type = (_lastAdapterId == null) ? "network.up" : type,
                    Adapter = adapter.NetworkInterfaceType.ToString(),
                    Ssid = ssid,
                    Ipv4 = ipv4,
                    Internet = internet
                };

                NetworkChanged?.Invoke(this, netEvent);
            }

            _lastAdapterId = adapter.Id;
            _lastAdapterType = adapter.NetworkInterfaceType.ToString();
            _lastIpv4 = ipv4;
            _lastSsid = ssid;
            _lastInternet = internet;
        }

        // Internetni tekshirdim google dns ga ulanib ko'rib
        private bool CheckInternet()
        {
            try
            {
                using (var client = new TcpClient("8.8.8.8", 53))
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }
        }

        // wifi ssd ni olayabman wlan show interfaces dan
        private string GetWifiSsid()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "netsh",
                    Arguments = "wlan show interfaces",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var p = Process.Start(psi))
                {
                    string output = p.StandardOutput.ReadToEnd();
                    p.WaitForExit();

                    var match = Regex.Match(output, @"^\s*SSID\s*:\s*(.+)$",
                        RegexOptions.Multiline | RegexOptions.IgnoreCase);

                    if (match.Success)
                        return match.Groups[1].Value.Trim();
                }
            }
            catch
            {
                
            }

            return null;
        }
    }
}
