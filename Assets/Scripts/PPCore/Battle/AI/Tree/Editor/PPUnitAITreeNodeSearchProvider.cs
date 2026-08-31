/* =====================================
 * Copyright hqrse. All rights reserved.
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
    //
    // ツリーが大きくなると目視でノードを探せなくなるため、名前で絞り込んで飛べるようにする
    // 種別ごとに束ねて並べ、同名のノードがあっても区別できるよう要約の 1 行目を添える
    internal sealed class PPUnitAITreeNodeSearchProvider : ScriptableObject, ISearchWindowProvider
    {
        // 検索対象の判断ツリー
        private PPUnitAIProfileDefinition mProfile;
        // ノードが選ばれたときに呼ぶ処理。ノード ID を渡す
        private Action<string> mOnSelected;

        // 検索対象と選択時の処理を差し込む
        // aProfile : 検索対象の判断ツリー
        // aOnSelected : ノードが選ばれたときに呼ぶ処理
        public void Setup(PPUnitAIProfileDefinition aProfile, Action<string> aOnSelected)
        {
            mProfile = aProfile;
            mOnSelected = aOnSelected;
        }

        // 検索窓に並べる項目を組み立てる
        // aContext : 検索窓の文脈
        // return : 並べる項目
        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext aContext)
        {
            var entries = new List<SearchTreeEntry>
            {
                new SearchTreeGroupEntry(new GUIContent("ノードを検索"), 0),
            };
            if (mProfile == null) return entries;

            foreach (var node in mProfile.Nodes)
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

        // 項目が選ばれたときに呼ばれる
        // aEntry : 選ばれた項目
        // aContext : 検索窓の文脈
        // return : 窓を閉じる場合 true
        public bool OnSelectEntry(SearchTreeEntry aEntry, SearchWindowContext aContext)
        {
            if (aEntry.userData is not string nodeId) return false;

            mOnSelected?.Invoke(nodeId);
            return true;
        }

        // 一覧へ出す 1 行分の文言を組み立てる
        // 同名のノードを見分けられるよう、要約の 1 行目を添える
        // aNode : 対象ノード
        // return : 一覧へ出す文言
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
