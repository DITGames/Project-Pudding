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
    //
    // 値はアセットへ数値のまま保存されるため、必ず明示的に振ること
    // 途中へ挿入して後続の値がずれると、既存アセットの対象選択が黙って別のものへ書き換わる
    // インスペクタの選択肢も値の順に並ぶため、追加するときは末尾へ足す
    public enum PPUnitAITargetPolicy
    {
        // スキルの TargetScope 既定のリゾルバをそのまま使う。全体攻撃など対象指定が要らない場合
        [InspectorName("スコープ既定")]
        ScopeDefault = 0,
        // 行動の条件チェックで条件を満たしているユニットがいれば選択する
        [InspectorName("条件を満たしているユニット")]
        ConditionedUnit = 1,
        // 通常攻撃 1 回で倒しきれる敵。とどめを最優先で狙う
        [InspectorName("通常攻撃で倒せる敵")]
        FinishableEnemy = 2,
        // 弱点を突ける敵。相性で選ぶ
        [InspectorName("弱点を突ける敵")]
        WeaknessEnemy = 3,
        // HP 割合が最も低い敵
        [InspectorName("HP割合が最低の敵")]
        LowestHpRatioEnemy = 4,
        // HP 実数値が最も低い敵
        [InspectorName("HPが最低の敵")]
        LowestHpEnemy = 5,
        // 攻撃力が最も高い敵
        [InspectorName("最も脅威の高い敵")]
        HighestThreatEnemy = 6,
        // 生存する敵からランダム
        [InspectorName("ランダムな敵")]
        RandomEnemy = 7,
        // HP 割合が最も低い味方。回復の基本
        [InspectorName("HP割合が最低の味方")]
        LowestHpRatioAlly = 8,
        // 生存する味方からランダム
        [InspectorName("ランダムな味方")]
        RandomAlly = 9,
        // 実行者自身。自己強化に使う
        [InspectorName("自分自身")]
        Self = 10,

        // ここから下が後から追加した分。既存の値を動かさないよう末尾へ足していく
        // HP 実数値が最も高い敵
        [InspectorName("HPが最高の敵")]
        HighestHpEnemy = 11,
        // 一度狙った敵を狙い続ける。固定していない・解除済みなら対象なし
        [InspectorName("固定した敵")]
        FocusedEnemy = 12,
        // HP 割合が最も高い味方。かばう相手や強化先を選ぶのに使う
        [InspectorName("HP割合が最高の味方")]
        HighestHpRatioAlly = 13,
        // 自分を除いた生存する味方からランダム
        [InspectorName("自分以外のランダムな味方")]
        RandomAllyExceptSelf = 14,
        // 自分へ最も多くダメージを与えてきた敵。「殴られた相手に殴り返す」の基本
        // 攻撃力で測る「最も脅威の高い敵」とは別物で、こちらは実際に受けた被害で決まる
        [InspectorName("最も殴ってきた敵")]
        HateTopEnemy = 15,
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
                PPUnitAITargetPolicy.HighestHpEnemy => snap.HighestHpEnemy,
                PPUnitAITargetPolicy.HighestThreatEnemy => snap.HighestThreatEnemy,
                PPUnitAITargetPolicy.RandomEnemy => PickRandom(snap.AliveEnemies, aContext),
                PPUnitAITargetPolicy.FocusedEnemy => aContext.Blackboard?.ResolveFocus(aContext.TurnCount),
                PPUnitAITargetPolicy.HateTopEnemy => aContext.Blackboard?.MostThreateningUnit(),
                PPUnitAITargetPolicy.LowestHpRatioAlly => snap.LowestHpRatioAlly,
                PPUnitAITargetPolicy.HighestHpRatioAlly => snap.HighestHpRatioAlly,
                PPUnitAITargetPolicy.RandomAlly => PickRandom(snap.AliveMembers, aContext),
                PPUnitAITargetPolicy.RandomAllyExceptSelf => PickRandomExcept(snap.AliveMembers, aContext.Unit, aContext),
                PPUnitAITargetPolicy.Self => aContext.Unit,
                _ => null,
            };
        }

        // 対象選択方針を表示用の日本語へ変換する
        // グラフ上の要約表示から使う。インスペクタの表記と揃えてある
        // aPolicy : 対象選択方針
        // return : 日本語の表記
        public static string ToDisplayString(PPUnitAITargetPolicy aPolicy)
            => aPolicy switch
            {
                PPUnitAITargetPolicy.ScopeDefault => "スコープ既定",
                PPUnitAITargetPolicy.ConditionedUnit => "条件を満たしているユニット",
                PPUnitAITargetPolicy.FinishableEnemy => "通常攻撃で倒せる敵",
                PPUnitAITargetPolicy.WeaknessEnemy => "弱点を突ける敵",
                PPUnitAITargetPolicy.LowestHpRatioEnemy => "HP割合が最低の敵",
                PPUnitAITargetPolicy.LowestHpEnemy => "HPが最低の敵",
                PPUnitAITargetPolicy.HighestHpEnemy => "HPが最高の敵",
                PPUnitAITargetPolicy.HighestThreatEnemy => "最も脅威の高い敵",
                PPUnitAITargetPolicy.RandomEnemy => "ランダムな敵",
                PPUnitAITargetPolicy.FocusedEnemy => "固定した敵",
                PPUnitAITargetPolicy.LowestHpRatioAlly => "HP割合が最低の味方",
                PPUnitAITargetPolicy.HighestHpRatioAlly => "HP割合が最高の味方",
                PPUnitAITargetPolicy.RandomAlly => "ランダムな味方",
                PPUnitAITargetPolicy.RandomAllyExceptSelf => "自分以外のランダムな味方",
                PPUnitAITargetPolicy.HateTopEnemy => "最も殴ってきた敵",
                PPUnitAITargetPolicy.Self => "自分自身",
                _ => "",
            };

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

        // 指定したユニットを除いた候補から 1 体をランダムに選ぶ
        // 除外後に候補が居なくなった場合は null を返し、行動側のフォールバックに任せる
        // aCandidates : 候補
        // aExcluded : 除外するユニット
        // aContext : 評価 1 回分の入力
        // return : 選ばれたユニット。候補が居なければ null
        private static PPBattleUnit PickRandomExcept(IReadOnlyList<PPBattleUnit> aCandidates,
            PPBattleUnit aExcluded, PPUnitAIEvalContext aContext)
        {
            var candidates = new List<PPBattleUnit>(aCandidates.Count);
            foreach (var candidate in aCandidates)
            {
                if (candidate != aExcluded) candidates.Add(candidate);
            }
            return PickRandom(candidates, aContext);
        }
    }
}
