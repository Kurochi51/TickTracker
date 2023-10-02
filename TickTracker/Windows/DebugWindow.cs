using System;
using System.Text;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using System.Collections.Concurrent;

using ImGuiNET;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;

namespace TickTracker.Windows;

public class DebugWindow : Window
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

    private float hpWidth, mpWidth, disabledHPWidth, disabledMPWidth;
    private bool invalidList, firstTime = true;

    public DebugWindow() : base("DebugWindow")
    {
        Size = new(400, 500);
        SizeCondition = ImGuiCond.Appearing;
        Flags = ImGuiWindowFlags.NoResize;
    }

    public override void OnClose()
    {
        invalidList = false;
    }

    public override void Draw()
    {
        if (HealthRegenDictionary.IsEmpty || ManaRegenDictionary.IsEmpty || DisabledHealthRegenDictionary.IsEmpty || DisabledManaRegenDictionary.IsEmpty)
        {
            invalidList = true;
            ImGui.Text("Lists are empty!");
            CopyAndClose();
            return;
        }
        ImGui.Text($"HP regen list generated with {HealthRegenDictionary.Count} status effects.");
        ImGui.Text($"MP regen list generated with {ManaRegenDictionary.Count} status effects.");
        ImGui.Text($"HP regen disabled list generated with {DisabledHealthRegenDictionary.Count} status effects.");
        ImGui.Text($"MP regen disabled list generated with {DisabledManaRegenDictionary.Count} status effects.");
        ImGui.Spacing();
        var maxRegenCount = Math.Max(HealthRegenDictionary.Count, ManaRegenDictionary.Count);
        var maxDisabledRegenCount = Math.Max(DisabledHealthRegenDictionary.Count, DisabledManaRegenDictionary.Count);
        var Table1Column1 = " Health Regen Status IDs";
        var Table1Column2 = "Mana Regen Status IDs";
        var Table2Column1 = " Disabled HP Regen Status IDs";
        var Table2Column2 = "Disabled MP Regen Status IDs";
        if (firstTime)
        {
            DetermineColumnWidth(Table1Column1, Table1Column2, HealthRegenDictionary, ManaRegenDictionary, ref hpWidth, ref mpWidth);
            DetermineColumnWidth(Table2Column1, Table2Column2, DisabledHealthRegenDictionary, DisabledManaRegenDictionary, ref disabledHPWidth, ref disabledMPWidth);
            firstTime = false;
        }
        using (var scrollArea = ImRaii.Child("ScrollArea", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 40f), border: true))
        {
            DrawTable("DisabledRegenSID", Table2Column1, Table2Column2, disabledHPWidth, disabledMPWidth, maxDisabledRegenCount, DisabledHealthRegenDictionary, DisabledManaRegenDictionary);
            ImGui.Separator();
            ImGui.Spacing();
            DrawTable("RegenSID", Table1Column1, Table1Column2, hpWidth, mpWidth, maxRegenCount, HealthRegenDictionary, ManaRegenDictionary);
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
            ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 5f);
            if (ImGui.Button("Copy top table"))
            {
                var topTable = new StringBuilder();
                GetTableContentAsText(ref topTable, "Disabled Health Regen Status IDs", "Disabled Mana Regen Status IDs", DisabledHealthRegenDictionary, DisabledManaRegenDictionary);
                ImGui.SetClipboardText(topTable.ToString());
            }
            ImGui.SameLine();
            if (ImGui.Button("Copy bottom table"))
            {
                var bottomTable = new StringBuilder();
                GetTableContentAsText(ref bottomTable, "Health Regen Status IDs", "Mana Regen Status IDs", HealthRegenDictionary, ManaRegenDictionary);
                ImGui.SetClipboardText(bottomTable.ToString());
            }
            ImGui.SetCursorPos(originPos);
        }
        // Place a button in bottom right + some padding / extra space
        ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 10f);
        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 5f);
        if (ImGui.Button("Close"))
        {
            this.IsOpen = false;
        }
        ImGui.SetCursorPos(originPos);
    }

    private static void GetTableContentAsText(ref StringBuilder text, string column1Name, string column2Name, IDictionary<uint, string> dictionary1, IDictionary<uint, string> dictionary2)
    {
        text.Append(column1Name);
        for (var i = 0; i < dictionary1.Count; i++)
        {
            text.AppendLine();
            var kvp = dictionary1.ElementAt(i);
            text.Append($"{kvp.Key}: {kvp.Value}");
        }
        text.AppendLine();
        text.AppendLine();
        text.Append(column2Name);
        for (var i = 0; i < dictionary2.Count; i++)
        {
            text.AppendLine();
            var kvp = dictionary2.ElementAt(i);
            text.Append($"{kvp.Key}: {kvp.Value}");
        }
    }

    private static void DetermineColumnWidth(string column1, string column2, IDictionary<uint, string> dictionary1, IDictionary<uint, string> dictionary2, ref float column1Width, ref float column2Width)
    {
        column1Width = ImGui.CalcTextSize(column1).X;
        foreach (var item in dictionary1)
        {
            var textWidth = ImGui.CalcTextSize(item.Value) + ImGui.CalcTextSize(": ") + ImGui.CalcTextSize(item.Key.ToString());
            if (textWidth.X > column1Width)
            {
                column1Width = textWidth.X;
            }
        }

        column2Width = ImGui.CalcTextSize(column2).X;
        foreach (var item in dictionary2)
        {
            var textWidth = ImGui.CalcTextSize(item.Value) + ImGui.CalcTextSize(": ") + ImGui.CalcTextSize(item.Key.ToString());
            if (textWidth.X > column2Width)
            {
                column2Width = textWidth.X;
            }
        }
    }

    private static void DrawTable(string id, string column1, string column2, float column1Width, float column2Width, int maxItemCount, IDictionary<uint, string> dictionary1, IDictionary<uint, string> dictionary2)
    {
        using var table = ImRaii.Table(id, 2, ImGuiTableFlags.None);

        ImGui.TableSetupColumn(column1, ImGuiTableColumnFlags.WidthFixed, column1Width);
        ImGui.TableSetupColumn(column2, ImGuiTableColumnFlags.WidthFixed, column2Width);
        ImGui.TableHeadersRow();
        ImGui.TableNextRow();

        for (var i = 0; i < maxItemCount; i++)
        {
            // Health Regen column
            ImGui.TableSetColumnIndex(0);
            if (i < dictionary1.Count)
            {
                var kvp = dictionary1.ElementAt(i);
                ImGui.Text($" {kvp.Key}: {kvp.Value}");
            }

            // Mana Regen column
            ImGui.TableSetColumnIndex(1);
            if (i < dictionary2.Count)
            {
                var kvp = dictionary2.ElementAt(i);
                ImGui.Text($"{kvp.Key}: {kvp.Value}");
            }

            if (i + 1 < maxItemCount)
            {
                ImGui.TableNextRow();
            }
        }
    }
}
