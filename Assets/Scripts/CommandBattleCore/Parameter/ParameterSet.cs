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
    // ユニット 1 体分のパラメータ一式
    // HP・攻撃・防御・速度をプロパティとして直接触れるようにしつつ、
    // 同じ実体を ID 付きの辞書にも登録している
    // 前者は通常の計算用、後者はエフェクトが「ID を指定してバフを掛ける」ための経路
    // 最大 HP も修飾対象として辞書に入っているため、最大 HP 上昇バフが作れる
    public class ParameterSet
    {
        // 最大 HP のパラメータ ID
        public static readonly string ParamIdMaxHp = "MaxHP";
        // 攻撃力のパラメータ ID
        public static readonly string ParamIdAttack = "Attack";
        // 防御力のパラメータ ID
        public static readonly string ParamIdDefense = "Defense";
        // 速度のパラメータ ID。行動順の決定に使う
        public static readonly string ParamIdSpeed = "Speed";

        // HP。消費・回復されるため ResourceParameter
        public ResourceParameter Hp { get; }
        // 攻撃力
        public Parameter Attack { get; }
        // 防御力
        public Parameter Defense { get; }
        // 速度
        public Parameter Speed { get; }

        // ID から引くための登録テーブル。値はプロパティ側と同じ実体を指す
        protected readonly Dictionary<string, Parameter> mParameters = new();

        // aMaxHp : 最大 HP
        // aAttack : 攻撃力
        // aDefense : 防御力
        // aSpeed : 速度
        public ParameterSet(float aMaxHp, float aAttack, float aDefense, float aSpeed)
        {
            Hp = new ResourceParameter(aMaxHp);

            Attack = RegisterModifiable(ParamIdAttack, new Parameter(aAttack));
            Defense = RegisterModifiable(ParamIdDefense, new Parameter(aDefense));
            Speed = RegisterModifiable(ParamIdSpeed, new Parameter(aSpeed));

            // HP 本体ではなく上限側を登録する。最大 HP へのバフを掛けられるようにするため
            RegisterModifiable(ParamIdMaxHp, Hp.Max);
        }

        // パラメータを ID 付きで登録し、そのまま返すヘルパー
        // 登録と代入を 1 行で書けるようにするためのもの
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
            return mParameters.TryGetValue(aKey, out var aParameter) ? aParameter : null;
        }

        // 登録済みパラメータの読み取り専用ビュー
        public IReadOnlyDictionary<string, Parameter> Parameters => mParameters;

        // すべてのパラメータから、指定した付与元の修飾子を除去する
        // エフェクトが複数パラメータへバフを撒いている場合、解除時にこれ 1 回で剥がせる
        // aSource : 付与元
        public void RemoveModifiersFromSource(object aSource)
        {
            foreach (var param in mParameters.Values)
            {
                param.RemoveModifiersFromSource(aSource);
            }
        }
    }
}
