using Dalamud.Game;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace StatusLinker;

public class Svc
{
    public static void Initialize(IDalamudPluginInterface pluginInterface) => pluginInterface.Create<Svc>();

    [PluginService] public static ICommandManager Commands { get; private set; } = null!;

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService] public static IDataManager Data { get; private set; } = null!;
}
