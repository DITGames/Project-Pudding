/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAITreeNodeSearchProvider.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief 判断ツリーのノードを名前で検索する窓の中身
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

namespace PPCore
{
    // ノードを名前で検索する窓（Ctrl+F）の中身を組み立てる
    // 種別ごとに束ね、同名のノードを区別できるよう要約の 1 行目を添える
    internal sealed class PPUnitAITreeNodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        /* 状態 */

        // 検索対象の判断ツリー
        private PPUnitBehaviourTree mTree;

        /* イベント */

        // ノードが選ばれたときに呼ぶ処理。ノード ID を渡す
        private Action<string> mOnSelected;

        /* 生成 */

        // 検索窓に並べる項目を組み立てる
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext aContext)
        {
            var entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("ノードを検索"), 0),
            };
            if (mTree == null) return entries;

            foreach (var node in mTree.Nodes)
            {
                if (node == null) continue;

                entries.Add(new SearchTreeEntry(new GUIContent(BuildLabel(node)))
                {
                    level = 1,
                    userData = node.NodeId,
                });
            }
            return entries;
        }

        /* 初期化・接続 */

        // 検索対象と選択時の処理を差し込む
        public void Setup(PPUnitBehaviourTree aTree, Action<string> aOnSelected)
        {
            mTree = aTree;
            mOnSelected = aOnSelected;
        }

        /* イベントハンドラ */

        // 項目が選ばれたときに呼ばれる
        public bool OnSelectEntry(SearchTreeEntry aEntry, SearchWindowContext aContext)
        {
            if (aEntry.userData is not string nodeId) return false;

            mOnSelected?.Invoke(nodeId);
            return true;
        }

        /* ヘルパー */

        // 一覧へ出す 1 行分の文言を組み立てる
        // 同名のノードを見分けられるよう、要約の 1 行目を添える
        private static string BuildLabel(PPUnitAINode aNode)
        {
            string summary = aNode.Summary;
            if (string.IsNullOrEmpty(summary)) return aNode.NodeName;

            int lineEnd = summary.IndexOf('\n');
            if (lineEnd >= 0) summary = summary[..lineEnd];
            return $"{aNode.NodeName}  ({summary})";
        }
    }
}
