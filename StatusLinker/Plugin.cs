using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Lumina.Excel.Sheets;

namespace StatusLinker;

public class Plugin : IDalamudPlugin
{
    public const string CommandName = "/slink";

    internal IDalamudPluginInterface PluginInterface;
    internal Configuration Configuration;
    internal WindowSystem WindowSystem;
    internal StatusWindow StatusWindow;

    internal Dictionary<uint, Status> StatusCache;
    internal Dictionary<uint, string> StatusNameCache;

    public Plugin(IDalamudPluginInterface pluginInterface)
    {
        PluginInterface = pluginInterface;

        Svc.Initialize(PluginInterface);

        StatusCache = Svc.Data.GetExcelSheet<Status>().Where(s => !string.IsNullOrEmpty(s.Name.ToString()))
            .Where(s => !s.Name.ToString().StartsWith("_rsv_"))
            .GroupBy(s => s.Name.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToDictionary(s => s.RowId, s => s);
        StatusNameCache = Svc.Data.GetExcelSheet<Status>()
            .Where(s => !string.IsNullOrEmpty(s.Name.ToString()))
            .Where(s => !s.Name.ToString().StartsWith("_rsv_"))
            .GroupBy(s => s.Name.ToString(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToDictionary(s => s.RowId, s => s.Name.ToString());
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? Configuration.InitializeDefaults();

        WindowSystem = new WindowSystem("StatusLinker");

        StatusWindow = new StatusWindow(this);

        WindowSystem.AddWindow(StatusWindow);

        // Draw and config
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi += OpenStatusWindow;


        Svc.Commands.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = "Opens the Status Linker window."
        });
    }

    private void OpenStatusWindow()
    {
        StatusWindow.IsOpen = !StatusWindow.IsOpen;
    }

    private void OnCommand(string command, string arguments)
    {
        if (string.IsNullOrWhiteSpace(arguments)) {
            StatusWindow.IsOpen = true;
        }

        // @todo Try finding a group name matching the arguments or ID else try to find a status matching the arguments or ID to search for
    }

    public void Dispose()
    {
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenMainUi -= OpenStatusWindow;

        WindowSystem.RemoveAllWindows();

        Svc.Commands.RemoveHandler(CommandName);
    }
}
