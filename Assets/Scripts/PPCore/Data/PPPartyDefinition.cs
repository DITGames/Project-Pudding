/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPPartyDefinition.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief パーティ定義
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;
using CustomConsole;

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

    // パーティ編成の定義（ScriptableObject）。編成の順にメンバーを並べる(最大 PPPartyFactory.MaxMembers 体)
    // ランタイムの PPBattleParty への変換は PPPartyFactory が担う
    [CreateAssetMenu(fileName = "PPPartyDefinition", menuName = "Project-Pudding/Battle/PPPartyDefinition")]
    public class PPPartyDefinition : ScriptableObject
    {
        /* 定数 */

        private const string LogTag = "Battle";

        /* インスペクタ設定 */

        // 編成の順に並べる。ユニット定義が空の枠は飛ばす
        [Header("パーティ")]
        [Label("メンバー", true)]
        [SerializeField] private List<PPPartyMemberEntry> mMembers = new();

        /* プロパティ */

        public IReadOnlyList<PPPartyMemberEntry> Members => mMembers;

        /* Unity イベント */

#if UNITY_EDITOR
        // 上限を超えた分はバトルに出ないため警告する。値は書き換えない
        void OnValidate()
        {
            if (mMembers.Count <= PPPartyFactory.MaxMembers) return;
            CustomConsoleLog.Warning(LogTag, $"パーティ定義 {name} のメンバーが {mMembers.Count} 体あります。{PPPartyFactory.MaxMembers + 1} 体目からはバトルに出ません", this);
        }
#endif
    }
}
