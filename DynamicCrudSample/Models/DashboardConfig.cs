// ファイル概要: Dashboard に表示する統計情報の設定モデルです。
// config/dashboard.yml に対応する C# クラス定義。
// 保守時は副作用を避けるため、公開シグネチャと呼び出し関係の整合性を維持してください。

namespace DynamicCrudSample.Models;

public class DashboardConfig
{
    public List<DashboardStatDefinition> Stats { get; set; } = new();
}

public class DashboardStatDefinition
{
    /// <summary>統計ラベル（デフォルト言語）</summary>
    public string Label { get; set; } = "";

    /// <summary>ロケール別ラベル（例: en-US / zh-CN / ja-JP）</summary>
    public Dictionary<string, string> LabelI18n { get; set; } = new();

    /// <summary>集計対象エンティティ名（entities.yml のキーと一致）</summary>
    public string Entity { get; set; } = "";

    /// <summary>集計種別: count / sum / avg</summary>
    public string Aggregate { get; set; } = "count";

    /// <summary>sum / avg の場合に集計するカラム名</summary>
    public string? Column { get; set; }

    /// <summary>WHERE 句（任意）。例: "IsDeleted = 0"</summary>
    public string? Filter { get; set; }

    /// <summary>アイコン文字（絵文字など）</summary>
    public string? Icon { get; set; }

    /// <summary>DaisyUI バッジカラークラス（例: badge-primary）</summary>
    public string? Color { get; set; }

    /// <summary>現在のロケールに対応するラベルを返します。</summary>
    public string GetLabel()
    {
        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        if (LabelI18n.TryGetValue(culture, out var localized) && !string.IsNullOrEmpty(localized))
            return localized;
        return Label;
    }
}
