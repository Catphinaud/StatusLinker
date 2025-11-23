using Dalamud.Configuration;

namespace StatusLinker;

public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public List<FavoriteGroup> FavoriteGroups { get; set; } = [];

    public static Configuration InitializeDefaults()
    {
        var config = new Configuration();

        if (config.FavoriteGroups.Count == 0) {
            config.FavoriteGroups.Add(new FavoriteGroup { Name = "Default", StatusIds = [] });
        }

        Svc.PluginInterface.SavePluginConfig(config);

        return config;
    }

    public class FavoriteGroup
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public List<uint> StatusIds { get; set; } = [];
    }

    public void Save()
    {
        Svc.PluginInterface.SavePluginConfig(this);
    }
}
