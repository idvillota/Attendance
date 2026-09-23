using System.Buffers.Binary;
using System.Text;
using Attendance.Core.Domain;

namespace Attendance.Core.Device;

/// <summary>
/// Parses K30 ATTLOG payloads. Layout confirmed against this device:
/// 4-byte size prefix, then 40-byte records with user id at 2, verify at 26,
/// ZK time at 27, in/out at 31.
/// </summary>
public static class K30AttLogParser
{
    public const int RecordSize = 40;

    public static bool TryBufferedSize(ReadOnlySpan<byte> data, out int size)
    {
        size = 0;
        if (data.Length < 9 || data[0] != 0)
        {
            return false;
        }

        var first = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(1, 4));
        var second = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(5, 4));
        if (first <= 0 || first != second || first > 10_000_000)
        {
            return false;
        }

        size = first;
        return true;
    }

    public static IReadOnlyList<DevicePunch> Parse(byte[] data)
    {
        var records = new List<DevicePunch>();
        if (data.Length < RecordSize)
        {
            return records;
        }

        var body = SliceRecords(data);
        var count = body.Length / RecordSize;
        for (var i = 0; i < count; i++)
        {
            var punch = ParseRecord(body.Slice(i * RecordSize, RecordSize));
            if (punch is not null)
            {
                records.Add(punch);
            }
        }

        return records;
    }

    private static ReadOnlySpan<byte> SliceRecords(byte[] data)
    {
        if (data.Length >= 4)
        {
            var declared = BinaryPrimitives.ReadUInt32LittleEndian(data);
            if (declared > 0 &&
                declared % RecordSize == 0 &&
                declared <= (uint)(data.Length - 4))
            {
                return data.AsSpan(4, (int)declared);
            }
        }

        if (data.Length % RecordSize == 0)
        {
            return data;
        }

        if (data.Length > 4 && (data.Length - 4) % RecordSize == 0)
        {
            return data.AsSpan(4);
        }

        var usable = data.Length / RecordSize * RecordSize;
        return data.AsSpan(0, usable);
    }

    private static DevicePunch? ParseRecord(ReadOnlySpan<byte> record)
    {
        var userId = ReadFixedString(record.Slice(2, 9));
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        var verifyMode = record[26];
        var time = DecodeZkTime(BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(27, 4)));
        if (time is null)
        {
            return null;
        }

        var status = record[31];
        return new DevicePunch
        {
            DeviceUserId = userId,
            PunchedAt = time.Value,
            Direction = MapStatus(status),
            VerifyMode = verifyMode,
            Status = status,
            DeviceKey = $"k30:{userId}:{time.Value:yyyyMMddHHmmss}:{status}"
        };
    }

    internal static PunchDirection MapStatus(int status) => status switch
    {
        0 or 3 or 4 => PunchDirection.In,
        1 or 2 or 5 => PunchDirection.Out,
        _ => PunchDirection.Unknown
    };

    internal static DateTime? DecodeZkTime(uint packed)
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

    private static string ReadFixedString(ReadOnlySpan<byte> bytes)
    {
        var zero = bytes.IndexOf((byte)0);
        var slice = zero < 0 ? bytes : bytes[..zero];
        return Encoding.ASCII.GetString(slice).Trim();
    }
}
