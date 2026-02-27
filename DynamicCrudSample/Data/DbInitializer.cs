// ファイル概要: DB初期化・Chinook配置・認証関連テーブル準備を行います。
// DatabaseProvider 設定で SQLite / SQL Server を切り替えます。
// このファイルはアプリの重要な構成要素を定義します。
// 保守時は副作用を避けるため、公開シグネチャと呼び出し関係の整合性を維持してください。

using Dapper;
using DynamicCrudSample.Models.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace DynamicCrudSample.Data;

public static class DbInitializer
{
    private const string ChinookUrl = "https://github.com/lerocha/chinook-database/releases/download/v1.4.5/Chinook_Sqlite.sqlite";

    public static async Task InitializeAsync(IServiceProvider services, IConfiguration configuration)
    {
        using var scope = services.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        var provider = (configuration["DatabaseProvider"] ?? "sqlite").ToLowerInvariant();

        if (provider == "sqlserver")
        {
            await InitializeSqlServerAsync(configuration, logger);
        }
        else
        {
            await InitializeSqliteAsync(configuration, logger);
        }
    }

    // ── SQLite ──────────────────────────────────────────────────────────────

    private static async Task InitializeSqliteAsync(IConfiguration configuration, ILogger logger)
    {
        var cs = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=chinook.db";
        var sqliteBuilder = new SqliteConnectionStringBuilder(cs);
        var dbPath = sqliteBuilder.DataSource;

        await EnsureChinookDatabaseAsync(dbPath, logger);

        await using var conn = new SqliteConnection(cs);
        await conn.OpenAsync();

        await EnsureSqliteAuthTablesAsync(conn, logger);
        await EnsureSqliteDefaultAdminAsync(conn, logger);
    }

    private static async Task EnsureChinookDatabaseAsync(string dbPath, ILogger logger)
    {
        if (File.Exists(dbPath))
        {
            logger.LogInformation("Using existing database file at {Path}", dbPath);
            return;
        }

        var fullPath = Path.GetFullPath(dbPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath) ?? Directory.GetCurrentDirectory());

        logger.LogInformation("Downloading Chinook database from {Url}", ChinookUrl);
        using var http = new HttpClient();
        await using var source = await http.GetStreamAsync(ChinookUrl);
        await using var target = File.Create(fullPath);
        await source.CopyToAsync(target);
        logger.LogInformation("Chinook database saved at {Path}", fullPath);
    }

    private static async Task EnsureSqliteAuthTablesAsync(SqliteConnection conn, ILogger logger)
    {
        var sql = @"
CREATE TABLE IF NOT EXISTS AppUser (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserName TEXT NOT NULL UNIQUE,
    PasswordHash TEXT NOT NULL,
    DisplayName TEXT NOT NULL,
    PreferredLanguage TEXT NOT NULL DEFAULT 'en-US',
    IsAdmin INTEGER NOT NULL DEFAULT 0,
    IsActive INTEGER NOT NULL DEFAULT 1,
    CreatedAt TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS AuditLog (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UserName TEXT,
    Action TEXT NOT NULL,
    Entity TEXT,
    Detail TEXT,
    CreatedAt TEXT NOT NULL
);";

        await conn.ExecuteAsync(sql);
        logger.LogInformation("Authentication and audit tables ensured (SQLite)");
    }

    private static async Task EnsureSqliteDefaultAdminAsync(SqliteConnection conn, ILogger logger)
    {
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AppUser");
        if (count > 0)
        {
            return;
        }

        var admin = new AppUser
        {
            UserName = "admin",
            DisplayName = "System Admin",
            PreferredLanguage = "en-US",
            IsAdmin = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };

        var hasher = new PasswordHasher<AppUser>();
        admin.PasswordHash = hasher.HashPassword(admin, "Admin@123");

        await conn.ExecuteAsync(@"
INSERT INTO AppUser (UserName, PasswordHash, DisplayName, PreferredLanguage, IsAdmin, IsActive, CreatedAt)
VALUES (@UserName, @PasswordHash, @DisplayName, @PreferredLanguage, @IsAdmin, @IsActive, @CreatedAt)", admin);

        logger.LogWarning("Default admin user created. user=admin password=Admin@123. Change it immediately.");
    }

    // ── SQL Server ──────────────────────────────────────────────────────────

    private static async Task InitializeSqlServerAsync(IConfiguration configuration, ILogger logger)
    {
        var cs = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is required for SQL Server provider.");

        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();

        await EnsureSqlServerAuthTablesAsync(conn, logger);
        await EnsureSqlServerDefaultAdminAsync(conn, logger);
    }

    private static async Task EnsureSqlServerAuthTablesAsync(SqlConnection conn, ILogger logger)
    {
        var sql = @"
IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AppUser' AND xtype='U')
CREATE TABLE AppUser (
    Id         INT IDENTITY(1,1) PRIMARY KEY,
    UserName   NVARCHAR(256) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(512) NOT NULL,
    DisplayName  NVARCHAR(256) NOT NULL,
    PreferredLanguage NVARCHAR(10) NOT NULL DEFAULT 'en-US',
    IsAdmin    BIT NOT NULL DEFAULT 0,
    IsActive   BIT NOT NULL DEFAULT 1,
    CreatedAt  NVARCHAR(32) NOT NULL
);

IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='AuditLog' AND xtype='U')
CREATE TABLE AuditLog (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    UserName NVARCHAR(256),
    Action   NVARCHAR(100) NOT NULL,
    Entity   NVARCHAR(100),
    Detail   NVARCHAR(MAX),
    CreatedAt NVARCHAR(32) NOT NULL
);";

        await conn.ExecuteAsync(sql);
        logger.LogInformation("Authentication and audit tables ensured (SQL Server)");
    }

    private static async Task EnsureSqlServerDefaultAdminAsync(SqlConnection conn, ILogger logger)
    {
        var count = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM AppUser");
        if (count > 0)
        {
            return;
        }

        var admin = new AppUser
        {
            UserName = "admin",
            DisplayName = "System Admin",
            PreferredLanguage = "en-US",
            IsAdmin = true,
            IsActive = true,
            CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
        };

        var hasher = new PasswordHasher<AppUser>();
        admin.PasswordHash = hasher.HashPassword(admin, "Admin@123");

        await conn.ExecuteAsync(@"
INSERT INTO AppUser (UserName, PasswordHash, DisplayName, PreferredLanguage, IsAdmin, IsActive, CreatedAt)
VALUES (@UserName, @PasswordHash, @DisplayName, @PreferredLanguage, @IsAdmin, @IsActive, @CreatedAt)", admin);

        logger.LogWarning("Default admin user created. user=admin password=Admin@123. Change it immediately.");
    }
}
