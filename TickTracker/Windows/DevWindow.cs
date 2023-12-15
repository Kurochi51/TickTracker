using System;
using System.Numerics;
using System.Collections.Generic;

using ImGuiNET;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;

namespace TickTracker.Windows;

public class DevWindow : Window
{
    public IList<string> PrintLines { get; set; } = new List<string>();

    public DevWindow() : base("DevWindow")
    {

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 200),
            MaximumSize = Plugin.Resolution,
        };
    }

    public override void Draw()
    {
        foreach (var line in PrintLines)
        {
            ImGui.TextUnformatted(line);
        }
        PrintLines.Clear();
    }
}
