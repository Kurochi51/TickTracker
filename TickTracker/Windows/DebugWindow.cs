using System;
using System.Text;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Collections.Concurrent;

using ImGuiNET;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;

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

    private string table1Column1, table1Column2, table2Column1, table2Column2;
    private float hpWidth, mpWidth, disabledHPWidth, disabledMPWidth;
    private bool invalidList, fontChange, firstTime = true;

    public DebugWindow(DalamudPluginInterface _pluginInterface) : base("DebugWindow")
    {
        pluginInterface = _pluginInterface;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 500),
            MaximumSize = Plugin.Resolution,
        };
        table1Column1 = table1Column2 = table2Column1 = table2Column2 = string.Empty;
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
            table1Column1 = " Health Regen Status IDs";
            table1Column2 = "Mana Regen Status IDs";
            table2Column1 = " Disabled HP Regen Status IDs";
            table2Column2 = "Disabled MP Regen Status IDs";
            ProcessDictionaries();
            DetermineColumnWidth(table1Column1, table1Column2, healthRegenList, manaRegenList, out hpWidth, out mpWidth);
            DetermineColumnWidth(table2Column1, table2Column2, disabledHealthRegenList, disabledManaRegenList, out disabledHPWidth, out disabledMPWidth);
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
        if (fontChange)
        {
            DetermineColumnWidth(table1Column1, table1Column2, healthRegenList, manaRegenList, out hpWidth, out mpWidth);
            DetermineColumnWidth(table2Column1, table2Column2, disabledHealthRegenList, disabledManaRegenList, out disabledHPWidth, out disabledMPWidth);
            fontChange = false;
        }
        ImGui.TextUnformatted($"HP regen list generated with {healthRegenList.Count} status effects.");
        ImGui.TextUnformatted($"MP regen list generated with {manaRegenList.Count} status effects.");
        ImGui.TextUnformatted($"HP regen disabled list generated with {disabledHealthRegenList.Count} status effects.");
        ImGui.TextUnformatted($"MP regen disabled list generated with {disabledManaRegenList.Count} status effects.");
        ImGui.Spacing();
        using (var scrollArea = ImRaii.Child("ScrollArea", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 40f), border: true))
        {
            DrawTable("DisabledRegenSID", table2Column1, table2Column2, disabledHPWidth, disabledMPWidth, disabledHealthRegenList, disabledManaRegenList);
            ImGui.Separator();
            ImGui.Spacing();
            DrawTable("RegenSID", table1Column1, table1Column2, hpWidth, mpWidth, healthRegenList, manaRegenList);
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
                GetTableContentAsText(ref topTable, "Disabled Health Regen Status IDs", "Disabled Mana Regen Status IDs", disabledHealthRegenList, disabledManaRegenList);
                ImGui.SetClipboardText(topTable.ToString());
            }
            ImGui.SameLine();
            if (ImGui.Button("Copy bottom table"))
            {
                var bottomTable = new StringBuilder();
                GetTableContentAsText(ref bottomTable, "Health Regen Status IDs", "Mana Regen Status IDs", healthRegenList, manaRegenList);
                ImGui.SetClipboardText(bottomTable.ToString());
            }
            ImGui.SetCursorPos(originPos);
        }
        // Place a button in bottom right + some padding / extra space
        ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 15f);
        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 3f + (ImGui.GetScrollY() * 2));
        if (ImGui.Button("Close"))
        {
            this.IsOpen = false;
        }
        ImGui.SetCursorPos(originPos);
    }

    private static void GetTableContentAsText(ref StringBuilder text, string column1Name, string column2Name, IReadOnlyList<string> list1, IReadOnlyList<string> list2)
    {
        text.Append(column1Name);
        foreach (var item in list1)
        {
            text.AppendLine();
            text.Append(item.Trim());
        }
        text.AppendLine();
        text.AppendLine();
        text.Append(column2Name);
        foreach (var item in list2)
        {
            text.AppendLine();
            text.Append(item.Trim());
        }
    }

    private static void DetermineColumnWidth(string column1, string column2, IReadOnlyList<string> list1, IReadOnlyList<string> list2, out float column1Width, out float column2Width)
    {
        column1Width = ImGui.CalcTextSize(column1).X;
        foreach (var item in list1)
        {
            var textWidth = ImGui.CalcTextSize(item);
            if (textWidth.X > column1Width)
            {
                column1Width = textWidth.X;
            }
        }

        column2Width = ImGui.CalcTextSize(column2).X;
        foreach (var item in list2)
        {
            var textWidth = ImGui.CalcTextSize(item);
            if (textWidth.X > column2Width)
            {
                column2Width = textWidth.X;
            }
        }
    }

    private static unsafe void DrawTable(string id, string column1, string column2, float column1Width, float column2Width, IReadOnlyList<string> list1, IReadOnlyList<string> list2)
    {
        using var table = ImRaii.Table(id, 2, ImGuiTableFlags.None);
        if (!table)
        {
            return;
        }

        var maxItemCount = Math.Max(list1.Count, list2.Count);
        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper_ImGuiListClipper());
        clipper.Begin(maxItemCount, ImGui.GetTextLineHeightWithSpacing());

        ImGui.TableSetupColumn(column1, ImGuiTableColumnFlags.WidthFixed, column1Width);
        ImGui.TableSetupColumn(column2, ImGuiTableColumnFlags.WidthFixed, column2Width);
        ImGui.TableHeadersRow();

        while (clipper.Step())
        {
            for (var i = clipper.DisplayStart; i < clipper.DisplayEnd; i++)
            {
                if (i >= maxItemCount)
                {
                    return;
                }
                ImGui.TableNextRow();

                // Health Regen column
                ImGui.TableSetColumnIndex(0);
                if (i < list1.Count)
                {
                    ImGui.TextUnformatted($"{list1[i]}");
                }

                // Mana Regen column
                ImGui.TableSetColumnIndex(1);
                if (i < list2.Count)
                {
                    ImGui.TextUnformatted($"{list2[i]}");
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
