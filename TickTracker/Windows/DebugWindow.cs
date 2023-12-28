using System;
using System.Text;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Collections.Concurrent;

using ImGuiNET;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using TickTracker.Structs;

namespace TickTracker.Windows;

public sealed class DebugWindow : Window, IDisposable
{
    /// <summary>
    /// An <see cref="ConcurrentDictionary{TKey, TValue}" /> of Status IDs and Status Names that trigger HP regen
    /// </summary>
    public ConcurrentDictionary<uint, string> HealthRegenDictionary { get; set; } = new();
    /// <summary>
    /// An <see cref="ConcurrentDictionary{TKey, TValue}" /> of Status IDs and Status Names that trigger MP regen
    /// </summary>
    public ConcurrentDictionary<uint, string> ManaRegenDictionary { get; set; } = new();
    /// <summary>
    /// An <see cref="ConcurrentDictionary{TKey, TValue}" /> of Status IDs and Status Names that stop HP regen
    /// </summary>
    public ConcurrentDictionary<uint, string> DisabledHealthRegenDictionary { get; set; } = new();
    /// <summary>
    /// An <see cref="ConcurrentDictionary{TKey, TValue}" /> of Status IDs and Status Names that stop MP regen
    /// </summary>
    public ConcurrentDictionary<uint, string> DisabledManaRegenDictionary { get; set; } = new();

    private readonly DalamudPluginInterface pluginInterface;

    private readonly List<string> healthRegenList = new();
    private readonly List<string> manaRegenList = new();
    private readonly List<string> disabledHealthRegenList = new();
    private readonly List<string> disabledManaRegenList = new();

    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.PreciseWidths;
    private TableStruct table1, table2;
    private Vector2 lastSize = Vector2.Zero;
    private bool invalidList, fontChange, firstTime = true;

    public DebugWindow(DalamudPluginInterface _pluginInterface) : base("DebugWindow")
    {
        pluginInterface = _pluginInterface;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 500),
            MaximumSize = Plugin.Resolution,
        };
        pluginInterface.UiBuilder.AfterBuildFonts += QueueColumnWidthChange;
    }

    public override void OnClose()
    {
        invalidList = false;
    }
    public override void OnOpen()
    {
        if (HealthRegenDictionary.IsEmpty || ManaRegenDictionary.IsEmpty || DisabledHealthRegenDictionary.IsEmpty || DisabledManaRegenDictionary.IsEmpty)
        {
            invalidList = true;
        }
        if (firstTime && !invalidList)
        {
            ProcessDictionaries();
            table1 = new TableStruct()
            {
                Id = "DisabledRegenSID",
                Column1Header = " Disabled HP Regen Status IDs",
                Column2Header = "Disabled MP Regen Status IDs",
                Column1Width = 0,
                Column2Width = 0,
                Column1Content = disabledHealthRegenList,
                Column2Content = disabledManaRegenList,
                Size = Vector2.Zero,
            };
            table2 = new TableStruct()
            {
                Id = "RegenSID",
                Column1Header = " Health Regen Status IDs",
                Column2Header = "Mana Regen Status IDs",
                Column1Width = 0,
                Column2Width = 0,
                Column1Content = healthRegenList,
                Column2Content = manaRegenList,
                Size = Vector2.Zero,
            };
            DetermineColumnWidth(ref table1);
            DetermineColumnWidth(ref table2);
            firstTime = false;
        }
    }

    public override void Draw()
    {
        if (invalidList)
        {
            ImGui.TextUnformatted("Lists are empty!");
            CopyAndClose();
            return;
        }
        var tableSizeChange = false;
        if (fontChange)
        {
            DetermineColumnWidth(ref table1);
            DetermineColumnWidth(ref table2);
            fontChange = false;
            tableSizeChange = true;
        }
        if (lastSize != ImGui.GetWindowSize())
        {
            lastSize = ImGui.GetWindowSize();
            tableSizeChange = true;
        }
        ImGui.TextUnformatted($"HP regen list generated with {healthRegenList.Count} status effects.");
        ImGui.TextUnformatted($"MP regen list generated with {manaRegenList.Count} status effects.");
        ImGui.TextUnformatted($"HP regen disabled list generated with {disabledHealthRegenList.Count} status effects.");
        ImGui.TextUnformatted($"MP regen disabled list generated with {disabledManaRegenList.Count} status effects.");
        ImGui.Spacing();
        using (var tableArea = ImRaii.Child("TableArea", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 40f), border: true, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse))
        {
            if (tableSizeChange)
            {
                table1.Size = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y / 2);
                table2.Size = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y / 2);
            }
            if (table1.Size == Vector2.Zero)
            {
                table1.Size = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y / 2);
            }
            if (table2.Size == Vector2.Zero)
            {
                table2.Size = new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y / 2);
            }
            DrawTable(table1);
            ImGui.Separator();
            ImGui.Spacing();
            DrawTable(table2);
        }
        CopyAndClose();
    }

    private void CopyAndClose()
    {
        var originPos = ImGui.GetCursorPos();
        if (!invalidList)
        {
            // Place two buttons in bottom left + some padding / extra space
            ImGui.SetCursorPosX(10f);
            ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 3f + (ImGui.GetScrollY() * 2));
            if (ImGui.Button("Copy top table"))
            {
                var topTable = new StringBuilder();
                GetTableContentAsText(ref topTable, table1);
                ImGui.SetClipboardText(topTable.ToString());
            }
            ImGui.SameLine();
            if (ImGui.Button("Copy bottom table"))
            {
                var bottomTable = new StringBuilder();
                GetTableContentAsText(ref bottomTable, table2);
                ImGui.SetClipboardText(bottomTable.ToString());
            }
            ImGui.SetCursorPos(originPos);
        }
        // Place a button in bottom right + some padding / extra space
        ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 15f);
        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 3f + (ImGui.GetScrollY() * 2));
        if (ImGui.Button("Close"))
        {
            IsOpen = false;
        }
        ImGui.SetCursorPos(originPos);
    }

    private static void GetTableContentAsText(ref StringBuilder text, TableStruct table)
    {
        text.Append(table.Column1Header.Trim());
        foreach (var item in table.Column1Content)
        {
            text.AppendLine();
            text.Append(item.Trim());
        }
        text.AppendLine();
        text.AppendLine();
        text.Append(table.Column2Header.Trim());
        foreach (var item in table.Column2Content)
        {
            text.AppendLine();
            text.Append(item.Trim());
        }
    }

    private static void DetermineColumnWidth(ref TableStruct table)
    {
        table.Column1Width = ImGui.CalcTextSize(table.Column1Header).X;
        foreach (var item in table.Column1Content)
        {
            var textWidth = ImGui.CalcTextSize(item).X;
            if (textWidth > table.Column1Width)
            {
                table.Column1Width = textWidth;
            }
        }

        table.Column2Width = ImGui.CalcTextSize(table.Column2Header).X;
        foreach (var item in table.Column2Content)
        {
            var textWidth = ImGui.CalcTextSize(item).X;
            if (textWidth > table.Column2Width)
            {
                table.Column2Width = textWidth;
            }
        }
    }

    private static unsafe void DrawTable(TableStruct table)
    {
        if (!table.IsValid())
        {
            ImGui.TextUnformatted($"{table.Id} has an invalid member.\n{table}");
            return;
        }
        using var tableUsable = ImRaii.Table(table.Id, 2, TableFlags, table.Size);
        if (!tableUsable)
        {
            return;
        }

        var clipperCount = Math.Max(table.Column1Content.Count, table.Column2Content.Count);
        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
        clipper.Begin(clipperCount, ImGui.GetTextLineHeightWithSpacing());

        ImGui.TableSetupColumn(table.Column1Header, ImGuiTableColumnFlags.WidthFixed, table.Column1Width);
        ImGui.TableSetupColumn(table.Column2Header, ImGuiTableColumnFlags.WidthFixed, table.Column2Width);
        ImGui.TableSetupScrollFreeze(0, 1);

        ImGui.TableHeadersRow();

        var clipperBreak = false;
        while (clipper.Step())
        {
            if (clipperBreak)
            {
                break;
            }
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                if (i >= clipperCount)
                {
                    clipperBreak = true;
                    break;
                }
                ImGui.TableNextRow();

                // Health Regen column
                ImGui.TableSetColumnIndex(0);
                if (i < table.Column1Content.Count)
                {
                    ImGui.TextUnformatted($"{table.Column1Content[i]}");
                }

                // Mana Regen column
                ImGui.TableSetColumnIndex(1);
                if (i < table.Column2Content.Count)
                {
                    ImGui.TextUnformatted($"{table.Column2Content[i]}");
                }
            }
        }
        clipper.End();
        clipper.Destroy();
    }

    private void ProcessDictionaries()
    {
        foreach (var kvp in HealthRegenDictionary.OrderBy(x => x.Key))
        {
            var entry = " " + kvp.Key + ": " + kvp.Value;
            healthRegenList.Add(entry);
        }
        foreach (var kvp in ManaRegenDictionary.OrderBy(x => x.Key))
        {
            var entry = kvp.Key + ": " + kvp.Value;
            manaRegenList.Add(entry);
        }
        foreach (var kvp in DisabledHealthRegenDictionary.OrderBy(x => x.Key))
        {
            var entry = " " + kvp.Key + ": " + kvp.Value;
            disabledHealthRegenList.Add(entry);
        }
        foreach (var kvp in DisabledManaRegenDictionary.OrderBy(x => x.Key))
        {
            var entry = kvp.Key + ": " + kvp.Value;
            disabledManaRegenList.Add(entry);
        }
    }

    private void QueueColumnWidthChange()
    {
        fontChange = true;
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.AfterBuildFonts -= QueueColumnWidthChange;
    }
}
