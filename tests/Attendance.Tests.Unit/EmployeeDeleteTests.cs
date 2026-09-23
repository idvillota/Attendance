using Attendance.Core.Auth;
using Attendance.Core.Data;
using Attendance.Core.Domain;

namespace Attendance.Tests.Unit;

public class EmployeeDeleteTests
{
    [Fact]
    public void Deletes_person_punches_and_unlinks_basic_login()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attendance-delete-{Guid.NewGuid():N}.db");
        try
        {
            var db = new AttendanceDb(path);
            db.Initialize();
            var role = db.GetRoles().First();
            var person = db.AddEmployee("Temporal", role.Id, "8888");
            db.AddPunch(person.Id, new DateTime(2026, 9, 21, 8, 0, 0), PunchDirection.Unknown, PunchSource.Manual);
            db.AddPunch(person.Id, new DateTime(2026, 9, 21, 16, 0, 0), PunchDirection.Unknown, PunchSource.Manual);
            db.AddUser("tempuser", PasswordHasher.Hash("temp123"), AppRole.Basic, person.Id);

            var preview = db.GetEmployeeDeletePreview(person.Id);
            Assert.NotNull(preview);
            Assert.Equal(2, preview.PunchCount);
            Assert.Equal(["tempuser"], preview.LinkedUsernames);

            var leftover = db.GetPunches().Count;
            var result = db.DeleteEmployee(person.Id);
            Assert.Equal("Temporal", result.Name);
            Assert.Equal("8888", result.DeviceUserId);
            Assert.Equal(2, result.RemovedPunches);
            Assert.Equal(["tempuser"], result.UnlinkedUsernames);
            Assert.DoesNotContain(db.GetEmployees(), employee => employee.Id == person.Id);
            Assert.DoesNotContain(db.GetPunches(), punch => punch.EmployeeId == person.Id);
            Assert.Equal(leftover - 2, db.GetPunches().Count);

            var login = db.FindByUsername("tempuser");
            Assert.NotNull(login);
            Assert.Null(login.EmployeeId);
            Assert.False(login.IsActive);
            Assert.Null(db.GetEmployeeDeletePreview(person.Id));
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
    public void Admin_can_delete_and_keeps_admin_login_active()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attendance-delete-auth-{Guid.NewGuid():N}.db");
        try
        {
            var db = new AttendanceDb(path);
            db.Initialize();
            var session = new AuthSession();
            var auth = new AuthService(db, db, session);
            var store = new AuthorizedAttendanceStore(db, session);
            var admin = auth.CreateAdmin("admin", "admin123");
            session.SignIn(admin);

            var role = store.GetRoles().First();
            var person = store.AddEmployee("Corto", role.Id, "7777");
            db.UpdateUser(admin.Id, "admin", null, AppRole.Admin, person.Id, true);

            var deleted = store.DeleteEmployee(person.Id);
            Assert.Equal("Corto", deleted.Name);
            Assert.DoesNotContain(store.GetEmployees(), employee => employee.Id == person.Id);

            var login = db.GetStoredUser(admin.Id);
            Assert.NotNull(login);
            Assert.Null(login.EmployeeId);
            Assert.True(login.IsActive);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
