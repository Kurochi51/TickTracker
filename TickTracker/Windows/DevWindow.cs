using System;
using System.Linq;
using System.Numerics;
using System.Globalization;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using ImGuiNET;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Dalamud.Game.ClientState.Objects.SubKinds;
using FFXIVClientStructs.FFXIV.Component.GUI;
using TickTracker.Helpers;
using TickTracker.NativeNodes;

namespace TickTracker.Windows;

public sealed class DevWindow : Window, IDisposable
{
    private readonly IDalamudPluginInterface pluginInterface;
    private readonly IDataManager dataManager;
    private readonly IPluginLog log;
    private readonly IGameGui gameGui;
    private readonly Utilities utilities;

    private readonly uint devNodeImageId = NativeUi.Get("DevNode");

    private const string GachaUldPath = "ui/uld/Gacha.uld";
    private static readonly List<string> PrintLines = [];
    public int partId { get; set; }
    public int partListIndex { get; set; }
    public string uldPath { get; set; } = GachaUldPath;
    private int nodeX, nodeY;
    private ImageNode? devNode;
    public bool startBenchmark { get; set; }
    private unsafe AtkUnitBase* NameplateAddon => (AtkUnitBase*)gameGui.GetAddonByName("NamePlate");

    public DevWindow(IDalamudPluginInterface _pluginInterface, IDataManager _dataManager, IPluginLog _log, IGameGui _gameGui, Utilities _utilities) : base("DevWindow")
    {
        pluginInterface = _pluginInterface;
        dataManager = _dataManager;
        log = _log;
        gameGui = _gameGui;
        utilities = _utilities;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(200, 200),
            MaximumSize = TickTracker.Resolution,
        };
    }

    public override void Draw()
    {
        if (ImGui.Button("Start Benchmark"))
        {
            startBenchmark = true;
        }
        ImageNodeStuff();
        foreach (var line in PrintLines)
        {
            ImGui.TextUnformatted(line);
        }
        PrintLines.Clear();
    }

    private unsafe void ImageNodeStuff()
    {
        var pId = partId;
        var pListIndex = partListIndex;
        var uld = uldPath;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 3);
        if (ImGui.InputInt("ImageNode PartId", ref pId, 1))
        {
            partId = pId;
        }
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 3);
        if (ImGui.InputInt("ImageNode PartsList Index", ref pListIndex, 1))
        {
            partListIndex = pListIndex;
        }
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 3);
        if (ImGui.InputTextWithHint("Uld", "Uld Path...", ref uld, 200, ImGuiInputTextFlags.EnterReturnsTrue))
        {
            uldPath = uld;
        }
        ImGui.SameLine();
        if (ImGui.Button("Change uld"))
        {
            var tempUld = pluginInterface.UiBuilder.LoadUld(uldPath);
            if (tempUld.Valid)
            {
                devNode?.Dispose();
                devNode = new ImageNode(dataManager, log, tempUld)
                {
                    NodeId = devNodeImageId,
                };
            }
            tempUld.Dispose();
        }
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 5);
        ImGui.InputInt("ImageNode X", ref nodeX);
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X / 5);
        ImGui.InputInt("ImageNode Y", ref nodeY);
        if (ImGui.Button("Change Coords"))
        {
            devNode?.SetNodePosition(nodeX, nodeY);
        }
        if (devNode is null || !utilities.IsAddonReady(NameplateAddon))
        {
            return;
        }
        if (devNode.imageNode is not null)
        {
            InspectNodeStuff();
            return;
        }
        if (devNode.imageNode is null)
        {
            devNode.CreateCompleteImageNode(0);
            if (devNode.imageNode is null)
            {
                Print("Dev imageNode creation failed");
                return;
            }
            devNode.imageNode->WrapMode = 0;
            NativeUi.LinkNodeAtRoot((AtkResNode*)devNode.imageNode, NameplateAddon);
            devNode.SetNodePosition(1280, 600);
        }
    }

    private unsafe void InspectNodeStuff()
    {
        partId = Math.Clamp(partId, 0, (int)devNode!.imageNode->PartsList->PartCount - 1);
        partListIndex = Math.Clamp(partListIndex, 0, devNode.atkUldPartsListsAvailable - 1);
        devNode.imageNode->PartId = (ushort)partId;
        if (devNode.imageNode->PartsList->Id - 1 != partListIndex)
        {
            partId = 0;
            devNode.imageNode->PartId = 0;
            devNode.ChangePartsList(partListIndex);
        }
        var texFileNamePtr = devNode.imageNode->PartsList->Parts[devNode.imageNode->PartId].UldAsset->AtkTexture.Resource->TexFileResourceHandle->ResourceHandle.FileName;
        var texString = Marshal.PtrToStringAnsi(new IntPtr(texFileNamePtr.BufferPtr));
        var isHighResolution = texString?.Contains("_hr1", StringComparison.Ordinal) ?? false;
        var currentPart = devNode.imageNode->PartsList->Parts[devNode.imageNode->PartId];
        var width = isHighResolution ? currentPart.Width * 2 : currentPart.Width;
        var height = isHighResolution ? currentPart.Height * 2 : currentPart.Height;
        devNode.imageNode->AtkResNode.SetHeight((ushort)height);
        devNode.imageNode->AtkResNode.SetWidth((ushort)width);
        Print($"NodeId: {devNode.imageNode->AtkResNode.NodeId}\n" +
        $"Has {devNode.imageNode->PartsList->PartCount} Parts\n" +
        $"Current partsList: {devNode.imageNode->PartsList->Id}\n" +
        $"Current part: {devNode.imageNode->PartId}\n");
        for (var i = 0; i < devNode.imageNode->PartsList->PartCount; i++)
        {
            Print($"Part {i.ToString(CultureInfo.InvariantCulture)} Texture id: {devNode.imageNode->PartsList->Parts[i].UldAsset->Id}" +
                $" ; Texture.Resource version: {devNode.imageNode->PartsList->Parts[i].UldAsset->AtkTexture.Resource->Version}");
        }
    }

    public void BenchmarkSpawner(IPlayerCharacter player, int iterations, FrozenSet<uint> bag1, FrozenSet<uint> bag2, FrozenSet<uint> bag3)
    {
        log.Debug("Benchmark Started");
        foreach (var test in GetActionDictionary(player, bag1, bag2, bag3))
        {
            utilities.Benchmark(test.Value, iterations, test.Key);
        }
        log.Debug("Benchmark Finished");
    }

    private static Dictionary<string, Action> GetActionDictionary(IPlayerCharacter player, FrozenSet<uint> bag1, FrozenSet<uint> bag2, FrozenSet<uint> bag3)
    {
        var testList = player.StatusList;
        return new Dictionary<string, Action>(StringComparer.Ordinal)
        {
            {
                "StatusList shared-instance",
                () =>
                {
                    var test = player.StatusList;
                    _ = test.Any(status => bag1.Contains(status.StatusId));
                    _ = test.Any(status => bag2.Contains(status.StatusId));
                    _ = test.Any(status => bag3.Contains(status.StatusId));
                }
            },
            {
                "StatusList single-instance",
                () =>
                {
                    _ = testList.Any(status => bag1.Contains(status.StatusId));
                    _ = testList.Any(status => bag2.Contains(status.StatusId));
                    _ = testList.Any(status => bag3.Contains(status.StatusId));
                }
            },
            {
                "StatusList direct access",
                () =>
                {
                    _ = player.StatusList.Any(status => bag1.Contains(status.StatusId));
                    _ = player.StatusList.Any(status => bag2.Contains(status.StatusId));
                    _ = player.StatusList.Any(status => bag3.Contains(status.StatusId));
                }
            },
            {
                "StatusList.ToList",
                () =>
                {
                    var test = player.StatusList.ToList();
                    _ = test.Exists(status => bag1.Contains(status.StatusId));
                    _ = test.Exists(status => bag2.Contains(status.StatusId));
                    _ = test.Exists(status => bag3.Contains(status.StatusId));
                }
            },
            {
                "StatusList.ToArray",
                () =>
                {
                    var test = player.StatusList.ToArray();
                    _ = Array.Exists(test, status => bag1.Contains(status.StatusId));
                    _ = Array.Exists(test, status => bag2.Contains(status.StatusId));
                    _ = Array.Exists(test, status => bag3.Contains(status.StatusId));
                }
            },
            {
                "StatusList.ToFrozenSet",
                () =>
                {
                    var test = player.StatusList.ToFrozenSet();
                    _ = test.Any(status => bag1.Contains(status.StatusId));
                    _ = test.Any(status => bag2.Contains(status.StatusId));
                    _ = test.Any(status => bag3.Contains(status.StatusId));
                }
            },
            {
                "StatusList.ToHashSet",
                () =>
                {
                    var test = player.StatusList.ToHashSet();
                    _ = test.Any(status => bag1.Contains(status.StatusId));
                    _ = test.Any(status => bag2.Contains(status.StatusId));
                    _ = test.Any(status => bag3.Contains(status.StatusId));
                }
            },
        };
    }

    public static void Print(string text)
    {
        PrintLines.Add(text);
    }

    public static void Separator()
    {
        PrintLines.Add("--------------------------");
    }

    public unsafe void Dispose()
    {
        if (devNode is not null && devNode.imageNode is not null)
        {
            NativeUi.UnlinkFromRoot((AtkResNode*)devNode.imageNode, NameplateAddon);
            devNode.Dispose();
        }
    }
}
