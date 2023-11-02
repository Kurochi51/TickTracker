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

    private void DrawAppearanceTab()
    {
        using var appearanceTab = ImRaii.TabItem("Appearance");
        if (appearanceTab)
        {
            using var child = ImRaii.Child("TabContent", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 40f), border: false);
            ImGui.Spacing();
            var lockBar = config.LockBar;
            if (ImGui.Checkbox("Lock bar size and position", ref lockBar))
            {
                config.LockBar = lockBar;
                config.Save(pluginInterface);
            }
            if (!lockBar)
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

            var HideOnFullResource = config.HideOnFullResource;
            if (ImGui.Checkbox("Hide bar on full resource", ref HideOnFullResource))
            {
                config.HideOnFullResource = HideOnFullResource;
                config.Save(pluginInterface);
            }
            ImGui.Indent();

            var AlwaysShowInCombat = config.AlwaysShowInCombat;
            if (ImGui.Checkbox("Always show in combat", ref AlwaysShowInCombat))
            {
                config.AlwaysShowInCombat = AlwaysShowInCombat;
                config.Save(pluginInterface);
            }
            ImGui.Unindent();

            var HideOutOfCombat = config.HideOutOfCombat;
            if (ImGui.Checkbox("Show only in combat", ref HideOutOfCombat))
            {
                config.HideOutOfCombat = HideOutOfCombat;
                config.Save(pluginInterface);
            }
            ImGui.Indent();

            var AlwaysShowInDuties = config.AlwaysShowInDuties;
            if (ImGui.Checkbox("Always show while in duties", ref AlwaysShowInDuties))
            {
                config.AlwaysShowInDuties = AlwaysShowInDuties;
                config.Save(pluginInterface);
            }

            var AlwaysShowWithHostileTarget = config.AlwaysShowWithHostileTarget;
            if (ImGui.Checkbox("Always show with enemy target", ref AlwaysShowWithHostileTarget))
            {
                config.AlwaysShowWithHostileTarget = AlwaysShowWithHostileTarget;
                config.Save(pluginInterface);
            }
            var DisableCollisionInCombat = config.DisableCollisionInCombat;
            if (ImGui.Checkbox("Disable collision detection while in combat", ref DisableCollisionInCombat))
            {
                config.DisableCollisionInCombat = DisableCollisionInCombat;
                config.Save(pluginInterface);
            }
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
        var HPBarBackgroundColor = config.HPBarBackgroundColor;
        if (ImGui.ColorEdit4("HP Bar Background Color", ref HPBarBackgroundColor, flags))
        {
            config.HPBarBackgroundColor = HPBarBackgroundColor;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        var resetButtonX = ImGui.GetCursorPosX();
        if (ImGui.Button("Reset##ResetHPBarBackgroundColor"))
        {
            config.HPBarBackgroundColor = defaultBlack;
            config.Save(pluginInterface);
        }

        var HPBarFillColor = config.HPBarFillColor;
        if (ImGui.ColorEdit4("HP Bar Fill Color", ref HPBarFillColor, flags))
        {
            config.HPBarFillColor = HPBarFillColor;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        if (ImGui.Button("Reset##ResetHPBarFillColor"))
        {
            config.HPBarFillColor = defaultGreen;
            config.Save(pluginInterface);
        }

        var HPBarBorderColor = config.HPBarBorderColor;
        if (ImGui.ColorEdit4("HP Bar Border Color", ref HPBarBorderColor, flags))
        {
            config.HPBarBorderColor = HPBarBorderColor;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        if (ImGui.Button("Reset##ResetHPBarBorderColor"))
        {
            config.HPBarBorderColor = defaultDarkGrey;
            config.Save(pluginInterface);
        }

        var HPIconColor = config.HPIconColor;
        if (ImGui.ColorEdit4("HP Icon Color", ref HPIconColor, flags))
        {
            config.HPIconColor = HPIconColor;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        if (ImGui.Button("Reset##ResetHPIconColor"))
        {
            config.HPIconColor = defaultWhite;
            config.Save(pluginInterface);
        }
    }

    private void DrawOptionsMP(ImGuiColorEditFlags flags)
    {
        var MPBarBackgroundColor = config.MPBarBackgroundColor;
        if (ImGui.ColorEdit4("MP Bar Background Color", ref MPBarBackgroundColor, flags))
        {
            config.MPBarBackgroundColor = MPBarBackgroundColor;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        var resetButtonX = ImGui.GetCursorPosX();
        if (ImGui.Button("Reset##ResetMPBarBackgroundColor"))
        {
            config.MPBarBackgroundColor = defaultBlack;
            config.Save(pluginInterface);
        }

        var MPBarFillColor = config.MPBarFillColor;
        if (ImGui.ColorEdit4("MP Bar Fill Color", ref MPBarFillColor, flags))
        {
            config.MPBarFillColor = MPBarFillColor;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        if (ImGui.Button("Reset##ResetMPBarFillColor"))
        {
            config.MPBarFillColor = defaultPink;
            config.Save(pluginInterface);
        }

        var MPBarBorderColor = config.MPBarBorderColor;
        if (ImGui.ColorEdit4("MP Bar Border Color", ref MPBarBorderColor, flags))
        {
            config.MPBarBorderColor = MPBarBorderColor;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        if (ImGui.Button("Reset##ResetMPBarBorderColor"))
        {
            config.MPBarBorderColor = defaultDarkGrey;
            config.Save(pluginInterface);
        }

        var MPIconColor = config.MPIconColor;
        if (ImGui.ColorEdit4("MP Icon Color", ref MPIconColor, flags))
        {
            config.MPIconColor = MPIconColor;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        if (ImGui.Button("Reset##ResetMPIconColor"))
        {
            config.MPIconColor = defaultWhite;
            config.Save(pluginInterface);
        }
    }

    private void DrawOptionsGP(ImGuiColorEditFlags flags)
    {
        var GPBarBackgroundColor = config.GPBarBackgroundColor;
        if (ImGui.ColorEdit4("GP Bar Background Color", ref GPBarBackgroundColor, flags))
        {
            config.GPBarBackgroundColor = GPBarBackgroundColor;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        var resetButtonX = ImGui.GetCursorPosX();
        if (ImGui.Button("Reset##ResetGPBarBackgroundColor"))
        {
            config.GPBarBackgroundColor = defaultBlack;
            config.Save(pluginInterface);
        }
        var GPBarFillColor = config.GPBarFillColor;
        if (ImGui.ColorEdit4("GP Bar Fill Color", ref GPBarFillColor, flags))
        {
            config.GPBarFillColor = GPBarFillColor;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        if (ImGui.Button("Reset##ResetGPBarFillColor"))
        {
            config.GPBarFillColor = defaultBlue;
            config.Save(pluginInterface);
        }

        var GPBarBorderColor = config.GPBarBorderColor;
        if (ImGui.ColorEdit4("GP Bar Border Color", ref GPBarBorderColor, flags))
        {
            config.GPBarBorderColor = GPBarBorderColor;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        if (ImGui.Button("Reset##ResetGPBarBorderColor"))
        {
            config.GPBarBorderColor = defaultDarkGrey;
            config.Save(pluginInterface);
        }

        var GPIconColor = config.GPIconColor;
        if (ImGui.ColorEdit4("GP Icon Color", ref GPIconColor, flags))
        {
            config.GPIconColor = GPIconColor;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        ImGui.SetCursorPosX(resetButtonX);
        if (ImGui.Button("Reset##ResetGPIconColor"))
        {
            config.GPIconColor = defaultWhite;
            config.Save(pluginInterface);
        }
    }

    private void DrawBarVisibilityOptions()
    {
        var HPVisible = config.HPVisible;
        if (ImGui.Checkbox("Show HP Bar", ref HPVisible))
        {
            config.HPVisible = HPVisible;
            config.Save(pluginInterface);
        }
        var MPVisible = config.MPVisible;
        if (ImGui.Checkbox("Show MP Bar", ref MPVisible))
        {
            config.MPVisible = MPVisible;
            config.Save(pluginInterface);
        }
        var GPVisible = config.GPVisible;
        if (ImGui.Checkbox("Show GP Bar", ref GPVisible))
        {
            config.GPVisible = GPVisible;
            config.Save(pluginInterface);
        }
        ImGui.SameLine();
        ImGuiComponents.HelpMarker("Only shown while a Disciple of Land job is active or bars are unlocked.");
        var HideMpBarOnMeleeRanged = config.HideMpBarOnMeleeRanged;
        if (ImGui.Checkbox("Hide MP bar on melee and ranged DPS", ref HideMpBarOnMeleeRanged))
        {
            config.HideMpBarOnMeleeRanged = HideMpBarOnMeleeRanged;
            config.Save(pluginInterface);
        }
        var CollisionDetection = config.CollisionDetection;
        if (ImGui.Checkbox("Hide bar on collision with native ui", ref CollisionDetection))
        {
            config.CollisionDetection = CollisionDetection;
            config.Save(pluginInterface);
        }
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
}
