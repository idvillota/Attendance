using System.Buffers.Binary;
using System.Text;
using Attendance.Core.Device;
using Attendance.Core.Domain;

namespace Attendance.Tests.Unit;

public class K30AttLogParserTests
{
    [Fact]
    public void Parse_reads_user_time_verify_and_status()
    {
        var at = new DateTime(2026, 9, 19, 8, 15, 30);
        var payload = BuildPayload(BuildRecord("42", verifyMode: 1, at, status: 0));

        var punch = Assert.Single(K30AttLogParser.Parse(payload));

        Assert.Equal("42", punch.DeviceUserId);
        Assert.Equal(at, punch.PunchedAt);
        Assert.Equal(PunchDirection.In, punch.Direction);
        Assert.Equal(1, punch.VerifyMode);
        Assert.Equal(0, punch.Status);
        Assert.Equal("Huella", punch.VerifyModeLabel);
        Assert.Equal("Entrada", punch.StatusLabel);
    }

    [Fact]
    public void Parse_does_not_treat_verify_mode_as_direction()
    {
        var at = new DateTime(2026, 9, 19, 16, 2, 0);
        var payload = BuildPayload(BuildRecord("7", verifyMode: 1, at, status: 1));

        var punch = Assert.Single(K30AttLogParser.Parse(payload));

        Assert.Equal(PunchDirection.Out, punch.Direction);
        Assert.Equal("Huella", punch.VerifyModeLabel);
        Assert.Equal("Salida", punch.StatusLabel);
    }

    [Fact]
    public void Parse_keeps_unmatched_user_ids()
    {
        var at = new DateTime(2026, 9, 19, 9, 0, 0);
        var payload = BuildPayload(BuildRecord("999", verifyMode: 2, at, status: 0));

        var punch = Assert.Single(K30AttLogParser.Parse(payload));

        Assert.Equal("999", punch.DeviceUserId);
        Assert.Equal("Tarjeta", punch.VerifyModeLabel);
    }

    [Fact]
    public void Parse_accepts_records_without_size_prefix()
    {
        var at = new DateTime(2026, 9, 19, 10, 0, 0);
        var record = BuildRecord("15", verifyMode: 0, at, status: 0);

        var punch = Assert.Single(K30AttLogParser.Parse(record));

        Assert.Equal("15", punch.DeviceUserId);
        Assert.Equal("Clave", punch.VerifyModeLabel);
    }

    [Fact]
    public void Buffered_size_header_is_not_treated_as_empty_log()
    {
        var header = new byte[]
        {
            0x00, 0xB4, 0x04, 0x00, 0x00, 0xB4, 0x04, 0x00, 0x00, 0x67, 0xE4, 0xFC, 0x00
        };

        Assert.True(K30AttLogParser.TryBufferedSize(header, out var size));
        Assert.Equal(1204, size);
        Assert.Empty(K30AttLogParser.Parse(header));
    }

    private static byte[] BuildPayload(byte[] record)
    {
        var payload = new byte[4 + record.Length];
        BinaryPrimitives.WriteUInt32LittleEndian(payload, (uint)record.Length);
        record.CopyTo(payload, 4);
        return payload;
    }

    private static byte[] BuildRecord(string userId, byte verifyMode, DateTime at, byte status)
    {
        var record = new byte[K30AttLogParser.RecordSize];
        Encoding.ASCII.GetBytes(userId).CopyTo(record, 2);
        record[26] = verifyMode;
        BinaryPrimitives.WriteUInt32LittleEndian(record.AsSpan(27, 4), EncodeZkTime(at));
        record[31] = status;
        return record;
    }

    private static uint EncodeZkTime(DateTime value) =>
        (uint)(
            value.Second +
            (value.Minute * 60) +
            (value.Hour * 60 * 60) +
            ((value.Day - 1) * 24 * 60 * 60) +
            ((value.Month - 1) * 31 * 24 * 60 * 60) +
            ((value.Year - 2000) * 12 * 31 * 24 * 60 * 60));
}
