/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIProfileDefinition.cs
 * @author hqrse
 * @date 2026/07/16
 * @brief パーティAIが使う戦術リストと思考設定
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // パーティ AI の設定をまとめた ScriptableObject
    // 保持するのは「どの戦術をどの優先度で使うか」と思考の駆動設定だけで、
    // AI の挙動そのものは戦術アセット（PPBattleTacticsDefinition）側で決まる
    // 性格・重み・警戒度といったパラメータは持たない。強弱は持たせる戦術セットの違いで表現する
    [CreateAssetMenu(fileName = "PPPartyAIProfileDefinition", menuName = "Project-Pudding/AI/PPPartyAIProfileDefinition")]
    public class PPPartyAIProfileDefinition : ScriptableObject
    {
        [Label("説明")]
        [SerializeField][Multiline] protected string mDescription = "";

        [Header("思考")]
        // 1 ティックの間に何回思考するか。思考間隔はティック間隔をこの値で割って決まる
        [Label("1ティックあたりの思考回数")]
        [SerializeField] private int mThinkCountPerTick = 1;
        // リソース推移の平均を取るサンプル数。戦術の待機判断に使う
        [Label("リソース推移サンプル数")]
        [SerializeField] private int mTrendSampleCount = 4;

        [Header("戦術")]
        // 並び順がそのまま優先度になる（上ほど高い）
        [Label("戦術リスト", true)]
        [SerializeField] private List<PPBattleTacticsDefinition> mTactics = new();

        public string Description => mDescription;
        public int ThinkCountPerTick => Mathf.Max(1, mThinkCountPerTick);
        public int TrendSampleCount => Mathf.Max(1, mTrendSampleCount);
        public IReadOnlyList<PPBattleTacticsDefinition> Tactics => mTactics;
    }
}
