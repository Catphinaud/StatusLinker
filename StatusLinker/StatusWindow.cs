using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Windowing;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;

namespace StatusLinker;

public class StatusWindow : Window
{
    internal readonly Plugin Plugin;
    internal readonly Configuration Configuration;
    internal string SearchQuery = string.Empty;
    internal uint? SelectedStatusId;
    internal int StatusCount;

    internal Configuration.FavoriteGroup? SelectedFavoriteGroup;
    private string _newGroupName = string.Empty;
    private string _renameGroupBuffer = string.Empty;
    private uint? _lastSelectedStatusId;


    public StatusWindow(Plugin plugin) : base("Status Linker")
    {
        Plugin = plugin;
        Configuration = plugin.Configuration;
        RespectCloseHotkey = true;
        IsOpen = false;
        StatusCount = Plugin.StatusCache.Count;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(600, 500),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue)
        };

        SelectedFavoriteGroup ??= Configuration.FavoriteGroups.FirstOrDefault();
        if (SelectedFavoriteGroup != null) {
            _renameGroupBuffer = SelectedFavoriteGroup.Name;
        }
    }

    public override void Draw()
    {
        // left groups, right statuses
        var avail = ImGui.GetContentRegionAvail();
        float leftWidth = Math.Max(250f, avail.X * 0.30f);

        ImGui.BeginChild("##status_linker_groups_panel", new Vector2(leftWidth, avail.Y), true);
        DrawGroupsPanel();
        ImGui.EndChild();

        ImGui.SameLine();

        ImGui.BeginChild("##status_linker_status_panel", new Vector2(0, avail.Y), true);
        DrawStatusPanel();
        ImGui.EndChild();
    }

    private unsafe void DrawGroupsPanel()
    {
        ImGui.Text("Favorite Groups");
        ImGui.Separator();

        // Group list
        if (Configuration.FavoriteGroups.Count == 0) {
            ImGui.TextDisabled("No groups yet.");
        } else {
            foreach (var g in Configuration.FavoriteGroups) {
                bool selected = SelectedFavoriteGroup != null && g.Id == SelectedFavoriteGroup.Id;
                if (ImGui.Selectable(g.Name + $"##group_{g.Id}", selected)) {
                    SelectedFavoriteGroup = g;
                    _renameGroupBuffer = g.Name;
                }

                if (selected) {
                    ImGui.SetItemDefaultFocus();
                }
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Text("New Group");
        ImGui.InputText("##new_group_name", ref _newGroupName, 64);
        ImGui.SameLine();
        if (ImGui.Button("Add##add_group_btn")) {
            var name = _newGroupName.Trim();
            if (name.Length > 0 && !Configuration.FavoriteGroups.Any(g => g.Name.Equals(name, StringComparison.OrdinalIgnoreCase))) {
                var group = new Configuration.FavoriteGroup { Name = name };
                Configuration.FavoriteGroups.Add(group);
                SelectedFavoriteGroup = group;
                _renameGroupBuffer = group.Name;
                Configuration.Save();
            }

            _newGroupName = string.Empty;
        }

        // Edit selected group stuff
        if (SelectedFavoriteGroup != null) {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text("Edit Selected Group");
            ImGui.InputText("##rename_group", ref _renameGroupBuffer, 64);
            ImGui.SameLine();
            // if (ImGui.Button("Save##rename_group_btn")) {
            if (ImGuiComponents.IconButton(FontAwesomeIcon.Save)) {
                var newName = _renameGroupBuffer.Trim();
                if (newName.Length > 0 &&
                    !Configuration.FavoriteGroups.Any(g => g != SelectedFavoriteGroup && g.Name.Equals(newName, StringComparison.OrdinalIgnoreCase))) {
                    SelectedFavoriteGroup!.Name = newName;
                    Configuration.Save();
                }
            }

            ImGui.SameLine();

            bool flag = !ImGui.GetIO().KeyCtrl;

            if (flag) {
                ImGui.BeginDisabled();
            }

            if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash) && !flag) {
                Configuration.FavoriteGroups.Remove(SelectedFavoriteGroup!);
                SelectedFavoriteGroup = Configuration.FavoriteGroups.FirstOrDefault();
                _renameGroupBuffer = SelectedFavoriteGroup?.Name ?? string.Empty;
                Configuration.Save();
            }

            if (ImGui.IsWindowHovered()) {
                var ttPos = ImGui.GetItemRectMin();
                if (ImGui.GetIO().MousePos.X >= ttPos.X && ImGui.GetIO().MousePos.X <= ttPos.X + ImGui.GetItemRectSize().X &&
                    ImGui.GetIO().MousePos.Y >= ttPos.Y && ImGui.GetIO().MousePos.Y <= ttPos.Y + ImGui.GetItemRectSize().Y) {
                    ImGui.SetNextWindowPos(ttPos, ImGuiCond.Always, new Vector2(0, 1));
                    ImGui.PushStyleVar(ImGuiStyleVar.Alpha, 1.0f);
                    ImGui.BeginTooltip();
                    ImGui.TextUnformatted("Hold Ctrl to enable deletion");
                    ImGui.EndTooltip();
                    ImGui.PopStyleVar();
                }
            }

            if (flag) {
                ImGui.EndDisabled();
            }

            // Favorites list for selected group
            if (SelectedFavoriteGroup == null) return;

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Text($"Favorites ({SelectedFavoriteGroup.StatusIds.Count})");
            if (SelectedFavoriteGroup.StatusIds.Count != 0) {
                if (ImGui.BeginChild("##favorites_child", new Vector2(0, 140), true)) {
                    foreach (var id in SelectedFavoriteGroup.StatusIds.ToList()) {
                        if (!Plugin.StatusCache.TryGetValue(id, out var status)) {
                            continue;
                        }

                        ImGui.PushID("fav_" + id);

                        if (ImGui.GetIO().KeyCtrl &&
                            ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) {
                            SelectedFavoriteGroup.StatusIds.Remove(id);
                            Configuration.Save();
                        }

                        if (!ImGui.GetIO().KeyCtrl) {
                            var last = _lastSelectedStatusId;
                            if (last == status.RowId) {
                                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.26f, 0.59f, 0.98f, 0.40f));
                                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.26f, 0.59f, 0.98f, 0.60f));
                            }

                            if (
                                ImGuiComponents.IconButton(FontAwesomeIcon.Paw)) {
                                _lastSelectedStatusId = id;
                                SelectedStatusId = id;
                                AgentChatLog.Instance()->ContextStatusId = id;
                            }

                            if (last == status.RowId) {
                                ImGui.PopStyleColor(2);
                            }
                        }

                        // if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash)) {
                        //     SelectedFavoriteGroup.StatusIds.Remove(id);
                        //     Configuration.Save();
                        // }

                        ImGui.PopID();
                        ImGui.SameLine();

                        ImGui.TextWrapped($"{status.Name}");
                    }
                }

                ImGui.EndChild();
            } else {
                ImGui.TextDisabled("None yet. Star statuses to add.");
            }
        }
    }

    private void DrawStatusPanel()
    {
        var width = ImGui.GetContentRegionAvail().X;
        ImGui.SetNextItemWidth(100);
        ImGui.PushFont(UiBuilder.MonoFont);
        ImGui.Text($"Total: {StatusCount.ToString().PadRight(5, ' ')}");
        ImGui.PopFont();
        ImGui.SameLine(0, 0);
        ImGui.SetNextItemWidth(width - ImGui.GetCursorPosX() - ImGui.GetStyle().FramePadding.X);
        if (ImGui.InputText("##status_linker_search", ref SearchQuery, 256)) {
            // Fix count
            if (string.IsNullOrWhiteSpace(SearchQuery)) {
                StatusCount = Plugin.StatusCache.Count;
            } else {
                StatusCount = Plugin.StatusCache.Values.Count(s =>
                    s.RowId.ToString().Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) ||
                    s.Name.ToString().Contains(SearchQuery, StringComparison.OrdinalIgnoreCase));
            }
        }

        ImGui.Separator();

        var avail = ImGui.GetContentRegionAvail();
        ImGui.BeginChild("##status_list_child", new Vector2(0, Math.Max(0f, avail.Y)));
        DrawStatusList();
        ImGui.EndChild();
    }

    public void DrawStatusList()
    {
        if (SearchQuery.Length > 0) {
            for (int i = 0; i < Plugin.StatusCache.Count; i++) {
                var status = Plugin.StatusCache.ElementAt(i).Value;
                if (!status.RowId.ToString().Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) &&
                    !status.Name.ToString().Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)) {
                    continue;
                }

                DrawStatusEntry(status);
            }

            return;
        }


        var clipper = new ImGuiListClipper();
        clipper.Begin(StatusCount);

        while (clipper.Step()) {
            for (int i = clipper.DisplayStart; i < clipper.DisplayEnd; i++) {
                var status = Plugin.StatusCache.ElementAt(i).Value;
                DrawStatusEntry(status);
            }
        }
    }

    private unsafe void DrawStatusEntry(Status status)
    {
        if (!string.IsNullOrWhiteSpace(SearchQuery)) {
            if (!status.RowId.ToString().Contains(SearchQuery, StringComparison.OrdinalIgnoreCase) &&
                !status.Name.ToString().Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)) {
                return;
            }
        }

        uint statusId = status.RowId;
        bool isFavorite = SelectedFavoriteGroup != null && SelectedFavoriteGroup.StatusIds.Contains(statusId);
        if (isFavorite) {
            ImGui.PushStyleColor(ImGuiCol.Header, ImGui.GetColorU32(ImGuiCol.PlotHistogram));
            ImGui.PushStyleColor(ImGuiCol.HeaderHovered, ImGui.GetColorU32(ImGuiCol.PlotHistogramHovered));
            ImGui.PushStyleColor(ImGuiCol.HeaderActive, ImGui.GetColorU32(ImGuiCol.TabActive));
        }

        bool isFav = SelectedFavoriteGroup?.StatusIds.Contains(status.RowId) == true;
        if (SelectedFavoriteGroup != null) {
            if (ImGui.SmallButton($"{(isFav ? "★" : "☆")}##toggle_fav_{status.RowId}")) {
                if (isFav) {
                    SelectedFavoriteGroup.StatusIds.Remove(status.RowId);
                } else {
                    SelectedFavoriteGroup.StatusIds.Add(status.RowId);
                }

                Configuration.Save();
            }

            ImGui.SameLine();
        }

        var last = _lastSelectedStatusId;
        if (last == status.RowId) {
            ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.26f, 0.59f, 0.98f, 0.40f));
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.26f, 0.59f, 0.98f, 0.60f));
        }

        if (ImGui.SmallButton($"Select##status{status.RowId}")) {
            SelectedStatusId = status.RowId;
            _lastSelectedStatusId = status.RowId;
            AgentChatLog.Instance()->ContextStatusId = status.RowId;
        }

        if (last == status.RowId) {
            ImGui.PopStyleColor(2);
        }

        ImGui.SameLine();

        ImGui.Text($"{status.RowId}:");
        ImGui.SameLine();
        ImGui.TextUnformatted(status.Name.ToString());
        if (isFavorite) {
            ImGui.PopStyleColor(3);
        }
    }
}
