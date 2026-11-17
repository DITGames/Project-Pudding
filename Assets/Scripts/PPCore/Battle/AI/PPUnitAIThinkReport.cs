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
        // ツリーのどの枝も成立しなかった（末尾に無条件の行動を置けば埋められる）
        NoMatchedNode,
        // ツリーが待機を選んだ（スキルを選ばず、通常攻撃でスキルゲージを溜める）
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
    // 何を選んだかに加えて、なぜ動かなかったかまで残す
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
        // 待機コミットの残りの行動回数。コミットしていなければ 0
        public int CommitRemainingActions;
        // そのユニットの何回目の思考か。行動が終わるたびに 1 回思考するため、行動回数と同じ意味になる
        public int ActionCount;
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
    // 思考はユニットの行動が終わるたびに 1 体ずつ行うため、通常は 1 体分の判断だけが入る
    public sealed class PPUnitAIThinkReport
    {
        // 思考した陣営
        public BattleSide Side;
        // 思考時点の経過時間（バトル専用の時計の秒）
        public float Timestamp;
        // ユニットごとの判断結果
        public List<PPUnitAIThinkEntry> Units { get; } = new();
    }

    // ユニット AI の思考記録をエディタ側へ流すための中継点
    // 購読者が居ないときは HasListener を確認して記録の組み立てを省ける
    public static class PPUnitAIDebugHub
    {
        // 購読者が居るか。記録の組み立てを省く判定に使う
        public static bool HasListener => OnReported != null;

        // 思考記録が報告されたときに発火する(思考1回分の記録)
        public static event Action<PPUnitAIThinkReport> OnReported;

        // 思考記録を購読者へ流す
        // aReport : 思考 1 回分の記録
        public static void Report(PPUnitAIThinkReport aReport) => OnReported?.Invoke(aReport);
    }
}
