/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file VFXSequenceNodePickerUtility.cs
 * @author hqrse
 * @date 2026/08/19
 * @brief StopVFX/StopNode/ループ継続ノード等が参照する対象ノードを、グラフ内の一覧から選択するためのピッカー
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VFXUtility.Editor
{
    internal static class VFXSequenceNodePickerUtility
    {
        // 選択中の対象ノードIDに対応する表示ラベルを返す(VFXを保持するノードのみが対象)
        // aGraphView : ノード一覧の取得元 / aNodeId : 表示ラベルを解決する対象ノードID
        public static string GetNodeLabel(VFXSequencerGraphView aGraphView, string aNodeId)
        {
            if (string.IsNullOrEmpty(aNodeId))
            {
                return "未設定";
            }

            foreach (VFXSequenceNodeBase node in aGraphView.GetAllNodes())
            {
                if (node is VFXSequencePlayableNodeBase playable && node.NodeId == aNodeId)
                {
                    return BuildLabel(playable);
                }
            }

            return "未設定(参照先が見つかりません)";
        }

        // グラフ内のVFXを保持するノード(通常ノード・イベントノード)のみを一覧メニュー表示し、選択されたノードIDをコールバックへ渡す
        // (StopNode/StopVFX/StopAll/SetParameter/PlayEvent発火ノードはVFXを持たないため対象外)
        // aGraphView : ノード一覧の取得元 / aOnSelected : 選択されたノードIDを受け取るコールバック
        public static void ShowPicker(VFXSequencerGraphView aGraphView, Action<string> aOnSelected)
        {
            var menu = new GenericMenu();
            foreach (VFXSequenceNodeBase node in aGraphView.GetAllNodes())
            {
                if (node is not VFXSequencePlayableNodeBase playable)
                {
                    continue; // VFXを保持しない制御ノードは対象外
                }

                string label = BuildLabel(playable);
                menu.AddItem(new GUIContent(label), false, () => aOnSelected(node.NodeId));
            }

            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("VFXを保持するノードがありません"));
            }

            menu.ShowAsContext();
        }

        // 選択中の対象ノードIDに対応する表示ラベルを返す(ルートノードの直接の接続先のみが対象)
        // aGraphView : ノード一覧の取得元 / aNodeId : 表示ラベルを解決する対象ノードID
        public static string GetBranchHeadLabel(VFXSequencerGraphView aGraphView, string aNodeId)
        {
            if (string.IsNullOrEmpty(aNodeId))
            {
                return "未設定";
            }

            foreach (VFXSequenceNodeBase node in aGraphView.GetAllNodes())
            {
                if (node.NodeId == aNodeId)
                {
                    return BuildGenericLabel(node);
                }
            }

            return "未設定(参照先が見つかりません)";
        }

        // ルートノードの直接の接続先(ブランチの先頭)のみを一覧メニュー表示し、選択されたノードIDをコールバックへ渡す
        // (それ以外のノードを指定してもOriginRootIdが一致せず何も停止できないため、選択肢自体を絞る)
        // aGraphView : ノード一覧の取得元 / aOnSelected : 選択されたノードIDを受け取るコールバック
        public static void ShowBranchHeadPicker(VFXSequencerGraphView aGraphView, Action<string> aOnSelected)
        {
            VFXSequenceNodeBase rootNode = null;
            foreach (VFXSequenceNodeBase node in aGraphView.GetAllNodes())
            {
                if (node is VFXSequenceRootNode)
                {
                    rootNode = node;
                    break;
                }
            }

            var menu = new GenericMenu();
            if (rootNode != null)
            {
                foreach (string nextId in rootNode.NextNodeIds)
                {
                    VFXSequenceNodeBase branchHead = null;
                    foreach (VFXSequenceNodeBase node in aGraphView.GetAllNodes())
                    {
                        if (node.NodeId == nextId)
                        {
                            branchHead = node;
                            break;
                        }
                    }
                    if (branchHead == null)
                    {
                        continue;
                    }

                    string label = BuildGenericLabel(branchHead);
                    menu.AddItem(new GUIContent(label), false, () => aOnSelected(branchHead.NodeId));
                }
            }

            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("ルートノードの接続先がありません"));
            }

            menu.ShowAsContext();
        }

        // 選択中の対象ノードIDに対応する表示ラベルを返す(種別を問わず、グラフ内の任意のノードが対象)
        // aGraphView : ノード一覧の取得元 / aNodeId : 表示ラベルを解決する対象ノードID
        public static string GetGenericNodeLabel(VFXSequencerGraphView aGraphView, string aNodeId)
        {
            if (string.IsNullOrEmpty(aNodeId))
            {
                return "未設定";
            }

            foreach (VFXSequenceNodeBase node in aGraphView.GetAllNodes())
            {
                if (node.NodeId == aNodeId)
                {
                    return BuildGenericLabel(node);
                }
            }

            return "未設定(参照先が見つかりません)";
        }

        // グラフ内のループノードのみを一覧メニュー表示し、選択されたノードIDをコールバックへ渡す
        // (ループ継続ノードの対象参照専用。ループノード以外を指定しても対応する周回カウントが存在しないため選択肢を絞る)
        // aGraphView : ノード一覧の取得元 / aOnSelected : 選択されたノードIDを受け取るコールバック
        public static void ShowLoopNodePicker(VFXSequencerGraphView aGraphView, Action<string> aOnSelected)
        {
            var menu = new GenericMenu();
            foreach (VFXSequenceNodeBase node in aGraphView.GetAllNodes())
            {
                if (node is not VFXSequenceLoopNode loopNode)
                {
                    continue;
                }

                string label = BuildGenericLabel(loopNode);
                menu.AddItem(new GUIContent(label), false, () => aOnSelected(loopNode.NodeId));
            }

            if (menu.GetItemCount() == 0)
            {
                menu.AddDisabledItem(new GUIContent("ループノードがありません"));
            }

            menu.ShowAsContext();
        }

        private static string BuildLabel(VFXSequencePlayableNodeBase aNode)
        {
            if (!string.IsNullOrEmpty(aNode.DisplayName))
            {
                return aNode.DisplayName;
            }

            string typeName = VFXSequenceNodeTypeMenuUtility.GetDisplayName(aNode);
            string assetName = aNode.VisualEffectAsset != null ? aNode.VisualEffectAsset.name : null;

            string idSuffix = aNode.NodeId.Length >= 6 ? aNode.NodeId[..6] : aNode.NodeId;
            return assetName != null ? $"{assetName} ({typeName} #{idSuffix})" : $"{typeName} #{idSuffix}";
        }

        // VFXを保持しない制御ノードも含めた汎用のラベルを生成する(表示名が設定されていればそれを、無ければ種別名 + ノードID短縮)
        private static string BuildGenericLabel(VFXSequenceNodeBase aNode)
        {
            if (!string.IsNullOrEmpty(aNode.DisplayName))
            {
                return aNode.DisplayName;
            }

            string typeName = VFXSequenceNodeTypeMenuUtility.GetDisplayName(aNode);
            string idSuffix = aNode.NodeId.Length >= 6 ? aNode.NodeId[..6] : aNode.NodeId;
            return $"{typeName} #{idSuffix}";
        }
    }
}
