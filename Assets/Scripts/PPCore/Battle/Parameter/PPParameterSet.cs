/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPParameterSet.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットが持つパラメータのセット
 * =====================================*/
using System.Collections.Generic;
using CommandBattleCore;

namespace PPCore
{
    /// <summary>
    /// 本作固有の追加パラメータ一式。
    /// <para>
    /// 基底の <see cref="ParameterSet"/> を継承せず、<see cref="PPBattleUnit.ExtraParameters"/> として
    /// 別に持たせる構成。基本パラメータとは独立して修飾子を掛けられる。
    /// ID から引く仕組みは <see cref="ParameterSet"/> と同じで、
    /// <see cref="PPBattleUnit.ResolveParameter"/> が両方をまたいで解決する。
    /// </para>
    /// </summary>
    public class PPParameterSet
    {
        /// <summary>通常攻撃コストのパラメータ ID。</summary>
        public static readonly string ParameterIdAttackCost = "AttackCost";

        /// <summary>通常攻撃 1 回あたりの消費リソース量。バフで増減しうる。</summary>
        public Parameter AttackCost { get; }

        /// <summary>ID から引くための登録テーブル。値はプロパティ側と同じ実体を指す。</summary>
        protected readonly Dictionary<string, Parameter> mParameters = new();

        /// <param name="aAttackCost">通常攻撃コストの初期値。</param>
        public PPParameterSet(float aAttackCost)
        {
            AttackCost = RegisterModifiable(ParameterIdAttackCost, new Parameter(aAttackCost));
        }

        /// <summary>
        /// パラメータを ID 付きで登録し、そのまま返すヘルパー。
        /// </summary>
        /// <param name="aKey">パラメータ ID。</param>
        /// <param name="aParameter">登録するパラメータ。</param>
        /// <returns>渡されたパラメータをそのまま返す。</returns>
        protected Parameter RegisterModifiable(string aKey, Parameter aParameter)
        {
            mParameters[aKey] = aParameter;
            return aParameter;
        }

        /// <summary>
        /// ID からパラメータを取得する。
        /// </summary>
        /// <param name="aKey">パラメータ ID。</param>
        /// <returns>該当パラメータ。未登録なら null。</returns>
        public Parameter Get(string aKey)
        {
            return mParameters.TryGetValue(aKey, out Parameter aParameter) ? aParameter : null;
        }

        /// <summary>登録済みパラメータの読み取り専用ビュー。</summary>
        public IReadOnlyDictionary<string, Parameter> Parameters => mParameters;

        /// <summary>
        /// すべての追加パラメータから、指定した付与元の修飾子を除去する。
        /// </summary>
        /// <param name="aSource">付与元。</param>
        public void RemoveModifiesFromSource(object aSource)
        {
            foreach (var param in mParameters.Values)
            {
                param.RemoveModifiersFromSource(aSource);
            }
        }
    }
}
