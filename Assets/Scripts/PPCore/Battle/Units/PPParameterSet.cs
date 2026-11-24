/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPParameterSet.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットのパラメータ集合
 * =====================================*/

using System.Collections.Generic;

namespace PPCore
{
    // ユニット 1 体分のパラメータ一式（HP・攻撃・防御・速度）
    // プロパティとして直接触れるほか、エフェクトが ID を指定して修飾できるよう辞書にも登録している
    public class ParameterSet
    {
        /* 定数 */

        // 最大 HP のパラメータ ID
        public static readonly string ParamIdMaxHp = "MaxHP";
        // 攻撃力のパラメータ ID
        public static readonly string ParamIdAttack = "Attack";
        // 防御力のパラメータ ID
        public static readonly string ParamIdDefense = "Defense";
        // 速度のパラメータ ID。行動順の決定に使う
        public static readonly string ParamIdSpeed = "Speed";

        /* 状態 */

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

        /* プロパティ */

        // 登録済みパラメータの読み取り専用ビュー
        public IReadOnlyDictionary<string, Parameter> Parameters => mParameters;

        /* 生成 */

        public ParameterSet(float aMaxHp, float aAttack, float aDefense, float aSpeed)
        {
            Hp = new ResourceParameter(aMaxHp);

            Attack = RegisterModifiable(ParamIdAttack, new Parameter(aAttack));
            Defense = RegisterModifiable(ParamIdDefense, new Parameter(aDefense));
            Speed = RegisterModifiable(ParamIdSpeed, new Parameter(aSpeed));

            // HP 本体ではなく上限側を登録する。最大 HP へのバフを掛けられるようにするため
            RegisterModifiable(ParamIdMaxHp, Hp.Max);
        }

        /* 初期化・接続 */

        // パラメータを ID 付きで登録し、そのまま返すヘルパー
        // 登録と代入を 1 行で書けるようにするためのもの
        protected Parameter RegisterModifiable(string aKey, Parameter aParameter)
        {
            mParameters[aKey] = aParameter;
            return aParameter;
        }

        /* 操作 */

        // すべてのパラメータから、指定した付与元の修飾子を除去する
        // エフェクトが複数パラメータへバフを撒いている場合、解除時にこれ 1 回で剥がせる
        public void RemoveModifiersFromSource(object aSource)
        {
            foreach (var param in mParameters.Values)
            {
                param.RemoveModifiersFromSource(aSource);
            }
        }

        /* 問い合わせ */

        // ID からパラメータを取得する
        public Parameter Get(string aKey)
        {
            return mParameters.TryGetValue(aKey, out var aParameter) ? aParameter : null;
        }
    }

    // 追加パラメータ一式。PPBattleUnit.ExtraParameters として ParameterSet とは別に持ち、ResolveParameter が両方をまたいで解決する
    public class PPParameterSet
    {
        /* 定数 */

        // きようさのパラメータ ID。会心率の算出に使う
        public static readonly string ParameterIdDexterity = "Dexterity";

        /* 状態 */

        // きようさ。会心率(%) = 現在値 × 0.25 で求める（PPCriticalResolver 参照）
        // Parameter で持つことで、既存のパラメータ変動エフェクトから ID 指定でバフ・デバフを掛けられる
        public Parameter Dexterity { get; }
        // ID から引くための登録テーブル。値はプロパティ側と同じ実体を指す
        protected readonly Dictionary<string, Parameter> mParameters = new();

        /* プロパティ */

        // 登録済みパラメータの読み取り専用ビュー
        public IReadOnlyDictionary<string, Parameter> Parameters => mParameters;

        /* 生成 */

        public PPParameterSet(float aDexterity)
        {
            Dexterity = RegisterModifiable(ParameterIdDexterity, new Parameter(aDexterity));
        }

        /* 初期化・接続 */

        // パラメータを ID 付きで登録し、そのまま返すヘルパー
        protected Parameter RegisterModifiable(string aKey, Parameter aParameter)
        {
            mParameters[aKey] = aParameter;
            return aParameter;
        }

        /* 操作 */

        // すべての追加パラメータから、指定した付与元の修飾子を除去する
        // aSource : 付与元
        public void RemoveModifiesFromSource(object aSource)
        {
            foreach (var param in mParameters.Values)
            {
                param.RemoveModifiersFromSource(aSource);
            }
        }

        /* 問い合わせ */

        // ID からパラメータを取得する
        public Parameter Get(string aKey)
        {
            return mParameters.TryGetValue(aKey, out Parameter aParameter) ? aParameter : null;
        }
    }
}
