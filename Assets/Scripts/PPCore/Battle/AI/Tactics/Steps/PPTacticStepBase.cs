/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPTacticStepBase.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術の手順1つ分を表す基底クラス
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 戦術の中身を 1 手順ずつ表す基底クラス
    // 「誰が」「何を」「誰に」を条件と方針で指定し、思考のたびに今の盤面へ当てはめ直す
    // 対象が毎回解決し直されるので、HP が最も低い味方のように状況で変わる相手も追従できる
    // PPUnitConditionValidator と同じく ScriptableObject ではなく [SerializeReference] 対応の通常クラスとし、
    // PPBattleTacticsDefinition のリストにインスタンスとして直接保持される
    // 派生クラスを追加するときは PPTypeMenuName を必ず付けること（型選択ピッカーがこれに依存する）
    [Serializable]
    public abstract class PPTacticStepBase
    {
        [Header("表示")]
        [Label("説明")]
        [TextArea]
        [SerializeField] protected string mDescription;

        [Header("実行者")]
        // AND 判定。空なら生存している全ユニットが候補になる
        [SerializeReference]
        [Label("実行者条件", true)] protected List<PPUnitConditionValidator> mActorConditions = new();
        [Label("実行者選択ルール")]
        [SerializeField] protected PPTacticSelectRule mActorSelectRule = PPTacticSelectRule.HighestAIScore;

        [Header("対象")]
        [Label("対象選択方針")]
        [SerializeField] protected PPTacticTargetPolicy mTargetPolicy = PPTacticTargetPolicy.ScopeDefault;
        // 対象選択方針が「条件に合う味方/敵」のときだけ使う AND 判定の条件
        [SerializeReference]
        [Label("対象条件", true)] protected List<PPUnitConditionValidator> mTargetConditions = new();
        [Label("対象選択ルール")]
        [SerializeField] protected PPTacticSelectRule mTargetSelectRule = PPTacticSelectRule.HighestAIScore;

        [Header("進行")]
        // AND 判定。空なら常に未達成として扱われ、必ず実行される
        // 判定は「このステップの対象ユニット」に対して行う
        [SerializeReference]
        [Label("達成済み判定条件", true)] protected List<PPUnitConditionValidator> mCompletionConditions = new();
        [Label("必要行動回数")]
        [SerializeField] protected int mRequiredActionCount = 1;

        public string Description => mDescription;
        public PPTacticTargetPolicy TargetPolicy => mTargetPolicy;
        // このステップの消化に必要な行動回数。下限 1 で丸める
        public int RequiredActionCount => Mathf.Max(1, mRequiredActionCount);

        // このステップを今の盤面へ当てはめる
        // 派生クラスで実装する。状態を変えず解決のみを行うこと
        // aSnap : パーティ状況スナップショット
        // aRuntime : 進行状況を保持するランタイム戦術。直前ステップの対象を引くのに使う
        // aLedger : 行動回数の仮押さえ帳
        // aReason : 解決できなかった場合の理由
        // return : 解決結果。解決できない場合は null
        public abstract PPTacticStepResolution Resolve(PPPartyAIContext aSnap, PPRuntimeTactics aRuntime,
            PPTacticActionLedger aLedger, out PPTacticRejectReason aReason);

        // このステップが既に達成済みかを判定する
        // 判定は対象ユニットに対して行うため、対象を解決できない場合は「未達成」に倒す
        // 実行してみないと分からない状態を「達成済み」と誤判定して飛ばすより、
        // もう一度実行してしまう方が戦術としては安全なため
        // aSnap : パーティ状況スナップショット
        // aRuntime : 進行状況を保持するランタイム戦術
        // return : 達成済みなら true
        public virtual bool IsCompleted(PPPartyAIContext aSnap, PPRuntimeTactics aRuntime)
        {
            // 条件が未設定のステップは判定しようがないため、常に実行対象として扱う
            if (!PPUnitConditionValidator.HasAny(mCompletionConditions)) return false;

            // 達成判定の時点では行動回数を消費しないため、行動可否を見ずに実行者を引く
            var actor = SelectActor(aSnap, null);
            var target = ResolveTarget(aSnap, aRuntime, actor);
            if (target == null) return false;

            return PPUnitConditionValidator.EvaluateAll(mCompletionConditions, target, aSnap);
        }

        // 実行者条件に合うユニットを 1 体選ぶ
        // aSnap : パーティ状況スナップショット
        // aLedger : 行動回数の仮押さえ帳。null なら行動可否を見ない（達成判定用）
        // return : 選ばれた実行者。候補が居なければ null
        protected PPBattleUnit SelectActor(PPPartyAIContext aSnap, PPTacticActionLedger aLedger)
        {
            var candidates = new List<PPBattleUnit>();
            foreach (var unit in aSnap.AliveMembers)
            {
                if (aLedger != null && !aLedger.CanAct(unit, RequiredActionCount)) continue;
                if (!PPUnitConditionValidator.EvaluateAll(mActorConditions, unit, aSnap)) continue;

                candidates.Add(unit);
            }
            return PPTacticUnitSelector.SelectUnit(candidates, mActorSelectRule, aSnap.Context);
        }

        // 対象選択方針に従って対象を解決する
        // スコープ既定の場合は対象を指定しないため null を返す（呼び出し側は解決失敗と区別すること）
        // aSnap : パーティ状況スナップショット
        // aRuntime : 進行状況を保持するランタイム戦術
        // aActor : 解決済みの実行者。自分自身を対象にする場合に使う
        // return : 対象ユニット。解決不要または解決できない場合は null
        protected PPBattleUnit ResolveTarget(PPPartyAIContext aSnap, PPRuntimeTactics aRuntime, PPBattleUnit aActor)
            => mTargetPolicy switch
            {
                PPTacticTargetPolicy.ScopeDefault => null,
                PPTacticTargetPolicy.LowestHpRatioAlly => aSnap.LowestHpRatioAlly,
                PPTacticTargetPolicy.LowestHpRatioEnemy => aSnap.LowestHpRatioEnemy,
                PPTacticTargetPolicy.HighestThreatEnemy => aSnap.HighestThreatEnemy,
                PPTacticTargetPolicy.PreviousStepTarget => aRuntime?.PreviousTarget,
                PPTacticTargetPolicy.Self => aActor,
                PPTacticTargetPolicy.ConditionAlly => SelectConditional(aSnap, aSnap.AliveMembers),
                PPTacticTargetPolicy.ConditionEnemy => SelectConditional(aSnap, aSnap.AliveEnemies),
                PPTacticTargetPolicy.RandomAlly => PickRandom(aSnap, aSnap.AliveMembers),
                PPTacticTargetPolicy.RandomEnemy => PickRandom(aSnap, aSnap.AliveEnemies),
                _ => null,
            };

        // 対象選択方針が「スコープ既定」以外かどうか
        // 対象の解決に失敗したことを不成立として扱うべきかの判定に使う
        public bool IsRequireTarget => mTargetPolicy != PPTacticTargetPolicy.ScopeDefault;

        // 対象条件に合うユニットを 1 体選ぶ
        // aSnap : パーティ状況スナップショット
        // aPool : 探索対象のユニット
        // return : 選ばれたユニット。候補が居なければ null
        private PPBattleUnit SelectConditional(PPPartyAIContext aSnap, IReadOnlyList<PPBattleUnit> aPool)
        {
            var candidates = new List<PPBattleUnit>();
            foreach (var unit in aPool)
            {
                if (!PPUnitConditionValidator.EvaluateAll(mTargetConditions, unit, aSnap)) continue;

                candidates.Add(unit);
            }
            return PPTacticUnitSelector.SelectUnit(candidates, mTargetSelectRule, aSnap.Context);
        }

        // 候補からランダムに 1 体選ぶ
        // aSnap : パーティ状況スナップショット
        // aPool : 探索対象のユニット
        // return : 選ばれたユニット。候補が居なければ null
        private static PPBattleUnit PickRandom(PPPartyAIContext aSnap, IReadOnlyList<PPBattleUnit> aPool)
            => aPool.Count == 0 ? null : aPool[aSnap.Context.Rules.RandomProvider.NextInt(aPool.Count)];

        // AI が選んだ対象を焼き込んだターゲットリゾルバを作る
        // 単体対象のスコープのみ差し替え、範囲スコープはスコープ既定のリゾルバをそのまま使う
        // aScope : スキルのターゲットスコープ
        // aTarget : AI が選んだ対象。null ならスコープ既定を使う
        // return : コマンドに渡すターゲットリゾルバ
        protected static ITargetResolver BuildResolver(TargetScope aScope, BattleUnit aTarget)
        {
            if (aTarget == null) return aScope.CreateResolver();

            return aScope switch
            {
                TargetScope.SingleEnemy => new SingleEnemyResolver(aTarget),
                TargetScope.SingleAlly => new SingleAllyResolver(aTarget),
                _ => aScope.CreateResolver(),
            };
        }

        // 設定内容から mDescription を組み立てる
        // 派生クラスでオーバーライドして、インスペクタ上でステップの意味が読めるようにする
        protected virtual void BuildDescription()
        {
        }

        // 対象選択方針を説明文用の日本語へ変換する
        // aPolicy : 対象選択方針
        // return : 日本語の表記
        protected static string GetTargetPolicyString(PPTacticTargetPolicy aPolicy)
            => aPolicy switch
            {
                PPTacticTargetPolicy.ScopeDefault => "スコープ既定",
                PPTacticTargetPolicy.LowestHpRatioAlly => "HP割合が最低の味方",
                PPTacticTargetPolicy.LowestHpRatioEnemy => "HP割合が最低の敵",
                PPTacticTargetPolicy.ConditionAlly => "条件に合う味方",
                PPTacticTargetPolicy.ConditionEnemy => "条件に合う敵",
                PPTacticTargetPolicy.PreviousStepTarget => "直前ステップと同じ対象",
                PPTacticTargetPolicy.Self => "自分自身",
                PPTacticTargetPolicy.HighestThreatEnemy => "最も脅威の高い敵",
                PPTacticTargetPolicy.RandomEnemy => "ランダムな敵",
                PPTacticTargetPolicy.RandomAlly => "ランダムな味方",
                _ => "",
            };
    }
}
