using Attendance.Core.Auth;
using Attendance.Core.Data;
using Attendance.Core.Domain;

namespace Attendance.Tests.Unit;

public class PasswordHasherTests
{
    [Fact]
    public void Hash_verifies_and_rejects_wrong_password()
    {
        var hash = PasswordHasher.Hash("secret");
        Assert.True(PasswordHasher.Verify("secret", hash));
        Assert.False(PasswordHasher.Verify("other", hash));
        Assert.NotEqual(hash, PasswordHasher.Hash("secret"));
    }
}

public class AuthServiceTests
{
    [Fact]
    public void Admin_sees_all_people_basic_sees_only_self()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attendance-auth-{Guid.NewGuid():N}.db");
        try
        {
            var db = new AttendanceDb(path);
            db.Initialize();
            var session = new AuthSession();
            var auth = new AuthService(db, db, session);
            var store = new AuthorizedAttendanceStore(db, session);

            var admin = auth.CreateAdmin("admin", "admin123");
            Assert.True(admin.IsAdmin);
            Assert.True(store.GetEmployees().Count >= 5);

            var ana = db.GetEmployees().Single(employee => employee.Name.StartsWith("Ana"));
            var basic = auth.CreateUser("ana", "ana123", AppRole.Basic, ana.Id);
            session.SignIn(basic);

            var visible = store.GetEmployees();
            Assert.Equal(ana.Id, Assert.Single(visible).Id);
            Assert.All(store.GetPunches(), punch => Assert.Equal(ana.Id, punch.EmployeeId));
            Assert.Throws<UnauthorizedAccessException>(() => store.AddEmployee("X", ana.RoleId, "99"));
            Assert.Throws<UnauthorizedAccessException>(() => store.GetEmployeeDeletePreview(ana.Id));
            Assert.Throws<UnauthorizedAccessException>(() => store.DeleteEmployee(ana.Id));
            Assert.Throws<UnauthorizedAccessException>(() => store.DeletePunch(1));
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
    public void Rejects_bad_password_and_protects_last_admin()
    {
        var path = Path.Combine(Path.GetTempPath(), $"attendance-auth-{Guid.NewGuid():N}.db");
        try
        {
            var db = new AttendanceDb(path);
            db.Initialize();
            var session = new AuthSession();
            var auth = new AuthService(db, db, session);
            var admin = auth.CreateAdmin("admin", "admin123");

            Assert.Throws<InvalidOperationException>(() => auth.SignIn("admin", "nope"));

            var ex = Assert.Throws<InvalidOperationException>(() =>
                auth.UpdateUser(admin.Id, "admin", null, AppRole.Basic, db.GetEmployees()[0].Id, true));
            Assert.Contains("administrador", ex.Message, StringComparison.OrdinalIgnoreCase);
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
