/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAIConditionNodes.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ビヘイビアツリーの条件系ノード（条件・ラッチ・探索）
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 条件を評価して、成立側・不成立側のどちらの枝へ進むかを決めるノード
    // ユニット条件とパーティ条件を AND で束ねて 1 つの問いにする
    // 不成立側の枝は繋がなくてよく、その場合は「この枝では行動が決まらない」扱いになり、
    // 親の優先度リストが次の候補へ進む（「N: 次の条件へ」の書き方になる）
    [Serializable]
    [PPTypeMenuName("制御/条件分岐")]
    public sealed class PPUnitAIConditionNode : PPUnitAINode
    {
        // 接続口の番号。エディタからの接続操作で使う
        private const int PortMatched = 0;
        private const int PortUnmatched = 1;

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

        protected override string DefaultNodeName => "条件分岐";

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

        // 判定内容を要約する。反転指定はひと目で分かるよう末尾へ付ける
        public override string Summary
        {
            get
            {
                string body = BuildConditionSummary(mUnitConditions, mPartyConditions);
                return mIsInvert ? $"{body} ／ 反転" : body;
            }
        }

        // 条件を評価し、対応する枝へ進む
        // 枝が未接続の場合は不成立として扱い、親の次の候補へ処理を渡す
        // aContext : 評価 1 回分の入力
        // return : 進んだ枝の結果。枝が無ければ Failed
        protected override PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext)
        {
            bool isMatched = EvaluateConditions(aContext) != mIsInvert;
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

    // ターゲット検索の探索範囲
    public enum PPUnitAISearchScope
    {
        [InspectorName("味方")]
        Ally,
        [InspectorName("敵")]
        Enemy,
        [InspectorName("味方と敵")]
        Both,
    }

    // 条件に合うユニットを探し、見つかった分を対象候補（PPPartyAIContext.ConditionedUnits）へ積むノード
    //
    // 「誰を狙うか」を決めるのがこのノードの役割で、条件クラス側は判定だけを担う
    // 条件が対象の収集まで兼ねると、判定に使っただけのつもりが候補を書き換えていた、という事故が起きるため、
    // 収集はツリー上の明示的なノードとしてここへ切り出している
    //
    // 積んだ候補は、行動側の対象の選び方で「条件を満たしているユニット」を選ぶと使われる
    // 重複を許して積むと、その分だけランダム選択で選ばれやすくなる
    // 検索ノードを重ねて「複数の条件に当てはまるユニットほど狙われやすい」を作れる
    [Serializable]
    [PPTypeMenuName("制御/ターゲット検索")]
    public sealed class PPUnitAISearchNode : PPUnitAINode
    {
        // 接続口の番号。エディタからの接続操作で使う
        private const int PortFound = 0;
        private const int PortNotFound = 1;

        [Header("探索")]
        [Label("探索範囲")]
        [SerializeField] private PPUnitAISearchScope mScope = PPUnitAISearchScope.Ally;
        // 候補 1 体ずつに対して評価する条件。全て満たしたユニットだけを積む
        [Label("抽出条件", true)]
        [SerializeReference]
        [SerializeField] private List<PPUnitConditionValidator> mConditions = new();
        // 自分自身を探索対象に含めるか。味方への支援で自分を除きたい場合に外す
        [Label("自分自身を含める")]
        [SerializeField] private bool mIsIncludeSelf = true;

        [Header("積み方")]
        // 探索を始める前に、それまでの候補を捨てるか
        // 複数の検索ノードを重ねて候補を積み増したい場合は外す
        [Label("探索前に候補を空にする")]
        [SerializeField] private bool mIsResetBefore = true;
        // 既に積まれているユニットを積み直さないか
        // 外すと重複して積まれ、その分だけランダム選択で選ばれやすくなる
        [Label("重複して積まない")]
        [SerializeField] private bool mIsUnique = true;

        [Header("枝")]
        [Label("見つかったとき")]
        [SerializeField] private string mFoundId = "";
        [Label("見つからなかったとき")]
        [SerializeField] private string mNotFoundId = "";

        protected override string DefaultNodeName => "ターゲット検索";

        // 発見側の枝が繋がっているか。エディタの診断から参照する
        public bool HasFoundBranch => !string.IsNullOrEmpty(mFoundId);

        // 絞り込みに使う条件。エディタの診断が設定漏れを調べるために参照する
        public IReadOnlyList<PPUnitConditionValidator> Conditions => mConditions;

        // 探索範囲と抽出条件を要約する
        public override string Summary
            => $"{ScopeDisplayName} から探す\n{BuildConditionSummary(mConditions)}";

        // 探索範囲の日本語表記。インスペクタの表記と揃えてある
        private string ScopeDisplayName
            => mScope switch
            {
                PPUnitAISearchScope.Ally => mIsIncludeSelf ? "味方" : "自分以外の味方",
                PPUnitAISearchScope.Enemy => "敵",
                PPUnitAISearchScope.Both => mIsIncludeSelf ? "味方と敵" : "自分以外の味方と敵",
                _ => "",
            };

        public override IReadOnlyList<PPUnitAINodePort> Ports
            => new[]
            {
                new PPUnitAINodePort("発見", ToSingle(mFoundId), false),
                new PPUnitAINodePort("なし", ToSingle(mNotFoundId), false),
            };

        // 探索範囲のユニットを条件で絞り込み、合致したものを対象候補へ積む
        // 1 体でも積めたら発見側の枝へ、1 体も積めなければ なし 側の枝へ進む
        // aContext : 評価 1 回分の入力
        // return : 進んだ枝の結果。枝が未接続なら Failed
        protected override PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext)
        {
            var snapshot = aContext.Snapshot;
            if (mIsResetBefore)
            {
                snapshot.ResetConditionedUnits();
            }

            int found = 0;
            if (mScope != PPUnitAISearchScope.Enemy) found += Collect(snapshot.AliveMembers, aContext);
            if (mScope != PPUnitAISearchScope.Ally) found += Collect(snapshot.AliveEnemies, aContext);

            var next = aContext.ResolveNode(found > 0 ? mFoundId : mNotFoundId);
            return next == null ? PPUnitAINodeResult.Failed : next.Evaluate(aContext);
        }

        // 指定範囲のユニットを条件で絞り込んで積む
        // aCandidates : 探索対象のユニット
        // aContext : 評価 1 回分の入力
        // return : 積んだ体数
        private int Collect(IReadOnlyList<PPBattleUnit> aCandidates, PPUnitAIEvalContext aContext)
        {
            int count = 0;
            foreach (var candidate in aCandidates)
            {
                if (candidate == null) continue;
                if (!mIsIncludeSelf && ReferenceEquals(candidate, aContext.Unit)) continue;
                // 条件は候補 1 体ずつに対して評価する。思考主体ではない点に注意
                if (!PPUnitConditionValidator.EvaluateAll(mConditions, candidate, aContext.Snapshot)) continue;

                aContext.Snapshot.RegisterConditionedUnit(candidate, mIsUnique);
                count++;
            }
            return count;
        }

        // 持っている条件の説明文を組み立て直す
        public override void RefreshConditionDescriptions() => RefreshDescriptions(mConditions);

        // 指定した接続口へ子ノードを繋ぐ。既に繋がっていた場合は置き換える
        // aPortIndex : 接続口の番号
        // aChildId : 繋ぐ子ノードの ID
        public override void ConnectChild(int aPortIndex, string aChildId)
        {
            if (aPortIndex == PortFound) mFoundId = aChildId;
            else if (aPortIndex == PortNotFound) mNotFoundId = aChildId;
        }

        // 接続先の子ノード ID を対応表に従って置き換える
        // aMap : 対応表
        public override void RemapChildIds(IReadOnlyDictionary<string, string> aMap)
        {
            mFoundId = RemapChildId(mFoundId, aMap);
            mNotFoundId = RemapChildId(mNotFoundId, aMap);
        }

        // 指定した接続口の接続を外す
        // aPortIndex : 接続口の番号
        // aChildId : 外す子ノードの ID
        public override void DisconnectChild(int aPortIndex, string aChildId)
        {
            if (aPortIndex == PortFound && mFoundId == aChildId) mFoundId = "";
            else if (aPortIndex == PortNotFound && mNotFoundId == aChildId) mNotFoundId = "";
        }

        // 単一の接続先を接続口の形式へ揃える。未接続なら空の並びを返す
        // aChildId : 接続先の ID
        // return : 接続口が持つ子ノード ID の並び
        private static IReadOnlyList<string> ToSingle(string aChildId)
            => string.IsNullOrEmpty(aChildId) ? Array.Empty<string>() : new[] { aChildId };
    }
}
