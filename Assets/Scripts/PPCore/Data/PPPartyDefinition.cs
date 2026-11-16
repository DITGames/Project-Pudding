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
        // パーティ内の配置。同じパーティの中で重ならないようにする
        [Label("配置")] public PPPartyPosition Position = PPPartyPosition.Center;
        // 乱数のシードを固定するか。固定すると毎回同じ乱数列で動く
        // 外した場合も、ユニットごとに別の乱数列は割り当てられる
        [Label("乱数シードを固定する")] public bool IsFixedSeed = false;

        // 固定時に使うシード値
        // 同じユニット定義を複数体並べる場合はここを変えないと全員が同じ行動をする
        [Label("乱数シード")]
        [EditCondition(nameof(IsFixedSeed), true, false)]
        public int Seed = 0;
    }

    // パーティ編成の定義（ScriptableObject）。メンバー構成・コイン変換設定・AI の駆動設定を持つ
    // ランタイムの PPBattleParty への変換は PPPartyFactory が担う
    [CreateAssetMenu(fileName = "PPPartyDefinition", menuName = "Project-Pudding/Battle/PPPartyDefinition")]
    public class PPPartyDefinition : ScriptableObject
    {
        /* インスペクタ設定 */

        [Header("パーティ")]
        [Label("パーティメンバー", true)] public List<PPPartyMemberEntry> Members = new();
        [Label("コイン変換レート初期値")] public float BaseResourceConversionRate = 1f;

        // 1 ティックの間に何回思考するか。思考間隔はティック間隔をこの値で割って決まる
        // ドライバの駆動周期はパーティ単位のため、ユニットごとではなくここで持つ
        [Header("AI")]
        [Label("1ティックあたりの思考回数")] [SerializeField] private int mThinkCountPerTick = 1;

        /* プロパティ */

        public int ThinkCountPerTick => Mathf.Max(1, mThinkCountPerTick);

        /* Unity イベント */

        // 配置が重なっているメンバーを警告する。配置は 1 つにつき 1 体まで
        private void OnValidate()
        {
            for (int i = 0; i < Members.Count; i++)
            {
                if (Members[i] == null) continue;

                for (int j = i + 1; j < Members.Count; j++)
                {
                    if (Members[j] == null || Members[i].Position != Members[j].Position) continue;

                    CustomConsoleLog.Warning("Battle",
                        $"{name}: メンバー {i} と {j} の配置が重なっています。配置は 1 つにつき 1 体にしてください。", this);
                }
            }
        }
    }
}
