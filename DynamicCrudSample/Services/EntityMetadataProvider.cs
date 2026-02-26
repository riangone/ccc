using DynamicCrudSample.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DynamicCrudSample.Services;

public interface IEntityMetadataProvider
{
    EntityDefinition Get(string entityName);
    IReadOnlyDictionary<string, EntityDefinition> GetAll();
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
}
