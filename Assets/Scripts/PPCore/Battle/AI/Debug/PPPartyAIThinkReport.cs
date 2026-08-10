/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyAIThinkReport.cs
 * @author hqrse
 * @date 2026/08/10
 * @brief AI思考1回分の記録。デバッグウィンドウの表示元になる
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // 行動候補 1 件分の思考記録
    // 採用されたものだけでなく却下されたものも残す。
    // 調整で知りたいのは「なぜ撃ったか」より「なぜ撃たなかったか」であることが多い
    public struct PPPartyAIThinkCandidateEntry
    {
        public string UnitName;
        public string ActionName;
        public string TargetName;
        public PPBattleSkillRole Role;
        // 戦況を反映した最終的な効用
        public float Utility;
        // コストの合計量
        public float CostTotal;
        // λ × コスト。効用がこれを超えないと採用されない
        public float LambdaCost;
        public bool IsAdopted;
        // 同じユニットの上位候補が却下された後で採用されたか
        public bool IsFallback;
        public PPActionRejectReason RejectReason;
    }

    // AI 思考 1 回分の記録
    // ルール解決 → 作戦 → 予算 → 候補の採否、という流れをそのまま追えるようにしてある
    // リアルタイムに進むバトルでは見たい瞬間を捉えられないため、
    // デバッグウィンドウ側がこれを複数件ためて遡れるようにする
    public sealed class PPPartyAIThinkReport
    {
        // どちらの陣営の思考か
        public BattleSide Side;
        // 記録時点の経過ターン数
        public int TurnCount;
        // 記録時刻（Time.time）。ウィンドウでの並び順と識別に使う
        public float Timestamp;
        // 成立して適用された状況ルールの名前。成立が無ければ既定作戦の旨を表す
        public string ResolvedRules;
        // 解決後の作戦の要約
        public string DoctrineSummary;
        // 予算計画の要約（λ・使用可能額・保険）
        public string BudgetSummary;
        // 採用された行動の数
        public int AdoptedCount;
        // 全候補の記録
        public List<PPPartyAIThinkCandidateEntry> Candidates = new();
    }
}
