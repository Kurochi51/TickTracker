using System;
using System.Text;
using System.Linq;
using System.Numerics;
using System.Collections.Frozen;
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

    /*private FrozenSet<string> healthRegenSet = null!;
    private FrozenSet<string> manaRegenSet = null!;
    private FrozenSet<string> disabledHealthRegenSet = null!;
    private FrozenSet<string> disabledManaRegenSet = null!;*/
    private FrozenDictionary<uint, string> healthRegenDic = null!;
    private FrozenDictionary<uint, string> manaRegenDic = null!;
    private FrozenDictionary<uint, string> disabledHealthRegenDic = null!;
    private FrozenDictionary<uint, string> disabledManaRegenDic = null!;

    private string table1Column1, table1Column2, table2Column1, table2Column2;
    private float hpWidth, mpWidth, disabledHPWidth, disabledMPWidth;
    private bool invalidList, firstTime = true;

    public DebugWindow() : base("DebugWindow")
    {
        Size = new(400, 500);
        SizeCondition = ImGuiCond.Appearing;
        Flags = ImGuiWindowFlags.NoResize;
        table1Column1 = table1Column2 = table2Column1 = table2Column2 = string.Empty;
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
            //ProcessDictionaries();
            healthRegenDic = HealthRegenDictionary.OrderBy(x => x.Key).ToFrozenDictionary();
            manaRegenDic = ManaRegenDictionary.OrderBy(x => x.Key).ToFrozenDictionary();
            disabledHealthRegenDic = DisabledHealthRegenDictionary.OrderBy(x => x.Key).ToFrozenDictionary();
            disabledManaRegenDic = DisabledManaRegenDictionary.OrderBy(x => x.Key).ToFrozenDictionary();
            //DetermineColumnWidth(table1Column1, table1Column2, healthRegenSet, manaRegenSet, out hpWidth, out mpWidth);
            DetermineColumnWidth2(table1Column1, table1Column2, healthRegenDic, manaRegenDic, out hpWidth, out mpWidth);
            DetermineColumnWidth2(table2Column1, table2Column2, disabledHealthRegenDic, disabledManaRegenDic, out disabledHPWidth, out disabledMPWidth);
            //DetermineColumnWidth(table2Column1, table2Column2, disabledHealthRegenSet, disabledManaRegenSet, out disabledHPWidth, out disabledMPWidth);
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
        ImGui.TextUnformatted($"HP regen list generated with {healthRegenDic.Count} status effects.");
        ImGui.TextUnformatted($"MP regen list generated with {manaRegenDic.Count} status effects.");
        ImGui.TextUnformatted($"HP regen disabled list generated with {disabledHealthRegenDic.Count} status effects.");
        ImGui.TextUnformatted($"MP regen disabled list generated with {disabledManaRegenDic.Count} status effects.");
        ImGui.Spacing();
        using (var scrollArea = ImRaii.Child("ScrollArea", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 40f), border: true))
        {
            //DrawTable("DisabledRegenSID", table2Column1, table2Column2, disabledHPWidth, disabledMPWidth, disabledHealthRegenSet, disabledManaRegenSet);
            DrawTable2("DisabledRegenSID", table2Column1, table2Column2, disabledHPWidth, disabledMPWidth, disabledHealthRegenDic, disabledManaRegenDic);
            ImGui.Separator();
            ImGui.Spacing();
            DrawTable2("RegenSID", table1Column1, table1Column2, hpWidth, mpWidth, healthRegenDic, manaRegenDic);
            //DrawTable("RegenSID", table1Column1, table1Column2, hpWidth, mpWidth, healthRegenSet, manaRegenSet);
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
            ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 5f + (ImGui.GetScrollY() * 2));
            if (ImGui.Button("Copy top table"))
            {
                var topTable = new StringBuilder();
                //GetTableContentAsText(ref topTable, "Disabled Health Regen Status IDs", "Disabled Mana Regen Status IDs", disabledHealthRegenSet, disabledManaRegenSet);
                GetTableContentAsText2(ref topTable, "Disabled Health Regen Status IDs", "Disabled Mana Regen Status IDs", disabledHealthRegenDic, disabledManaRegenDic);
                ImGui.SetClipboardText(topTable.ToString());
            }
            ImGui.SameLine();
            if (ImGui.Button("Copy bottom table"))
            {
                var bottomTable = new StringBuilder();
                //GetTableContentAsText(ref bottomTable, "Health Regen Status IDs", "Mana Regen Status IDs", healthRegenSet, manaRegenSet);
                GetTableContentAsText2(ref bottomTable, "Health Regen Status IDs", "Mana Regen Status IDs", healthRegenDic, manaRegenDic);
                ImGui.SetClipboardText(bottomTable.ToString());
            }
            ImGui.SetCursorPos(originPos);
        }
        // Place a button in bottom right + some padding / extra space
        ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 10f);
        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 5f + (ImGui.GetScrollY() * 2));
        if (ImGui.Button("Close"))
        {
            this.IsOpen = false;
        }
        ImGui.SetCursorPos(originPos);
    }

    private static void GetTableContentAsText2(ref StringBuilder text, string column1Name, string column2Name, FrozenDictionary<uint, string> list1, FrozenDictionary<uint, string> list2)
    {
        text.Append(column1Name);
        foreach (var item in list1)
        {
            text.AppendLine();
            var entry = item.Key + ": " + item.Value;
            text.Append(entry.Trim());
        }
        text.AppendLine();
        text.AppendLine();
        text.Append(column2Name);
        foreach (var item in list2)
        {
            text.AppendLine();
            var entry = item.Key + ": " + item.Value;
            text.Append(entry.Trim());
        }
    }

    private static void GetTableContentAsText(ref StringBuilder text, string column1Name, string column2Name, FrozenSet<string> list1, FrozenSet<string> list2)
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

    private static void DetermineColumnWidth(string column1, string column2, FrozenSet<string> list1, FrozenSet<string> list2, out float column1Width, out float column2Width)
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

    private static void DetermineColumnWidth2(string column1, string column2, FrozenDictionary<uint, string> list1, FrozenDictionary<uint, string> list2, out float column1Width, out float column2Width)
    {
        column1Width = ImGui.CalcTextSize(column1).X;
        foreach (var item in list1)
        {
            var entry = " " + item.Key + ": " + item.Value;
            var textWidth = ImGui.CalcTextSize(entry);
            if (textWidth.X > column1Width)
            {
                column1Width = textWidth.X;
            }
        }

        column2Width = ImGui.CalcTextSize(column2).X;
        foreach (var item in list2)
        {
            var entry = item.Key + ": " + item.Value;
            var textWidth = ImGui.CalcTextSize(entry);
            if (textWidth.X > column2Width)
            {
                column2Width = textWidth.X;
            }
        }
    }

    private static unsafe void DrawTable2(string id, string column1, string column2, float column1Width, float column2Width, FrozenDictionary<uint, string> list1, FrozenDictionary<uint, string> list2)
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
                    var entry = " " + list1.ElementAt(i).Key + ": " + list1.ElementAt(i).Value;
                    ImGui.TextUnformatted($"{entry}");
                }

                // Mana Regen column
                ImGui.TableSetColumnIndex(1);
                if (i < list2.Count)
                {
                    var entry = list2.ElementAt(i).Key + ": " + list2.ElementAt(i).Value;
                    ImGui.TextUnformatted($"{entry}");
                }
            }
        }
        clipper.End();
        clipper.Destroy();
    }

    private static unsafe void DrawTable(string id, string column1, string column2, float column1Width, float column2Width, FrozenSet<string> list1, FrozenSet<string> list2)
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
                    ImGui.TextUnformatted($"{list1.ElementAt(i)}");
                }

                // Mana Regen column
                ImGui.TableSetColumnIndex(1);
                if (i < list2.Count)
                {
                    ImGui.TextUnformatted($"{list2.ElementAt(i)}");
                }
            }
        }
        clipper.End();
        clipper.Destroy();
    }

    /*private void ProcessDictionaries()
    {
        List<string> tempHealthRegen = [];
        List<string> tempManaRegen = [];
        List<string> tempDisabledHealthRegen = [];
        List<string> tempDisabledManaRegen = [];
        foreach (var kvp in HealthRegenDictionary.OrderBy(x => x.Key))
        {
            var entry = " " + kvp.Key + ": " + kvp.Value;
            tempHealthRegen.Add(entry);
        }
        foreach (var kvp in ManaRegenDictionary.OrderBy(x => x.Key))
        {
            var entry = kvp.Key + ": " + kvp.Value;
            tempManaRegen.Add(entry);
        }
        foreach (var kvp in DisabledHealthRegenDictionary.OrderBy(x => x.Key))
        {
            var entry = " " + kvp.Key + ": " + kvp.Value;
            tempDisabledHealthRegen.Add(entry);
        }
        foreach (var kvp in DisabledManaRegenDictionary.OrderBy(x => x.Key))
        {
            var entry = kvp.Key + ": " + kvp.Value;
            tempDisabledManaRegen.Add(entry);
        }
        healthRegenSet = tempHealthRegen.ToFrozenSet();
        manaRegenSet = tempManaRegen.ToFrozenSet();
        disabledHealthRegenSet = tempDisabledHealthRegen.ToFrozenSet();
        disabledManaRegenSet = tempDisabledManaRegen.ToFrozenSet();
    }*/
}
