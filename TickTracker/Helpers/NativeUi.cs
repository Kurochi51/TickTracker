using System;
using System.Globalization;
using System.Collections.Generic;

using FFXIVClientStructs.FFXIV.Component.GUI;

namespace TickTracker.Helpers;

// Mostly from SimpleTweaks
public static unsafe class NativeUi
{
    private static readonly Dictionary<string, uint> NodeIds = new(StringComparer.Ordinal);
    private static uint NodeIdBase = 0x5469636B;

    public static uint Get(string name, int index = 0)
    {
        var key = name + "#" + index.ToString(CultureInfo.InvariantCulture);
        if (NodeIds.TryGetValue(key, out var id))
        {
            return id;
        }
        lock (NodeIds)
        {
            id = NodeIdBase;
            NodeIdBase += 16;
            NodeIds.Add(key, id);
            return id;
        }
    }

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
}
