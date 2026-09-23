using Attendance.Core.Device;

namespace Attendance.Tests.Unit;

public class K30UserRecordTests
{
    [Fact]
    public void Pack_and_parse_round_trip()
    {
        var packed = K30UserRecord.Pack(3, 0, "482193", "Lucía Rojas", "1140");
        var user = K30UserRecord.Parse(packed);

        Assert.Equal((ushort)3, user.Serial);
        Assert.Equal("1140", user.Pin);
        Assert.Equal("LUCIA ROJAS", user.Name);
        Assert.Equal("482193", user.Password);
        Assert.True(user.IsEnabled);
    }

    [Fact]
    public void ParseTable_reads_size_prefix_and_two_users()
    {
        var first = K30UserRecord.Pack(1, 14, "", "GABRIEL", "1138");
        var second = K30UserRecord.Pack(2, 14, "", "AAA", "123");
        var payload = new byte[4 + 144];
        BitConverter.GetBytes(144).CopyTo(payload, 0);
        first.CopyTo(payload, 4);
        second.CopyTo(payload, 76);

        var users = K30UserRecord.ParseTable(payload);

        Assert.Equal(2, users.Count);
        Assert.Equal("1138", users[0].Pin);
        Assert.Equal("123", users[1].Pin);
        Assert.Equal((ushort)3, K30UserRecord.NextSerial(users));
    }

    [Fact]
    public void New_password_is_six_digits_and_pin_rules()
    {
        var password = K30UserRecord.NewPassword();
        Assert.Equal(6, password.Length);
        Assert.All(password, character => Assert.True(char.IsDigit(character)));
        Assert.NotEqual('0', password[0]);
        Assert.True(K30UserRecord.IsValidPin("1138"));
        Assert.False(K30UserRecord.IsValidPin("AB"));
        Assert.False(K30UserRecord.IsValidPin("1234567890"));
    }

    [Fact]
    public void Enroll_and_verify_payloads_fit_protocol()
    {
        var enroll = K30UserRecord.EnrollPayload("1138", 0);
        Assert.Equal(26, enroll.Length);
        Assert.Equal((byte)'1', enroll[0]);
        Assert.Equal(0, enroll[24]);
        Assert.Equal(1, enroll[25]);

        var verify = K30UserRecord.VerifyModePayload(1, K30UserRecord.VerifyFingerprintOrPassword);
        Assert.Equal(24, verify.Length);
        Assert.Equal(1, verify[0]);
        Assert.Equal(K30UserRecord.VerifyFingerprintOrPassword, verify[2]);
        Assert.True(K30UserRecord.IsEnrollSample(0x64, 500));
        Assert.True(K30UserRecord.IsEnrollComplete(8, 8));
        Assert.True(K30UserRecord.IsEnrollFailed(4));
        Assert.False(K30UserRecord.IsEnrollComplete(0, 500));

        var deleteUser = K30UserRecord.DeleteUserPayload(1138);
        Assert.Equal(2, deleteUser.Length);
        Assert.Equal(1138, BitConverter.ToUInt16(deleteUser, 0));
        var deletePin = K30UserRecord.DeleteUserByPinPayload("1138");
        Assert.Equal(9, deletePin.Length);
        Assert.Equal((byte)'1', deletePin[0]);
        Assert.Equal(0, deletePin[4]);
        var deleteFinger = K30UserRecord.DeleteTemplatePayload(3, 2);
        Assert.Equal(3, deleteFinger.Length);
        Assert.Equal(3, deleteFinger[0]);
        Assert.Equal(2, deleteFinger[2]);
    }

    [Fact]
    public void Next_free_pin_is_the_smallest_unused_positive_id()
    {
        Assert.Equal("1", K30UserRecord.NextFreePin([]));
        Assert.Equal("3", K30UserRecord.NextFreePin(["1", "2", "5", "123", "1138"]));
        Assert.Equal("6", K30UserRecord.NextFreePin(["1", "2", "3", "4", "5", "123"]));
        Assert.Equal("1", K30UserRecord.NextFreePin(["abc", "0", "-2"]));
    }
}
