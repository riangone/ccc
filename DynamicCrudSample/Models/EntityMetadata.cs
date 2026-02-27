// ファイル概要: YAMLメタデータ構造（列・フォーム・フィルタ・レイアウト）を定義します。
// このファイルはアプリの重要な構成要素を定義します。
// 保守時は副作用を避けるため、公開シグネチャと呼び出し関係の整合性を維持してください。

namespace DynamicCrudSample.Models;

public static class I18nText
{
    public static string Resolve(Dictionary<string, string>? map, string fallback)
    {
        if (map == null || map.Count == 0)
        {
            return fallback;
        }

        var culture = System.Globalization.CultureInfo.CurrentUICulture.Name;
        if (map.TryGetValue(culture, out var exact) && !string.IsNullOrWhiteSpace(exact))
        {
            return exact;
        }

        var neutral = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var neutralKey = map.Keys.FirstOrDefault(k => k.StartsWith(neutral, StringComparison.OrdinalIgnoreCase));
        if (neutralKey != null && map.TryGetValue(neutralKey, out var val) && !string.IsNullOrWhiteSpace(val))
        {
            return val;
        }

        if (map.TryGetValue("en-US", out var enUs) && !string.IsNullOrWhiteSpace(enUs))
        {
            return enUs;
        }

        if (map.TryGetValue("en", out var en) && !string.IsNullOrWhiteSpace(en))
        {
            return en;
        }

        return fallback;
    }
}

public class ForeignKeyDefinition
{
    public string Entity { get; set; } = default!;
    public string DisplayColumn { get; set; } = "Id";
    // ドロップダウンの代わりにピッカーモーダルで選択するか（単一選択）
    public bool Picker { get; set; }
    // ピッカーモーダルで複数選択するか
    public bool MultiPicker { get; set; }
}

public class JoinDefinition
{
    public string Type { get; set; } = "left";
    public string Table { get; set; } = default!;
    public string Alias { get; set; } = default!;
    public string On { get; set; } = default!;
}

public class FormDefinition
{
    public string Type { get; set; } = "string";
    public bool Identity { get; set; }
    public bool Required { get; set; }
    public string? Label { get; set; }
    public bool Searchable { get; set; }
    public bool Sortable { get; set; }
    public bool Editable { get; set; } = true;
    public string? Expression { get; set; }
    public ForeignKeyDefinition? ForeignKey { get; set; }
    public List<string>? Options { get; set; }
    public Dictionary<string, string>? LabelI18n { get; set; }
    public bool Hidden { get; set; }

    public string GetLabel(string fallback) => I18nText.Resolve(LabelI18n, Label ?? fallback);
}

public class ColumnDefinition
{
    public string Type { get; set; } = "string";
    public bool Identity { get; set; }
    public bool Required { get; set; }
    public string? Label { get; set; }
    public bool Searchable { get; set; }
    public bool Sortable { get; set; }
    public bool Editable { get; set; } = true;
    public string? Expression { get; set; }
    public ForeignKeyDefinition? ForeignKey { get; set; }
    public Dictionary<string, string>? LabelI18n { get; set; }
    public bool Hidden { get; set; }

    public string GetLabel(string fallback) => I18nText.Resolve(LabelI18n, Label ?? fallback);
}

public class PagingDefinition
{
    public int PageSize { get; set; } = 5;
    public string Mode { get; set; } = "numbered";
    public bool EnableCount { get; set; } = true;
}

public class FilterDefinition
{
    public string Type { get; set; } = "dropdown";
    public string? Label { get; set; }
    public Dictionary<string, string>? LabelI18n { get; set; }
    public string? Expression { get; set; }
    public ForeignKeyDefinition? ForeignKey { get; set; }
    public List<string>? Options { get; set; }

    public string GetLabel(string fallback) => I18nText.Resolve(LabelI18n, Label ?? fallback);
}

public class FormLayoutDefinition
{
    public int Columns { get; set; } = 2;
    public List<string> Order { get; set; } = new();
}

public class FilterLayoutDefinition
{
    public int Columns { get; set; } = 4;
    public List<string> Order { get; set; } = new();
}

public class EntityLayoutDefinition
{
    public FormLayoutDefinition Forms { get; set; } = new();
    public FilterLayoutDefinition Filters { get; set; } = new();
}

public class EntityDefinition
{
    public string Table { get; set; } = default!;
    public string Key { get; set; } = default!;
    public string DisplayName { get; set; } = default!;
    public Dictionary<string, string>? DisplayNameI18n { get; set; }
    public List<JoinDefinition> Joins { get; set; } = new();
    public Dictionary<string, FormDefinition> Forms { get; set; } = new();
    public Dictionary<string, ColumnDefinition> Columns { get; set; } = new();
    public PagingDefinition Paging { get; set; } = new();
    public EntityLayoutDefinition Layout { get; set; } = new();
    public bool SoftDelete { get; set; }
    public bool IsPublic { get; set; } = true;
    public Dictionary<string, FilterDefinition> Filters { get; set; } = new();
    public Dictionary<string, EntityLinkDefinition> Links { get; set; } = new();
    /// <summary>新規作成・更新時の確認ダイアログ設定</summary>
    public ConfirmationDefinition? Confirmation { get; set; }
    /// <summary>前処理・後処理フックの設定</summary>
    public EntityHooksDefinition? Hooks { get; set; }

    public string GetDisplayName() => I18nText.Resolve(DisplayNameI18n, DisplayName);

    public IEnumerable<KeyValuePair<string, FormDefinition>> GetOrderedForms()
    {
        if (Layout.Forms.Order.Count == 0)
        {
            return Forms;
        }

        var result = new List<KeyValuePair<string, FormDefinition>>();
        foreach (var key in Layout.Forms.Order)
        {
            if (Forms.TryGetValue(key, out var def))
            {
                result.Add(new KeyValuePair<string, FormDefinition>(key, def));
            }
        }

        result.AddRange(Forms.Where(f => result.All(x => x.Key != f.Key)));
        return result;
    }

    public IEnumerable<KeyValuePair<string, FilterDefinition>> GetOrderedFilters()
    {
        if (Layout.Filters.Order.Count == 0)
        {
            return Filters;
        }

        var result = new List<KeyValuePair<string, FilterDefinition>>();
        foreach (var key in Layout.Filters.Order)
        {
            if (Filters.TryGetValue(key, out var def))
            {
                result.Add(new KeyValuePair<string, FilterDefinition>(key, def));
            }
        }

        result.AddRange(Filters.Where(f => result.All(x => x.Key != f.Key)));
        return result;
    }
}

/// <summary>
/// 新規作成・更新時の確認ダイアログメッセージ設定。
/// entities.yml の confirmation セクションに対応します。
/// </summary>
public class ConfirmationDefinition
{
    /// <summary>新規作成時の確認メッセージ（null/空の場合は確認なし）</summary>
    public string? Create { get; set; }
    /// <summary>更新時の確認メッセージ（null/空の場合は確認なし）</summary>
    public string? Update { get; set; }
}

/// <summary>
/// 前処理・後処理フックの設定。
/// entities.yml の hooks セクションに対応します。
/// </summary>
public class EntityHooksDefinition
{
    /// <summary>DB 書き込み前に実行するフック名（create 時）</summary>
    public string? BeforeCreate { get; set; }
    /// <summary>DB 書き込み後に実行するフック名（create 時）</summary>
    public string? AfterCreate { get; set; }
    /// <summary>DB 書き込み前に実行するフック名（update 時）</summary>
    public string? BeforeUpdate { get; set; }
    /// <summary>DB 書き込み後に実行するフック名（update 時）</summary>
    public string? AfterUpdate { get; set; }
}

public class EntityLinkDefinition
{
    public string Label { get; set; } = default!;
    /// <summary>多言語ラベル（entities.yml の labelI18n セクション）</summary>
    public Dictionary<string, string>? LabelI18n { get; set; }
    public string TargetEntity { get; set; } = default!;
    // 静的クエリパラメータ（例: sort=Name）
    public Dictionary<string, string>? Query { get; set; }
    // 行ごとの動的フィルタ: targetQueryParam → sourceRowColumn
    // 例: { "CustomerId": "CustomerId" } → 行の CustomerId 値を ?CustomerId=xxx として付与
    public Dictionary<string, string>? Filter { get; set; }

    public string GetLabel() => I18nText.Resolve(LabelI18n, Label);
}

public class EntityConfigRoot
{
    public Dictionary<string, EntityDefinition> Entities { get; set; } = new();
}
