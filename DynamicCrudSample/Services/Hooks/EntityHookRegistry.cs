// ファイル概要: DI に登録された IEntityHook 実装を名前で検索するレジストリ実装です。

namespace DynamicCrudSample.Services.Hooks;

/// <summary>
/// DI コンテナから IEnumerable&lt;IEntityHook&gt; を受け取り、
/// 名前をキーとした辞書に格納するレジストリ実装。
/// フック名の重複がある場合は後勝ちになります。
/// </summary>
public class EntityHookRegistry : IEntityHookRegistry
{
    private readonly Dictionary<string, IEntityHook> _hooks;
    private readonly ILogger<EntityHookRegistry> _logger;

    public EntityHookRegistry(IEnumerable<IEntityHook> hooks, ILogger<EntityHookRegistry> logger)
    {
        _logger = logger;
        _hooks = new Dictionary<string, IEntityHook>(StringComparer.OrdinalIgnoreCase);
        foreach (var hook in hooks)
        {
            if (_hooks.ContainsKey(hook.Name))
            {
                _logger.LogWarning("Duplicate hook name '{Name}' — overwriting with {Type}", hook.Name, hook.GetType().Name);
            }
            _hooks[hook.Name] = hook;
        }

        _logger.LogInformation("EntityHookRegistry initialized with {Count} hook(s): {Names}",
            _hooks.Count, string.Join(", ", _hooks.Keys));
    }

    public IEntityHook? Find(string name)
    {
        if (_hooks.TryGetValue(name, out var hook))
        {
            return hook;
        }

        _logger.LogWarning("Hook '{Name}' not found in registry. Check entities.yml and DI registration.", name);
        return null;
    }
}
