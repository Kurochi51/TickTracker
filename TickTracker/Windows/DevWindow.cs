using System.Numerics;
using System.Collections.Generic;

using ImGuiNET;
using Dalamud.Interface.Windowing;
using System;

namespace TickTracker.Windows;

public sealed class DevWindow : Window
{
    private static readonly List<string> PrintLines = new();
    public int partId;
    public int partListIndex;

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
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 3);
        if (ImGui.InputInt("ImageNode PartId", ref partId, 1))
        {
            partId = Math.Clamp(partId, 0, 6);
        }
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 3);
        ImGui.InputInt("ImageNode PartsList Index", ref partListIndex, 1);
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
