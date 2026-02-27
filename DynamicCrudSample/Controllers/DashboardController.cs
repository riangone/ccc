// ファイル概要: ダッシュボード画面を担当するコントローラです。
// config/dashboard.yml で定義した統計カードとグラフを DB から集計して表示します。
// 保守時は副作用を避けるため、公開シグネチャと呼び出し関係の整合性を維持してください。

using System.Data;
using System.Text.Json;
using Dapper;
using DynamicCrudSample.Services;
using Microsoft.AspNetCore.Mvc;

namespace DynamicCrudSample.Controllers;

// ─── ViewModels ──────────────────────────────────────────────────────────────

public class DashboardStatViewModel
{
    public string Label     { get; set; } = "";
    public string Value     { get; set; } = "";
    public string? Icon     { get; set; }
    public string? Color    { get; set; }
    /// <summary>クリック時の遷移先 URL（エンティティ一覧）</summary>
    public string? EntityUrl { get; set; }
}

public class DashboardChartViewModel
{
    public string Title       { get; set; } = "";
    public string Type        { get; set; } = "bar";
    public string LabelsJson  { get; set; } = "[]";
    public string ValuesJson  { get; set; } = "[]";
    public string? ColorBg    { get; set; }
    public string? ColorBorder { get; set; }
    /// <summary>doughnut / pie 用 JSON カラー配列（null の場合は ColorBg 単色）</summary>
    public string? ColorsJson { get; set; }
}

public class DashboardViewModel
{
    public List<DashboardStatViewModel>  Stats  { get; set; } = new();
    public List<DashboardChartViewModel> Charts { get; set; } = new();
}

// ─── Controller ──────────────────────────────────────────────────────────────

public class DashboardController : Controller
{
    private readonly IDashboardConfigProvider _dashConfig;
    private readonly IEntityMetadataProvider  _meta;
    private readonly IDbConnection            _db;

    private static readonly JsonSerializerOptions _jsonOpts =
        new() { WriteIndented = false };

    public DashboardController(
        IDashboardConfigProvider dashConfig,
        IEntityMetadataProvider  meta,
        IDbConnection            db)
    {
        _dashConfig = dashConfig;
        _meta       = meta;
        _db         = db;
    }

    // ─── Index ───────────────────────────────────────────────────────────────

    public async Task<IActionResult> Index()
    {
        var config = _dashConfig.GetConfig();
        var vm     = new DashboardViewModel();

        // 統計カード
        vm.Stats = await BuildStatsAsync(config);

        // グラフ
        vm.Charts = await BuildChartsAsync(config);

        return View(vm);
    }

    // ─── Stats ───────────────────────────────────────────────────────────────

    private async Task<List<DashboardStatViewModel>> BuildStatsAsync(
        Models.DashboardConfig config)
    {
        var result = new List<DashboardStatViewModel>();

        foreach (var stat in config.Stats)
        {
            if (!_meta.TryGet(stat.Entity, out var meta))
                continue;

            string sql = stat.Aggregate.ToLowerInvariant() switch
            {
                "sum" when !string.IsNullOrEmpty(stat.Column)
                    => $"SELECT COALESCE(SUM({stat.Column}), 0) FROM {meta.Table}",
                "avg" when !string.IsNullOrEmpty(stat.Column)
                    => $"SELECT COALESCE(AVG({stat.Column}), 0) FROM {meta.Table}",
                "count"
                    => $"SELECT COUNT(*) FROM {meta.Table}",
                _ => ""
            };
            if (string.IsNullOrEmpty(sql)) continue;

            if (!string.IsNullOrEmpty(stat.Filter))
                sql += $" WHERE {stat.Filter}";

            try
            {
                var raw = await _db.ExecuteScalarAsync<object>(sql);
                var formatted = FormatScalar(raw, stat.Aggregate);

                result.Add(new DashboardStatViewModel
                {
                    Label     = stat.GetLabel(),
                    Value     = formatted,
                    Icon      = stat.Icon,
                    Color     = stat.Color,
                    EntityUrl = Url.Action("Index", "DynamicEntity", new { entity = stat.Entity })
                });
            }
            catch
            {
                // 集計クエリが失敗した場合はスキップ
            }
        }

        return result;
    }

    // ─── Charts ──────────────────────────────────────────────────────────────

    private async Task<List<DashboardChartViewModel>> BuildChartsAsync(
        Models.DashboardConfig config)
    {
        var result = new List<DashboardChartViewModel>();

        foreach (var chart in config.Charts)
        {
            if (!_meta.TryGet(chart.Entity, out var meta))
                continue;

            // 集計式
            string valueExpr = chart.ValueAggregate.ToLowerInvariant() switch
            {
                "sum" when !string.IsNullOrEmpty(chart.ValueColumn)
                    => $"SUM({chart.ValueColumn})",
                "avg" when !string.IsNullOrEmpty(chart.ValueColumn)
                    => $"AVG({chart.ValueColumn})",
                _ => "COUNT(*)"
            };

            string sql;
            string groupByClause;

            // out 変数を先に宣言して未代入エラーを回避
            Models.EntityDefinition? joinMeta = null;
            bool hasJoin = !string.IsNullOrEmpty(chart.LabelJoinEntity)
                           && _meta.TryGet(chart.LabelJoinEntity!, out joinMeta);

            if (hasJoin && joinMeta is not null)
            {
                // JOIN して FK 先のラベルカラムを取得
                var display = chart.LabelJoinDisplay ?? joinMeta.Key;
                sql = $"SELECT j.{display} as label, {valueExpr} as value " +
                      $"FROM {meta.Table} " +
                      $"JOIN {joinMeta.Table} j ON {meta.Table}.{chart.LabelJoinKey} = j.{joinMeta.Key}";
                groupByClause = $"GROUP BY j.{display}";
            }
            else
            {
                // GroupExpression（カラム名 or SQL 式）でグルーピング
                var grp = chart.GroupExpression ?? "1";
                sql           = $"SELECT {grp} as label, {valueExpr} as value FROM {meta.Table}";
                groupByClause = $"GROUP BY {grp}";
            }

            if (!string.IsNullOrEmpty(chart.Filter))
                sql += $" WHERE {chart.Filter}";

            sql += $" {groupByClause}";

            var orderCol = (chart.OrderBy?.ToLowerInvariant() == "label") ? "label" : "value";
            var orderDir = (chart.OrderDir?.ToLowerInvariant() == "asc")  ? "ASC"   : "DESC";
            sql += $" ORDER BY {orderCol} {orderDir} LIMIT {chart.Limit}";

            try
            {
                var rows   = await _db.QueryAsync(sql);
                var labels = new List<string>();
                var values = new List<double>();

                foreach (IDictionary<string, object> row in rows)
                {
                    labels.Add(row.TryGetValue("label", out var lv) ? (lv?.ToString() ?? "") : "");
                    values.Add(row.TryGetValue("value", out var vv) ? Convert.ToDouble(vv ?? 0) : 0);
                }

                result.Add(new DashboardChartViewModel
                {
                    Title        = chart.GetTitle(),
                    Type         = chart.Type,
                    LabelsJson   = JsonSerializer.Serialize(labels, _jsonOpts),
                    ValuesJson   = JsonSerializer.Serialize(values, _jsonOpts),
                    ColorBg      = chart.ColorBg,
                    ColorBorder  = chart.ColorBorder,
                    ColorsJson   = chart.Colors != null
                                   ? JsonSerializer.Serialize(chart.Colors, _jsonOpts)
                                   : null
                });
            }
            catch
            {
                // グラフクエリが失敗した場合はスキップ
            }
        }

        return result;
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string FormatScalar(object? raw, string aggregate)
    {
        // sum / avg は小数2桁で表示
        if (aggregate.Equals("sum", StringComparison.OrdinalIgnoreCase) ||
            aggregate.Equals("avg", StringComparison.OrdinalIgnoreCase))
        {
            return raw switch
            {
                decimal d  => d.ToString("N2"),
                double  db => db.ToString("N2"),
                float   f  => f.ToString("N2"),
                _          => Convert.ToDecimal(raw ?? 0).ToString("N2")
            };
        }
        return raw?.ToString() ?? "0";
    }
}
