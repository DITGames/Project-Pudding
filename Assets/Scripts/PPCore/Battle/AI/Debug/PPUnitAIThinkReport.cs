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
        // そのユニットのこのティック何手目か。0 から始まる
        // 行動回数が複数のユニットは、同じティックに複数件の記録が並ぶ
        public int ActionIndex;
        // 評価した判断ツリー。ツリーウィンドウで開いているものと同じかを見分けるのに使う
        public PPUnitAIProfileDefinition Profile;
        // 評価中に通過したノードの ID。引き返した枝は含まない
        public List<string> VisitedNodeIds = new();
        // 行動が確定したノードの ID。経路上の他のノードと区別して表示するために持つ
        public string DecidedNodeId;
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
