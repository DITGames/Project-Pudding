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
    // 本作固有の追加パラメータ一式
    // 基底の ParameterSet を継承せず、PPBattleUnit.ExtraParameters として
    // 別に持たせる構成。基本パラメータとは独立して修飾子を掛けられる
    // ID から引く仕組みは ParameterSet と同じで、PPBattleUnit.ResolveParameter が両方をまたいで解決する
    public class PPParameterSet
    {
        // 通常攻撃コストのパラメータ ID
        public static readonly string ParameterIdAttackCost = "AttackCost";
        // 行動回数上限のパラメータ ID
        public static readonly string ParameterIdActionCount = "ActionCount";

        // 通常攻撃 1 回あたりの消費リソース量。バフで増減しうる
        public Parameter AttackCost { get; }

        // 1 ティックあたりに行動できる回数。Parameter で持つことで、
        // 既存のパラメータ変動エフェクトでそのまま増減させられる
        // 実際の消費・リセットは基底の ActionBudget が担うため、
        // この値は PPBattleUnit.UnitTick が ActionBudget.Max へ同期する
        public Parameter ActionCount { get; }

        // ID から引くための登録テーブル。値はプロパティ側と同じ実体を指す
        protected readonly Dictionary<string, Parameter> mParameters = new();

        // aAttackCost : 通常攻撃コストの初期値
        // aActionCount : 行動回数上限の初期値。1 未満が渡された場合は 1 に丸める
        public PPParameterSet(float aAttackCost, int aActionCount = 1)
        {
            AttackCost = RegisterModifiable(ParameterIdAttackCost, new Parameter(aAttackCost));
            ActionCount = RegisterModifiable(ParameterIdActionCount, new Parameter(Mathf.Max(1, aActionCount)));
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
