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
    // 「優先度リスト＋末尾に無条件の通常攻撃」を最初から入れ、何も行動が決まらない状態を避ける
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
        public static PPUnitBehaviourTree Create(string aName)
        {
            var tree = ScriptableObject.CreateInstance<PPUnitBehaviourTree>();
            Build(tree);

            // オブジェクト名を先に入れておく
            // 空のままだとファイル名との不一致の警告が保存のたびに出るため
            tree.name = aName;
            return tree;
        }

        // ツリーへ雛形の内容を組み立てる
        // 「優先度リスト」を根に置き、その末尾へ無条件の通常攻撃をぶら下げる
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
        private static void AppendNode(SerializedProperty aNodesProperty, PPUnitAINode aNode)
        {
            aNodesProperty.arraySize++;
            aNodesProperty.GetArrayElementAtIndex(aNodesProperty.arraySize - 1).managedReferenceValue = aNode;
        }
    }

    // 判断ツリーのオブジェクト名を、取り込みのたびにファイル名へ揃える
    // 複製や外部での改名でずれると、保存のたびに不一致の警告が出続けるため
    public sealed class PPUnitBehaviourTreeNamePostprocessor : AssetPostprocessor
    {
        // 取り込まれたアセットのうち、判断ツリーの名前がファイル名と食い違っていれば直す
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
