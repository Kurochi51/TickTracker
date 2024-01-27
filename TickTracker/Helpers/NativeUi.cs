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

    private static AtkUldPartsList* CreateAtkUldPartsList(UldWrapper uld, int partListIndex, string texturePath)
    {
        if (!uld.Valid || uld.Uld is null)
        {
            return null;
        }
        var uldFile = uld.Uld;
        var uldPartList = uldFile.Parts[partListIndex];

        var atkPartsList = (AtkUldPartsList*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPartsList), 8);
        if (atkPartsList is null)
        {
            return null;
        }
        atkPartsList->Id = uldPartList.Id;
        atkPartsList->PartCount = uldPartList.PartCount;
        var atkUldPart = (AtkUldPart*)IMemorySpace.GetUISpace()->Malloc((ulong)sizeof(AtkUldPart) * atkPartsList->PartCount, 8);
        if (atkUldPart is null)
        {
            IMemorySpace.Free(atkPartsList, (ulong)sizeof(AtkUldPartsList));
            return null;
        }
        for (var i = 0; i < uldPartList.PartCount; i++)
        {
            var part = uldPartList.Parts[i];
            atkUldPart[i].U = part.U;
            atkUldPart[i].V = part.V;
            atkUldPart[i].Width = part.W;
            atkUldPart[i].Height = part.H;
        }
        atkPartsList->Parts = atkUldPart;

        var atkUldAsset = (AtkUldAsset*)IMemorySpace.GetUISpace()->Malloc((ulong)(sizeof(AtkUldAsset) * atkPartsList->PartCount), 8);
        if (atkUldAsset is null)
        {
            IMemorySpace.Free(atkUldPart, (ulong)sizeof(AtkUldPart) * atkPartsList->PartCount);
            IMemorySpace.Free(atkPartsList, (ulong)sizeof(AtkUldPartsList));
            return null;
        }

        for (var i = 0; i < uldPartList.PartCount; i++)
        {
            var partTextureId = uldPartList.Parts[i].TextureId;
            atkUldAsset[i].Id = partTextureId;
            atkUldAsset[i].AtkTexture.Ctor();
            // Technically not a proper call, because the texturePath is blindly passed here
            // Different parts can belong to different textures
            atkUldAsset[i].AtkTexture.LoadTexture(texturePath);
            atkPartsList->Parts[i].UldAsset = &atkUldAsset[i];
        }

        return atkPartsList;
    }

    public static AtkImageNode* CreateImageNode(UldWrapper uld, int partListIndex, uint ImageNodeID, string texturePath, ushort partIndex, AtkComponentNode* parent, AtkResNode* targetNode, bool visibility)
    {
        var imageNode = IMemorySpace.GetUISpace()->Create<AtkImageNode>();
        if (imageNode is null)
        {
            return null;
        }
        var atkPartList = CreateAtkUldPartsList(uld, partListIndex, texturePath);
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
