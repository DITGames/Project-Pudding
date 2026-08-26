/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAIThinkReport.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットAIの思考1回分の記録
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    // ユニット 1 体分の判断結果
    // 「何を選んだか」だけでなく「なぜ動かなかったか」まで残すことで、
    // 待機している状態が意図通りかを後から追えるようにする
    public sealed class PPUnitAIThinkEntry
    {
        // 思考対象のユニット名
        public string UnitName;
        // 最終的に選んだ行動
        public PPUnitAIDecision Decision;
        // 行動しなかった場合の理由。行動した場合は None
        public PPUnitAIRejectReason RejectReason;
        // 判断ツリーが選んだ行動の表示名。何も選ばなければ "-"
        public string ActionName;
        // 選んだ行動の対象名。対象なしなら "-"
        public string TargetName;
        // スキルゲージの現在値
        public float SkillGauge;
        // スキルゲージの上限
        public float SkillGaugeMax;
        // コインゲージの現在値
        public float CoinGauge;
        // コインゲージの上限
        public float CoinGaugeMax;
        // 待機コミットの残りティック数。コミットしていなければ 0
        public int CommitRemainingTicks;
    }

    // ユニット AI の思考 1 回分の記録
    // 1 パーティ分のユニット判断をまとめて 1 件として扱う
    public sealed class PPUnitAIThinkReport
    {
        // 思考した陣営
        public BattleSide Side;
        // 思考時点のターン数
        public int TurnCount;
        // 思考時点の経過時間（秒）
        public float Timestamp;
        // 採用された行動の総数
        public int AdoptedCount;
        // ユニットごとの判断結果
        public List<PPUnitAIThinkEntry> Units { get; } = new();
    }
}
