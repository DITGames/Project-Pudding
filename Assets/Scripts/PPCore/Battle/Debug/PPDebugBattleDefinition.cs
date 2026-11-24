/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPDebugBattleDefinition.cs
 * @author hqrse
 * @date 2026/11/22
 * @brief デバッグバトルの設定と、Editor から S_Battle への受け渡し
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // デバッグバトルの片方のパーティ。配置ごとのユニットとレベルを持つ
    [Serializable]
    public sealed class PPDebugBattleParty
    {
        /* インスペクタ設定 */

        // ユニット定義が空の枠にはユニットを置かない
        [Label("中央")] [SerializeField] private PPPartyMemberEntry mCenter = new();
        [Label("右")] [SerializeField] private PPPartyMemberEntry mRight = new();
        [Label("左")] [SerializeField] private PPPartyMemberEntry mLeft = new();

        /* 問い合わせ */

        public PPPartyMemberEntry GetMember(PPPartyPosition aPosition) => aPosition switch
        {
            PPPartyPosition.Right => mRight,
            PPPartyPosition.Left => mLeft,
            _ => mCenter,
        };

        public PPBattleParty Create(BattleSide aSide) => PPPartyFactory.CreateFromMembers(GetMember, aSide);
    }

    // Editor のデバッグバトルで S_Battle を始めるときの設定。バトル周りの設定項目はここへ足していく
    [CreateAssetMenu(fileName = "PPDebugBattle", menuName = "Project-Pudding/Battle/PPDebugBattleDefinition")]
    public class PPDebugBattleDefinition : ScriptableObject
    {
        /* インスペクタ設定 */

        [Header("味方")]
        [Label("味方パーティ")]
        [SerializeField] private PPDebugBattleParty mAlly = new();

        [Header("敵")]
        [Label("敵パーティ")]
        [SerializeField] private PPDebugBattleParty mEnemy = new();

        /* プロパティ */

        public PPDebugBattleParty Ally => mAlly;
        public PPDebugBattleParty Enemy => mEnemy;
    }

    // Editor のデバッグバトルウィンドウから S_Battle へ、使う設定を渡す
    // Play に入るときに static が消えないよう、設定アセットの GUID を SessionState に置く
    public static class PPDebugBattleLauncher
    {
        /* 定数 */

        public const string RequestKey = "PPCore.DebugBattle.RequestedGuid";

        /* 操作 */

        // デバッグバトルとして始めるよう頼まれていれば、その設定を返して頼みを消す。頼まれていなければ null
        // プレイヤービルドでは常に null
        public static PPDebugBattleDefinition TakeRequested()
        {
#if UNITY_EDITOR
            string guid = UnityEditor.SessionState.GetString(RequestKey, string.Empty);
            if (string.IsNullOrEmpty(guid)) return null;
            UnityEditor.SessionState.EraseString(RequestKey);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<PPDebugBattleDefinition>(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
#else
            return null;
#endif
        }
    }
}
