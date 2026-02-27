// ファイル概要: YAML定義からエンティティメタデータを読み込み提供します。
// このファイルはアプリの重要な構成要素を定義します。
// 保守時は副作用を避けるため、公開シグネチャと呼び出し関係の整合性を維持してください。

using System.Diagnostics.CodeAnalysis;
using DynamicCrudSample.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DynamicCrudSample.Services;

public interface IEntityMetadataProvider
{
    EntityDefinition Get(string entityName);
    IReadOnlyDictionary<string, EntityDefinition> GetAll();
    bool TryGet(string entityName, [NotNullWhen(true)] out EntityDefinition? definition);
}

public class EntityMetadataProvider : IEntityMetadataProvider
{
    private readonly Dictionary<string, EntityDefinition> _entities;

    public EntityMetadataProvider(IWebHostEnvironment env)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        _entities = new Dictionary<string, EntityDefinition>(StringComparer.OrdinalIgnoreCase);

        var dir = Path.Combine(env.ContentRootPath, "config", "entities");
        if (Directory.Exists(dir))
        {
            var files = Directory.GetFiles(dir, "*.yml").OrderBy(x => x).ToList();
            foreach (var file in files)
            {
                var yaml = File.ReadAllText(file);
                var root = deserializer.Deserialize<EntityConfigRoot>(yaml);
                foreach (var entity in root.Entities)
                {
                    _entities[entity.Key] = entity.Value;
                }
            }
        }

        if (_entities.Count == 0)
        {
            var fallback = Path.Combine(env.ContentRootPath, "config", "entities.yml");
            if (!File.Exists(fallback))
            {
                throw new FileNotFoundException("No entity yaml found", fallback);
            }

            var yaml = File.ReadAllText(fallback);
            var root = deserializer.Deserialize<EntityConfigRoot>(yaml);
            foreach (var entity in root.Entities)
            {
                _entities[entity.Key] = entity.Value;
            }
        }
    }

    public EntityDefinition Get(string entityName) => _entities[entityName];

    public IReadOnlyDictionary<string, EntityDefinition> GetAll() => _entities;

    public bool TryGet(string entityName, [NotNullWhen(true)] out EntityDefinition? definition) =>
        _entities.TryGetValue(entityName, out definition);
}
