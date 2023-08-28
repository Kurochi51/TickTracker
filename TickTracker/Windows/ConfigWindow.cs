using ImGuiNET;
using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace TickTracker.Windows;

public class ConfigWindow : Window, IDisposable
{
    private static Configuration config => Plugin.config;
    private readonly DalamudPluginInterface pluginInterface;

    public ConfigWindow(DalamudPluginInterface _pluginInterface) : base("Timer Settings")
    {
        Size = new(320, 420);
        SizeCondition = ImGuiCond.Appearing;
        Flags = ImGuiWindowFlags.NoResize;
        pluginInterface = _pluginInterface;
    }

    public override void OnClose()
    {
        config.Save(pluginInterface);
    }

    public override void Draw()
    {
        var pluginEnabled = config.PluginEnabled;
        if (ImGui.Checkbox("Enable plugin", ref pluginEnabled))
        {
            config.PluginEnabled = pluginEnabled;
            config.Save(pluginInterface);
        }

        if (ImGui.BeginTabBar("ConfigTabBar", ImGuiTabBarFlags.None))
        {
            DrawAppearanceTab();
            DrawBehaviorTab();
            ImGui.EndTabBar();
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
            Plugin.DebugWindow.Toggle();
        }
        ImGui.SetCursorPos(originPos);
        // Place a button in the bottom right + some padding / extra space
        ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 10f);
        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 5f);
        if (ImGui.Button("Close"))
        {
            config.Save(pluginInterface);
            this.IsOpen = false;
        }
        ImGui.SetCursorPos(originPos);
    }

    private void DrawAppearanceTab()
    {
        if (ImGui.BeginTabItem("Appearance"))
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
                DrawBarPositions();
            }
            DrawColorOptions();
            ImGui.EndTabItem();
        }
    }

    private void DrawBehaviorTab()
    {
        if (ImGui.BeginTabItem("Behavior"))
        {
            ImGui.Spacing();
            var changed = false;

            changed |= ImGui.Checkbox("Show HP Bar", ref config.HPVisible);
            changed |= ImGui.Checkbox("Show MP Bar", ref config.MPVisible);
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
            ImGui.EndTabItem();
        }
    }

    private void DrawColorOptions()
    {
        var colorModified = false;
        var flags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar;
        colorModified |= ImGui.ColorEdit4("HP Bar Background Color", ref config.HPBarBackgroundColor, flags);
        ImGui.SameLine();
        var resetButtonX = ImGui.GetCursorPosX();
        ResetButton("ResetHPBarBackgroundColor", ref config.HPBarBackgroundColor, new Vector4(0f, 0f, 0f, 1f)); // Default Color: Black - #000000

        colorModified |= ImGui.ColorEdit4("HP Bar Fill Color", ref config.HPBarFillColor, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        ResetButton("ResetHPBarFillColor", ref config.HPBarFillColor, new Vector4(0.276f, 0.8f, 0.24f, 1f)); // Default Color: Green - #46CC3D

        colorModified |= ImGui.ColorEdit4("HP Bar Border Color", ref config.HPBarBorderColor, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        ResetButton("ResetHPBarBorderColor", ref config.HPBarBorderColor, new Vector4(0.246f, 0.262f, 0.270f, 1f)); // Default Color: Dark Grey - #3F4345
        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        colorModified |= ImGui.ColorEdit4("MP Bar Background Color", ref config.MPBarBackgroundColor, flags);
        ImGui.SameLine();
        resetButtonX = ImGui.GetCursorPosX();
        ResetButton("ResetMPBarBackgroundColor", ref config.MPBarBackgroundColor, new Vector4(0f, 0f, 0f, 1f)); // Default Color: Black - #000000

        colorModified |= ImGui.ColorEdit4("MP Bar Fill Color", ref config.MPBarFillColor, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        ResetButton("ResetMPBarFillColor", ref config.MPBarFillColor, new Vector4(0.753f, 0.271f, 0.482f, 1f)); // Default Color: Pink - #C0457B

        colorModified |= ImGui.ColorEdit4("MP Bar Border Color", ref config.MPBarBorderColor, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        ResetButton("ResetMPBarBorderColor", ref config.MPBarBorderColor, new Vector4(0.246f, 0.262f, 0.270f, 1f)); // Default Color: Dark Grey - #3F4345
        if (colorModified)
        {
            config.Save(pluginInterface);
        }
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
