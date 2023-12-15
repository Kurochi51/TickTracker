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
    private readonly IGameConfig gameConfig;
    private int sMode = 0, wWidth = 1280, wHeight = 720;

    public DevWindow(IGameConfig _gameConfig) : base("DevWindow")
    {
        gameConfig = _gameConfig;

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

        var size = ImGui.GetMainViewport().Size;
        ImGui.TextUnformatted("ImGui main viewport size: " + size.X + "x" + size.Y);
        ImGui.InputInt("Screen Mode", ref sMode);
        sMode = Math.Clamp(sMode, 0, 2);
        ImGui.InputInt("Windowed Width", ref wWidth);
        wWidth = Math.Clamp(wWidth, 1280, 2560);
        ImGui.InputInt("Windowed Height", ref wHeight);
        wHeight = Math.Clamp(wHeight, 720, 1440);

        var originPos = ImGui.GetCursorPos();
        ImGui.SetCursorPosX(10f);
        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 5f + (ImGui.GetScrollY() * 2));
        if (ImGui.Button("Change ScreenMode"))
        {
            gameConfig.Set(Dalamud.Game.Config.SystemConfigOption.ScreenMode, (uint)sMode);
        }
        ImGui.SameLine();
        if (ImGui.Button("Change Windowed Resolution"))
        {
            gameConfig.Set(Dalamud.Game.Config.SystemConfigOption.ScreenWidth, (uint)wWidth);
            gameConfig.Set(Dalamud.Game.Config.SystemConfigOption.ScreenHeight, (uint)wHeight);
        }
        ImGui.SetCursorPos(originPos);
    }
}
