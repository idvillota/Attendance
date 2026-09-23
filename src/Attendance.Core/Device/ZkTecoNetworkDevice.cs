using System.Buffers.Binary;
using System.Net.Sockets;

namespace Attendance.Core.Device;

/// <summary>
/// ZKTeco standalone protocol (TCP 4370), used by K30 and similar clocks.
/// Pulls ATTLOG with CMD_DATA_WRRQ — CMD_ATTLOG_RRQ is not accepted by this firmware.
/// </summary>
public sealed partial class ZkTecoNetworkDevice : IAttendanceDevice
{
    private const ushort CmdConnect = 1000;
    private const ushort CmdExit = 1001;
    private const ushort CmdEnableDevice = 1002;
    private const ushort CmdDisableDevice = 1003;
    private const ushort CmdAckOk = 2000;
    private const ushort CmdPrepareData = 1500;
    private const ushort CmdData = 1501;
    private const ushort CmdFreeData = 1502;
    private const ushort CmdDataWrrq = 1503;
    private const ushort CmdDataRdy = 1504;
    private const int HeaderSize = 8;
    private const int MaxPayload = 10_000_000;

    private static readonly byte[] AttLogRequest =
    [
        0x01, 0x0D, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    ];

    private readonly string _ip;
    private readonly int _port;
    private readonly TimeSpan _timeout;

    public ZkTecoNetworkDevice(string ip, int port, TimeSpan? timeout = null)
    {
        _ip = ip;
        _port = port;
        _timeout = timeout ?? TimeSpan.FromSeconds(8);
    }

    public string Name => $"ZKTeco K30 ({_ip}:{_port})";

    public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        using var session = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        await session.SendCommandAsync(CmdExit, [], cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DevicePunch>> DownloadLogsAsync(CancellationToken cancellationToken = default)
    {
        var payload = await DownloadRawAttLogAsync(cancellationToken).ConfigureAwait(false);
        return K30AttLogParser.Parse(payload);
    }

    public async Task<byte[]> DownloadRawAttLogAsync(CancellationToken cancellationToken = default)
    {
        using var session = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        var disabled = false;
        try
        {
            var disable = await session.SendCommandAsync(CmdDisableDevice, [], cancellationToken).ConfigureAwait(false);
            if (disable.Command != CmdAckOk)
            {
                throw new InvalidOperationException(
                    $"No se pudo deshabilitar el K30 para leer marcaciones (código {disable.Command}).");
            }

            disabled = true;
            return await session.ReadAttLogAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (disabled)
            {
                try
                {
                    await session.SendCommandAsync(CmdEnableDevice, [], cancellationToken).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Re-enable is best-effort so disconnect still runs.
                }
            }

            try
            {
                await session.SendCommandAsync(CmdExit, [], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Device may already have closed the session.
            }
        }
    }

    private async Task<ZkSession> ConnectAsync(CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);
        try
        {
            await client.ConnectAsync(_ip, _port, timeoutCts.Token).ConfigureAwait(false);
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            client.Dispose();
            throw new InvalidOperationException(
                $"No se pudo conectar al K30 en {_ip}:{_port}. Verifique que la PC y el reloj estén en la misma red.",
                ex);
        }

        var session = new ZkSession(client, _timeout);
        var reply = await session.SendCommandAsync(CmdConnect, [], cancellationToken).ConfigureAwait(false);
        if (reply.Command is not CmdAckOk and not CmdConnect)
        {
            session.Dispose();
            throw new InvalidOperationException($"El K30 rechazó la conexión (código {reply.Command}).");
        }

        session.SessionId = reply.SessionId;
        return session;
    }

    private sealed class ZkSession : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly TimeSpan _timeout;
        private ushort _replyId;
        private bool _disposed;

        public ZkSession(TcpClient client, TimeSpan timeout)
        {
            _client = client;
            _stream = client.GetStream();
            _timeout = timeout;
        }

        public ushort SessionId { get; set; }

        public async Task<ZkReply> SendCommandAsync(
            ushort command,
            byte[] data,
            CancellationToken cancellationToken,
            TimeSpan? timeout = null)
        {
            var packet = WrapTcp(BuildPacket(command, data, SessionId, _replyId));
            await _stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
            var reply = await ReadReplyAsync(cancellationToken, timeout).ConfigureAwait(false);
            _replyId = unchecked((ushort)(reply.ReplyId + 1));
            return reply;
        }

        public Task<byte[]> ReadAttLogAsync(CancellationToken cancellationToken) =>
            ReadDatasetAsync(AttLogRequest, "marcaciones", cancellationToken);

        public async Task<byte[]> ReadDatasetAsync(byte[] request, string label, CancellationToken cancellationToken)
        {
            var reply = await SendCommandAsync(CmdDataWrrq, request, cancellationToken).ConfigureAwait(false);
            if (reply.Command == CmdAckOk && reply.Data.Length == 0)
            {
                return [];
            }

            if (reply.Command == CmdAckOk && K30AttLogParser.TryBufferedSize(reply.Data, out var buffered))
            {
                return await ReadBufferedDatasetAsync(buffered, label, cancellationToken).ConfigureAwait(false);
            }

            if (reply.Command == CmdPrepareData && reply.Data.Length >= 4)
            {
                var size = BinaryPrimitives.ReadInt32LittleEndian(reply.Data);
                if (size < 0 || size > MaxPayload)
                {
                    throw new InvalidOperationException($"El K30 anunció {label} inválidos ({size} bytes).");
                }

                return await ReadPreparedDataAsync(size, cancellationToken).ConfigureAwait(false);
            }

            if (reply.Command is CmdData or CmdAckOk)
            {
                return reply.Data;
            }

            throw new InvalidOperationException(
                $"El K30 no devolvió {label} (código {reply.Command}). Pruebe importar un CSV.");
        }

        private async Task<byte[]> ReadBufferedDatasetAsync(int size, string label, CancellationToken cancellationToken)
        {
            var ready = new byte[8];
            BinaryPrimitives.WriteInt32LittleEndian(ready.AsSpan(4), size);
            var reply = await SendCommandAsync(CmdDataRdy, ready, cancellationToken).ConfigureAwait(false);
            byte[] body;
            if (reply.Command == CmdPrepareData && reply.Data.Length >= 4)
            {
                var announced = BinaryPrimitives.ReadInt32LittleEndian(reply.Data);
                if (announced <= 0 || announced > MaxPayload)
                {
                    throw new InvalidOperationException($"El K30 anunció {label} inválidos ({announced} bytes).");
                }

                body = await ReadPreparedDataAsync(announced, cancellationToken).ConfigureAwait(false);
                try
                {
                    await ReadReplyAsync(cancellationToken, TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                }
                catch (Exception)
                {
                    // Trailing ACK is optional if the last DATA packet already filled the buffer.
                }
            }
            else if (reply.Command is CmdData or CmdAckOk)
            {
                body = reply.Data;
            }
            else
            {
                throw new InvalidOperationException($"El K30 no envió {label} en bloque (código {reply.Command}).");
            }

            try
            {
                await SendCommandAsync(CmdFreeData, [], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Buffer release is best-effort.
            }

            return body;
        }

        public async Task<byte[]> ReadPreparedDataAsync(int size, CancellationToken cancellationToken)
        {
            var buffer = new MemoryStream(size);
            while (buffer.Length < size)
            {
                var chunk = await ReadReplyAsync(cancellationToken, null).ConfigureAwait(false);
                if (chunk.Data.Length == 0)
                {
                    break;
                }

                buffer.Write(chunk.Data, 0, chunk.Data.Length);
            }

            return buffer.ToArray();
        }

        public async Task SendAckOkAsync(CancellationToken cancellationToken)
        {
            var packet = WrapTcp(BuildPacket(CmdAckOk, [], SessionId, 0xFFFE));
            await _stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
        }

        public Task<ZkReply> ReadRealtimeAsync(CancellationToken cancellationToken, TimeSpan timeout) =>
            ReadReplyAsync(cancellationToken, timeout);

        private async Task<ZkReply> ReadReplyAsync(CancellationToken cancellationToken, TimeSpan? timeout)
        {
            var tcpHeader = await ReadExactAsync(8, cancellationToken, timeout).ConfigureAwait(false);
            if (tcpHeader[0] != 0x50 || tcpHeader[1] != 0x50 || tcpHeader[2] != 0x82 || tcpHeader[3] != 0x7D)
            {
                throw new InvalidOperationException("Respuesta inválida del K30 (header).");
            }

            var payloadSize = BinaryPrimitives.ReadInt32LittleEndian(tcpHeader.AsSpan(4));
            if (payloadSize < HeaderSize || payloadSize > MaxPayload)
            {
                throw new InvalidOperationException("Respuesta inválida del K30.");
            }

            var payload = await ReadExactAsync(payloadSize, cancellationToken, timeout).ConfigureAwait(false);
            var command = BinaryPrimitives.ReadUInt16LittleEndian(payload);
            var sessionId = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(4));
            var replyId = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(6));
            var data = payload.Length > HeaderSize ? payload[HeaderSize..] : [];
            return new ZkReply(command, sessionId, replyId, data);
        }

        private static byte[] WrapTcp(byte[] payload)
        {
            var wrapped = new byte[8 + payload.Length];
            wrapped[0] = 0x50;
            wrapped[1] = 0x50;
            wrapped[2] = 0x82;
            wrapped[3] = 0x7D;
            BinaryPrimitives.WriteInt32LittleEndian(wrapped.AsSpan(4), payload.Length);
            payload.CopyTo(wrapped, 8);
            return wrapped;
        }

        private async Task<byte[]> ReadExactAsync(int size, CancellationToken cancellationToken, TimeSpan? timeout)
        {
            var buffer = new byte[size];
            var read = 0;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout ?? _timeout);
            while (read < size)
            {
                var n = await _stream.ReadAsync(buffer.AsMemory(read, size - read), timeoutCts.Token).ConfigureAwait(false);
                if (n == 0)
                {
                    throw new InvalidOperationException("El K30 cerró la conexión al leer datos.");
                }

                read += n;
            }

            return buffer;
        }

        private static byte[] BuildPacket(ushort command, byte[] data, ushort sessionId, ushort replyId)
        {
            var packet = new byte[HeaderSize + data.Length];
            BinaryPrimitives.WriteUInt16LittleEndian(packet, command);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(4), sessionId);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(6), replyId);
            data.CopyTo(packet, HeaderSize);
            var checksum = Checksum(packet);
            BinaryPrimitives.WriteUInt16LittleEndian(packet.AsSpan(2), checksum);
            return packet;
        }

        private static ushort Checksum(byte[] payload)
        {
            uint checksum = 0;
            var offset = 0;
            while (offset + 1 < payload.Length)
            {
                if (offset != 2)
                {
                    checksum += BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset, 2));
                }

                offset += 2;
            }

            if (offset < payload.Length)
            {
                checksum += payload[offset];
            }

            checksum = (checksum & 0xFFFF) + (checksum >> 16);
            checksum = (checksum & 0xFFFF) + (checksum >> 16);
            return (ushort)((~checksum) & 0xFFFF);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _stream.Dispose();
            _client.Dispose();
        }
    }

    private readonly record struct ZkReply(ushort Command, ushort SessionId, ushort ReplyId, byte[] Data);
}
