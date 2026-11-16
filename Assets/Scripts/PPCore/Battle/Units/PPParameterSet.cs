/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPParameterSet.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニットのパラメータ集合
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;

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

    // ユニットが持つ行動リソースのゲージ種別
    // スキルゲージとコインゲージはどちらも単一スカラーで、消費先だけが異なる
    public enum PPGaugeKind
    {
        [InspectorName("スキルゲージ")]
        Skill,
        [InspectorName("コインゲージ")]
        Coin,
    }

    // 追加パラメータ一式。PPBattleUnit.ExtraParameters として ParameterSet とは別に持ち、ResolveParameter が両方をまたいで解決する
    // 行動リソース（スキルゲージ・コインゲージ）はユニットごとに専有し、支払いは行動するユニット自身のゲージから行う
    public class PPParameterSet
    {
        /* 定数 */

        // 通常攻撃コストのパラメータ ID
        public static readonly string ParameterIdAttackCost = "AttackCost";
        // 行動回数上限のパラメータ ID
        public static readonly string ParameterIdActionCount = "ActionCount";
        // きようさのパラメータ ID。会心率の算出に使う
        public static readonly string ParameterIdDexterity = "Dexterity";
        // スキルゲージ上限の ID。PPBaseParameterTable でスキル効果値を集計する際のキーにだけ使い、
        // バフ・デバフの対象（mParameters）には登録しない（上限をバフで増減させない仕様のため）
        public static readonly string ParameterIdSkillGaugeMax = "SkillGaugeMax";

        /* 状態 */

        // 行動 1 回あたりの消費コインゲージ量（行動コスト）。バフで増減しうる
        public Parameter AttackCost { get; }
        // 行動回数。進行では使っておらず、行動回数アップの状態異常と一緒に削除する予定
        public Parameter ActionCount { get; }
        // きようさ。会心率(%) = 現在値 × 0.25 で求める（PPCriticalResolver 参照）
        // Parameter で持つことで、既存のパラメータ変動エフェクトから ID 指定でバフ・デバフを掛けられる
        public Parameter Dexterity { get; }
        // スキル発動に使うゲージ。HP と同じ ResourceParameter で残量と上限を表す
        public ResourceParameter SkillGauge { get; }
        // 通常攻撃に使うゲージ。プッシャー由来のコインが PPCoinResourceBridge 経由でここへ加算される
        public ResourceParameter CoinGauge { get; }
        // ID から引くための登録テーブル。値はプロパティ側と同じ実体を指す
        // ResourceParameter は Parameter を継承しないため、ゲージ 2 種はここに含まれない
        protected readonly Dictionary<string, Parameter> mParameters = new();

        /* プロパティ */

        // 登録済みパラメータの読み取り専用ビュー
        public IReadOnlyDictionary<string, Parameter> Parameters => mParameters;

        /* 生成 */

        public PPParameterSet(float aAttackCost, int aActionCount, float aSkillGaugeMax, float aCoinGaugeMax, float aDexterity)
        {
            AttackCost = RegisterModifiable(ParameterIdAttackCost, new Parameter(aAttackCost));
            ActionCount = RegisterModifiable(ParameterIdActionCount, new Parameter(Mathf.Max(1, aActionCount)));
            Dexterity = RegisterModifiable(ParameterIdDexterity, new Parameter(aDexterity));

            SkillGauge = CreateEmptyGauge(aSkillGaugeMax);
            CoinGauge = CreateEmptyGauge(aCoinGaugeMax);
        }

        // 残量 0 のゲージを生成する
        // ResourceParameter は上限で満タン初期化されるため、生成直後に上限分を削って 0 から始める
        protected static ResourceParameter CreateEmptyGauge(float aMax)
        {
            float max = Mathf.Max(0f, aMax);
            var gauge = new ResourceParameter(max);
            gauge.Damage(max);
            return gauge;
        }

        /* 初期化・接続 */

        // パラメータを ID 付きで登録し、そのまま返すヘルパー
        protected Parameter RegisterModifiable(string aKey, Parameter aParameter)
        {
            mParameters[aKey] = aParameter;
            return aParameter;
        }

        /* 操作 */

        // 種別に対応するゲージを取得する
        public ResourceParameter Gauge(PPGaugeKind aKind)
            => aKind == PPGaugeKind.Skill ? SkillGauge : CoinGauge;

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
