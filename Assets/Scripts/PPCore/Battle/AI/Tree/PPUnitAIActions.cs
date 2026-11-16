/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitAIActions.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ビヘイビアツリーのアクションノードとアクション（攻撃・スキル・待機）
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 行動を 1 つ実行する葉ノード
    // アクションが実際に組み立てられた場合のみ確定とし、
    // 組み立てられなければ不成立として親の次の候補へ処理を渡す
    // 「条件は満たしたがゲージが足りない・対象が居ない」場合に自然と次の手へ流れる
    //
    // 維持ティック数を設定すると、この行動を選んだあとしばらく判断が固定される
    // 「強いスキルが撃てるまで通常攻撃で溜める」のように、
    // 数ティックまたいで初めて意味を持つ判断がティックごとに揺れるのを防ぐためのもの
    [Serializable]
    [PPTypeMenuName("行動/実行")]
    public sealed class PPUnitAIActionNode : PPUnitAINode
    {
        [Header("行動")]
        [Label("実行する行動")]
        [SerializeReference]
        [SerializeField] private PPUnitAIActionBase mAction;

        // この行動を選んだあと、判断を維持するティック数
        // 0 なら維持せず、次のティックはツリーの先頭から自由に選び直す
        [Label("判断を維持するティック数")]
        [SerializeField] private int mCommitTicks = 0;

        // この行動で狙った対象へ、狙いを固定するティック数
        // 0 なら固定しない。1 以上なら、その対象が倒れるか指定ティック数が過ぎるまで固定が続く
        // 固定した狙いは、対象の選び方に「固定した敵」を指定した行動が拾う
        [Label("狙いを固定するティック数")]
        [SerializeField] private int mFocusTicks = 0;

        protected override string DefaultNodeName => mAction != null ? mAction.ActionName : "行動";

        // 実行する行動が設定されているか。エディタの診断から参照する
        public bool HasAction => mAction != null;

        // 実行する行動。エディタの診断が設定漏れを調べるために参照する
        public PPUnitAIActionBase Action => mAction;

        // 実行する行動を設定する。雛形の生成など、エディタからノードを組み立てる処理で使う
        // aAction : 設定する行動
        public void SetAction(PPUnitAIActionBase aAction) => mAction = aAction;

        // 行動の要約に、判断の維持と狙いの固定を添える
        public override string Summary
        {
            get
            {
                if (mAction == null) return "行動が未設定";

                string body = mAction.Summary;
                if (mCommitTicks > 0) body += $"\n維持 {mCommitTicks}T";
                if (mFocusTicks > 0) body += $" ／ 狙い固定 {mFocusTicks}T";
                return body;
            }
        }

        // アクションを組み立てて確定させる
        // aContext : 評価 1 回分の入力
        // return : 組み立てられた行動。組み立てられなければ Failed
        protected override PPUnitAINodeResult EvaluateCore(PPUnitAIEvalContext aContext)
        {
            if (mAction == null) return PPUnitAINodeResult.Failed;

            var result = mAction.Build(aContext);
            if (!result.IsDecided) return result;

            if (mCommitTicks > 0) result = result.WithCommit(mCommitTicks);
            if (mFocusTicks > 0) result = result.WithFocus(mFocusTicks);
            return result;
        }
    }

    // ユニット AI が実行する行動の基底クラス
    // 行動ノード（PPUnitAIActionNode）が 1 つ保持し、実行できる場合のみ確定させる
    // 実行できるかどうかの判定と、コマンドの組み立てを 1 つのメソッドで行うのは、
    // 「撃てるか調べてから作る」の二度手間を避け、判定と生成のズレを防ぐため
    // 派生クラスを追加するときは PPTypeMenuName を必ず付けること（型選択ピッカーがこれに依存する）
    [Serializable]
    public abstract class PPUnitAIActionBase
    {
        [Header("表示")]
        [Label("行動名")]
        [SerializeField] protected string mActionName = "";

        // 記録・ログに出す行動名。未入力なら型ごとの既定名を使う
        public string ActionName => string.IsNullOrEmpty(mActionName) ? DefaultActionName : mActionName;

        // 型ごとの既定表示名。派生側で上書きする
        protected abstract string DefaultActionName { get; }

        // グラフ上のノードへ出す設定内容の要約
        // 「何を・誰に」が行動ノードのタイトルだけでは読めないため、派生側で示す
        public virtual string Summary => "";

        // 今の状況でこの行動を組み立てる
        // 派生クラスで実装する。バトルの状態を変えず、コマンドを作るだけに留めること
        // aContext : 評価 1 回分の入力
        // return : 組み立てられた行動。実行できない場合は PPUnitAINodeResult.Failed
        public abstract PPUnitAINodeResult Build(PPUnitAIEvalContext aContext);
    }

    // コインゲージを消費して通常攻撃を行う行動
    // 通常攻撃は消費量・威力ともに固定で強弱の段階を持たないため、設定は対象の選び方だけ
    // コインゲージが足りない、または対象が居ない場合は組み立てに失敗し、次の候補へ処理が渡る
    [Serializable]
    [PPTypeMenuName("通常攻撃")]
    public sealed class PPUnitAIAttackAction : PPUnitAIActionBase
    {
        [Header("対象")]
        [Label("対象の選び方")]
        [SerializeField] private PPUnitAITargetPolicy mTargetPolicy = PPUnitAITargetPolicy.LowestHpRatioEnemy;
        // 指定した選び方で対象が見つからなかった場合に、HP 割合が最も低い敵へ切り替えるか
        // 「倒せる敵」「弱点を突ける敵」のように、該当者が居ないことが普通にある方針で使う
        [Label("見つからなければ他の敵を狙う")]
        [SerializeField] private bool mIsFallbackAnyEnemy = true;

        protected override string DefaultActionName => "通常攻撃";

        // 狙う相手と、外れたときの振る舞いを 1 行で示す
        public override string Summary
            => $"対象 : {PPUnitAITargeting.ToDisplayString(mTargetPolicy)}"
               + (mIsFallbackAnyEnemy ? "（居なければ他の敵）" : "");

        // コインゲージの残量と対象を確認し、足りていれば通常攻撃コマンドを組み立てる
        // aContext : 評価 1 回分の入力
        // return : 組み立てられた通常攻撃。撃てない場合は Failed
        public override PPUnitAINodeResult Build(PPUnitAIEvalContext aContext)
        {
            var unit = aContext.Unit;

            // 生成時点のコストで支払えるかを先に見る。実際の消費はコマンド実行時に行われる
            // 台帳がある場合は、同じティックで既に積んだ行動が使う予定の分も差し引いて判定する
            float attackCost = unit.ExtraParameters.Get(PPParameterSet.ParameterIdAttackCost)?.CurrentValue ?? 0f;
            var ledger = aContext.Snapshot.Ledger;
            bool canPay = ledger != null
                ? ledger.CanReserveCoin(unit, attackCost)
                : PPGaugeUtility.CanPay(unit.ExtraParameters.CoinGauge, attackCost);
            if (!canPay) return PPUnitAINodeResult.Failed;

            var target = PPUnitAITargeting.Resolve(mTargetPolicy, aContext);
            if (target == null && mIsFallbackAnyEnemy)
            {
                target = aContext.Snapshot.LowestHpRatioEnemy;
            }
            if (target == null)
                return PPUnitAINodeResult.Failed;

            var command = new PPAttackCommand(unit, new SingleEnemyResolver(target));
            return PPUnitAINodeResult.Execute(command, ActionName, target);
        }
    }

    // スキルの絞り込み条件を保持していることを示すインターフェース
    // 行動と条件のどちらも同じ絞り込みを持つため、
    // エディタの診断が「直接指定なのにスキル未設定」を型を問わず拾えるようにする
    public interface IPPUnitAISkillFilterOwner
    {
        // 保持しているスキルの絞り込み条件
        PPUnitAISkillFilter Filter { get; }
    }

    // 絞り込んだ所持スキルの中から 1 つを選んで発動する行動
    // 「攻撃グループで一番強いものを撃つ」「回復タグのスキルを撃つ」のように、
    // どのスキルを指すかは絞り込みと選び方の組み合わせで表現する
    // 発動できるスキルが無い、または対象が居ない場合は組み立てに失敗し、次の候補へ処理が渡る
    [Serializable]
    [PPTypeMenuName("スキル発動")]
    public sealed class PPUnitAISkillAction : PPUnitAIActionBase, IPPUnitAISkillFilterOwner
    {
        [Header("スキル")]
        [Label("対象スキル")]
        [SerializeField] private PPUnitAISkillFilter mFilter = new();
        [Label("複数ある場合の選び方")]
        [SerializeField] private PPUnitAISkillSelectRule mSelectRule = PPUnitAISkillSelectRule.HighestAIScore;

        [Header("対象")]
        [Label("対象の選び方")]
        [SerializeField] private PPUnitAITargetPolicy mTargetPolicy = PPUnitAITargetPolicy.ScopeDefault;

        // 保持しているスキルの絞り込み条件。エディタの診断から参照する
        public PPUnitAISkillFilter Filter => mFilter;

        protected override string DefaultActionName => $"スキル発動（{mFilter.ToDisplayString()}）";

        // 「何を・どう選んで・誰に」を 1 行にまとめる
        public override string Summary
            => $"{mFilter.ToDisplayString()} ・ {PPUnitAISkillSelectRuleUtility.ToDisplayString(mSelectRule)}"
               + $"\n対象 : {PPUnitAITargeting.ToDisplayString(mTargetPolicy)}";

        // 今発動できるスキルを絞り込んで 1 つ選び、対象を解決してコマンドを組み立てる
        // aContext : 評価 1 回分の入力
        // return : 組み立てられたスキル発動。撃てない場合は Failed
        public override PPUnitAINodeResult Build(PPUnitAIEvalContext aContext)
        {
            var (skill, definition) = PPUnitAISkillQuery.SelectCastable(
                aContext.Unit, mFilter, mSelectRule, aContext.Battle, aContext.Snapshot.Ledger);
            if (skill == null) return PPUnitAINodeResult.Failed;

            var target = PPUnitAITargeting.Resolve(mTargetPolicy, aContext);
            // 単体対象のスキルは対象が決まらなければ撃てない
            // 全体・自己完結のスキルは対象未指定のままスキル既定のリゾルバへ任せる
            if (target == null && PPUnitAITargeting.NeedsExplicitTarget(definition.TargetScope))
            {
                target = ResolveDefaultTarget(definition, aContext);
                if (target == null) return PPUnitAINodeResult.Failed;
            }

            var resolver = PPUnitAITargeting.BuildResolver(definition.TargetScope, target);
            var command = new PPSkillCommand(aContext.Unit, skill, resolver);
            return PPUnitAINodeResult.Execute(command, ActionName, target);
        }

        // 対象の選び方が「スコープ既定」なのに単体対象だった場合の落としどころを決める
        // 味方単体なら HP 割合が最も低い味方、敵単体なら HP 割合が最も低い敵を狙う
        // aDefinition : 発動するスキルの定義
        // aContext : 評価 1 回分の入力
        // return : 決まった対象。候補が居なければ null
        private static PPBattleUnit ResolveDefaultTarget(PPSkillDefinition aDefinition, PPUnitAIEvalContext aContext)
            => aDefinition.TargetScope == TargetScope.SingleAlly
                ? aContext.Snapshot.LowestHpRatioAlly ?? aContext.Unit
                : aContext.Snapshot.LowestHpRatioEnemy;
    }

    // 何もせずそのティックを見送る行動
    // 「上位スキルが撃てるまで溜める」のように、意図的に手を出さないことを明示したい場合に置く
    // 常に確定するため、これより下の候補は評価されない
    [Serializable]
    [PPTypeMenuName("待機")]
    public sealed class PPUnitAIWaitAction : PPUnitAIActionBase
    {
        protected override string DefaultActionName => "待機";

        // 常に待機で確定する
        // aContext : 評価 1 回分の入力
        // return : 待機を表す結果
        public override PPUnitAINodeResult Build(PPUnitAIEvalContext aContext)
            => PPUnitAINodeResult.Wait(ActionName);
    }
}
