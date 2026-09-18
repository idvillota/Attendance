using Attendance.Core.Data;
using Attendance.Core.Domain;
using Microsoft.Data.Sqlite;

namespace Attendance.Core.Auth;

public sealed class AuthService : IAuthService
{
    private readonly IUserStore _users;
    private readonly AttendanceDb _db;
    private readonly IAuthSession _session;

    public AuthService(IUserStore users, AttendanceDb db, IAuthSession session)
    {
        _users = users;
        _db = db;
        _session = session;
    }

    public bool HasUsers() => _users.CountUsers() > 0;

    public AppUser SignIn(string username, string password)
    {
        var stored = _users.FindByUsername(username.Trim());
        if (stored is null || !stored.IsActive || !PasswordHasher.Verify(password, stored.PasswordHash))
        {
            throw new InvalidOperationException("Usuario o contraseña incorrectos.");
        }

        if (stored.Role == AppRole.Basic)
        {
            if (stored.EmployeeId is null)
            {
                throw new InvalidOperationException("Esta cuenta básica no está ligada a una persona.");
            }

            var employee = _db.GetEmployees().FirstOrDefault(e => e.Id == stored.EmployeeId.Value);
            if (employee is null || !employee.IsActive)
            {
                throw new InvalidOperationException("La persona de esta cuenta está inactiva.");
            }
        }

        var user = stored.ToAppUser();
        _session.SignIn(user);
        return user;
    }

    public AppUser CreateAdmin(string username, string password)
    {
        if (HasUsers())
        {
            throw new InvalidOperationException("Ya existe un administrador.");
        }

        ValidateCredentials(username, password, requirePassword: true);
        var user = AddUser(username.Trim(), password, AppRole.Admin, null);
        _session.SignIn(user);
        return user;
    }

    public IReadOnlyList<AppUser> GetUsers()
    {
        EnsureAdmin();
        return _users.GetUsers();
    }

    public AppUser CreateUser(string username, string password, AppRole role, long? employeeId)
    {
        EnsureAdmin();
        ValidateCredentials(username, password, requirePassword: true);
        ValidateAssignment(role, employeeId);
        return AddUser(username.Trim(), password, role, employeeId);
    }

    public void UpdateUser(long id, string username, string? password, AppRole role, long? employeeId, bool isActive)
    {
        EnsureAdmin();
        ValidateCredentials(username, password, requirePassword: false);
        ValidateAssignment(role, employeeId);

        var existing = _users.GetStoredUser(id) ?? throw new InvalidOperationException("Usuario no encontrado.");
        var wouldRemainAdmin = existing.Role == AppRole.Admin && existing.IsActive
            && role == AppRole.Admin && isActive;
        if (!wouldRemainAdmin && existing.Role == AppRole.Admin && existing.IsActive && _users.CountActiveAdmins() <= 1)
        {
            throw new InvalidOperationException("Debe quedar al menos un administrador activo.");
        }

        string? hash = null;
        if (!string.IsNullOrWhiteSpace(password))
        {
            hash = PasswordHasher.Hash(password);
        }

        try
        {
            _users.UpdateUser(id, username.Trim(), hash, role, employeeId, isActive);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("Ese usuario ya existe.", ex);
        }

        if (_session.User?.Id == id)
        {
            _session.SignIn(_users.GetStoredUser(id)!.ToAppUser());
        }
    }

    private AppUser AddUser(string username, string password, AppRole role, long? employeeId)
    {
        try
        {
            return _users.AddUser(username, PasswordHasher.Hash(password), role, employeeId);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new InvalidOperationException("Ese usuario ya existe.", ex);
        }
    }

    private void EnsureAdmin()
    {
        if (!_session.IsAdmin)
        {
            throw new UnauthorizedAccessException("Solo un administrador puede gestionar usuarios.");
        }
    }

    private void ValidateAssignment(AppRole role, long? employeeId)
    {
        if (role == AppRole.Admin)
        {
            return;
        }

        if (employeeId is null)
        {
            throw new InvalidOperationException("Una cuenta básica debe estar ligada a una persona.");
        }

        var employee = _db.GetEmployees().FirstOrDefault(e => e.Id == employeeId.Value);
        if (employee is null)
        {
            throw new InvalidOperationException("La persona seleccionada no existe.");
        }
    }

    private static void ValidateCredentials(string username, string? password, bool requirePassword)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Trim().Length < 3)
        {
            throw new InvalidOperationException("El usuario debe tener al menos 3 caracteres.");
        }

        if (requirePassword || !string.IsNullOrWhiteSpace(password))
        {
            if (string.IsNullOrWhiteSpace(password) || password.Length < 4)
            {
                throw new InvalidOperationException("La contraseña debe tener al menos 4 caracteres.");
            }
        }
    }
}
