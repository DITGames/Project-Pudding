/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitAITargeting.cs
 * @author hqrse
 * @date 2026/08/24
 * @brief ユニットAIの対象選択方針と解決処理
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 行動の対象をどう選ぶか
    // 単体対象の行動はここで選ばれたユニットを焼き込んだリゾルバで実行される
    public enum PPUnitAITargetPolicy
    {
        // スキルの TargetScope 既定のリゾルバをそのまま使う。全体攻撃など対象指定が要らない場合
        [InspectorName("スコープ既定")]
        ScopeDefault,
        // 行動の条件チェックで条件を満たしているユニットがいれば選択する
        [InspectorName("条件を満たしているユニット")]
        ConditionedUnit,
        // 通常攻撃 1 回で倒しきれる敵。とどめを最優先で狙う
        [InspectorName("通常攻撃で倒せる敵")]
        FinishableEnemy,
        // 弱点を突ける敵。相性で選ぶ
        [InspectorName("弱点を突ける敵")]
        WeaknessEnemy,
        // HP 割合が最も低い敵
        [InspectorName("HP割合が最低の敵")]
        LowestHpRatioEnemy,
        // HP 実数値が最も低い敵
        [InspectorName("HPが最低の敵")]
        LowestHpEnemy,
        // 攻撃力が最も高い敵
        [InspectorName("最も脅威の高い敵")]
        HighestThreatEnemy,
        // 生存する敵からランダム
        [InspectorName("ランダムな敵")]
        RandomEnemy,
        // HP 割合が最も低い味方。回復の基本
        [InspectorName("HP割合が最低の味方")]
        LowestHpRatioAlly,
        // 生存する味方からランダム
        [InspectorName("ランダムな味方")]
        RandomAlly,
        // 実行者自身。自己強化に使う
        [InspectorName("自分自身")]
        Self,
    }

    // 対象選択方針を実際の対象へ解決するヘルパー
    // 通常攻撃・スキルの双方から同じ規則を使うため、片方に実装を寄せずここへ集約している
    // 乱数は行動するユニット自身の供給元を経由すること（UnityEngine.Random は使わない）
    public static class PPUnitAITargeting
    {
        // 方針に従って対象を 1 体解決する
        // 該当者が居ない場合は null を返し、呼び出し側でその行動を取りやめる
        // aPolicy : 対象選択方針
        // aContext : 評価 1 回分の入力
        // return : 選ばれた対象。スコープ既定・該当者なしの場合は null
        public static PPBattleUnit Resolve(PPUnitAITargetPolicy aPolicy, PPUnitAIEvalContext aContext)
        {
            var snap = aContext.Snapshot;
            return aPolicy switch
            {
                PPUnitAITargetPolicy.ScopeDefault => null,
                PPUnitAITargetPolicy.ConditionedUnit => FindConditionedUnit(aContext),
                PPUnitAITargetPolicy.FinishableEnemy => FindFinishableEnemy(aContext),
                PPUnitAITargetPolicy.WeaknessEnemy => FindWeaknessEnemy(aContext),
                PPUnitAITargetPolicy.LowestHpRatioEnemy => snap.LowestHpRatioEnemy,
                PPUnitAITargetPolicy.LowestHpEnemy => snap.LowestHpEnemy,
                PPUnitAITargetPolicy.HighestThreatEnemy => snap.HighestThreatEnemy,
                PPUnitAITargetPolicy.RandomEnemy => PickRandom(snap.AliveEnemies, aContext),
                PPUnitAITargetPolicy.LowestHpRatioAlly => snap.LowestHpRatioAlly,
                PPUnitAITargetPolicy.RandomAlly => PickRandom(snap.AliveMembers, aContext),
                PPUnitAITargetPolicy.Self => aContext.Unit,
                _ => null,
            };
        }

        // 解決した対象を焼き込んだリゾルバを組み立てる
        // 対象が未指定（スコープ既定）の場合は null を返し、行動側の既定リゾルバを使わせる
        // aScope : 行動の対象範囲
        // aTarget : 焼き込む対象
        // return : リゾルバ。既定に任せる場合は null
        public static ITargetResolver BuildResolver(TargetScope aScope, PPBattleUnit aTarget)
        {
            if (aTarget == null) return null;

            return aScope switch
            {
                TargetScope.SingleEnemy => new SingleEnemyResolver(aTarget),
                TargetScope.SingleAlly => new SingleAllyResolver(aTarget),
                _ => null,
            };
        }

        // 対象を明示的に決める必要がある対象範囲かを判定する
        // aScope : 行動の対象範囲
        // return : 単体対象なら true
        public static bool NeedsExplicitTarget(TargetScope aScope)
            => aScope is TargetScope.SingleEnemy or TargetScope.SingleAlly;

        // ターゲット検索ノードが積んだ対象候補から 1 体を選ぶ
        // 重複して積まれたユニットはその回数だけ抽選に入るため、重ねて条件に合うほど選ばれやすくなる
        // aContext : 評価 1 回分の入力
        // return : 選ばれたユニット。候補が積まれていなければ null
        public static PPBattleUnit FindConditionedUnit(PPUnitAIEvalContext aContext)
        {
            if(aContext.Snapshot.ConditionedUnits.Count == 0) return null;
            return PickRandom(aContext.Snapshot.ConditionedUnits, aContext);
        }

        // 通常攻撃 1 回で倒しきれる敵を探す
        // 倒せる相手が複数いる場合は、最も HP の低い相手を選んで確実に仕留める
        // 見積もりには命中・クリティカルの乱数を含めないため、実際には落としきれないこともある
        // aContext : 評価 1 回分の入力
        // return : 倒しきれる敵。居なければ null
        public static PPBattleUnit FindFinishableEnemy(PPUnitAIEvalContext aContext)
        {
            PPBattleUnit best = null;

            foreach (var enemy in aContext.Snapshot.AliveEnemies)
            {
                float damage = PPDamageUtility.ResolveAttackDamage(aContext.Unit, enemy);
                if (damage < enemy.Parameters.Hp.Current) continue;

                if (best == null || enemy.Parameters.Hp.Current < best.Parameters.Hp.Current)
                {
                    best = enemy;
                }
            }
            return best;
        }

        // 弱点を突ける敵を探す
        // 候補が複数いる場合は HP 割合が最も低い相手を選ぶ
        // aContext : 評価 1 回分の入力
        // return : 弱点を突ける敵。居なければ null
        public static PPBattleUnit FindWeaknessEnemy(PPUnitAIEvalContext aContext)
        {
            PPBattleUnit best = null;
            float bestRatio = 0f;

            foreach (var enemy in aContext.Snapshot.AliveEnemies)
            {
                if (PPAttributeAffinity.Resolve(aContext.Unit.TypeAttribute, enemy.TypeAttribute) != PPAffinityResult.Weak)
                    continue;

                float ratio = PPPartyAIContext.HpRatio(enemy);
                if (best == null || ratio < bestRatio)
                {
                    best = enemy;
                    bestRatio = ratio;
                }
            }
            return best;
        }

        // 候補からランダムに 1 体選ぶ
        // aCandidates : 選択候補
        // aContext : 乱数供給元を含む評価入力
        // return : 選ばれたユニット。候補が空なら null
        private static PPBattleUnit PickRandom(IReadOnlyList<PPBattleUnit> aCandidates,
            PPUnitAIEvalContext aContext)
            => aCandidates.Count == 0
                ? null
                : aCandidates[aContext.Unit.ResolveRandom(aContext.Battle).NextInt(aCandidates.Count)];
    }
}
