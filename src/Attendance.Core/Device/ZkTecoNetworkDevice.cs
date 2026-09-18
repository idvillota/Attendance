using System.Buffers.Binary;
using System.Net.Sockets;
using Attendance.Core.Domain;

namespace Attendance.Core.Device;

/// <summary>
/// ZKTeco standalone protocol (TCP 4370), used by K30 and similar clocks.
/// Official zkemkeeper.dll is Windows-only; this talks to the device over the LAN.
/// </summary>
public sealed class ZkTecoNetworkDevice : IAttendanceDevice
{
    private const ushort CmdConnect = 1000;
    private const ushort CmdExit = 1001;
    private const ushort CmdAckOk = 2000;
    private const ushort CmdAttLogRrq = 13;
    private const ushort CmdPrepareData = 1500;
    private const ushort CmdData = 1501;
    private const int HeaderSize = 8;

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
        await session.SendCommandAsync(CmdExit, Array.Empty<byte>(), cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<DevicePunch>> DownloadLogsAsync(CancellationToken cancellationToken = default)
    {
        using var session = await ConnectAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var payload = await session.ReadBufferedAsync(CmdAttLogRrq, cancellationToken).ConfigureAwait(false);
            return ParseAttendance(payload);
        }
        finally
        {
            try
            {
                await session.SendCommandAsync(CmdExit, Array.Empty<byte>(), cancellationToken).ConfigureAwait(false);
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
        }
        catch (Exception ex) when (ex is SocketException or OperationCanceledException)
        {
            client.Dispose();
            throw new InvalidOperationException(
                $"No se pudo conectar al K30 en {_ip}:{_port}. Verifique que la PC y el reloj estén en la misma red.",
                ex);
        }

        var session = new ZkSession(client, _timeout);
        var reply = await session.SendCommandAsync(CmdConnect, Array.Empty<byte>(), cancellationToken).ConfigureAwait(false);
        if (reply.Command is not CmdAckOk and not CmdConnect)
        {
            session.Dispose();
            throw new InvalidOperationException($"El K30 rechazó la conexión (código {reply.Command}).");
        }

        session.SessionId = reply.SessionId;
        return session;
    }

    private static IReadOnlyList<DevicePunch> ParseAttendance(byte[] data)
    {
        var list = new List<DevicePunch>();
        if (data.Length == 0)
        {
            return list;
        }

        var recordSize = GuessRecordSize(data.Length);
        for (var offset = 0; offset + recordSize <= data.Length; offset += recordSize)
        {
            var punch = recordSize >= 40
                ? ParseRecord40(data.AsSpan(offset, recordSize))
                : ParseRecord16(data.AsSpan(offset, recordSize));
            if (punch is not null)
            {
                list.Add(punch);
            }
        }

        return list;
    }

    private static int GuessRecordSize(int length)
    {
        if (length % 40 == 0)
        {
            return 40;
        }

        if (length % 16 == 0)
        {
            return 16;
        }

        return 40;
    }

    private static DevicePunch? ParseRecord40(ReadOnlySpan<byte> record)
    {
        var userId = ReadFixedString(record.Slice(2, 24));
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var status = record[26];
        var time = DecodeTime(BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(27, 4)));
        if (time is null)
        {
            return null;
        }

        return new DevicePunch
        {
            DeviceUserId = userId,
            PunchedAt = time.Value,
            Direction = MapStatus(status),
            DeviceKey = $"k30:{userId}:{time.Value:yyyyMMddHHmmss}:{status}"
        };
    }

    private static DevicePunch? ParseRecord16(ReadOnlySpan<byte> record)
    {
        var userSn = BinaryPrimitives.ReadUInt16LittleEndian(record);
        var time = DecodeTime(BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(4, 4)));
        if (time is null)
        {
            return null;
        }

        var userId = userSn.ToString();
        var status = record[8];
        return new DevicePunch
        {
            DeviceUserId = userId,
            PunchedAt = time.Value,
            Direction = MapStatus(status),
            DeviceKey = $"k30:{userId}:{time.Value:yyyyMMddHHmmss}:{status}"
        };
    }

    private static PunchDirection MapStatus(byte status) => status switch
    {
        0 => PunchDirection.In,
        1 => PunchDirection.Out,
        _ => PunchDirection.Unknown
    };

    private static string ReadFixedString(ReadOnlySpan<byte> bytes)
    {
        var zero = bytes.IndexOf((byte)0);
        var slice = zero < 0 ? bytes : bytes[..zero];
        return System.Text.Encoding.ASCII.GetString(slice).Trim();
    }

    private static DateTime? DecodeTime(uint packed)
    {
        try
        {
            var second = (int)(packed % 60);
            packed /= 60;
            var minute = (int)(packed % 60);
            packed /= 60;
            var hour = (int)(packed % 24);
            packed /= 24;
            var day = (int)(packed % 31) + 1;
            packed /= 31;
            var month = (int)(packed % 12) + 1;
            packed /= 12;
            var year = (int)packed + 2000;
            return new DateTime(year, month, day, hour, minute, second);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    private sealed class ZkSession : IDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream;
        private readonly TimeSpan _timeout;
        private ushort _replyId = 0;
        private bool _disposed;

        public ZkSession(TcpClient client, TimeSpan timeout)
        {
            _client = client;
            _stream = client.GetStream();
            _timeout = timeout;
        }

        public ushort SessionId { get; set; }

        public async Task<ZkReply> SendCommandAsync(ushort command, byte[] data, CancellationToken cancellationToken)
        {
            var packet = WrapTcp(BuildPacket(command, data, SessionId, _replyId));
            await _stream.WriteAsync(packet, cancellationToken).ConfigureAwait(false);
            var reply = await ReadReplyAsync(cancellationToken).ConfigureAwait(false);
            _replyId = (ushort)(reply.ReplyId + 1);
            return reply;
        }

        public async Task<byte[]> ReadBufferedAsync(ushort command, CancellationToken cancellationToken)
        {
            var reply = await SendCommandAsync(command, Array.Empty<byte>(), cancellationToken).ConfigureAwait(false);
            if (reply.Command == CmdAckOk && reply.Data.Length == 0)
            {
                return [];
            }

            if (reply.Command == CmdPrepareData && reply.Data.Length >= 4)
            {
                var size = BinaryPrimitives.ReadInt32LittleEndian(reply.Data);
                return await ReadExactAsync(size, cancellationToken).ConfigureAwait(false);
            }

            if (reply.Command is CmdData or CmdAckOk)
            {
                return reply.Data;
            }

            throw new InvalidOperationException(
                $"El K30 no devolvió marcaciones (código {reply.Command}). Pruebe importar un CSV o use el simulador.");
        }

        private async Task<ZkReply> ReadReplyAsync(CancellationToken cancellationToken)
        {
            var tcpHeader = await ReadExactAsync(8, cancellationToken).ConfigureAwait(false);
            var payloadSize = BinaryPrimitives.ReadInt32LittleEndian(tcpHeader.AsSpan(4));
            if (payloadSize < HeaderSize || payloadSize > 1_000_000)
            {
                throw new InvalidOperationException("Respuesta inválida del K30.");
            }

            var payload = await ReadExactAsync(payloadSize, cancellationToken).ConfigureAwait(false);
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

        private async Task<byte[]> ReadExactAsync(int size, CancellationToken cancellationToken)
        {
            var buffer = new byte[size];
            var read = 0;
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(_timeout);
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

        private static ushort Checksum(byte[] packet)
        {
            const int max = 65535;
            var chksum = 0;
            var i = 0;
            while (i + 1 < packet.Length)
            {
                chksum += packet[i] + (packet[i + 1] << 8);
                if (chksum > max)
                {
                    chksum -= max;
                }

                i += 2;
            }

            if (i < packet.Length)
            {
                chksum += packet[i];
            }

            while (chksum > max)
            {
                chksum -= max;
            }

            chksum = ~chksum;
            while (chksum < 0)
            {
                chksum += max;
            }

            return (ushort)chksum;
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
