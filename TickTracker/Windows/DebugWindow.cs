using ImGuiNET;
using System;
using System.Text;
using System.Linq;
using System.Numerics;
using System.Collections.Generic;
using Dalamud.Interface.Windowing;

namespace TickTracker.Windows;

public class DebugWindow : Window, IDisposable
{
    /// <summary>
    /// A <see cref="Dictionary{TKey, TValue}" /> of Status IDs and Status Names that trigger HP regen
    /// </summary>
    public static readonly Dictionary<uint, string> HealthRegenDictionary = new();
    /// <summary>
    /// A <see cref="Dictionary{TKey, TValue}" /> of Status IDs and Status Names that trigger MP regen
    /// </summary>
    public static readonly Dictionary<uint, string> ManaRegenDictionary = new();

    private float hpWidth = 0, mpWidth = 0;
    private bool firstTime = true, invalidList = false;

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
        if (HealthRegenDictionary is null || ManaRegenDictionary is null || HealthRegenDictionary.Count <= 0 || ManaRegenDictionary.Count <= 0)
        {
            invalidList = true;
            ImGui.Text("Lists are null or empty!");
            CopyAndClose();
            return;
        }
        ImGui.Text($"HP regen list generated with {HealthRegenDictionary.Count} status effects.");
        ImGui.Text($"MP regen list generated with {ManaRegenDictionary.Count} status effects.");
        ImGui.Spacing();
        var maxItemCount = Math.Max(HealthRegenDictionary.Count, ManaRegenDictionary.Count);
        if (firstTime)
        {
            hpWidth = ImGui.CalcTextSize("Health Regen Status IDs").X;
            foreach (var item in HealthRegenDictionary)
            {
                var textWidth = ImGui.CalcTextSize(item.Value.ToString()) + ImGui.CalcTextSize(": ") + ImGui.CalcTextSize(item.Key.ToString());
                if (textWidth.X > hpWidth)
                {
                    hpWidth = textWidth.X;
                }
            }

            mpWidth = ImGui.CalcTextSize("Mana Regen Status IDs").X;
            foreach (var item in ManaRegenDictionary)
            {
                var textWidth = ImGui.CalcTextSize(item.Value.ToString()) + ImGui.CalcTextSize(": ") + ImGui.CalcTextSize(item.Key.ToString());
                if (textWidth.X > mpWidth)
                {
                    mpWidth = textWidth.X;
                }
            }
            firstTime = false;
        }
        ImGui.BeginChild("ScrollArea", new Vector2(ImGui.GetContentRegionAvail().X, ImGui.GetContentRegionAvail().Y - 40f), true);
        ImGui.BeginTable("StatusIDTable", 2, ImGuiTableFlags.None);

        ImGui.TableSetupColumn("Health Regen Status IDs", ImGuiTableColumnFlags.WidthFixed, hpWidth);
        ImGui.TableSetupColumn("Mana Regen Status IDs", ImGuiTableColumnFlags.WidthFixed, mpWidth);
        ImGui.TableHeadersRow();
        ImGui.TableNextRow();

        for (var i = 0; i < maxItemCount; i++)
        {
            // Health Regen column
            ImGui.TableSetColumnIndex(0);
            if (i < HealthRegenDictionary.Count)
            {
                var kvp = HealthRegenDictionary.ElementAt(i);
                ImGui.Text($"{kvp.Key}: {kvp.Value}");
            }

            // Mana Regen column
            ImGui.TableSetColumnIndex(1);
            if (i < ManaRegenDictionary.Count)
            {
                var kvp = ManaRegenDictionary.ElementAt(i);
                ImGui.Text($"{kvp.Key}: {kvp.Value}");
            }

            if (i + 1 < maxItemCount)
            {
                ImGui.TableNextRow();
            }
        }

        ImGui.EndTable();
        ImGui.EndChild();

        CopyAndClose();
    }

    private void CopyAndClose()
    {
        var originPos = ImGui.GetCursorPos();
        if (!invalidList)
        {
            // Place a button in bottom left + some padding / extra space
            ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.GetWindowContentRegionMax().X + 10f);
            ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - 5f);
            if (ImGui.Button("Copy table"))
            {
                ImGui.SetClipboardText(GetTableContentAsText());
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

    private static string GetTableContentAsText()
    {
        var tableText = new StringBuilder();
        tableText.Append("Health Regen Status IDs");
        for (var i = 0; i < HealthRegenDictionary.Count; i++)
        {
            tableText.AppendLine();
            var kvp = HealthRegenDictionary.ElementAt(i);
            tableText.Append($"{kvp.Key}: {kvp.Value}");
        }
        tableText.AppendLine();
        tableText.AppendLine();
        tableText.Append("Mana Regen Status IDs");
        for (var i = 0; i < ManaRegenDictionary.Count; i++)
        {
            tableText.AppendLine();
            var kvp = ManaRegenDictionary.ElementAt(i);
            tableText.Append($"{kvp.Key}: {kvp.Value}");
        }

        return tableText.ToString();
    }


    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

}
