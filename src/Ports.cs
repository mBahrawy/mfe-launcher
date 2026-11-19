using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace MfeLauncher
{
    static class Ports
    {
        const int AfInet = 2;
        const int AfInet6 = 23;
        const int TcpTableOwnerPidListener = 3;
        const int ErrorInsufficientBuffer = 122;

        /// <summary>Port → owning PID of every local TCP listener, IPv4 and IPv6.</summary>
        public static Dictionary<int, int> ListeningOwners()
        {
            var owners = new Dictionary<int, int>();
            // Row layouts of MIB_TCPROW_OWNER_PID and MIB_TCP6ROW_OWNER_PID.
            Read(AfInet, rowSize: 24, portOffset: 8, pidOffset: 20, owners);
            Read(AfInet6, rowSize: 56, portOffset: 20, pidOffset: 52, owners);
            return owners;
        }

        public static string Describe(int pid)
        {
            try
            {
                using (var process = Process.GetProcessById(pid)) return $"{process.ProcessName} (PID {pid})";
            }
            catch (ArgumentException)
            {
                return $"PID {pid}";
            }
        }

        static void Read(int family, int rowSize, int portOffset, int pidOffset, Dictionary<int, int> owners)
        {
            var size = 0;
            GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, TcpTableOwnerPidListener, 0);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var table = Marshal.AllocHGlobal(size);
                try
                {
                    var result = GetExtendedTcpTable(table, ref size, false, family, TcpTableOwnerPidListener, 0);
                    if (result == ErrorInsufficientBuffer) continue;
                    if (result != 0) return;

                    var count = Marshal.ReadInt32(table);
                    for (var i = 0; i < count; i++)
                    {
                        var row = IntPtr.Add(table, 4 + i * rowSize);
                        var networkPort = Marshal.ReadInt32(row, portOffset);
                        var port = ((networkPort & 0xFF) << 8) | ((networkPort >> 8) & 0xFF);
                        owners[port] = Marshal.ReadInt32(row, pidOffset);
                    }
                    return;
                }
                finally
                {
                    Marshal.FreeHGlobal(table);
                }
            }
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        static extern int GetExtendedTcpTable(IntPtr table, ref int size, bool sort, int family, int tableClass, int reserved);
    }
}
