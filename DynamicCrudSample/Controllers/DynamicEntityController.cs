// ファイル概要: 動的エンティティの一覧・作成・編集・削除・部分更新を処理します。
// このファイルはアプリの重要な構成要素を定義します。
// 保守時は副作用を避けるため、公開シグネチャと呼び出し関係の整合性を維持してください。

using System.Data;
using DynamicCrudSample.Models;
using DynamicCrudSample.Services;
using DynamicCrudSample.Services.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace DynamicCrudSample.Controllers;

 [Authorize]
public class DynamicEntityController : Controller
{
    private readonly IDbConnection _db;
    private readonly IDynamicCrudRepository _repo;
    private readonly IEntityMetadataProvider _meta;
    private readonly IValueConverter _converter;
    private readonly IAuditLogService _audit;
    private readonly ILogger<DynamicEntityController> _logger;

    public DynamicEntityController(
        IDbConnection db,
        IDynamicCrudRepository repo,
        IEntityMetadataProvider meta,
        IValueConverter converter,
        IAuditLogService audit,
        ILogger<DynamicEntityController> logger)
    {
        _db = db;
        _repo = repo;
        _meta = meta;
        _converter = converter;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IActionResult> Index(
        string entity = "customer",
        string? search = null,
        string? sort = null,
        string? dir = null,
        int? pageSize = null,
        string? count = null,
        string? cursor = null)
    {
        // 初期画面表示。メタデータから検索条件を解釈し、一覧表示モデルを構築します。
        var meta = _meta.Get(entity);
        pageSize ??= meta.Paging.PageSize;
        var isKeyset = meta.Paging.Mode.Equals("keyset", StringComparison.OrdinalIgnoreCase);
        var includeCount = ResolveCountEnabled(meta, count);

        var filters = BuildFilters(meta);

        var itemsRaw = await _repo.GetAllAsync(
            entity, search, sort, dir, filters, 1, pageSize,
            cursor: cursor, keyset: isKeyset, fetchOneExtra: isKeyset || !includeCount);
        var (items, hasMore, nextCursor) = BuildPagedItems(meta, itemsRaw, pageSize.Value, isKeyset || !includeCount);
        var total = includeCount ? await _repo.CountAsync(entity, search, filters) : -1;
        var fkData = await LoadForeignKeyDataFilter(meta);
        var page = 1;

        return View("Index",
            new DynamicListViewModel(
                entity,
                meta,
                items,
                search,
                sort,
                dir,
                fkData,
                page,
                total,
                filters,
                pageSize.Value,
                includeCount,
                hasMore,
                nextCursor,
                cursor));
    }

    public async Task<IActionResult> ListPartial(
        string entity = "customer",
        string? search = null,
        string? sort = null,
        string? dir = null,
        int page = 1,
        int? pageSize = null,
        string? count = null,
        string? cursor = null)
    {
        // HTMXによる一覧部分更新。count有無・keyset有無をここで切り替えます。
        var meta = _meta.Get(entity);
        pageSize ??= meta.Paging.PageSize;
        var isKeyset = meta.Paging.Mode.Equals("keyset", StringComparison.OrdinalIgnoreCase);
        var includeCount = ResolveCountEnabled(meta, count);

        var filters = BuildFilters(meta);

        var itemsRaw = await _repo.GetAllAsync(
            entity, search, sort, dir, filters, page, pageSize,
            cursor: cursor, keyset: isKeyset, fetchOneExtra: isKeyset || !includeCount);
        var (items, hasMore, nextCursor) = BuildPagedItems(meta, itemsRaw, pageSize.Value, isKeyset || !includeCount);
        var total = includeCount ? await _repo.CountAsync(entity, search, filters) : -1;
        var fkData = await LoadForeignKeyDataForm(meta);

        return PartialView("_List",
            new DynamicListViewModel(
                entity, meta, items, search, sort, dir, fkData, page, total, filters, pageSize.Value,
                includeCount, hasMore, nextCursor, cursor));
    }

    public async Task<IActionResult> CreateForm(string entity = "customer", string mode = "modal")
    {
        // 新規作成フォームの描画（モーダル/ページ両対応）。
        var meta = _meta.Get(entity);
        var fkData = await LoadForeignKeyDataForm(meta);
        return PartialView("_Form", new DynamicFormViewModel(entity, meta, null, fkData, new Dictionary<string, string>(), mode));
    }

    public async Task<IActionResult> CreatePage(string entity = "customer")
    {
        var meta = _meta.Get(entity);
        var fkData = await LoadForeignKeyDataForm(meta);
        return View("FormPage", new DynamicFormViewModel(entity, meta, null, fkData, new Dictionary<string, string>(), "page"));
    }

    [HttpPost]
    public async Task<IActionResult> Create(string entity, [FromForm] Dictionary<string, string?> form, string mode = "modal")
    {
        // 登録処理。CRUD本体と監査ログを同一トランザクションで実行します。
        var meta = _meta.Get(entity);
        var (values, errors) = ConvertAndValidate(meta, form, isEdit: false);
        var isPageMode = mode.Equals("page", StringComparison.OrdinalIgnoreCase);

        if (errors.Any())
        {
            var fkData = await LoadForeignKeyDataForm(meta);
            var vm = new DynamicFormViewModel(entity, meta, null, fkData, errors, mode);
            return isPageMode ? View("FormPage", vm) : PartialView("_Form", vm);
        }

        await ExecuteCrudTransactionAsync(async tx =>
        {
            await _repo.InsertAsync(entity, values, tx);
            await _audit.WriteAsync("create", entity, $"Created {entity}", User.Identity?.Name, _db, tx);
        });
        if (isPageMode)
        {
            return RedirectToAction(nameof(Index), new { entity });
        }

        var total = await _repo.CountAsync(entity, "");
        var items = await _repo.GetAllAsync(entity, null, null, null);
        Response.Headers["HX-Retarget"] = "#list-container";
        Response.Headers["HX-Trigger"] = "entity-form-saved";
        return PartialView("_List", new DynamicListViewModel(entity, meta, items, null, null, null, new(), 1, total, null, 5, true, false, null, null));
    }

    public async Task<IActionResult> EditForm(string entity, int id, string mode = "modal")
    {
        // 既存レコードを読み込み、編集フォームを返します。
        var meta = _meta.Get(entity);
        var item = await _repo.GetByIdAsync(entity, id);
        var fkData = await LoadForeignKeyDataForm(meta);
        return PartialView("_Form", new DynamicFormViewModel(entity, meta, item, fkData, new Dictionary<string, string>(), mode));
    }

    public async Task<IActionResult> EditPage(string entity, int id)
    {
        var meta = _meta.Get(entity);
        var item = await _repo.GetByIdAsync(entity, id);
        var fkData = await LoadForeignKeyDataForm(meta);
        return View("FormPage", new DynamicFormViewModel(entity, meta, item, fkData, new Dictionary<string, string>(), "page"));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string entity, int id, [FromForm] Dictionary<string, string?> form, string mode = "modal")
    {
        // 更新処理。登録同様に監査ログと整合性を取るためTx内で実行します。
        var meta = _meta.Get(entity);
        var (values, errors) = ConvertAndValidate(meta, form, isEdit: true);
        var isPageMode = mode.Equals("page", StringComparison.OrdinalIgnoreCase);

        if (errors.Any())
        {
            var item = await _repo.GetByIdAsync(entity, id);
            var fkData = await LoadForeignKeyDataForm(meta);
            var vm = new DynamicFormViewModel(entity, meta, item, fkData, errors, mode);
            return isPageMode ? View("FormPage", vm) : PartialView("_Form", vm);
        }

        await ExecuteCrudTransactionAsync(async tx =>
        {
            await _repo.UpdateAsync(entity, id, values, tx);
            await _audit.WriteAsync("update", entity, $"Updated {entity} id={id}", User.Identity?.Name, _db, tx);
        });
        if (isPageMode)
        {
            return RedirectToAction(nameof(Index), new { entity });
        }

        var total = await _repo.CountAsync(entity, "");
        var items = await _repo.GetAllAsync(entity, null, null, null);
        Response.Headers["HX-Retarget"] = "#list-container";
        Response.Headers["HX-Trigger"] = "entity-form-saved";
        return PartialView("_List", new DynamicListViewModel(entity, meta, items, null, null, null, new(), 1, total, null, 5, true, false, null, null));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string entity, int id)
    {
        // 削除処理（論理/物理はRepository側で自動分岐）。
        var meta = _meta.Get(entity);
        await ExecuteCrudTransactionAsync(async tx =>
        {
            await _repo.DeleteAsync(entity, id, tx);
            await _audit.WriteAsync("delete", entity, $"Deleted {entity} id={id}", User.Identity?.Name, _db, tx);
        });
        var total = await _repo.CountAsync(entity, null);
        var items = await _repo.GetAllAsync(entity, null, null, null);
        return PartialView("_List", new DynamicListViewModel(entity, meta, items, null, null, null, new(), 1, total, null, 5, true, false, null, null));
    }

    private (Dictionary<string, object?> values, Dictionary<string, string> errors)
        ConvertAndValidate(EntityDefinition meta, Dictionary<string, string?> form, bool isEdit)
    {
        // YAMLの列定義に従って入力値を型変換し、エラーを収集します。
        var values = new Dictionary<string, object?>();
        var errors = new Dictionary<string, string>();

        foreach (var kv in meta.Columns)
        {
            var name = kv.Key;
            var col = kv.Value;

            if (col.Identity)
            {
                continue;
            }

            form.TryGetValue(name, out var raw);

            if (col.Type.Equals("bool", StringComparison.OrdinalIgnoreCase) && !form.ContainsKey(name))
            {
                raw = "false";
            }

            if (!_converter.TryConvert(raw, col, out var val, out var error))
            {
                errors[name] = error ?? "Invalid value";
            }
            else
            {
                values[name] = val;
            }
        }

        return (values, errors);
    }

    private async Task<Dictionary<string, IEnumerable<dynamic>>> LoadForeignKeyDataFilter(EntityDefinition meta)
    {
        var result = new Dictionary<string, IEnumerable<dynamic>>();
        foreach (var col in meta.Filters.Where(c => c.Value.ForeignKey != null))
        {
            var fk = col.Value.ForeignKey!;
            var items = await _repo.GetAllForEntityAsync(fk.Entity);
            result[col.Key] = items;
        }

        return result;
    }

    private async Task<Dictionary<string, IEnumerable<dynamic>>> LoadForeignKeyDataForm(EntityDefinition meta)
    {
        var result = new Dictionary<string, IEnumerable<dynamic>>();
        foreach (var col in meta.Forms.Where(c => c.Value.ForeignKey != null))
        {
            var fk = col.Value.ForeignKey!;
            var items = await _repo.GetAllForEntityAsync(fk.Entity);
            result[col.Key] = items;
        }

        return result;
    }

    private Dictionary<string, string?> BuildFilters(EntityDefinition meta)
    {
        // QueryStringからフィルタ値を抽出し、型ごとのキー形式へ正規化します。
        var filters = new Dictionary<string, string?>();
        foreach (var f in meta.Filters)
        {
            var key = f.Key;
            var type = (f.Value.Type ?? "dropdown").ToLowerInvariant();
            switch (type)
            {
                case "range":
                    filters[$"{key}_min"] = Request.Query[$"{key}_min"].FirstOrDefault();
                    filters[$"{key}_max"] = Request.Query[$"{key}_max"].FirstOrDefault();
                    break;
                case "date-range":
                    filters[$"{key}_from"] = Request.Query[$"{key}_from"].FirstOrDefault();
                    filters[$"{key}_to"] = Request.Query[$"{key}_to"].FirstOrDefault();
                    break;
                case "checkbox":
                case "multi-select":
                    var allValues = Request.Query[key].ToArray()
                        .Where(v => !string.IsNullOrWhiteSpace(v))
                        .Distinct()
                        .ToArray();
                    filters[key] = allValues.Length == 0 ? null : string.Join(",", allValues);
                    break;
                default:
                    filters[key] = Request.Query[key].FirstOrDefault();
                    break;
            }
        }

        return filters;
    }

    private async Task ExecuteCrudTransactionAsync(Func<IDbTransaction, Task> action)
    {
        // CRUD + Audit の原子性を担保するための共通Txラッパーです。
        if (_db.State != ConnectionState.Open)
        {
            _db.Open();
        }

        using var tx = _db.BeginTransaction();
        try
        {
            await action(tx);
            tx.Commit();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CRUD transaction failed");
            tx.Rollback();
            throw;
        }
    }

    private static bool ResolveCountEnabled(EntityDefinition meta, string? count)
    {
        // リクエスト明示値を優先し、未指定時はYAML設定(enableCount)を採用します。
        if (string.IsNullOrWhiteSpace(count))
        {
            return meta.Paging.EnableCount;
        }

        return !count.Equals("false", StringComparison.OrdinalIgnoreCase)
            && !count.Equals("0", StringComparison.OrdinalIgnoreCase);
    }

    private static (List<dynamic> Items, bool HasMore, string? NextCursor) BuildPagedItems(
        EntityDefinition meta,
        IEnumerable<dynamic> rawItems,
        int pageSize,
        bool expectExtraRow)
    {
        // +1件取得結果から hasMore / nextCursor を計算します。
        var list = rawItems.ToList();
        var hasMore = expectExtraRow && list.Count > pageSize;
        if (hasMore)
        {
            list = list.Take(pageSize).ToList();
        }

        string? nextCursor = null;
        if (hasMore && list.Count > 0)
        {
            var last = list.Last() as IDictionary<string, object>;
            if (last != null && last.TryGetValue(meta.Key, out var keyVal))
            {
                nextCursor = keyVal?.ToString();
            }
        }

        return (list, hasMore, nextCursor);
    }
}

public record DynamicListViewModel(
    string Entity,
    EntityDefinition Meta,
    IEnumerable<dynamic> Items,
    string? Search,
    string? Sort,
    string? Dir,
    Dictionary<string, IEnumerable<dynamic>> ForeignKeyData,
    int Page,
    int Total,
    Dictionary<string, string?>? Filters = null,
    int PageSize = 5,
    bool CountEnabled = true,
    bool HasMore = false,
    string? NextCursor = null,
    string? Cursor = null);

public record DynamicFormViewModel(
    string Entity,
    EntityDefinition Meta,
    dynamic? Item,
    Dictionary<string, IEnumerable<dynamic>> ForeignKeyData,
    Dictionary<string, string> Errors,
    string Mode = "modal");
