/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPParameterSet.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットが持つパラメータのセット
 * =====================================*/

using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // ユニットが持つ行動リソースのゲージ種別
    // スキルゲージとコインゲージはどちらも単一スカラーで、消費先だけが異なる
    public enum PPGaugeKind
    {
        [InspectorName("スキルゲージ")]
        Skill,
        [InspectorName("コインゲージ")]
        Coin,
    }

    // 本作固有の追加パラメータ一式
    // 基底の ParameterSet を継承せず、PPBattleUnit.ExtraParameters として
    // 別に持たせる構成。基本パラメータとは独立して修飾子を掛けられる
    // ID から引く仕組みは ParameterSet と同じで、PPBattleUnit.ResolveParameter が両方をまたいで解決する
    // 行動リソース（スキルゲージ・コインゲージ）はユニットごとに専有する
    // パーティ共有のリソースプールは持たず、支払いは必ず行動するユニット自身のゲージから行う
    public class PPParameterSet
    {
        // 通常攻撃コストのパラメータ ID
        public static readonly string ParameterIdAttackCost = "AttackCost";
        // 行動回数上限のパラメータ ID
        public static readonly string ParameterIdActionCount = "ActionCount";

        // 通常攻撃 1 回あたりの消費コインゲージ量。バフで増減しうる
        public Parameter AttackCost { get; }

        // 1 ティックあたりに行動できる回数。Parameter で持つことで、
        // 既存のパラメータ変動エフェクトでそのまま増減させられる
        // 実際の消費・リセットは基底の ActionBudget が担うため、
        // この値は PPBattleUnit.UnitTick が ActionBudget.Max へ同期する
        public Parameter ActionCount { get; }

        // スキル発動に使うゲージ。HP と同じ ResourceParameter で残量と上限を表す
        public ResourceParameter SkillGauge { get; }

        // 通常攻撃に使うゲージ。プッシャー由来のコインが PPCoinResourceBridge 経由でここへ加算される
        public ResourceParameter CoinGauge { get; }

        // ID から引くための登録テーブル。値はプロパティ側と同じ実体を指す
        // ResourceParameter は Parameter を継承しないため、ゲージ 2 種はここに含まれない
        protected readonly Dictionary<string, Parameter> mParameters = new();

        // aAttackCost : 通常攻撃コストの初期値
        // aActionCount : 行動回数上限の初期値。1 未満が渡された場合は 1 に丸める
        // aSkillGaugeMax : スキルゲージの上限
        // aCoinGaugeMax : コインゲージの上限
        public PPParameterSet(float aAttackCost, int aActionCount, float aSkillGaugeMax, float aCoinGaugeMax)
        {
            AttackCost = RegisterModifiable(ParameterIdAttackCost, new Parameter(aAttackCost));
            ActionCount = RegisterModifiable(ParameterIdActionCount, new Parameter(Mathf.Max(1, aActionCount)));

            SkillGauge = CreateEmptyGauge(aSkillGaugeMax);
            CoinGauge = CreateEmptyGauge(aCoinGaugeMax);
        }

        // 種別に対応するゲージを取得する
        // aKind : 取得するゲージの種別
        // return : 該当するゲージ
        public ResourceParameter Gauge(PPGaugeKind aKind)
            => aKind == PPGaugeKind.Skill ? SkillGauge : CoinGauge;

        // 残量 0 のゲージを生成する
        // ResourceParameter は上限で満タン初期化されるため、生成直後に上限分を削って 0 から始める
        // aMax : ゲージの上限。負値は 0 に丸める
        // return : 残量 0 のゲージ
        protected static ResourceParameter CreateEmptyGauge(float aMax)
        {
            float max = Mathf.Max(0f, aMax);
            var gauge = new ResourceParameter(max);
            gauge.Damage(max);
            return gauge;
        }

        // パラメータを ID 付きで登録し、そのまま返すヘルパー
        // aKey : パラメータ ID
        // aParameter : 登録するパラメータ
        // return : 渡されたパラメータをそのまま返す
        protected Parameter RegisterModifiable(string aKey, Parameter aParameter)
        {
            mParameters[aKey] = aParameter;
            return aParameter;
        }

        // ID からパラメータを取得する
        // aKey : パラメータ ID
        // return : 該当パラメータ。未登録なら null
        public Parameter Get(string aKey)
        {
            return mParameters.TryGetValue(aKey, out Parameter aParameter) ? aParameter : null;
        }

        // 登録済みパラメータの読み取り専用ビュー
        public IReadOnlyDictionary<string, Parameter> Parameters => mParameters;

        // すべての追加パラメータから、指定した付与元の修飾子を除去する
        // aSource : 付与元
        public void RemoveModifiesFromSource(object aSource)
        {
            foreach (var param in mParameters.Values)
            {
                param.RemoveModifiersFromSource(aSource);
            }
        }
    }
}
