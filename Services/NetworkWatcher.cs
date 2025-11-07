using Microsoft.Win32;
using NetworkWatcher.Models;
using System;
using System.Linq;
using System.Net.NetworkInformation;
using System.Net.Sockets;

namespace NetworkWatcher.Services
{
    public class NetworkWatcherService
    {
        public event EventHandler<NetEvent> NetworkChanged;
        public event EventHandler<bool> ServiceStatusChanged;

        private string _lastAdapterId;
        private string _lastAdapterType;
        private string _lastIpv4;
        private string _lastSsid;
        private bool _lastInternet;
        private bool _isRunning;

        public void Start()
        {
            if (_isRunning) return;
            NetworkChange.NetworkAvailabilityChanged += OnAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += OnAddressChanged;

            _isRunning = true;
            ServiceStatusChanged?.Invoke(this, true);

            PrintEvent("network.init"); // boshlang'ich holatini ko'rsata olishim uchun
        }

        public void Stop()
        {
            if (!_isRunning) return;

            NetworkChange.NetworkAvailabilityChanged -= OnAvailabilityChanged;
            NetworkChange.NetworkAddressChanged -= OnAddressChanged;

            _isRunning = false;
            ServiceStatusChanged?.Invoke(this, false);
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
                        Internet = false,
                        Vpn = CheckVpnActive(_lastSsid)
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
                    Internet = internet,
                    Vpn = CheckVpnActive(_lastSsid)
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
            var info = WifiInfoModule.GetConnectedWifi();
            if (info == null)
            {
                Console.WriteLine("Wifi ssid ni olib bo'lmadi");
                return null;
            }
            else
            {
                Console.WriteLine($"SSID = {info.SSID}");
                return info.SSID;
            }
        }

        private bool CheckVpnActive(string ssid)
        {
            bool CheckIsInActiveInterface = NetworkInterface.GetAllNetworkInterfaces()
                .Any(nic =>
                    nic.OperationalStatus == OperationalStatus.Up &&
                    (nic.Description.ToLower().Contains("vpn") ||
                     nic.Name.ToLower().Contains("vpn")));

            if (CheckIsInActiveInterface)
            {
                Console.WriteLine($"1-shartga tushdi: {CheckIsInActiveInterface}");
                return true;
            }
            else if (!string.IsNullOrEmpty(ssid) && CheckGPS() == false)
            {
                Console.WriteLine($"2-shartga tushdi, a={string.IsNullOrEmpty(ssid)}, ");
                return true;
            }
            Console.WriteLine("Hech qaysi shartga tushmadi");
            return false;

        }

        private bool CheckGPS()
        {
            const string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\location";
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
            using (RegistryKey key = baseKey.OpenSubKey(keyPath))
            {
                if (key != null)
                {
                    string value = key.GetValue("Value")?.ToString() ?? "Unknown";
                    if(value == "Deny")
                    {
                        Console.WriteLine(value);
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
            }
            return false;
        }

    }
}
