using Attendance.Core.Domain;

namespace Attendance.Core.Auth;

public interface IAuthSession
{
    AppUser? User { get; }
    bool IsAuthenticated { get; }
    bool IsAdmin { get; }
    long? EmployeeId { get; }
    void SignIn(AppUser user);
    void SignOut();
}

public sealed class AuthSession : IAuthSession
{
    public AppUser? User { get; private set; }
    public bool IsAuthenticated => User is not null;
    public bool IsAdmin => User?.IsAdmin == true;
    public long? EmployeeId => User?.EmployeeId;
    public void SignIn(AppUser user) => User = user;
    public void SignOut() => User = null;
}
