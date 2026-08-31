/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIProfileTemplateFactory.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief 判断ツリーの雛形を生成する
 * =====================================*/

using UnityEditor;
using UnityEngine;

namespace PPCore
{
    // 判断ツリーの雛形を生成するファクトリ
    //
    // 空のプロファイルから作り始めると、ルート未設定・末尾の受け皿なしという
    // 「何も行動が決まらない」状態を必ず一度は通ることになる
    // 最初から「優先度リスト＋末尾に無条件の通常攻撃」が入っていれば、
    // その上に条件付きの枝を積んでいくだけで組めるため、雛形として用意する
    public static class PPUnitAIProfileTemplateFactory
    {
        // 新規作成したアセットの既定名
        private const string DefaultAssetName = "PUAP_New";

        // 雛形からプロファイルを新規作成する
        // 選択中のフォルダへ作り、名前の入力状態で Project ウィンドウへ出す
        [MenuItem("Assets/Create/Project-Pudding/AI/PPUnitAIProfileDefinition (雛形)", false, 0)]
        public static void CreateFromTemplate()
        {
            var profile = ScriptableObject.CreateInstance<PPUnitAIProfileDefinition>();
            Build(profile);

            // オブジェクト名を先に入れておく
            // CreateInstance 直後は空で、ファイル名との不一致は保存のたびに
            // 「Main Object Name '' does not match filename」の警告として出続けるため
            profile.name = DefaultAssetName;

            ProjectWindowUtil.CreateAsset(profile, $"{DefaultAssetName}.asset");
        }

        // プロファイルへ雛形の内容を組み立てる
        // 「優先度リスト」を根に置き、その末尾へ無条件の通常攻撃をぶら下げる
        // aProfile : 組み立て先のプロファイル
        public static void Build(PPUnitAIProfileDefinition aProfile)
        {
            var serialized = new SerializedObject(aProfile);
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
            aProfile.InvalidateNodeMap();
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
}
