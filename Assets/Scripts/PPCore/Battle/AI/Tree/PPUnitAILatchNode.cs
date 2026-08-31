/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAILatchNode.cs
 * @author hqrse
 * @date 2026/08/27
 * @brief 一度きり・ラッチの振る舞いを持つ分岐ノード
 * =====================================*/

using System;
using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // 一度きり・ラッチの振る舞い
    public enum PPUnitAILatchMode
    {
        // 一度成立したら、以後は不成立として扱う
        [InspectorName("一度きり")]
        Once = 0,
        // 一度成立したら、以後は条件を見ずに成立として扱う
        [InspectorName("ラッチ")]
        Latch = 1,
    }

    // 一度きり・ラッチの振る舞いを持つ分岐ノード
    //
    // 判定材料は条件分岐と同じ（ユニット条件とパーティ条件の AND、反転可）で、
    // 「一度成立したこと」を記憶して以後の判定を固定する点だけが異なる
    //   一度きり : 「開幕に必ず咆哮」「HP 半分を割った瞬間に一度だけ全体攻撃」
    //   ラッチ   : 「HP 50% を割ったら形態変化し、回復されても戻らない」
    //
    // 記憶が立つのは「条件が成立し、かつその枝で行動が確定したとき」だけ
    // 枝を通っただけで行動が決まらなかった場合に記憶を立ててしまうと、
    // 一度きりの行動が一度も実行されないまま封じられてしまうため
    //
    // 記憶の書き込みはストラテジストが通過記録（PPUnitAIEvalContext.VisitedNodeIds）を辿って行う
    // 通過記録にはノード ID がそのまま積まれるが、それは「不成立側へ進んだ」場合も同じく積まれる
    // そのため成立の記憶は専用のキーで別に積み、クールダウン用の通過記録と混ざらないようにしている
    [Serializable]
    [PPTypeMenuName("制御/一度きり・ラッチ")]
    public sealed class PPUnitAILatchNode : PPUnitAINode
    {
        // 接続口の番号。エディタからの接続操作で使う
        private const int PortMatched = 0;
        private const int PortUnmatched = 1;

        // 成立の記憶に使うキーの接尾辞。ノード自身の通過記録と区別するために付ける
        private const string MatchedKeySuffix = "#matched";

        [Header("振る舞い")]
        [Label("モード")]
        [SerializeField] private PPUnitAILatchMode mMode = PPUnitAILatchMode.Once;

        [Header("条件")]
        [Label("ユニット条件", true)]
        [SerializeReference]
        [SerializeField] private List<PPUnitConditionValidator> mUnitConditions = new();
        [Label("パーティ条件", true)]
        [SerializeReference]
        [SerializeField] private List<PPPartyConditionValidator> mPartyConditions = new();
        // 条件の判定結果を反転する。「〜でない場合」を条件クラスを増やさずに書くためのもの
        [Label("条件を反転する")]
        [SerializeField] private bool mIsInvert = false;

        // 成立側・不成立側それぞれに繋がる子ノードの ID
        [Header("枝")]
        [Label("成立したとき")]
        [SerializeField] private string mMatchedId = "";
        [Label("成立しなかったとき")]
        [SerializeField] private string mUnmatchedId = "";

        protected override string DefaultNodeName
            => mMode == PPUnitAILatchMode.Once ? "一度きり" : "ラッチ";

        // 成立側の枝が繋がっているか。エディタの診断から参照する
        public bool HasMatchedBranch => !string.IsNullOrEmpty(mMatchedId);

        // 判定に使うユニット条件。エディタの診断が設定漏れを調べるために参照する
        public IReadOnlyList<PPUnitConditionValidator> UnitConditions => mUnitConditions;

        public override IReadOnlyList<PPUnitAINodePort> Ports
            => new[]
            {
                new PPUnitAINodePort("成立", ToSingle(mMatchedId), false),
                new PPUnitAINodePort("不成立", ToSingle(mUnmatchedId), false),
            };

        // 振る舞いと判定内容を要約する
        public override string Summary
        {
            get
            {
                string mode = mMode == PPUnitAILatchMode.Once ? "一度きり" : "ラッチ";
                string body = BuildConditionSummary(mUnitConditions, mPartyConditions);
                return mIsInvert ? $"{mode} : {body} ／ 反転" : $"{mode} : {body}";
            }
        }

        // 記憶と条件から進む枝を決める
        // 一度きりは記憶が立っていれば条件を見ずに不成立、ラッチは記憶が立っていれば条件を見ずに成立になる
        // aContext : 評価 1 回分の入力
        // return : 進んだ枝の結果。枝が無ければ Failed
        protected override PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext)
        {
            bool hasFired = aContext.Memory != null && aContext.Memory.HasFired(MatchedKey);
            bool isMatched = mMode switch
            {
                // 済んでいれば条件を見るまでもなく封じる
                PPUnitAILatchMode.Once => !hasFired && EvaluateConditions(aContext) != mIsInvert,
                // 一度立ったら条件が崩れても成立側を維持する
                PPUnitAILatchMode.Latch => hasFired || EvaluateConditions(aContext) != mIsInvert,
                _ => false,
            };

            // 成立して進む場合だけ記憶の対象にする。行動が確定しなければ親が巻き戻すため記憶は残らない
            if (isMatched) aContext.PushVisited(MatchedKey);

            var next = aContext.ResolveNode(isMatched ? mMatchedId : mUnmatchedId);

            return next == null ? PPUnitAINodeResult.Failed : next.Evaluate(aContext);
        }

        // 持っている条件の説明文を組み立て直す
        public override void RefreshConditionDescriptions()
        {
            RefreshDescriptions(mUnitConditions);
            RefreshDescriptions(mPartyConditions);
        }

        // 指定した接続口へ子ノードを繋ぐ。既に繋がっていた場合は置き換える
        // aPortIndex : 接続口の番号
        // aChildId : 繋ぐ子ノードの ID
        public override void ConnectChild(int aPortIndex, string aChildId)
        {
            if (aPortIndex == PortMatched) mMatchedId = aChildId;
            else if (aPortIndex == PortUnmatched) mUnmatchedId = aChildId;
        }

        // 接続先の子ノード ID を対応表に従って置き換える
        // aMap : 対応表
        public override void RemapChildIds(IReadOnlyDictionary<string, string> aMap)
        {
            mMatchedId = RemapChildId(mMatchedId, aMap);
            mUnmatchedId = RemapChildId(mUnmatchedId, aMap);
        }

        // 指定した接続口の接続を外す
        // aPortIndex : 接続口の番号
        // aChildId : 外す子ノードの ID
        public override void DisconnectChild(int aPortIndex, string aChildId)
        {
            if (aPortIndex == PortMatched && mMatchedId == aChildId) mMatchedId = "";
            else if (aPortIndex == PortUnmatched && mUnmatchedId == aChildId) mUnmatchedId = "";
        }

        // 成立の記憶に使うキー
        private string MatchedKey => mNodeId + MatchedKeySuffix;

        // ユニット条件とパーティ条件を AND で評価する
        // どちらも空なら「条件なし」とみなして成立する
        // aContext : 評価 1 回分の入力
        // return : 全ての条件を満たす場合 true
        private bool EvaluateConditions(PPUnitAIEvalContext aContext)
        {
            if (!PPUnitConditionValidator.EvaluateAll(mUnitConditions, aContext.Unit, aContext.Snapshot))
                return false;

            foreach (var condition in mPartyConditions)
            {
                if (condition == null) continue;
                if (!condition.Evaluate(aContext.Snapshot)) return false;
            }
            return true;
        }

        // 単一の接続先を接続口の形式へ揃える。未接続なら空の並びを返す
        // aChildId : 接続先の ID
        // return : 接続口が持つ子ノード ID の並び
        private static IReadOnlyList<string> ToSingle(string aChildId)
            => string.IsNullOrEmpty(aChildId) ? Array.Empty<string>() : new[] { aChildId };
    }
}
