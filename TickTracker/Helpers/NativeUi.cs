using System;
using System.Collections.Generic;

using Dalamud.Interface;
using FFXIVClientStructs.FFXIV.Component.GUI;
using FFXIVClientStructs.FFXIV.Client.System.Memory;

namespace TickTracker.Helpers;

// Heavily inspired by SimpleTweaks and ReadyCheckHelper
public static unsafe class NativeUi
{
    private static readonly Dictionary<string, uint> NodeIds = new(StringComparer.Ordinal);
    private static readonly Dictionary<uint, string> NodeNames = new();
    private static uint NodeIdBase = 0x5469636B;

    public static uint Get(string name, int index = 0)
    {
        if (TryGet(name, index, out var id)) return id;
        lock (NodeIds)
        {
            lock (NodeNames)
            {
                id = NodeIdBase;
                NodeIdBase += 16;
                NodeIds.Add($"{name}#{index}", id);
                NodeNames.Add(id, $"{name}#{index}");
                return id;
            }
        }
    }
    public static bool TryGet(string name, int index, out uint id) => NodeIds.TryGetValue($"{name}#{index}", out id);
    public static AtkResNode* GetNodeByID(AtkUldManager* uldManager, uint nodeId, NodeType? type = null) => GetNodeByID<AtkResNode>(uldManager, nodeId, type);
    public static T* GetNodeByID<T>(AtkUldManager* uldManager, uint nodeId, NodeType? type = null) where T : unmanaged
    {
        for (var i = 0; i < uldManager->NodeListCount; i++)
        {
            var n = uldManager->NodeList[i];
            if (n->NodeID != nodeId || (type != null && n->Type != type.Value)) continue;
            return (T*)n;
        }
        return null;
    }

    public static void LinkNodeAfterTargetNode(AtkResNode* node, AtkComponentNode* parent, AtkResNode* targetNode)
    {
        var prev = targetNode->PrevSiblingNode;
        node->ParentNode = targetNode->ParentNode;

        targetNode->PrevSiblingNode = node;
        prev->NextSiblingNode = node;

        node->PrevSiblingNode = prev;
        node->NextSiblingNode = targetNode;

        parent->Component->UldManager.UpdateDrawNodeList();
    }

    public static void UnlinkNode<T>(T* atkNode, AtkComponentBase* componentBase) where T : unmanaged
    {
        var node = (AtkResNode*)atkNode;
        if (node == null) return;

        if (node->ParentNode->ChildNode == node)
        {
            node->ParentNode->ChildNode = node->NextSiblingNode;
        }

        if (node->NextSiblingNode is not null && node->NextSiblingNode->PrevSiblingNode == node)
        {
            node->NextSiblingNode->PrevSiblingNode = node->PrevSiblingNode;
        }

        if (node->PrevSiblingNode is not null && node->PrevSiblingNode->NextSiblingNode == node)
        {
            node->PrevSiblingNode->NextSiblingNode = node->NextSiblingNode;
        }
        componentBase->UldManager.UpdateDrawNodeList();
    }

    public static void FreeImageComponents(ref AtkImageNode* imageNode)
    {
        IMemorySpace.Free(imageNode->PartsList->Parts->UldAsset, (ulong)(sizeof(AtkUldAsset) * imageNode->PartsList->PartCount));
        IMemorySpace.Free(imageNode->PartsList->Parts, (ulong)(sizeof(AtkUldPart) * imageNode->PartsList->PartCount));
        IMemorySpace.Free(imageNode->PartsList, (ulong)sizeof(AtkUldPartsList));
        imageNode->AtkResNode.Destroy(false);
        IMemorySpace.Free(imageNode, (ulong)sizeof(AtkImageNode));
    }

    private static AtkUldPartsList* CreateUldParts(uint UldPartsListID, UldWrapper uld, int partList)
    {
        if (!uld.Valid || uld.Uld is null)
        {
            return null;
        }
        var uldFile = uld.Uld;
        var partCount = uldFile.Parts[partList].PartCount;
        var atkPartsList = (AtkUldPartsList*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPartsList), 8);
        if (atkPartsList is null)
        {
            return null;
        }
        atkPartsList->Id = UldPartsListID;
        atkPartsList->PartCount = partCount;
        var atkUldParts = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc((ulong)(sizeof(AtkUldPart) * partCount), 8);
        if (atkUldParts is null)
        {
            IMemorySpace.Free(atkPartsList, (ulong)sizeof(AtkUldPartsList));
            return null;
        }
        for (var i = 0; i < partCount; i++)
        {
            atkUldParts[i].U = uldFile.Parts[partList].Parts[i].U;
            atkUldParts[i].V = uldFile.Parts[partList].Parts[i].V;
            atkUldParts[i].Width = uldFile.Parts[partList].Parts[i].W;
            atkUldParts[i].Height = uldFile.Parts[partList].Parts[i].H;
        }
        atkPartsList->Parts = atkUldParts;
        var atkUldAsset = (AtkUldAsset*)IMemorySpace.GetUISpace()->Malloc((ulong)(sizeof(AtkUldAsset) * partCount), 8);
        if (atkUldAsset is null)
        {
            IMemorySpace.Free(atkUldParts, (ulong)(sizeof(AtkUldPart) * partCount));
            IMemorySpace.Free(atkPartsList, (ulong)sizeof(AtkUldPartsList));
            return null;
        }
        for (var i = 0; i < partCount; ++i)
        {
            atkUldAsset->Id = NodeIdBase;
            NodeIdBase += 16;
            atkUldAsset->AtkTexture.Ctor();
            atkUldParts[i].UldAsset = &atkUldAsset[i];
        }

        return atkPartsList;
    }

    public static AtkImageNode* CreateImageNode(uint UldPartsListID, UldWrapper uld, int partList, uint ImageNodeID, string texturePath, ushort partIndex, AtkComponentNode* parent, AtkResNode* targetNode, bool visibility)
    {
        var imageNode = IMemorySpace.GetUISpace()->Create<AtkImageNode>();
        if (imageNode is null)
        {
            return null;
        }
        var atkPartList = CreateUldParts(UldPartsListID, uld, partList);
        if (atkPartList is null)
        {
            IMemorySpace.Free(imageNode, (ulong)sizeof(AtkImageNode));
            return null;
        }
        imageNode->AtkResNode.Type = NodeType.Image;
        imageNode->AtkResNode.NodeID = ImageNodeID;
        imageNode->PartsList = atkPartList;
        imageNode->PartId = partIndex;
        imageNode->LoadTexture(texturePath);
        imageNode->AtkResNode.NodeFlags = NodeFlags.AnchorTop | NodeFlags.AnchorLeft | NodeFlags.Enabled | NodeFlags.Visible | NodeFlags.EmitsEvents;
        imageNode->AtkResNode.DrawFlags |= 1;
        imageNode->WrapMode = 1;
        imageNode->Flags = 0;
        imageNode->AtkResNode.SetWidth(160);
        imageNode->AtkResNode.SetHeight(20);
        imageNode->AtkResNode.SetScale(1, 1);
        imageNode->AtkResNode.ToggleVisibility(visibility);
        LinkNodeAfterTargetNode(&imageNode->AtkResNode, parent, targetNode);
        return imageNode;
    }
}
