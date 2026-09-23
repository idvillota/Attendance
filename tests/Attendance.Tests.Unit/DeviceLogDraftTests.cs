using Attendance.Core.Data;
using Attendance.Core.Device;
using Attendance.Core.Domain;

namespace Attendance.Tests.Unit;

public class DeviceLogDraftTests
{
    [Fact]
    public void Preview_pairs_matched_checkpoints_and_keeps_unmatched_visible()
    {
        var draft = new DeviceLogDraft();
        var ana = new Employee { Id = 1, Name = "Ana", DeviceUserId = "10", IsActive = true };
        draft.Replace(
            [
                Punch("10", "2026-09-14T08:00:00", "k1"),
                Punch("10", "2026-09-14T16:00:00", "k2"),
                Punch("1138", "2026-09-14T09:00:00", "k3")
            ],
            [ana],
            "K30",
            PunchSource.Device);

        var preview = draft.Preview();

        Assert.Equal(2, preview.MatchedCount);
        Assert.Equal(1, preview.UnmatchedCount);
        Assert.Equal(8m, Assert.Single(preview.Shifts).Hours);
        Assert.Equal("(sin persona)", Assert.Single(draft.Items, item => item.DeviceUserId == "1138").EmployeeName);
    }

    [Fact]
    public void Save_skips_unmatched_and_preserves_device_key()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attendance-draft-{Guid.NewGuid():N}.db");
        try
        {
            var db = new AttendanceDb(path);
            db.Initialize();
            var role = db.GetRoles().First();
            var employee = db.AddEmployee("Ana", role.Id, "10");

            var draft = new DeviceLogDraft();
            draft.Replace(
                [
                    Punch("10", "2026-09-14T08:00:00", "k30:10:20260914080000"),
                    Punch("1138", "2026-09-14T09:00:00", "k30:1138:20260914090000")
                ],
                [employee],
                "K30",
                PunchSource.Device);

            var inserted = draft.Save(db);

            Assert.Equal(1, inserted);
            var stored = Assert.Single(db.GetPunches(), punch => punch.DeviceKey == "k30:10:20260914080000");
            Assert.Equal(PunchDirection.Unknown, stored.Direction);
            Assert.Equal(0, draft.Save(db));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Remove_drops_double_tap_from_preview()
    {
        var draft = new DeviceLogDraft();
        var ana = new Employee { Id = 1, Name = "Ana", DeviceUserId = "10", IsActive = true };
        draft.Replace(
            [
                Punch("10", "2026-09-14T08:00:00", "k1"),
                Punch("10", "2026-09-14T08:30:00", "k2"),
                Punch("10", "2026-09-14T16:00:00", "k3")
            ],
            [ana],
            "K30",
            PunchSource.Device);

        Assert.Equal(0.5m, Assert.Single(draft.Preview().Shifts).Hours);

        draft.Remove(draft.Items[1]);
        var preview = draft.Preview();

        Assert.Equal(8m, Assert.Single(preview.Shifts).Hours);
        Assert.Equal(2, draft.Items.Count);
        Assert.Equal(2, draft.Items[1].RecordNumber);
    }

    [Fact]
    public void Date_range_filters_preview_and_save()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attendance-range-{Guid.NewGuid():N}.db");
        try
        {
            var db = new AttendanceDb(path);
            db.Initialize();
            var role = db.GetRoles().First();
            var employee = db.AddEmployee("Ana", role.Id, "10");
            var draft = new DeviceLogDraft();
            draft.Replace(
                [
                    Punch("10", "2000-01-01T00:05:00", "old"),
                    Punch("10", "2026-09-14T08:00:00", "in"),
                    Punch("10", "2026-09-14T16:00:00", "out"),
                    Punch("10", "2026-09-22T09:00:00", "later")
                ],
                [employee],
                "K30",
                PunchSource.Device);

            var week = draft.Preview(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20));
            Assert.Equal(2, week.MatchedCount);
            Assert.Equal(8m, Assert.Single(week.Shifts).Hours);
            Assert.Equal(2, draft.ItemsInRange(new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20)).Count);
            Assert.Empty(draft.ItemsInRange(new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 14)));

            var inserted = draft.Save(db, new DateOnly(2026, 9, 14), new DateOnly(2026, 9, 20));
            Assert.Equal(2, inserted);
            Assert.DoesNotContain(db.GetPunches(), punch => punch.DeviceKey == "old");
            Assert.DoesNotContain(db.GetPunches(), punch => punch.DeviceKey == "later");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    private static DevicePunch Punch(string deviceUserId, string iso, string key) => new()
    {
        DeviceUserId = deviceUserId,
        PunchedAt = DateTime.Parse(iso),
        Direction = PunchDirection.In,
        Status = 0,
        VerifyMode = 1,
        DeviceKey = key
    };
}
