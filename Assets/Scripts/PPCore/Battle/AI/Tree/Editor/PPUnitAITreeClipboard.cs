/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAITreeClipboard.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief 判断ツリーのノードを複写するためのクリップボード
 * =====================================*/

using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // ノードを複写して持ち運ぶための入れ物
    // [SerializeReference] の入れ子を保つため、ScriptableObject を挟んで EditorJsonUtility を通す
    internal sealed class PPUnitAITreeClipboard : ScriptableObject
    {
        /* インスペクタ設定 */

        // 複写するノード。EditorJsonUtility が読み書きする実体
        // インスペクタには出さないため表示名属性は付けない
        [SerializeReference]
        [SerializeField] private List<PPUnitAINode> mNodes = new();

        /* static 関数 */

        // ノード群を文字列へ書き出す
        public static string Serialize(IReadOnlyList<PPUnitAINode> aNodes)
        {
            var holder = CreateInstance<PPUnitAITreeClipboard>();
            try
            {
                holder.mNodes.AddRange(aNodes);
                return EditorJsonUtility.ToJson(holder);
            }
            finally
            {
                DestroyImmediate(holder);
            }
        }

        // 文字列からノード群を復元する
        // 復元されたノードは元のノードとは別インスタンスで、ID は複写元のまま
        public static List<PPUnitAINode> Deserialize(string aJson)
        {
            var holder = CreateInstance<PPUnitAITreeClipboard>();
            try
            {
                EditorJsonUtility.FromJsonOverwrite(aJson, holder);
                var nodes = new List<PPUnitAINode>();
                foreach (var node in holder.mNodes)
                {
                    if (node != null) nodes.Add(node);
                }
                return nodes;
            }
            catch (System.ArgumentException)
            {
                // 判断ツリー以外の内容がクリップボードに入っていた場合はここへ来る
                return new List<PPUnitAINode>();
            }
            finally
            {
                DestroyImmediate(holder);
            }
        }

        // 文字列が判断ツリーのノードとして読めるかを調べる
        // 貼り付け可否の判定に使う
        public static bool CanDeserialize(string aJson)
            => !string.IsNullOrEmpty(aJson) && Deserialize(aJson).Count > 0;
    }
}
