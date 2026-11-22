/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPartyDefinition.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief パーティ定義
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // パーティメンバー 1 体分の設定
    // ユニット定義の既定値をそのまま使い、編成上の違いはレベルで表現する
    [Serializable]
    public sealed class PPPartyMemberEntry
    {
        [Label("ユニット定義")] public PPUnitDefinition Unit;
        // 生成するレベル。成長曲線の評価に使う
        [Label("レベル")] public int Level = 1;
        // 乱数のシードを固定するか。固定すると毎回同じ乱数列で動く
        // 外した場合も、ユニットごとに別の乱数列は割り当てられる
        [Label("乱数シードを固定する")] public bool IsFixedSeed = false;

        // 固定時に使うシード値
        // 同じユニット定義を複数体並べる場合はここを変えないと全員が同じ行動をする
        [Label("乱数シード")]
        [EditCondition(nameof(IsFixedSeed), true, false)]
        public int Seed = 0;
    }

    // パーティ編成の定義（ScriptableObject）。配置ごとのメンバー・コイン変換設定・AI の駆動設定を持つ
    // ランタイムの PPBattleParty への変換は PPPartyFactory が担う
    [CreateAssetMenu(fileName = "PPPartyDefinition", menuName = "Project-Pudding/Battle/PPPartyDefinition")]
    public class PPPartyDefinition : ScriptableObject
    {
        /* インスペクタ設定 */

        // 配置ごとのメンバー枠。ユニット定義が空の枠にはユニットを置かない
        [Header("パーティ")]
        [Label("中央")] [SerializeField] private PPPartyMemberEntry mCenter = new();
        [Label("右")] [SerializeField] private PPPartyMemberEntry mRight = new();
        [Label("左")] [SerializeField] private PPPartyMemberEntry mLeft = new();
        [Label("コイン変換レート初期値")] public float BaseResourceConversionRate = 1f;

        // 敵として出たときに、敵のプッシャーへコインを投入する AI の型と段階
        [Header("コイン投入 AI")]
        [Label("コイン投入 AI")] [SerializeField] private PPCoinDropAISetting mCoinDropAI = new();

        /* プロパティ */

        public PPCoinDropAISetting CoinDropAI => mCoinDropAI;

        /* 問い合わせ */

        // 指定した配置のメンバー枠を返す。ユニット定義が空なら、その配置にはユニットを置かない
        public PPPartyMemberEntry GetMember(PPPartyPosition aPosition) => aPosition switch
        {
            PPPartyPosition.Right => mRight,
            PPPartyPosition.Left => mLeft,
            _ => mCenter,
        };
    }
}
