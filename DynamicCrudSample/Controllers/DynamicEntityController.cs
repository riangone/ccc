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
        int? pageSize = null)
    {
        var meta = _meta.Get(entity);
        pageSize ??= meta.Paging.PageSize;

        var filters = BuildFilters(meta);

        var total = await _repo.CountAsync(entity, search, filters);
        var items = await _repo.GetAllAsync(entity, search, sort, dir, filters, 1, pageSize);
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
                pageSize.Value));
    }

    public async Task<IActionResult> ListPartial(
        string entity = "customer",
        string? search = null,
        string? sort = null,
        string? dir = null,
        int page = 1,
        int? pageSize = null)
    {
        var meta = _meta.Get(entity);
        pageSize ??= meta.Paging.PageSize;

        var filters = BuildFilters(meta);

        var total = await _repo.CountAsync(entity, search, filters);
        var items = await _repo.GetAllAsync(entity, search, sort, dir, filters, page, pageSize);
        var fkData = await LoadForeignKeyDataForm(meta);

        return PartialView("_List",
            new DynamicListViewModel(entity, meta, items, search, sort, dir, fkData, page, total, filters, pageSize.Value));
    }

    public async Task<IActionResult> CreateForm(string entity = "customer", string mode = "modal")
    {
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
        return PartialView("_List", new DynamicListViewModel(entity, meta, items, null, null, null, new(), 1, total));
    }

    public async Task<IActionResult> EditForm(string entity, int id, string mode = "modal")
    {
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
        return PartialView("_List", new DynamicListViewModel(entity, meta, items, null, null, null, new(), 1, total));
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string entity, int id)
    {
        var meta = _meta.Get(entity);
        await ExecuteCrudTransactionAsync(async tx =>
        {
            await _repo.DeleteAsync(entity, id, tx);
            await _audit.WriteAsync("delete", entity, $"Deleted {entity} id={id}", User.Identity?.Name, _db, tx);
        });
        var total = await _repo.CountAsync(entity, null);
        var items = await _repo.GetAllAsync(entity, null, null, null);
        return PartialView("_List", new DynamicListViewModel(entity, meta, items, null, null, null, new(), 1, total));
    }

    private (Dictionary<string, object?> values, Dictionary<string, string> errors)
        ConvertAndValidate(EntityDefinition meta, Dictionary<string, string?> form, bool isEdit)
    {
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
    int PageSize = 5);

public record DynamicFormViewModel(
    string Entity,
    EntityDefinition Meta,
    dynamic? Item,
    Dictionary<string, IEnumerable<dynamic>> ForeignKeyData,
    Dictionary<string, string> Errors,
    string Mode = "modal");
