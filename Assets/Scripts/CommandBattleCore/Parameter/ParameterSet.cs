/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file ParemterSet.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ユニットが持つパラメータの定義
 * =====================================*/

using System.Collections.Generic;

namespace CommandBattleCore
{
    /// <summary>
    /// ユニット 1 体分のパラメータ一式。
    /// <para>
    /// HP・攻撃・防御・速度をプロパティとして直接触れるようにしつつ、
    /// 同じ実体を ID 付きの辞書にも登録している。
    /// 前者は通常の計算用、後者はエフェクトが「ID を指定してバフを掛ける」ための経路。
    /// 最大 HP も修飾対象として辞書に入っているため、最大 HP 上昇バフが作れる。
    /// </para>
    /// <para>
    /// 本作固有の追加パラメータは <see cref="PPParameterSet"/> 側で別に持つ。
    /// </para>
    /// </summary>
    public class ParameterSet
    {
        /// <summary>最大 HP のパラメータ ID。</summary>
        public static readonly string ParamIdMaxHp = "MaxHP";
        /// <summary>攻撃力のパラメータ ID。</summary>
        public static readonly string ParamIdAttack = "Attack";
        /// <summary>防御力のパラメータ ID。</summary>
        public static readonly string ParamIdDefense = "Defense";
        /// <summary>速度のパラメータ ID。行動順の決定に使う。</summary>
        public static readonly string ParamIdSpeed = "Speed";

        /// <summary>HP。消費・回復されるため <see cref="ResourceParameter"/>。</summary>
        public ResourceParameter Hp { get; }
        /// <summary>攻撃力。</summary>
        public Parameter Attack { get; }
        /// <summary>防御力。</summary>
        public Parameter Defense { get; }
        /// <summary>速度。</summary>
        public Parameter Speed { get; }

        /// <summary>ID から引くための登録テーブル。値はプロパティ側と同じ実体を指す。</summary>
        protected readonly Dictionary<string, Parameter> mParameters = new();

        /// <param name="aMaxHp">最大 HP。</param>
        /// <param name="aAttack">攻撃力。</param>
        /// <param name="aDefense">防御力。</param>
        /// <param name="aSpeed">速度。</param>
        public ParameterSet(float aMaxHp, float aAttack, float aDefense, float aSpeed)
        {
            Hp = new ResourceParameter(aMaxHp);

            Attack = RegisterModifiable(ParamIdAttack, new Parameter(aAttack));
            Defense = RegisterModifiable(ParamIdDefense, new Parameter(aDefense));
            Speed = RegisterModifiable(ParamIdSpeed, new Parameter(aSpeed));

            // HP 本体ではなく上限側を登録する。最大 HP へのバフを掛けられるようにするため
            RegisterModifiable(ParamIdMaxHp, Hp.Max);
        }

        /// <summary>
        /// パラメータを ID 付きで登録し、そのまま返すヘルパー。
        /// 登録と代入を 1 行で書けるようにするためのもの。
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
            return mParameters.TryGetValue(aKey, out var aParameter) ? aParameter : null;
        }

        /// <summary>登録済みパラメータの読み取り専用ビュー。</summary>
        public IReadOnlyDictionary<string, Parameter> Parameters => mParameters;

        /// <summary>
        /// すべてのパラメータから、指定した付与元の修飾子を除去する。
        /// エフェクトが複数パラメータへバフを撒いている場合、解除時にこれ 1 回で剥がせる。
        /// </summary>
        /// <param name="aSource">付与元。</param>
        public void RemoveModifiersFromSource(object aSource)
        {
            foreach (var param in mParameters.Values)
            {
                param.RemoveModifiersFromSource(aSource);
            }
        }
    }
}
