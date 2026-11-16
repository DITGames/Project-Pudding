/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAIThinkReport.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットAIの判断結果（列挙・思考レポート・デバッグハブ）
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace PPCore
{
    // ユニット 1 体がその思考で最終的に選んだ行動
    public enum PPUnitAIDecision
    {
        // 何もしない
        [InspectorName("待機")]
        Wait,
        // スキルを発動する
        [InspectorName("スキル")]
        Skill,
        // 通常攻撃を行う
        [InspectorName("通常攻撃")]
        NormalAttack,
    }

    // ユニットがその思考で行動しなかった理由
    // 判断ツリーのどこで止まったかを追うために使う
    public enum PPUnitAIRejectReason
    {
        // 行動を採用した（棄却されていない）
        None,
        // 使えるツリーが無い（個別・ロール既定・全体既定のどれも未設定）
        NoTree,
        // このティックの行動回数を使い切っている
        NoActionBudget,
        // ツリーのどの枝も成立しなかった（末尾に無条件の行動を置けば埋められる）
        NoMatchedNode,
        // ツリーが待機を選んだ（溜めるための意図的な待機）
        DecidedToWait,
    }

    // ユニットが使ったツリーの出どころ
    public enum PPUnitAITreeSource
    {
        // 使えるツリーが無い
        None,
        // ユニット定義に設定された個別ツリー
        Individual,
        // ロールの既定ツリー
        RoleDefault,
        // 全体既定ツリー
        GlobalDefault,
    }

    // ツリーの出どころを表示用の日本語へ変換するヘルパー。ログ・デバッグ表示・作成ウィンドウで表記を揃える
    public static class PPUnitAITreeSourceUtility
    {
        // ツリーの出どころを日本語表記へ変換する
        // aSource : ツリーの出どころ
        // return : 日本語の表記
        public static string ToDisplayString(PPUnitAITreeSource aSource)
            => aSource switch
            {
                PPUnitAITreeSource.Individual => "個別",
                PPUnitAITreeSource.RoleDefault => "ロール既定",
                PPUnitAITreeSource.GlobalDefault => "全体既定",
                _ => "なし",
            };
    }

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
        public PPUnitBehaviourTree Tree;
        // 評価した判断ツリーの出どころ
        public PPUnitAITreeSource TreeSource;
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

    // ユニット AI の思考記録をエディタ側へ流すための中継点
    // ランタイム側がエディタのクラスを直接参照しないよう、購読の口だけをここに置く
    // 購読者が居ないときは記録の組み立て自体を省けるよう、HasListener を先に確認できるようにしている
    public static class PPUnitAIDebugHub
    {
        // 思考記録が報告されたときに発火する(思考1回分の記録)
        public static event Action<PPUnitAIThinkReport> OnReported;

        // 購読者が居るか。記録の組み立てを省く判定に使う
        public static bool HasListener => OnReported != null;

        // 思考記録を購読者へ流す
        // aReport : 思考 1 回分の記録
        public static void Report(PPUnitAIThinkReport aReport) => OnReported?.Invoke(aReport);
    }
}
