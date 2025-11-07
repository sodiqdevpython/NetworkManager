using System;
using System.Runtime.InteropServices;
using System.Text;

namespace NetworkWatcher.Services
{
    public class WifiInfo
    {
        public string SSID { get; set; }
    }

    // Public wrapper class to get currently connected WiFi information
    public static class WifiInfoModule
    {
        public static WifiInfo GetConnectedWifi()
        {
            IntPtr clientHandle = IntPtr.Zero;
            uint negotiatedVersion = 0;
            uint result = WlanOpenHandle(2, IntPtr.Zero, out negotiatedVersion, out clientHandle);
            if (result != 0)
                throw new InvalidOperationException($"WlanOpenHandle failed with error {result}");

            try
            {
                IntPtr ifaceListPtr;
                result = WlanEnumInterfaces(clientHandle, IntPtr.Zero, out ifaceListPtr);
                if (result != 0)
                    throw new InvalidOperationException($"WlanEnumInterfaces failed with error {result}");

                WLAN_INTERFACE_INFO_LIST ifaceList = Marshal.PtrToStructure<WLAN_INTERFACE_INFO_LIST>(ifaceListPtr);
                int ifaceCount = ifaceList.NumberOfItems;
                long current = ifaceListPtr.ToInt64() + Marshal.OffsetOf<WLAN_INTERFACE_INFO_LIST>("InterfaceInfo").ToInt64();

                for (int i = 0; i < ifaceCount; i++)
                {
                    IntPtr pItem = new IntPtr(current + i * Marshal.SizeOf<WLAN_INTERFACE_INFO>());
                    WLAN_INTERFACE_INFO iface = Marshal.PtrToStructure<WLAN_INTERFACE_INFO>(pItem);

                    // Query connection attributes
                    IntPtr connAttrPtr;
                    uint dataSize = 0;
                    WLAN_OPCODE_VALUE_TYPE opcode;
                    result = WlanQueryInterface(clientHandle, ref iface.InterfaceGuid,
                     WLAN_INTF_OPCODE.wlan_intf_opcode_current_connection,
                     IntPtr.Zero, out dataSize, out connAttrPtr, out opcode);

                    if (result != 0)
                        continue; // interface might be disconnected

                    WLAN_CONNECTION_ATTRIBUTES connAttr = Marshal.PtrToStructure<WLAN_CONNECTION_ATTRIBUTES>(connAttrPtr);

                    // Only care about connected
                    if (connAttr.isState == WLAN_INTERFACE_STATE.wlan_interface_state_connected)
                    {
                        WifiInfo info = new WifiInfo();
                        // SSID
                        DOT11_SSID ssid = connAttr.wlanAssociationAttributes.dot11Ssid;
                        info.SSID = Encoding.ASCII.GetString(ssid.ucSSID, 0, (int)ssid.uSSIDLength);

                        WlanFreeMemory(connAttrPtr);
                        WlanFreeMemory(ifaceListPtr);
                        return info;
                    }

                    WlanFreeMemory(connAttrPtr);
                }

                WlanFreeMemory(ifaceListPtr);
            }
            finally
            {
                WlanCloseHandle(clientHandle, IntPtr.Zero);
            }

            return null; // no connected wifi found
        }

        #region PInvoke and Native Structures

        private const string WLANAPI = "wlanapi.dll";

        [DllImport(WLANAPI, SetLastError = true)]
        private static extern uint WlanOpenHandle(
            uint dwClientVersion,
            IntPtr pReserved,
            out uint pdwNegotiatedVersion,
            out IntPtr phClientHandle);

        [DllImport(WLANAPI, SetLastError = true)]
        private static extern uint WlanCloseHandle(
            IntPtr hClientHandle,
            IntPtr pReserved);

        [DllImport(WLANAPI, SetLastError = true)]
        private static extern uint WlanEnumInterfaces(
            IntPtr hClientHandle,
            IntPtr pReserved,
            out IntPtr ppInterfaceList);

        [DllImport(WLANAPI, SetLastError = true)]
        private static extern void WlanFreeMemory(IntPtr pMemory);

        [DllImport("wlanapi.dll", SetLastError = true, CallingConvention = CallingConvention.Winapi)]
        private static extern uint WlanQueryInterface(
    IntPtr hClientHandle,
    [In] ref Guid pInterfaceGuid,
    WLAN_INTF_OPCODE OpCode,
    IntPtr pReserved,
    out uint pdwDataSize,
    out IntPtr ppData,
    out WLAN_OPCODE_VALUE_TYPE pWlanOpcodeValueType
);

        private enum WLAN_INTF_OPCODE
        {
            wlan_intf_opcode_current_connection = 7
        }

        private enum WLAN_OPCODE_VALUE_TYPE
        {
            wlan_opcode_value_type_query_only = 0,
            wlan_opcode_value_type_set_by_group_policy,
            wlan_opcode_value_type_set_by_user,
            wlan_opcode_value_type_invalid
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_INTERFACE_INFO_LIST
        {
            public Int32 NumberOfItems;
            public Int32 Index;
            // follow by WLAN_INTERFACE_INFO[NumberOfItems]
            public WLAN_INTERFACE_INFO InterfaceInfo;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_INTERFACE_INFO
        {
            public Guid InterfaceGuid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string InterfaceDescription;
            public WLAN_INTERFACE_STATE isState;
        }

        private enum WLAN_INTERFACE_STATE
        {
            wlan_interface_state_not_ready = 0,
            wlan_interface_state_connected = 1,
            wlan_interface_state_ad_hoc_network_formed,
            wlan_interface_state_disconnecting,
            wlan_interface_state_disconnected,
            wlan_interface_state_associating,
            wlan_interface_state_discovering,
            wlan_interface_state_authenticating
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DOT11_SSID
        {
            public uint uSSIDLength;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] ucSSID;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_ASSOCIATION_ATTRIBUTES
        {
            public DOT11_SSID dot11Ssid;
            public uint dot11BssType;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 6)]
            public byte[] dot11Bssid;
            public uint dot11PhyType;
            public uint dot11PhyIndex;
            public uint wlanSignalQuality; // 0-100
            public uint ulRxRate; // in 100 kbps
            public uint ulTxRate; // in 100 kbps
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DOT11_AUTH_ALGORITHM
        {
            // placeholder if needed
            public uint dummy;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WLAN_SECURITY_ATTRIBUTES
        {
            [MarshalAs(UnmanagedType.Bool)]
            public bool bSecurityEnabled;
            [MarshalAs(UnmanagedType.Bool)]
            public bool bOneXEnabled;
            public DOT11_AUTH_ALGORITHM dot11AuthAlgorithm; // actually an enum - we'll print as number/string
            public DOT11_CIPHER_ALGORITHM dot11CipherAlgorithm;
        }

        private enum DOT11_CIPHER_ALGORITHM : uint
        {
            DOT11_CIPHER_ALGO_NONE = 0x00,
            DOT11_CIPHER_ALGO_WEP40 = 0x01,
            DOT11_CIPHER_ALGO_TKIP = 0x02,
            DOT11_CIPHER_ALGO_CCMP = 0x04,
            DOT11_CIPHER_ALGO_WEP104 = 0x05,
            DOT11_CIPHER_ALGO_WPA_USE_GROUP = 0x100,
            DOT11_CIPHER_ALGO_RSN_USE_GROUP = 0x100,
            DOT11_CIPHER_ALGO_WEP = 0x101
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WLAN_CONNECTION_ATTRIBUTES
        {
            public WLAN_INTERFACE_STATE isState;
            public WLAN_CONNECTION_MODE wlanConnectionMode;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string strProfileName;
            public WLAN_ASSOCIATION_ATTRIBUTES wlanAssociationAttributes;
            public WLAN_SECURITY_ATTRIBUTES wlanSecurityAttributes;
        }

        private enum WLAN_CONNECTION_MODE
        {
            wlan_connection_mode_profile = 0,
            wlan_connection_mode_temporary_profile,
            wlan_connection_mode_discovery_secure,
            wlan_connection_mode_discovery_unsecure,
            wlan_connection_mode_auto,
            wlan_connection_mode_invalid
        }

        #endregion
    }
}
