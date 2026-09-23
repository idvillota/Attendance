using System.Buffers.Binary;
using System.Globalization;
using System.Text;

namespace Attendance.Core.Device;

public sealed class DeviceUser
{
    public required ushort Serial { get; init; }
    public required string Pin { get; init; }
    public required string Name { get; init; }
    public string Password { get; init; } = string.Empty;
    public byte Privilege { get; init; }
    public uint Card { get; init; }
    public byte Group { get; init; } = 1;
    public bool IsEnabled => (Privilege & 0x01) == 0;
    public bool HasPassword => Password.Length > 0;
}

public sealed record DeviceUserWriteResult(ushort Serial, string Pin, string Password, bool Created);

public sealed record DeviceEnrollResult(bool Succeeded, string Message);

public sealed record DeviceUserDeleteResult(bool Succeeded, bool AlreadyAbsent, string Message);

public static class K30UserRecord
{
    public const int Size = 72;
    public const byte CommonEnabled = 0;
    public const byte VerifyPassword = 131;
    public const byte VerifyFingerprintOrPassword = 133;

    private static readonly byte[] UserTableRequest =
    [
        0x01, 0x09, 0x00, 0x05, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00
    ];

    public static byte[] TableRequest => UserTableRequest;

    public static IReadOnlyList<DeviceUser> ParseTable(byte[] payload)
    {
        var users = new List<DeviceUser>();
        if (payload.Length < Size)
        {
            return users;
        }

        var body = payload;
        if (payload.Length >= 4)
        {
            var declared = BinaryPrimitives.ReadInt32LittleEndian(payload);
            if (declared > 0 && declared % Size == 0 && declared <= payload.Length - 4)
            {
                body = payload[4..(4 + declared)];
            }
        }

        var count = body.Length / Size;
        for (var i = 0; i < count; i++)
        {
            users.Add(Parse(body.AsSpan(i * Size, Size)));
        }

        return users;
    }

    public static DeviceUser Parse(ReadOnlySpan<byte> record)
    {
        return new DeviceUser
        {
            Serial = BinaryPrimitives.ReadUInt16LittleEndian(record),
            Privilege = record[2],
            Password = ReadFixed(record.Slice(3, 8)),
            Name = ReadFixed(record.Slice(11, 24)),
            Card = BinaryPrimitives.ReadUInt32LittleEndian(record.Slice(35, 4)),
            Group = record[39],
            Pin = ReadFixed(record.Slice(48, 9))
        };
    }

    public static byte[] Pack(
        ushort serial,
        byte privilege,
        string password,
        string name,
        string pin,
        uint card = 0,
        byte group = 1)
    {
        var record = new byte[Size];
        BinaryPrimitives.WriteUInt16LittleEndian(record, serial);
        record[2] = privilege;
        WriteFixed(record.AsSpan(3, 8), password);
        WriteFixed(record.AsSpan(11, 24), ToDeviceName(name));
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(35), card);
        record[39] = group;
        WriteFixed(record.AsSpan(48, 9), pin.Trim());
        return record;
    }

    public static byte[] Pack(DeviceUser user, string? passwordOverride = null) =>
        Pack(
            user.Serial,
            user.Privilege,
            passwordOverride ?? user.Password,
            user.Name,
            user.Pin,
            user.Card,
            user.Group);

    public static byte[] VerifyModePayload(ushort serial, byte mode)
    {
        var payload = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, serial);
        payload[2] = mode;
        return payload;
    }

    public static byte[] DeleteUserPayload(ushort serial)
    {
        var payload = new byte[2];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, serial);
        return payload;
    }

    public static byte[] DeleteUserByPinPayload(string pin)
    {
        var payload = new byte[9];
        WriteFixed(payload, pin.Trim());
        return payload;
    }

    public static byte[] DeleteTemplatePayload(ushort serial, byte fingerIndex)
    {
        var payload = new byte[3];
        BinaryPrimitives.WriteUInt16LittleEndian(payload, serial);
        payload[2] = fingerIndex;
        return payload;
    }

    public static byte[] EnrollPayload(string pin, int fingerIndex = 0, byte flag = 1)
    {
        var payload = new byte[26];
        WriteFixed(payload.AsSpan(0, 24), pin.Trim());
        payload[24] = (byte)fingerIndex;
        payload[25] = flag;
        return payload;
    }

    public static int EnrollEventCode(ushort command, byte[] data)
    {
        if (data.Length >= 2)
        {
            return BinaryPrimitives.ReadUInt16LittleEndian(data);
        }

        if (data.Length >= 1)
        {
            return data[0];
        }

        return command;
    }

    public static bool IsEnrollSample(int code, ushort command) =>
        code is 0x64 or 256 || command is 256 or 2;

    public static bool IsEnrollFailed(int code) => code is 4 or 6;

    public static bool IsEnrollComplete(int code, ushort command) =>
        command is 8 || code is 8;

    public static bool IsEnrollDuplicate(int code) => code == 5;

    public static byte WithEnabled(byte privilege, bool enabled) =>
        enabled ? (byte)(privilege & 0xFE) : (byte)(privilege | 0x01);

    public static ushort NextSerial(IEnumerable<DeviceUser> users)
    {
        var max = users.Select(user => user.Serial).DefaultIfEmpty((ushort)0).Max();
        return (ushort)(max + 1);
    }

    public static string NextFreePin(IEnumerable<string> usedPins)
    {
        var taken = new HashSet<int>();
        foreach (var raw in usedPins)
        {
            if (int.TryParse((raw ?? string.Empty).Trim(), out var pin) && pin is >= 1 and <= 999_999_999)
            {
                taken.Add(pin);
            }
        }

        for (var candidate = 1; candidate <= 999_999_999; candidate++)
        {
            if (!taken.Contains(candidate))
            {
                return candidate.ToString();
            }
        }

        throw new InvalidOperationException("No hay un ID libre de 1 a 9 dígitos.");
    }

    public static string ToDeviceName(string name)
    {
        var normalized = (name ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (character < 128 && !char.IsControl(character))
            {
                builder.Append(char.ToUpperInvariant(character));
            }
        }

        var cleaned = builder.ToString().Trim();
        return cleaned.Length <= 23 ? cleaned : cleaned[..23];
    }

    public static string NewPassword()
    {
        var digits = new char[6];
        digits[0] = (char)('1' + Random.Shared.Next(9));
        for (var i = 1; i < digits.Length; i++)
        {
            digits[i] = (char)('0' + Random.Shared.Next(10));
        }

        return new string(digits);
    }

    public static bool IsValidPin(string pin) =>
        pin.Length is >= 1 and <= 9 && pin.All(char.IsDigit);

    private static string ReadFixed(ReadOnlySpan<byte> bytes)
    {
        var end = bytes.IndexOf((byte)0);
        if (end < 0)
        {
            end = bytes.Length;
        }

        return Encoding.ASCII.GetString(bytes[..end]).Trim();
    }

    private static void WriteFixed(Span<byte> target, string value)
    {
        target.Clear();
        var ascii = Encoding.ASCII.GetBytes((value ?? string.Empty).Trim());
        var length = Math.Min(ascii.Length, target.Length);
        ascii.AsSpan(0, length).CopyTo(target);
    }
}
