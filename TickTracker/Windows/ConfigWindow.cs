using System;
using System.Numerics;

using ImGuiNET;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Raii;
using Dalamud.Plugin;
using Dalamud.Interface.Components;

namespace TickTracker.Windows;

public class ConfigWindow : Window, IDisposable
{
    private static Configuration config => Plugin.config;
    private readonly DalamudPluginInterface pluginInterface;
    private readonly DebugWindow debugWindow;
    private readonly Vector4 defaultDarkGrey = new(0.246f, 0.262f, 0.270f, 1f); // Default Color: Dark Grey - #3F4345
    private readonly Vector4 defaultBlack = new(0f, 0f, 0f, 1f); // Default Color: Black - #000000
    private readonly Vector4 defaultPink = new(0.753f, 0.271f, 0.482f, 1f); // #C0457B
    private readonly Vector4 defaultGreen = new(0.276f, 0.8f, 0.24f, 1f); // #46CC3D
    private readonly Vector4 defaultBlue = new(0.169f, 0.747f, 0.892f, 1f); // #2BBEE3FF

    public ConfigWindow(DalamudPluginInterface _pluginInterface,DebugWindow _debugWindow) : base("Timer Settings")
    {
        Size = new(320, 420);
        Flags = ImGuiWindowFlags.NoResize;
        pluginInterface = _pluginInterface;
        debugWindow = _debugWindow;
    }

    public override void OnClose()
    {
        config.Save(pluginInterface);
    }

    public override void Draw()
    {
        Size = new(320, 420);
        var pluginEnabled = config.PluginEnabled;
        if (ImGui.Checkbox("Enable plugin", ref pluginEnabled))
        {
            config.PluginEnabled = pluginEnabled;
            config.Save(pluginInterface);
        }
        using (var tabBar = ImRaii.TabBar("ConfigTabBar", ImGuiTabBarFlags.None))
        {
            DrawAppearanceTab();
            DrawBehaviorTab();
        }
        DrawCloseButton();
    }

    private void DrawCloseButton()
    {
        var originPos = ImGui.GetCursorPos();
        // Place a button in the bottom left
        ImGui.SetCursorPosX(10f);
        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 5f);
        if (ImGui.Button("Debug"))
        {
            debugWindow.Toggle();
        }
        ImGui.SetCursorPos(originPos);
        // Place a button in the bottom right + some padding / extra space
        ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 10f);
        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 5f);
        if (ImGui.Button("Close"))
        {
            config.Save(pluginInterface);
            IsOpen = false;
        }
        ImGui.SetCursorPos(originPos);
    }

    private void DrawAppearanceTab()
    {
        using var appearanceTab = ImRaii.TabItem("Appearance");
        if (appearanceTab)
        {
            ImGui.Spacing();
            var lockBar = config.LockBar;
            if (ImGui.Checkbox("Lock bar size and position", ref lockBar))
            {
                config.LockBar = lockBar;
                config.Save(pluginInterface);
            }
            if (!config.LockBar)
            {
                Size = new(320, 560);
                DrawBarPositions();
            }
            DrawColorOptions();
        }
    }

    private void DrawBehaviorTab()
    {
        using var settingsTab = ImRaii.TabItem("Settings");
        if (settingsTab)
        {
            ImGui.Spacing();
            var changed = false;

            changed |= ImGui.Checkbox("Show HP Bar", ref config.HPVisible);
            changed |= ImGui.Checkbox("Show MP Bar", ref config.MPVisible);
            changed |= ImGui.Checkbox("Show GP Bar", ref config.GPVisible);
            ImGui.SameLine();
            ImGuiComponents.HelpMarker("Only shown while a Disciple of Land job is active or bars are unlocked.");
            changed |= ImGui.Checkbox("Hide bar on full resource", ref config.HideOnFullResource);

            ImGui.Indent();
            changed |= ImGui.Checkbox("Always show in combat", ref config.AlwaysShowInCombat);
            ImGui.Unindent();

            changed |= ImGui.Checkbox("Show only in combat", ref config.HideOutOfCombat);

            ImGui.Indent();
            changed |= ImGui.Checkbox("Always show while in duties", ref config.AlwaysShowInDuties);
            changed |= ImGui.Checkbox("Always show with enemy target", ref config.AlwaysShowWithHostileTarget);
            ImGui.Unindent();

            if (changed)
            {
                config.Save(pluginInterface);
            }
        }
    }

    private void DrawColorOptions()
    {
        var colorModified = false;
        var flags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar;

        colorModified |= ImGui.ColorEdit4("HP Bar Background Color", ref config.HPBarBackgroundColor, flags);
        ImGui.SameLine();
        var resetButtonX = ImGui.GetCursorPosX();
        ResetButton("ResetHPBarBackgroundColor", ref config.HPBarBackgroundColor, defaultBlack);
        ColorOption(ref colorModified, "HP Bar Fill Color", ref config.HPBarFillColor, flags, resetButtonX, "ResetHPBarFillColor", defaultGreen);
        ColorOption(ref colorModified, "HP Bar Border Color", ref config.HPBarBorderColor, flags, resetButtonX, "ResetHPBarBorderColor", defaultDarkGrey);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        colorModified |= ImGui.ColorEdit4("MP Bar Background Color", ref config.MPBarBackgroundColor, flags);
        ImGui.SameLine();
        resetButtonX = ImGui.GetCursorPosX();
        ResetButton("ResetMPBarBackgroundColor", ref config.MPBarBackgroundColor, defaultBlack);
        ColorOption(ref colorModified, "MP Bar Fill Color", ref config.MPBarFillColor, flags, resetButtonX, "ResetMPBarFillColor", defaultPink);
        ColorOption(ref colorModified, "MP Bar Border Color", ref config.MPBarBorderColor, flags, resetButtonX, "ResetMPBarBorderColor", defaultDarkGrey);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        colorModified |= ImGui.ColorEdit4("GP Bar Background Color", ref config.GPBarBackgroundColor, flags);
        ImGui.SameLine();
        resetButtonX = ImGui.GetCursorPosX();
        ResetButton("ResetGPBarBackgroundColor", ref config.GPBarBackgroundColor, defaultBlack);
        ColorOption(ref colorModified, "GP Bar Fill Color", ref config.GPBarFillColor, flags, resetButtonX, "ResetGPBarFillColor", defaultBlue);
        ColorOption(ref colorModified, "GP Bar Border Color", ref config.GPBarBorderColor, flags, resetButtonX, "ResetGPBarBorderColor", defaultDarkGrey);

        if (colorModified)
        {
            config.Save(pluginInterface);
        }
    }

    private void ColorOption(ref bool colorModified, string colorLabel, ref Vector4 color, ImGuiColorEditFlags flags, float cursorPos, string resetLabel, Vector4 resetColor)
    {
        colorModified |= ImGui.ColorEdit4(colorLabel, ref color, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(cursorPos);
        ResetButton(resetLabel, ref color, resetColor);
    }

    private void DrawBarPositions()
    {
        ImGui.Indent();
        ImGui.Spacing();
        if (DragInput2(ref config.HPBarPosition, "HPPositionX", "HPPositionY", "HP Bar Position"))
        {
            config.Save(pluginInterface);
        }
        if (DragInput2Size(ref config.HPBarSize, "HPSizeX", "HPSizeY", "HP Bar Size"))
        {
            config.Save(pluginInterface);
        }

        ImGui.Spacing();

        if (DragInput2(ref config.MPBarPosition, "MPPositionX", "MPPositionY", "MP Bar Position"))
        {
            config.Save(pluginInterface);
        }

        if (DragInput2Size(ref config.MPBarSize, "MPSizeX", "MPSizeY", "MP Bar Size"))
        {
            config.Save(pluginInterface);
        }

        ImGui.Spacing();

        if (DragInput2(ref config.GPBarPosition, "GPPositionX", "GPPositionY", "GP Bar Position"))
        {
            config.Save(pluginInterface);
        }

        if (DragInput2Size(ref config.GPBarSize, "GPSizeX", "GPSizeY", "GP Bar Size"))
        {
            config.Save(pluginInterface);
        }
        ImGui.Spacing();
        ImGui.Unindent();
    }
    /// <summary>
    ///     The <see cref="ImGui.DragInt2" /> we have at home.
    /// </summary>
    private static bool DragInput2(ref Vector2 vector, string label1, string label2, string description)
    {
        var change = false;
        var x = (int)vector.X;
        var y = (int)vector.Y;
        ImGui.PushItemWidth(ImGui.GetContentRegionMax().X / 3.5f);
        if (ImGui.DragInt($"##{label1}", ref x))
        {
            vector.X = x;
            change = true;
        }
        ImGui.PopItemWidth();
        ImGui.SameLine();
        ImGui.PushItemWidth(ImGui.GetContentRegionMax().X / 3.5f);
        if (ImGui.DragInt($"##{label2}", ref y))
        {
            vector.Y = y;
            change = true;
        }
        ImGui.PopItemWidth();
        ImGui.SameLine();
        ImGui.Text(description);
        return change;
    }
    /// <summary>
    ///     This exists because the window can't actually go under 32x32 because of the drawn bar inside it.
    /// </summary>
    private static bool DragInput2Size(ref Vector2 vector, string label1, string label2, string description)
    {
        var resolution = ImGui.GetMainViewport().Size;
        var change = false;
        var x = (int)vector.X;
        var y = (int)vector.Y;
        ImGui.PushItemWidth(ImGui.GetContentRegionMax().X / 3.5f);
        if (ImGui.DragInt($"##{label1}", ref x, 1, 32, (int)resolution.X, "%d", ImGuiSliderFlags.AlwaysClamp))
        {
            vector.X = x;
            change = true;
        }
        ImGui.PopItemWidth();
        ImGui.SameLine();
        ImGui.PushItemWidth(ImGui.GetContentRegionMax().X / 3.5f);
        if (ImGui.DragInt($"##{label2}", ref y, 1, 32, (int)resolution.Y, "%d", ImGuiSliderFlags.AlwaysClamp))
        {
            vector.Y = y;
            change = true;
        }
        ImGui.PopItemWidth();
        ImGui.SameLine();
        ImGui.Text(description);
        return change;
    }

    private void ResetButton(string label, ref Vector4 color, Vector4 defaultColor)
    {
        if (ImGui.Button("Reset##" + label))
        {
            color = defaultColor;
            config.Save(pluginInterface);
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
