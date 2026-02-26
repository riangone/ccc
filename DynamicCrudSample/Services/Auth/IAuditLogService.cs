using System.Data;

namespace DynamicCrudSample.Services.Auth;

public interface IAuditLogService
{
    Task WriteAsync(
        string action,
        string? entity = null,
        string? detail = null,
        string? userName = null,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null);
}
