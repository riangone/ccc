using System.Data;
using Dapper;
using DynamicCrudSample.Models.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;

namespace DynamicCrudSample.Services.Auth;

public class UserAuthService : IUserAuthService
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<UserAuthService> _logger;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public UserAuthService(IConfiguration configuration, ILogger<UserAuthService> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<AppUser?> ValidateCredentialsAsync(string userName, string password)
    {
        await using var conn = OpenConnection();
        var user = await conn.QueryFirstOrDefaultAsync<AppUser>(
            "SELECT * FROM AppUser WHERE UserName = @UserName AND IsActive = 1",
            new { UserName = userName });

        if (user == null)
        {
            _logger.LogWarning("Authentication failed for user '{UserName}' - user not found or inactive", userName);
            return null;
        }

        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Success || result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            _logger.LogInformation("Authentication success for user '{UserName}'", userName);
            return user;
        }

        _logger.LogWarning("Authentication failed for user '{UserName}' - invalid password", userName);
        return null;
    }

    public async Task<IReadOnlyList<AppUser>> GetAllAsync()
    {
        await using var conn = OpenConnection();
        var items = await conn.QueryAsync<AppUser>("SELECT * FROM AppUser ORDER BY Id ASC");
        return items.ToList();
    }

    public async Task<AppUser?> GetByIdAsync(int id)
    {
        await using var conn = OpenConnection();
        return await conn.QueryFirstOrDefaultAsync<AppUser>("SELECT * FROM AppUser WHERE Id = @Id", new { Id = id });
    }

    public async Task<int> CreateAsync(UserEditViewModel input, IDbConnection? connection = null, IDbTransaction? transaction = null)
    {
        var ownConnection = connection == null;
        var conn = connection ?? OpenConnection();
        try
        {
            var existing = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AppUser WHERE UserName = @UserName", new { input.UserName }, transaction);
            if (existing > 0)
            {
                throw new InvalidOperationException($"User '{input.UserName}' already exists.");
            }

            var user = new AppUser
            {
                UserName = input.UserName,
                DisplayName = input.DisplayName,
                PreferredLanguage = input.PreferredLanguage,
                IsAdmin = input.IsAdmin,
                IsActive = input.IsActive,
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };

            var password = string.IsNullOrWhiteSpace(input.Password) ? "ChangeMe123!" : input.Password;
            user.PasswordHash = _passwordHasher.HashPassword(user, password);

            var sql = @"
INSERT INTO AppUser (UserName, PasswordHash, DisplayName, PreferredLanguage, IsAdmin, IsActive, CreatedAt)
VALUES (@UserName, @PasswordHash, @DisplayName, @PreferredLanguage, @IsAdmin, @IsActive, @CreatedAt);
SELECT last_insert_rowid();";

            var id = await conn.ExecuteScalarAsync<long>(sql, user, transaction);
            _logger.LogInformation("Created user '{UserName}' with id {UserId}", user.UserName, id);
            return (int)id;
        }
        finally
        {
            if (ownConnection && conn is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    public async Task UpdateAsync(UserEditViewModel input, IDbConnection? connection = null, IDbTransaction? transaction = null)
    {
        if (!input.Id.HasValue)
        {
            throw new InvalidOperationException("User id is required for update.");
        }

        var ownConnection = connection == null;
        var conn = connection ?? OpenConnection();
        try
        {
            var current = await conn.QueryFirstOrDefaultAsync<AppUser>("SELECT * FROM AppUser WHERE Id = @Id", new { Id = input.Id.Value }, transaction);
            if (current == null)
            {
                throw new InvalidOperationException("User not found.");
            }

            var passwordHash = current.PasswordHash;
            if (!string.IsNullOrWhiteSpace(input.Password))
            {
                current.UserName = input.UserName;
                passwordHash = _passwordHasher.HashPassword(current, input.Password);
            }

            await conn.ExecuteAsync(@"
UPDATE AppUser
SET UserName = @UserName,
    PasswordHash = @PasswordHash,
    DisplayName = @DisplayName,
    PreferredLanguage = @PreferredLanguage,
    IsAdmin = @IsAdmin,
    IsActive = @IsActive
WHERE Id = @Id", new
        {
            Id = input.Id.Value,
            input.UserName,
            PasswordHash = passwordHash,
            input.DisplayName,
            input.PreferredLanguage,
            input.IsAdmin,
            input.IsActive
            }, transaction);

            _logger.LogInformation("Updated user {UserId} ('{UserName}')", input.Id.Value, input.UserName);
        }
        finally
        {
            if (ownConnection && conn is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }

    private SqliteConnection OpenConnection()
    {
        var cs = _configuration.GetConnectionString("DefaultConnection") ?? "Data Source=chinook.db";
        var conn = new SqliteConnection(cs);
        conn.Open();
        return conn;
    }
}
