using System.Net;
using System.Runtime.InteropServices;

namespace Posiflora.Recovery.Windows.Network;

public sealed class TcpConnectionReader : ITcpConnectionReader
{
    public Task<IReadOnlyList<TcpConnectionInfo>> GetConnectionsAsync(CancellationToken cancellationToken)
    {
        var connections = ReadTcpRows(cancellationToken)
            .Where(connection => !string.Equals(connection.State, "Listen", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return Task.FromResult<IReadOnlyList<TcpConnectionInfo>>(connections);
    }

    public Task<IReadOnlyList<TcpConnectionInfo>> GetListenersAsync(CancellationToken cancellationToken)
    {
        var listeners = ReadTcpRows(cancellationToken)
            .Where(connection => string.Equals(connection.State, "Listen", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return Task.FromResult<IReadOnlyList<TcpConnectionInfo>>(listeners);
    }

    private static IReadOnlyList<TcpConnectionInfo> ReadTcpRows(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var bufferSize = 0;
        var result = GetExtendedTcpTable(IntPtr.Zero, ref bufferSize, true, AddressFamilyInterNetwork, TcpTableClass.OwnerPidAll, 0);
        if (result != ErrorInsufficientBuffer)
        {
            return [];
        }

        var buffer = Marshal.AllocHGlobal(bufferSize);
        try
        {
            result = GetExtendedTcpTable(buffer, ref bufferSize, true, AddressFamilyInterNetwork, TcpTableClass.OwnerPidAll, 0);
            if (result != ErrorSuccess)
            {
                return [];
            }

            var rowCount = Marshal.ReadInt32(buffer);
            var rowPointer = IntPtr.Add(buffer, sizeof(int));
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();
            var rows = new List<TcpConnectionInfo>(rowCount);

            for (var index = 0; index < rowCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var row = Marshal.PtrToStructure<MibTcpRowOwnerPid>(IntPtr.Add(rowPointer, index * rowSize));
                rows.Add(new TcpConnectionInfo(
                    (int)row.OwningPid,
                    ConvertPort(row.LocalPort),
                    new IPAddress(row.RemoteAddr).ToString(),
                    ConvertPort(row.RemotePort),
                    ConvertState(row.State)));
            }

            return rows;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static int ConvertPort(uint port)
    {
        return (ushort)IPAddress.NetworkToHostOrder((short)port);
    }

    private static string ConvertState(uint state)
    {
        return state switch
        {
            1 => "Closed",
            2 => "Listen",
            3 => "SynSent",
            4 => "SynReceived",
            5 => "Established",
            6 => "FinWait1",
            7 => "FinWait2",
            8 => "CloseWait",
            9 => "Closing",
            10 => "LastAck",
            11 => "TimeWait",
            12 => "DeleteTcb",
            _ => $"Unknown({state})"
        };
    }

    private const int AddressFamilyInterNetwork = 2;
    private const uint ErrorSuccess = 0;
    private const uint ErrorInsufficientBuffer = 122;

    private enum TcpTableClass
    {
        OwnerPidAll = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    private readonly struct MibTcpRowOwnerPid
    {
        public readonly uint State;
        public readonly uint LocalAddr;
        public readonly uint LocalPort;
        public readonly uint RemoteAddr;
        public readonly uint RemotePort;
        public readonly uint OwningPid;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr tcpTable,
        ref int tcpTableLength,
        bool sort,
        int ipVersion,
        TcpTableClass tableClass,
        uint reserved);
}
