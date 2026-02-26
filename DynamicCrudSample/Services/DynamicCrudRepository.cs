using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using DynamicCrudSample.Models;

namespace DynamicCrudSample.Services;

public interface IDynamicCrudRepository
{
    Task<IEnumerable<dynamic>> GetAllAsync(string entity, string? search, string? sort, string? dir, Dictionary<string, string?>? filters = null, int page = 1, int? pageSize = null);
    Task<dynamic?> GetByIdAsync(string entity, object id);
    Task<int> InsertAsync(string entity, IDictionary<string, object?> values, IDbTransaction? tx = null);
    Task<int> UpdateAsync(string entity, object id, IDictionary<string, object?> values, IDbTransaction? tx = null);
    Task<int> DeleteAsync(string entity, object id, IDbTransaction? tx = null);
    Task<IEnumerable<dynamic>> GetAllForEntityAsync(string entity);
    Task<int> CountAsync(string entity, string? search, Dictionary<string, string?>? filters = null);
}

public class DynamicCrudRepository : IDynamicCrudRepository
{
    private readonly IDbConnection _db;
    private readonly IEntityMetadataProvider _meta;
    private readonly ILogger<DynamicCrudRepository> _logger;
    private static readonly Regex IdentifierRegex = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private static readonly Regex ExpressionRegex = new("^[A-Za-z0-9_\\.\\s,()\\+\\-*/%<>=!'|]+$", RegexOptions.Compiled);
    private static readonly HashSet<string> AllowedJoinTypes = new(StringComparer.OrdinalIgnoreCase) { "left", "inner", "right" };

    public DynamicCrudRepository(IDbConnection db, IEntityMetadataProvider meta, ILogger<DynamicCrudRepository> logger)
    {
        _db = db;
        _meta = meta;
        _logger = logger;
    }

    public async Task<IEnumerable<dynamic>> GetAllAsync(
        string entity,
        string? search,
        string? sort,
        string? dir,
        Dictionary<string, string?>? filters = null,
        int page = 1,
        int? pageSize = null)
    {
        var meta = _meta.Get(entity);
        ValidateMetadata(meta, entity);
        pageSize ??= meta.Paging.PageSize;

        var selectList = string.Join(", ",
            meta.Columns.Select(c =>
                c.Value.Expression != null
                    ? $"{c.Value.Expression} AS {c.Key}"
                    : $"{meta.Table}.{c.Key}"));

        var sql = new List<string> { $"SELECT {selectList} {BuildFromClause(meta)}" };
        var param = new DynamicParameters();
        var where = BuildWhere(meta, search, filters, param);

        AppendWhere(sql, where);

        if (!string.IsNullOrWhiteSpace(sort) && meta.Columns.TryGetValue(sort, out var colDef) && colDef.Sortable)
        {
            var expr = colDef.Expression ?? $"{meta.Table}.{sort}";
            var direction = (dir?.ToLowerInvariant() == "desc") ? "DESC" : "ASC";
            sql.Add($" ORDER BY {expr} {direction}");
        }

        sql.Add(" LIMIT @PageSize OFFSET @Offset");

        var offset = (page - 1) * pageSize.Value;
        param.Add("PageSize", pageSize);
        param.Add("Offset", offset);

        var statement = string.Join(Environment.NewLine, sql);
        _logger.LogInformation("GetAllAsync entity={Entity} page={Page} pageSize={PageSize} sql={Sql}", entity, page, pageSize, statement);
        return await _db.QueryAsync(statement, param);
    }

    public async Task<dynamic?> GetByIdAsync(string entity, object id)
    {
        var meta = _meta.Get(entity);
        ValidateMetadata(meta, entity);
        var sql = new StringBuilder();
        sql.AppendLine($"SELECT * FROM {meta.Table} WHERE {meta.Key} = @Id");
        if (meta.SoftDelete)
        {
            sql.AppendLine($"AND ({meta.Table}.IsDeleted = 0 OR {meta.Table}.IsDeleted IS NULL)");
        }

        _logger.LogInformation("GetByIdAsync entity={Entity} id={Id}", entity, id);
        return (await _db.QueryAsync(sql.ToString(), new { Id = id })).FirstOrDefault();
    }

    public async Task<int> InsertAsync(string entity, IDictionary<string, object?> values, IDbTransaction? tx = null)
    {
        var meta = _meta.Get(entity);
        ValidateMetadata(meta, entity);
        var cols = meta.Columns
            .Where(c => !c.Value.Identity)
            .Select(c => c.Key)
            .ToArray();

        var colList = string.Join(", ", cols);
        var paramList = string.Join(", ", cols.Select(c => "@" + c));
        var sql = $"INSERT INTO {meta.Table} ({colList}) VALUES ({paramList});";

        _logger.LogInformation("InsertAsync entity={Entity} sql={Sql}", entity, sql);
        return await _db.ExecuteAsync(sql, values, tx);
    }

    public async Task<int> UpdateAsync(string entity, object id, IDictionary<string, object?> values, IDbTransaction? tx = null)
    {
        var meta = _meta.Get(entity);
        ValidateMetadata(meta, entity);
        var fields = meta.Forms
            .Where(f => !f.Value.Identity && values.ContainsKey(f.Key) && f.Value.Editable)
            .Select(f => f.Key)
            .ToArray();

        var setClause = string.Join(", ", fields.Select(f => $"{f} = @{f}"));
        var sql = $"UPDATE {meta.Table} SET {setClause} WHERE {meta.Key} = @Id";

        var param = new DynamicParameters(values);
        param.Add("Id", id);

        _logger.LogInformation("UpdateAsync entity={Entity} id={Id} sql={Sql}", entity, id, sql);
        return await _db.ExecuteAsync(sql, param, tx);
    }

    public async Task<int> DeleteAsync(string entity, object id, IDbTransaction? tx = null)
    {
        var meta = _meta.Get(entity);
        ValidateMetadata(meta, entity);
        if (meta.SoftDelete)
        {
            var sqlSoft = $"UPDATE {meta.Table} SET IsDeleted = 1 WHERE {meta.Key} = @Id";
            _logger.LogInformation("SoftDelete entity={Entity} id={Id}", entity, id);
            return await _db.ExecuteAsync(sqlSoft, new { Id = id }, tx);
        }

        var sql = $"DELETE FROM {meta.Table} WHERE {meta.Key} = @Id";
        _logger.LogInformation("Delete entity={Entity} id={Id}", entity, id);
        return await _db.ExecuteAsync(sql, new { Id = id }, tx);
    }

    public async Task<IEnumerable<dynamic>> GetAllForEntityAsync(string entity)
    {
        var meta = _meta.Get(entity);
        ValidateMetadata(meta, entity);
        var sql = $"SELECT *, {meta.Key} AS Id FROM {meta.Table}";
        _logger.LogDebug("GetAllForEntityAsync entity={Entity}", entity);
        return await _db.QueryAsync(sql);
    }

    public async Task<int> CountAsync(string entity, string? search, Dictionary<string, string?>? filters = null)
    {
        var meta = _meta.Get(entity);
        ValidateMetadata(meta, entity);
        var sql = $"SELECT COUNT(*) {BuildFromClause(meta)}";
        var param = new DynamicParameters();
        var where = BuildWhere(meta, search, filters, param);

        if (where.Any())
        {
            sql += " WHERE " + string.Join(" AND ", where);
        }

        _logger.LogInformation("CountAsync entity={Entity} sql={Sql}", entity, sql);
        return await _db.ExecuteScalarAsync<int>(sql, param);
    }

    private static void ApplyFilters(
        EntityDefinition meta,
        Dictionary<string, string?>? filters,
        List<string> where,
        DynamicParameters param)
    {
        if (filters == null)
        {
            return;
        }

        foreach (var f in meta.Filters)
        {
            var key = f.Key;
            var filterType = (f.Value.Type ?? "dropdown").ToLowerInvariant();
            var expr = f.Value.Expression ?? $"{meta.Table}.{key}";

            switch (filterType)
            {
                case "range":
                    if (filters.TryGetValue($"{key}_min", out var minRaw) && !string.IsNullOrWhiteSpace(minRaw))
                    {
                        var pName = $"{key}_min";
                        if (decimal.TryParse(minRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var min))
                        {
                            where.Add($"{expr} >= @{pName}");
                            param.Add(pName, min);
                        }
                    }

                    if (filters.TryGetValue($"{key}_max", out var maxRaw) && !string.IsNullOrWhiteSpace(maxRaw))
                    {
                        var pName = $"{key}_max";
                        if (decimal.TryParse(maxRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var max))
                        {
                            where.Add($"{expr} <= @{pName}");
                            param.Add(pName, max);
                        }
                    }

                    break;

                case "date-range":
                    if (filters.TryGetValue($"{key}_from", out var fromRaw) && !string.IsNullOrWhiteSpace(fromRaw))
                    {
                        var pName = $"{key}_from";
                        if (DateTime.TryParse(fromRaw, out var from))
                        {
                            where.Add($"{expr} >= @{pName}");
                            param.Add(pName, from.ToString("yyyy-MM-dd"));
                        }
                    }

                    if (filters.TryGetValue($"{key}_to", out var toRaw) && !string.IsNullOrWhiteSpace(toRaw))
                    {
                        var pName = $"{key}_to";
                        if (DateTime.TryParse(toRaw, out var to))
                        {
                            where.Add($"{expr} <= @{pName}");
                            param.Add(pName, to.ToString("yyyy-MM-dd"));
                        }
                    }

                    break;

                case "checkbox":
                case "multi-select":
                    if (filters.TryGetValue(key, out var multiRaw) && !string.IsNullOrWhiteSpace(multiRaw))
                    {
                        var parts = multiRaw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 0)
                        {
                            var tokens = new List<string>();
                            for (var i = 0; i < parts.Length; i++)
                            {
                                var pName = $"{key}_{i}";
                                tokens.Add("@" + pName);
                                param.Add(pName, parts[i]);
                            }

                            where.Add($"{expr} IN ({string.Join(", ", tokens)})");
                        }
                    }

                    break;

                default:
                    if (filters.TryGetValue(key, out var val) && !string.IsNullOrWhiteSpace(val))
                    {
                        where.Add($"{expr} = @{key}");
                        param.Add(key, val);
                    }

                    break;
            }
        }
    }

    private static List<string> BuildWhere(
        EntityDefinition meta,
        string? search,
        Dictionary<string, string?>? filters,
        DynamicParameters param)
    {
        var where = new List<string>();
        ApplyFilters(meta, filters, where, param);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var searchable = meta.Columns.Where(c => c.Value.Searchable).ToList();
            if (searchable.Any())
            {
                var likeClauses = new List<string>();
                foreach (var col in searchable)
                {
                    var expr = col.Value.Expression ?? $"{meta.Table}.{col.Key}";
                    var p = $"@s_{col.Key}";
                    likeClauses.Add($"{expr} LIKE {p}");
                    param.Add(p, $"%{search}%");
                }

                where.Add("(" + string.Join(" OR ", likeClauses) + ")");
            }
        }

        if (meta.SoftDelete)
        {
            where.Add($"( {meta.Table}.IsDeleted = 0 OR {meta.Table}.IsDeleted IS NULL )");
        }

        return where;
    }

    private static void AppendWhere(List<string> sql, List<string> where)
    {
        if (where.Any())
        {
            sql.Add("WHERE " + string.Join(" AND ", where));
        }
    }

    private static string BuildFromClause(EntityDefinition meta)
    {
        var parts = new List<string> { $"FROM {meta.Table}" };
        foreach (var j in meta.Joins)
        {
            parts.Add($"{j.Type.ToUpperInvariant()} JOIN {j.Table} {j.Alias} ON {j.On}");
        }

        return string.Join(" ", parts);
    }

    private static void ValidateMetadata(EntityDefinition meta, string entityName)
    {
        static bool IsUnsafeToken(string value) =>
            value.Contains(';') || value.Contains("--") || value.Contains("/*") || value.Contains("*/");

        static void EnsureIdentifier(string value, string name)
        {
            if (!IdentifierRegex.IsMatch(value))
            {
                throw new InvalidOperationException($"Unsafe identifier '{name}': {value}");
            }
        }

        static void EnsureExpression(string value, string name)
        {
            if (IsUnsafeToken(value) || !ExpressionRegex.IsMatch(value))
            {
                throw new InvalidOperationException($"Unsafe expression '{name}': {value}");
            }
        }

        EnsureIdentifier(meta.Table, $"{entityName}.table");
        EnsureIdentifier(meta.Key, $"{entityName}.key");

        foreach (var col in meta.Columns)
        {
            EnsureIdentifier(col.Key, $"{entityName}.column");
            if (col.Value.Expression != null)
            {
                EnsureExpression(col.Value.Expression, $"{entityName}.columnExpression.{col.Key}");
            }
        }

        foreach (var form in meta.Forms)
        {
            EnsureIdentifier(form.Key, $"{entityName}.form");
            if (form.Value.ForeignKey != null)
            {
                EnsureIdentifier(form.Value.ForeignKey.DisplayColumn, $"{entityName}.form.fkDisplayColumn.{form.Key}");
            }
        }

        foreach (var filter in meta.Filters)
        {
            EnsureIdentifier(filter.Key, $"{entityName}.filter");
            if (filter.Value.Expression != null)
            {
                EnsureExpression(filter.Value.Expression, $"{entityName}.filterExpression.{filter.Key}");
            }

            if (filter.Value.ForeignKey != null)
            {
                EnsureIdentifier(filter.Value.ForeignKey.DisplayColumn, $"{entityName}.filter.fkDisplayColumn.{filter.Key}");
            }
        }

        foreach (var j in meta.Joins)
        {
            if (!AllowedJoinTypes.Contains(j.Type))
            {
                throw new InvalidOperationException($"Unsafe join type '{entityName}.joinType': {j.Type}");
            }

            EnsureIdentifier(j.Table, $"{entityName}.joinTable");
            EnsureIdentifier(j.Alias, $"{entityName}.joinAlias");
            EnsureExpression(j.On, $"{entityName}.joinOn");
        }
    }
}
