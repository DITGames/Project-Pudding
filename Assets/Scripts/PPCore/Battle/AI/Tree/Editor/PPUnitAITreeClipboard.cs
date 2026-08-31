/* =====================================
 * Copyright hqrse. All rights reserved.
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
    //
    // ノードは [SerializeReference] の派生クラス群で、中に条件や行動といった
    // 別の [SerializeReference] を入れ子で抱えている
    // JsonUtility はこの入れ子を落としてしまうため、Unity 本体のシリアライザを通す
    // EditorJsonUtility を使う。そのために ScriptableObject を 1 枚挟んでいる
    //
    // 文字列にしてから読み直すことで、元のノードとインスタンスが完全に切り離される
    // 貼り付け先が別のプロファイルでも同じ手順で扱える
    internal sealed class PPUnitAITreeClipboard : ScriptableObject
    {
        // 複写するノード。EditorJsonUtility が読み書きする実体
        // インスペクタには出さないため表示名属性は付けない
        [SerializeReference]
        [SerializeField] private List<PPUnitAINode> mNodes = new();

        // ノード群を文字列へ書き出す
        // aNodes : 複写するノード
        // return : 貼り付けに使う文字列
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
        // aJson : Serialize が書き出した文字列
        // return : 復元されたノード。復元できなければ空
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
        // aJson : 判定する文字列
        // return : 読めれば true
        public static bool CanDeserialize(string aJson)
            => !string.IsNullOrEmpty(aJson) && Deserialize(aJson).Count > 0;
    }
}
