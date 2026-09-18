using Attendance.Core.Domain;

namespace Attendance.Core.Auth;

public interface IUserStore
{
    int CountUsers();
    int CountActiveAdmins();
    IReadOnlyList<AppUser> GetUsers();
    StoredUser? FindByUsername(string username);
    StoredUser? GetStoredUser(long id);
    AppUser AddUser(string username, string passwordHash, AppRole role, long? employeeId);
    void UpdateUser(long id, string username, string? passwordHash, AppRole role, long? employeeId, bool isActive);
}

public interface IAuthService
{
    bool HasUsers();
    AppUser SignIn(string username, string password);
    AppUser CreateAdmin(string username, string password);
    IReadOnlyList<AppUser> GetUsers();
    AppUser CreateUser(string username, string password, AppRole role, long? employeeId);
    void UpdateUser(long id, string username, string? password, AppRole role, long? employeeId, bool isActive);
}
