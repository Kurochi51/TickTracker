using System;
using System.Numerics;

using ImGuiNET;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;

namespace TickTracker.Windows;

public class ConfigWindow : Window
{
    private readonly Vector4 defaultDarkGrey = new(0.246f, 0.262f, 0.270f, 1f); // #3F4345
    private readonly Vector4 defaultBlue = new(0.169f, 0.747f, 0.892f, 1f); // #2BBEE3
    private readonly Vector4 defaultPink = new(0.753f, 0.271f, 0.482f, 1f); // #C0457B
    private readonly Vector4 defaultGreen = new(0.276f, 0.8f, 0.24f, 1f); // #46CC3D
    private readonly Vector4 defaultBlack = new(0f, 0f, 0f, 1f); // #000000
    private readonly Vector4 defaultWhite = new(1f, 1f, 1f, 1f); // #FFFFFF

    private readonly Vector2 defaultSize = new(320, 470);
    private readonly Vector2 changedSize = new(320, 320);

    private readonly DalamudPluginInterface pluginInterface;
    private readonly DebugWindow debugWindow;
    private readonly Configuration config;

    public ConfigWindow(DalamudPluginInterface _pluginInterface, Configuration _config, DebugWindow _debugWindow) : base("Timer Settings")
    {
        Size = defaultSize;
        Flags = ImGuiWindowFlags.NoResize;
        pluginInterface = _pluginInterface;
        config = _config;
        debugWindow = _debugWindow;
    }

    public override void OnClose()
    {
        config.Save(pluginInterface);
    }

    public override void Draw()
    {
        Size = defaultSize;
        EditConfigProperty("Enable plugin", config, c => c.PluginEnabled, (c, value) => c.PluginEnabled = value, checkbox: true);
        using (var tabBar = ImRaii.TabBar("ConfigTabBar", ImGuiTabBarFlags.None))
        {
            DrawAppearanceTab();
            DrawBehaviorTab();
        }
        DrawCloseButton();
    }

    private void DrawAppearanceTab()
    {
        using var appearanceTab = ImRaii.TabItem("Appearance");
        if (appearanceTab)
        {
            using var child = ImRaii.Child("TabContent", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 40f), border: false);
            ImGui.Spacing();
            EditConfigProperty("Lock bar size and position", config, c => c.LockBar, (c, value) => c.LockBar = value, checkbox: true);
            if (!config.LockBar)
            {
                Size = changedSize;
                DrawBarPositions();
            }
            else
            {
                DrawColorOptions();
            }
        }
    }

    private void DrawBehaviorTab()
    {
        using var settingsTab = ImRaii.TabItem("Settings");
        if (settingsTab)
        {
            using var child = ImRaii.Child("TabContent", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 40f), border: false);
            ImGui.Spacing();
            DrawBarVisibilityOptions();

            EditConfigProperty("Hide bar on full resource", config, c => c.HideOnFullResource, (c, value) => c.HideOnFullResource = value, checkbox: true);

            ImGui.Indent();
            EditConfigProperty("Always show in combat", config, c => c.AlwaysShowInCombat, (c, value) => c.AlwaysShowInCombat = value, checkbox: true);
            ImGui.Unindent();

            EditConfigProperty("Show only in combat", config, c => c.HideOutOfCombat, (c, value) => c.HideOutOfCombat = value, checkbox: true);

            ImGui.Indent();
            EditConfigProperty("Always show while in duties", config, c => c.AlwaysShowInDuties, (c, value) => c.AlwaysShowInDuties = value, checkbox: true);
            EditConfigProperty("Always show with enemy target", config, c => c.AlwaysShowWithHostileTarget, (c, value) => c.AlwaysShowWithHostileTarget = value, checkbox: true);
            EditConfigProperty("Disable collision detection while in combat", config, c => c.DisableCollisionInCombat, (c, value) => c.DisableCollisionInCombat = value, checkbox: true);
            ImGui.Unindent();
        }
    }

    private void DrawCloseButton()
    {
        var originPos = ImGui.GetCursorPos();
        // Place a button in the bottom left
        ImGui.SetCursorPosX(10f);
        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 5f + (ImGui.GetScrollY() * 2));
        if (ImGui.Button("Debug"))
        {
            debugWindow.Toggle();
        }
        ImGui.SetCursorPos(originPos);
        // Place a button in the bottom right + some padding / extra space
        ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 10f);
        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 5f + (ImGui.GetScrollY() * 2));
        if (ImGui.Button("Close"))
        {
            config.Save(pluginInterface);
            IsOpen = false;
        }
        ImGui.SetCursorPos(originPos);
    }

    private void DrawBarPositions()
    {
        ImGui.Indent();
        ImGui.Spacing();
        var HPBarPosition = config.HPBarPosition;
        if (DragInput2(ref HPBarPosition, "HPPositionX", "HPPositionY", "HP Bar Position"))
        {
            config.HPBarPosition = HPBarPosition;
            config.Save(pluginInterface);
        }
        var HPBarSize = config.HPBarSize;
        if (DragInput2Size(ref HPBarSize, "HPSizeX", "HPSizeY", "HP Bar Size"))
        {
            config.HPBarSize = HPBarSize;
            config.Save(pluginInterface);
        }

        ImGui.Spacing();

        var MPBarPosition = config.MPBarPosition;
        if (DragInput2(ref MPBarPosition, "MPPositionX", "MPPositionY", "MP Bar Position"))
        {
            config.MPBarPosition = MPBarPosition;
            config.Save(pluginInterface);
        }

        var MPBarSize = config.MPBarSize;
        if (DragInput2Size(ref MPBarSize, "MPSizeX", "MPSizeY", "MP Bar Size"))
        {
            config.MPBarSize = MPBarSize;
            config.Save(pluginInterface);
        }

        ImGui.Spacing();

        var GPBarPosition = config.GPBarPosition;
        if (DragInput2(ref GPBarPosition, "GPPositionX", "GPPositionY", "GP Bar Position"))
        {
            config.GPBarPosition = GPBarPosition;
            config.Save(pluginInterface);
        }

        var GPBarSize = config.GPBarSize;
        if (DragInput2Size(ref GPBarSize, "GPSizeX", "GPSizeY", "GP Bar Size"))
        {
            config.GPBarSize = GPBarSize;
            config.Save(pluginInterface);
        }
        ImGui.Spacing();
        ImGui.Unindent();
    }

    private void DrawColorOptions()
    {
        var flags = ImGuiColorEditFlags.NoInputs | ImGuiColorEditFlags.AlphaPreviewHalf | ImGuiColorEditFlags.AlphaBar;

        DrawOptionsHP(flags);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawOptionsMP(flags);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        DrawOptionsGP(flags);
    }

    private void DrawOptionsHP(ImGuiColorEditFlags flags)
    {
        EditConfigProperty("HP Bar Background Color", config, c => c.HPBarBackgroundColor, (c, value) => c.HPBarBackgroundColor = value, checkbox: false, button: false, colorEdit: true, flags);
        ImGui.SameLine();
        var resetButtonX = ImGui.GetCursorPosX();
        EditConfigProperty("Reset##ResetHPBarBackgroundColor", config, c => c.HPBarBackgroundColor, (c, value) => c.HPBarBackgroundColor = value, checkbox: false, button: true, colorEdit: false, flags, defaultBlack);

        EditConfigProperty("HP Bar Fill Color", config, c => c.HPBarFillColor, (c, value) => c.HPBarFillColor = value, checkbox: false, button: false, colorEdit: true, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        EditConfigProperty("Reset##ResetHPBarFillColor", config, c => c.HPBarFillColor, (c, value) => c.HPBarFillColor = value, checkbox: false, button: true, colorEdit: false, flags, defaultGreen);

        EditConfigProperty("HP Bar Border Color", config, c => c.HPBarBorderColor, (c, value) => c.HPBarBorderColor = value, checkbox: false, button: false, colorEdit: true, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        EditConfigProperty("Reset##ResetHPBarBorderColor", config, c => c.HPBarBorderColor, (c, value) => c.HPBarBorderColor = value, checkbox: false, button: true, colorEdit: false, flags, defaultDarkGrey);

        EditConfigProperty("HP Icon Color", config, c => c.HPIconColor, (c, value) => c.HPIconColor = value, checkbox: false, button: false, colorEdit: true, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        EditConfigProperty("Reset##ResetHPIconColor", config, c => c.HPIconColor, (c, value) => c.HPIconColor = value, checkbox: false, button: true, colorEdit: false, flags, defaultWhite);
    }

    private void DrawOptionsMP(ImGuiColorEditFlags flags)
    {
        EditConfigProperty("MP Bar Background Color", config, c => c.MPBarBackgroundColor, (c, value) => c.MPBarBackgroundColor = value, checkbox: false, button: false, colorEdit: true, flags);
        ImGui.SameLine();
        var resetButtonX = ImGui.GetCursorPosX();
        EditConfigProperty("Reset##ResetMPBarBackgroundColor", config, c => c.MPBarBackgroundColor, (c, value) => c.MPBarBackgroundColor = value, checkbox: false, button: true, colorEdit: false, flags, defaultBlack);

        EditConfigProperty("MP Bar Fill Color", config, c => c.MPBarFillColor, (c, value) => c.MPBarFillColor = value, checkbox: false, button: false, colorEdit: true, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        EditConfigProperty("Reset##ResetMPBarFillColor", config, c => c.MPBarFillColor, (c, value) => c.MPBarFillColor = value, checkbox: false, button: true, colorEdit: false, flags, defaultPink);

        EditConfigProperty("MP Bar Border Color", config, c => c.MPBarBorderColor, (c, value) => c.MPBarBorderColor = value, checkbox: false, button: false, colorEdit: true, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        EditConfigProperty("Reset##ResetMPBarBorderColor", config, c => c.MPBarBorderColor, (c, value) => c.MPBarBorderColor = value, checkbox: false, button: true, colorEdit: false, flags, defaultDarkGrey);

        EditConfigProperty("MP Icon Color", config, c => c.MPIconColor, (c, value) => c.MPIconColor = value, checkbox: false, button: false, colorEdit: true, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        EditConfigProperty("Reset##ResetMPIconColor", config, c => c.MPIconColor, (c, value) => c.MPIconColor = value, checkbox: false, button: true, colorEdit: false, flags, defaultWhite);
    }

    private void DrawOptionsGP(ImGuiColorEditFlags flags)
    {
        EditConfigProperty("GP Bar Background Color", config, c => c.GPBarBackgroundColor, (c, value) => c.GPBarBackgroundColor = value, checkbox: false, button: false, colorEdit: true, flags);
        ImGui.SameLine();
        var resetButtonX = ImGui.GetCursorPosX();
        EditConfigProperty("Reset##ResetGPBarBackgroundColor", config, c => c.GPBarBackgroundColor, (c, value) => c.GPBarBackgroundColor = value, checkbox: false, button: true, colorEdit: false, flags, defaultBlack);

        EditConfigProperty("GP Bar Fill Color", config, c => c.GPBarFillColor, (c, value) => c.GPBarFillColor = value, checkbox: false, button: false, colorEdit: true, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        EditConfigProperty("Reset##ResetGPBarFillColor", config, c => c.GPBarFillColor, (c, value) => c.GPBarFillColor = value, checkbox: false, button: true, colorEdit: false, flags, defaultBlue);

        EditConfigProperty("GP Bar Border Color", config, c => c.GPBarBorderColor, (c, value) => c.GPBarBorderColor = value, checkbox: false, button: false, colorEdit: true, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        EditConfigProperty("Reset##ResetGPBarBorderColor", config, c => c.GPBarBorderColor, (c, value) => c.GPBarBorderColor = value, checkbox: false, button: true, colorEdit: false, flags, defaultDarkGrey);

        EditConfigProperty("GP Icon Color", config, c => c.GPIconColor, (c, value) => c.GPIconColor = value, checkbox: false, button: false, colorEdit: true, flags);
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        EditConfigProperty("Reset##ResetGPIconColor", config, c => c.GPIconColor, (c, value) => c.GPIconColor = value, checkbox: false, button: true, colorEdit: false, flags, defaultWhite);
    }

    private void DrawBarVisibilityOptions()
    {
        EditConfigProperty("Show HP Bar", config, c => c.HPVisible, (c, value) => c.HPVisible = value, checkbox: true);
        EditConfigProperty("Show MP Bar", config, c => c.MPVisible, (c, value) => c.MPVisible = value, checkbox: true);
        EditConfigProperty("Show GP Bar", config, c => c.GPVisible, (c, value) => c.GPVisible = value, checkbox: true);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Only shown while a Disciple of Land job is active or bars are unlocked.");

        EditConfigProperty("Hide MP bar on melee and ranged DPS", config, c => c.HideMpBarOnMeleeRanged, (c, value) => c.HideMpBarOnMeleeRanged = value, checkbox: true);
        EditConfigProperty("Hide bar on collision with native ui", config, c => c.CollisionDetection, (c, value) => c.CollisionDetection = value, checkbox: true);
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("This only affects the description of abilities and items");
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
        ImGui.TextUnformatted(description);
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
        ImGui.TextUnformatted(description);
        return change;
    }

    // A truly horrific piece of code
    private void EditConfigProperty<T>(string label, Configuration config, Func<Configuration, T> getProperty, Action<Configuration, T> setProperty, bool checkbox = false, bool button = false, bool colorEdit = false, ImGuiColorEditFlags flags = ImGuiColorEditFlags.None, Vector4 defaultColor = default)
    {
        // a T that's a Vector4 and has both button and colorEdit set as true would erroneously be forced to a button,
        // alternatively would have both of them present, which is an invalid use of the function

        var onlyCheckbox = checkbox && !button && !colorEdit;
        var onlyButton = button && !checkbox && !colorEdit;
        var onlyColorEdit = colorEdit && !button && !checkbox;

        if (typeof(T) == typeof(bool) && onlyCheckbox)
        {
            var option = (bool)(object)getProperty(config)!;
            if (ImGui.Checkbox(label, ref option))
            {
                setProperty(config, (T)(object)option);
                config.Save(pluginInterface);
            }
        }
        else if (typeof(T) == typeof(Vector4) && onlyButton)
        {
            if (ImGui.Button(label))
            {
                setProperty(config, (T)(object)defaultColor);
                config.Save(pluginInterface);
            }
        }
        else if (typeof(T) == typeof(Vector4) && onlyColorEdit)
        {
            var vectorValue = (Vector4)(object)getProperty(config)!;
            if (ImGui.ColorEdit4(label, ref vectorValue, flags))
            {
                setProperty(config, (T)(object)vectorValue);
                config.Save(pluginInterface);
            }
        }
        else
        {
            ImGui.TextUnformatted($"Invalid EditConfigProperty invokation.");
        }
    }
}
