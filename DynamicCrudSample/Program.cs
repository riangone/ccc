// ファイル概要: アプリケーションのエントリポイント。DI、認証、ローカライズ、ログ、ルーティングを初期化します。
// このファイルはアプリの重要な構成要素を定義します。
// 保守時は副作用を避けるため、公開シグネチャと呼び出し関係の整合性を維持してください。

using System.Data;
using System.Globalization;
using DynamicCrudSample.Data;
using DynamicCrudSample.Services;
using DynamicCrudSample.Services.Auth;
using DynamicCrudSample.Services.Dialect;
using DynamicCrudSample.Services.Hooks;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Localization;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, cfg) =>
{
    cfg.ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/app-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 14);
});

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
builder.Services.AddControllersWithViews()
    .AddViewLocalization();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Services.AddSingleton<IEntityMetadataProvider, EntityMetadataProvider>();

// ===== データベースプロバイダー設定 =====
// appsettings.json の "DatabaseProvider" で "sqlite"（既定）または "sqlserver" を指定してください。
var dbProvider = (builder.Configuration["DatabaseProvider"] ?? "sqlite").ToLowerInvariant();

if (dbProvider == "sqlserver")
{
    builder.Services.AddSingleton<ISqlDialect, SqlServerDialect>();
    builder.Services.AddScoped<IDbConnection>(_ =>
    {
        var cs = builder.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection is required for SQL Server provider.");
        return new SqlConnection(cs);
    });
}
else
{
    builder.Services.AddSingleton<ISqlDialect, SqliteDialect>();
    builder.Services.AddScoped<IDbConnection>(_ =>
    {
        var cs = builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=chinook.db";
        return new SqliteConnection(cs);
    });
}

builder.Services.AddSingleton<IValueConverter, ValueConverter>();
builder.Services.AddScoped<IDynamicCrudRepository, DynamicCrudRepository>();
builder.Services.AddScoped<IUserAuthService, UserAuthService>();
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

// ===== エンティティフック =====
// 新しいフックを追加する場合はここに IEntityHook の実装を登録してください。
builder.Services.AddSingleton<IEntityHook, CustomerEmailDomainHook>();
builder.Services.AddSingleton<IEntityHook, CustomerNameNormalizeHook>();
builder.Services.AddSingleton<IEntityHook, InvoiceMinimumTotalHook>();
builder.Services.AddSingleton<IEntityHook, ConsoleLogAfterHook>();
builder.Services.AddSingleton<IEntityHookRegistry, EntityHookRegistry>();

var app = builder.Build();

await DbInitializer.InitializeAsync(app.Services, app.Configuration);

var supportedCultures = new[] { "en-US", "zh-CN", "ja-JP" }
    .Select(x => new CultureInfo(x))
    .ToList();

var localizationOptions = new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en-US"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
};

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseSerilogRequestLogging();
app.UseRequestLocalization(localizationOptions);
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=DynamicEntity}/{action=Index}/{id?}");

app.Run();
