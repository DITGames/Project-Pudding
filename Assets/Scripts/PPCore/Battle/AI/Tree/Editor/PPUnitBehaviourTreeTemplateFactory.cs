/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitBehaviourTreeTemplateFactory.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief ビヘイビアツリーアセットのテンプレート生成と名前の同期
 * =====================================*/

using System.IO;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // 判断ツリーの雛形を生成するファクトリ
    //
    // 空のツリーから作り始めると、ルート未設定・末尾の受け皿なしという
    // 「何も行動が決まらない」状態を必ず一度は通ることになる
    // 最初から「優先度リスト＋末尾に無条件の通常攻撃」が入っていれば、
    // その上に条件付きの枝を積んでいくだけで組めるため、雛形として用意する
    public static class PPUnitBehaviourTreeTemplateFactory
    {
        // 新規作成したアセットの既定名
        private const string DefaultAssetName = "PUBT_New";

        // 雛形からツリーを新規作成する
        // 選択中のフォルダへ作り、名前の入力状態で Project ウィンドウへ出す
        [MenuItem("Assets/Create/Project-Pudding/AI/PPUnitBehaviourTree (雛形)", false, 0)]
        public static void CreateFromTemplate()
        {
            var tree = Create(DefaultAssetName);
            ProjectWindowUtil.CreateAsset(tree, $"{DefaultAssetName}.asset");
        }

        // 雛形を組み立てたツリーを、アセットとして保存する前の状態で作る
        // 保存先とファイル名は呼び出し側が決める（ユニット作成ウィンドウは PUBT_<ユニットID>.asset で保存する）
        // aName : オブジェクト名。保存するファイル名（拡張子なし）と揃える
        // return : 未保存のツリー
        public static PPUnitBehaviourTree Create(string aName)
        {
            var tree = ScriptableObject.CreateInstance<PPUnitBehaviourTree>();
            Build(tree);

            // オブジェクト名を先に入れておく
            // CreateInstance 直後は空で、ファイル名との不一致は保存のたびに
            // 「Main Object Name '' does not match filename」の警告として出続けるため
            tree.name = aName;
            return tree;
        }

        // ツリーへ雛形の内容を組み立てる
        // 「優先度リスト」を根に置き、その末尾へ無条件の通常攻撃をぶら下げる
        // aTree : 組み立て先のツリー
        public static void Build(PPUnitBehaviourTree aTree)
        {
            var serialized = new SerializedObject(aTree);
            var nodesProperty = serialized.FindProperty("mNodes");
            nodesProperty.ClearArray();

            var selector = new PPUnitAISelectorNode();
            selector.EnsureNodeId();
            selector.SetGraphPosition(new Vector2(0f, 0f));

            var attack = new PPUnitAIActionNode();
            attack.EnsureNodeId();
            attack.SetGraphPosition(new Vector2(320f, 0f));
            attack.SetAction(new PPUnitAIAttackAction());

            // 末尾の受け皿として繋ぐ。条件付きの枝はこの上へ足していく
            selector.ConnectChild(0, attack.NodeId);

            AppendNode(nodesProperty, selector);
            AppendNode(nodesProperty, attack);

            serialized.FindProperty("mRootNodeId").stringValue = selector.NodeId;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            aTree.InvalidateNodeMap();
        }

        // ノードをリストの末尾へ足す
        // aNodesProperty : ノードリストのプロパティ
        // aNode : 足すノード
        private static void AppendNode(SerializedProperty aNodesProperty, PPUnitAINode aNode)
        {
            aNodesProperty.arraySize++;
            aNodesProperty.GetArrayElementAtIndex(aNodesProperty.arraySize - 1).managedReferenceValue = aNode;
        }
    }

    // 判断ツリーのオブジェクト名を、取り込みのたびにファイル名へ揃える
    //
    // ScriptableObject は内部に持つ名前とファイル名が別物で、ずれると保存のたびに
    // 「Main Object Name '◯◯' does not match filename '△△'」の警告が出続ける
    //
    // ずれる経路は 1 つではない
    //   ・同じ名前で作ろうとしてファイル名だけ連番になった（PUBT_Verify と PUBT_Verify1）
    //   ・複製した
    //   ・Unity の外でファイル名を変えた
    // 作る側で塞いでも取りこぼすため、取り込みの時点で揃える
    public sealed class PPUnitBehaviourTreeNamePostprocessor : AssetPostprocessor
    {
        // 取り込まれたアセットのうち、判断ツリーの名前がファイル名と食い違っていれば直す
        // aImportedAssets : 取り込まれたアセットのパス
        // aDeletedAssets : 削除されたアセットのパス
        // aMovedAssets : 移動後のパス
        // aMovedFromAssetPaths : 移動前のパス
        private static void OnPostprocessAllAssets(string[] aImportedAssets, string[] aDeletedAssets,
            string[] aMovedAssets, string[] aMovedFromAssetPaths)
        {
            FixNames(aImportedAssets);
            // 移動やリネームの直後も食い違いが起きるため、そちらも見る
            FixNames(aMovedAssets);
        }

        // 指定されたパスの判断ツリーについて、名前をファイル名へ揃える
        // aPaths : 調べるアセットのパス
        private static void FixNames(string[] aPaths)
        {
            foreach (string path in aPaths)
            {
                if (!path.EndsWith(".asset")) continue;

                var tree = AssetDatabase.LoadAssetAtPath<PPUnitBehaviourTree>(path);
                if (tree == null) continue;

                string fileName = Path.GetFileNameWithoutExtension(path);
                if (tree.name == fileName) continue;

                // 揃えた結果また取り込みが走るが、次はここで弾かれるため繰り返しにはならない
                tree.name = fileName;
                EditorUtility.SetDirty(tree);
                AssetDatabase.SaveAssetIfDirty(tree);
            }
        }
    }
}
