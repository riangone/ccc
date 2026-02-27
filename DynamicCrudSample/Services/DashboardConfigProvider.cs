// ファイル概要: config/dashboard.yml を読み込み、DashboardConfig を提供するサービスです。
// このファイルはアプリの重要な構成要素を定義します。
// 保守時は副作用を避けるため、公開シグネチャと呼び出し関係の整合性を維持してください。

using DynamicCrudSample.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DynamicCrudSample.Services;

public interface IDashboardConfigProvider
{
    DashboardConfig GetConfig();
}

public class DashboardConfigProvider : IDashboardConfigProvider
{
    private readonly DashboardConfig _config;

    public DashboardConfigProvider(IWebHostEnvironment env)
    {
        var filePath = Path.Combine(env.ContentRootPath, "config", "dashboard.yml");
        if (!File.Exists(filePath))
        {
            _config = new DashboardConfig();
            return;
        }

        var yaml = File.ReadAllText(filePath);
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        _config = deserializer.Deserialize<DashboardConfig>(yaml) ?? new DashboardConfig();
    }

    public DashboardConfig GetConfig() => _config;
}
