using System.Numerics;
using System.Collections.Generic;

using ImGuiNET;
using Dalamud.Interface.Windowing;

namespace TickTracker.Windows;

public class DevWindow : Window
{
    public IList<string> printLines { get; set; } = new List<string>();
    public DevWindow() : base("DevWindow")
    {
        var resolution = ImGui.GetMainViewport().Size;
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 300),
            MaximumSize = resolution,
        };
    }

    public override void Draw()
    {
        foreach (var line in printLines)
        {
            ImGui.TextUnformatted(line);
        }
        printLines.Clear();
    }
}
