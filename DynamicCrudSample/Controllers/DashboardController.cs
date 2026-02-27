// ファイル概要: ダッシュボード画面を担当するコントローラです。
// config/dashboard.yml で定義した統計情報を DB から集計して表示します。
// 保守時は副作用を避けるため、公開シグネチャと呼び出し関係の整合性を維持してください。

using System.Data;
using Dapper;
using DynamicCrudSample.Services;
using Microsoft.AspNetCore.Mvc;

namespace DynamicCrudSample.Controllers;

public class DashboardStatViewModel
{
    public string Label { get; set; } = "";
    public string Value { get; set; } = "";
    public string? Icon { get; set; }
    public string? Color { get; set; }
}

public class DashboardController : Controller
{
    private readonly IDashboardConfigProvider _dashConfig;
    private readonly IEntityMetadataProvider _meta;
    private readonly IDbConnection _db;

    public DashboardController(IDashboardConfigProvider dashConfig, IEntityMetadataProvider meta, IDbConnection db)
    {
        _dashConfig = dashConfig;
        _meta = meta;
        _db = db;
    }

    public async Task<IActionResult> Index()
    {
        var config = _dashConfig.GetConfig();
        var stats = new List<DashboardStatViewModel>();

        foreach (var stat in config.Stats)
        {
            if (!_meta.TryGet(stat.Entity, out var meta))
                continue;

            string sql;
            if (stat.Aggregate.Equals("count", StringComparison.OrdinalIgnoreCase))
            {
                sql = $"SELECT COUNT(*) FROM {meta.Table}";
            }
            else if (stat.Aggregate.Equals("sum", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrEmpty(stat.Column))
            {
                sql = $"SELECT COALESCE(SUM({stat.Column}), 0) FROM {meta.Table}";
            }
            else if (stat.Aggregate.Equals("avg", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrEmpty(stat.Column))
            {
                sql = $"SELECT COALESCE(AVG({stat.Column}), 0) FROM {meta.Table}";
            }
            else
            {
                continue;
            }

            if (!string.IsNullOrEmpty(stat.Filter))
            {
                sql += $" WHERE {stat.Filter}";
            }

            try
            {
                var raw = await _db.ExecuteScalarAsync<object>(sql);
                string formatted = raw switch
                {
                    decimal d  => d.ToString("N2"),
                    double dbl => ((decimal)dbl).ToString("N2"),
                    float f    => ((decimal)f).ToString("N2"),
                    _          => raw?.ToString() ?? "0"
                };
                stats.Add(new DashboardStatViewModel
                {
                    Label = stat.GetLabel(),
                    Value = formatted,
                    Icon  = stat.Icon,
                    Color = stat.Color
                });
            }
            catch
            {
                // 集計クエリが失敗した場合はスキップ
            }
        }

        return View(stats);
    }
}
