/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyDefinition.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief パーティ定義
 * =====================================*/

using System;
using System.Collections.Generic;
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

        // 乱数のシードを固定するか
        // 固定すると毎回同じ乱数列で動くため、AI の挙動を追いかけたい検証時に使う
        // 外した場合も、ユニットごとに別の乱数列は割り当てられる
        [Label("乱数シードを固定する")] public bool IsFixedSeed = false;
        // 固定時に使うシード値
        // 同じユニット定義を複数体並べる場合はここを変えないと全員が同じ行動をする
        [Label("乱数シード")]
        [EditCondition(nameof(IsFixedSeed), true, false)]
        public int Seed = 0;
    }

    // パーティ編成の定義（ScriptableObject）
    // メンバー構成・コイン変換設定・AI の駆動設定をまとめて持つ
    // ランタイムの PPBattleParty への変換は PPPartyFactory が担う
    // 敵の遭遇パターンを 1 アセット = 1 戦闘の単位で用意できる
    // AI の判断内容はユニット側（PPUnitDefinition.AIProfile）が持つため、ここには駆動設定だけを置く
    [CreateAssetMenu(fileName = "PPPartyDefinition", menuName = "Project-Pudding/Battle/PPPartyDefinition")]
    public class PPPartyDefinition : ScriptableObject
    {
        [Header("パーティ")]
        [Label("パーティメンバー", true)] public List<PPPartyMemberEntry> Members = new();
        [Label("コイン変換レート初期値")] public float BaseResourceConversionRate = 1f;

        // 1 ティックの間に何回思考するか。思考間隔はティック間隔をこの値で割って決まる
        // ドライバの駆動周期はパーティ単位のため、ユニットごとではなくここで持つ
        [Header("AI")]
        [Label("1ティックあたりの思考回数")] [SerializeField] private int mThinkCountPerTick = 1;

        public int ThinkCountPerTick => Mathf.Max(1, mThinkCountPerTick);
    }
}
