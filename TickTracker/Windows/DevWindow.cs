using System.Numerics;
using System.Collections.Generic;

using ImGuiNET;
using Dalamud.Interface.Windowing;
using System;

namespace TickTracker.Windows;

public sealed class DevWindow : Window
{
    private static readonly List<string> PrintLines = new();
    public int partId { get; set; }
    public int partListIndex { get; set; }

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
        var pId = partId;
        var pListIndex = partListIndex;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 3);
        if (ImGui.InputInt("ImageNode PartId", ref pId, 1))
        {
            partId = pId;
        }
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 3);
        if (ImGui.InputInt("ImageNode PartsList Index", ref pListIndex, 1))
        {
            partListIndex = pListIndex;
        }
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
