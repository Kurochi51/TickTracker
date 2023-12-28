using System.Numerics;
using System.Collections.Generic;

using ImGuiNET;
using Dalamud.Interface.Windowing;

namespace TickTracker.Windows;

public class DevWindow : Window
{
    private static IList<string> PrintLines { get; set; } = new List<string>();

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

    public static void Print(string text)
    {
        PrintLines.Add(text);
    }

    public static void Separator()
    {
        PrintLines.Add("--------------------------");
    }
}
