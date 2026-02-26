using System.Data;
using Dapper;
using Microsoft.Data.Sqlite;

namespace DynamicCrudSample.Services.Auth;

public class AuditLogService : IAuditLogService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuditLogService> _logger;

    public AuditLogService(IConfiguration configuration, ILogger<AuditLogService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task WriteAsync(
        string action,
        string? entity = null,
        string? detail = null,
        string? userName = null,
        IDbConnection? connection = null,
        IDbTransaction? transaction = null)
    {
        if (connection != null)
        {
            await connection.ExecuteAsync(@"
INSERT INTO AuditLog (UserName, Action, Entity, Detail, CreatedAt)
VALUES (@UserName, @Action, @Entity, @Detail, @CreatedAt)", new
            {
                UserName = userName,
                Action = action,
                Entity = entity,
                Detail = detail,
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            }, transaction);
            _logger.LogInformation("AUDIT action={Action} entity={Entity} user={UserName} detail={Detail}", action, entity, userName, detail);
            return;
        }

        var cs = _configuration.GetConnectionString("DefaultConnection") ?? "Data Source=chinook.db";
        await using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        await conn.ExecuteAsync(@"
INSERT INTO AuditLog (UserName, Action, Entity, Detail, CreatedAt)
VALUES (@UserName, @Action, @Entity, @Detail, @CreatedAt)", new
        {
            UserName = userName,
            Action = action,
            Entity = entity,
            Detail = detail,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        });

        _logger.LogInformation("AUDIT action={Action} entity={Entity} user={UserName} detail={Detail}", action, entity, userName, detail);
    }
}
