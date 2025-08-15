using System;
using System.Text;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Windowing;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.ManagedFontAtlas;
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

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly CancellationTokenSource cts;
    private readonly CancellationToken cToken;

    private readonly List<string> healthRegenList = [];
    private readonly List<string> manaRegenList = [];
    private readonly List<string> disabledHealthRegenList = [];
    private readonly List<string> disabledManaRegenList = [];

    private const ImGuiTableFlags TableFlags = ImGuiTableFlags.ScrollY | ImGuiTableFlags.PreciseWidths | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersOuter;
    private TableStruct disabledRegenTable, regenTable;
    private bool invalidList, fontChange, firstTime = true;

    public DebugWindow(IDalamudPluginInterface _pluginInterface) : base("DebugWindow")
    {
        pluginInterface = _pluginInterface;
        cts = new CancellationTokenSource();
        cToken = cts.Token;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(400, 500),
            MaximumSize = TickTracker.Resolution,
        };
        pluginInterface.UiBuilder.DefaultFontHandle.ImFontChanged += QueueColumnWidthChange;
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
            disabledRegenTable = new TableStruct()
            {
                Id = "DisabledRegenSID",
                Column1Header = " Disabled HP Regen Status IDs",
                Column2Header = "Disabled MP Regen Status IDs",
                Column1Width = 0,
                Column2Width = 0,
                Column1Content = disabledHealthRegenList,
                Column2Content = disabledManaRegenList,
            };
            regenTable = new TableStruct()
            {
                Id = "RegenSID",
                Column1Header = " Health Regen Status IDs",
                Column2Header = "Mana Regen Status IDs",
                Column1Width = 0,
                Column2Width = 0,
                Column1Content = healthRegenList,
                Column2Content = manaRegenList,
            };
            DetermineColumnWidth(ref disabledRegenTable);
            DetermineColumnWidth(ref regenTable);
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
            DetermineColumnWidth(ref disabledRegenTable);
            DetermineColumnWidth(ref regenTable);
            fontChange = false;
        }
        ImGui.TextUnformatted($"HP regen list generated with {healthRegenList.Count} status effects.");
        ImGui.TextUnformatted($"MP regen list generated with {manaRegenList.Count} status effects.");
        ImGui.TextUnformatted($"HP regen disabled list generated with {disabledHealthRegenList.Count} status effects.");
        ImGui.TextUnformatted($"MP regen disabled list generated with {disabledManaRegenList.Count} status effects.");
        ImGui.Spacing();

        var currentSize = new Vector2(ImGui.GetContentRegionAvail().X, (ImGui.GetContentRegionAvail().Y - (36f * ImGuiHelpers.GlobalScale)) / 2);
        disabledRegenTable.ResizeIfNeeded(currentSize);
        regenTable.ResizeIfNeeded(currentSize);
        DrawTable(disabledRegenTable);
        DrawTable(regenTable);

        CopyAndClose();
    }

    private void CopyAndClose()
    {
        var originPos = ImGui.GetCursorPos();
        if (!invalidList)
        {
            // Place two buttons in bottom left + some padding / extra space
            ImGui.SetCursorPosX(10f);
            ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - (3f * ImGuiHelpers.GlobalScale) + (ImGui.GetScrollY() * 2));
            if (ImGui.Button("Copy top table"))
            {
                var topTable = new StringBuilder();
                GetTableContentAsText(ref topTable, disabledRegenTable);
                ImGui.SetClipboardText(topTable.ToString());
            }
            ImGui.SameLine();
            if (ImGui.Button("Copy bottom table"))
            {
                var bottomTable = new StringBuilder();
                GetTableContentAsText(ref bottomTable, regenTable);
                ImGui.SetClipboardText(bottomTable.ToString());
            }
            ImGui.SetCursorPos(originPos);
        }
        // Place a button in bottom right + some padding / extra space
        ImGui.SetCursorPosX(ImGui.GetWindowContentRegionMax().X - ImGui.CalcTextSize("Close").X - 15f);
        ImGui.SetCursorPosY(ImGui.GetWindowContentRegionMax().Y - ImGui.GetFrameHeight() - (3f * ImGuiHelpers.GlobalScale) + (ImGui.GetScrollY() * 2));
        if (ImGui.Button("Close"))
        {
            IsOpen = false;
        }
        ImGui.SetCursorPos(originPos);
    }

    private static void GetTableContentAsText(ref StringBuilder text, in TableStruct table)
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

    private static unsafe void DrawTable(in TableStruct table)
    {
        if (!table.IsValid())
        {
            ImGui.TextUnformatted($"{table.Id} has an invalid member.");
            return;
        }

        using var tableBorderColor = ImRaii.PushColor(ImGuiCol.TableBorderStrong, ColorHelpers.RgbaVector4ToUint(*ImGui.GetStyleColorVec4(ImGuiCol.Border)));
        using var tableUsable = ImRaii.Table(table.Id, 2, TableFlags, table.Size!.Value);
        if (!tableUsable)
        {
            return;
        }

        var clipperCount = Math.Max(table.Column1Content.Count, table.Column2Content.Count);
        var clipper = new ImGuiListClipperPtr(ImGuiNative.ImGuiListClipper());
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

    private async void QueueColumnWidthChange(IFontHandle handle, ILockedImFont lockedFont)
    {
        while (!handle.Available && !cToken.IsCancellationRequested)
        {
            await Task.Delay((int)TimeSpan.FromSeconds(1).TotalMilliseconds, cToken).ConfigureAwait(false);
        }
        fontChange = true;
    }

    public void Dispose()
    {
        pluginInterface.UiBuilder.DefaultFontHandle.ImFontChanged -= QueueColumnWidthChange;
        cts.Cancel();
        cts.Dispose();
    }
}
