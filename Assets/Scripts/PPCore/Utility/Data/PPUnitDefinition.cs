/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitDefinition.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニット定義
 * =====================================*/

using System;
using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 本作固有の要素を追加したユニット定義
    // 基底の UnitDefinition に対して、属性・追加ステータス・レベル成長曲線・AI プロファイルを持つ
    // 成長は「レベルごとの実数値テーブル」ではなく AnimationCurve の倍率で表現する
    // 基礎ステータスに倍率を掛けるだけで済み、成長カーブをインスペクタ上で視覚的に調整できる
    // 成長後の値（育成値）は四捨五入（0.5 は 0 から遠い側）で整数に丸める
    [CreateAssetMenu(fileName = "PPBattleUnitDefinition", menuName = "Project-Pudding/Definition/PPUnitDefinition")]
    public class PPUnitDefinition : UnitDefinition
    {
        [Header("ユニット拡張")]
        // 構造体はフィールド初期化子を持てないため、新規アセット用の既定値（きようさ・スキルゲージ上限）はここで与える
        // 既存アセットはシリアライズ済みの値が優先される
        [Label("ステータス")] // スキル前提なら消す
        [SerializeField]protected PPStatBlock mExpandStatBlock = new()
        {
            SkillGaugeMax = PPStatBlock.DefaultSkillGaugeMax,
            Dexterity = PPStatBlock.DefaultDexterity,
        };
        // ユニットの属性。弱点・耐性の判定に使う
        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mTypeAttribute = PPTypeAttribute.Normal;

        // このユニットの思考設定。AI はユニット単位で判断するため、プロファイルもユニットに紐づく
        // 未設定のユニットは思考の対象にならず、そのティックは何もしない
        [Header("AI")]
        [Label("AIプロファイル")]
        [SerializeField]protected PPUnitAIProfileDefinition mAIProfile;

        [Header("成長曲線 (X = レベル, Y = 倍率)")]
        [Label("HP成長曲線")][SerializeField]protected AnimationCurve mHpGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label("攻撃力成長曲線")][SerializeField]protected AnimationCurve mAttackGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label("防御力成長曲線")][SerializeField]protected AnimationCurve mDefenseGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label("素早さ成長曲線")][SerializeField]protected AnimationCurve mSpeedGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label("きようさ成長曲線")][SerializeField]protected AnimationCurve mDexterityGrowth = AnimationCurve.Linear(1, 1, 50, 3);

        public PPStatBlock ExpandStatBlock => mExpandStatBlock;
        public PPTypeAttribute TypeAttribute => mTypeAttribute;
        public PPUnitAIProfileDefinition AIProfile => mAIProfile;

        // レベル 1 でランタイムユニットを生成する。基底のシグネチャに合わせた入口
        // aDecider : コマンド決定クラス。null なら本作用のランダム AI が入る
        // return : 生成されたランタイムユニット
        public override BattleUnit CreateRuntimeUnit(ICommandDecider aDecider = null)
            => CreateRuntimeUnit(1, aDecider);

        // レベルを指定してランタイムユニットを生成する
        // 成長後のパラメータと追加パラメータを組み立て、所持スキルもランタイム化して持たせる
        // aLevel : 生成するレベル。成長曲線の評価に使う
        // aDecider : コマンド決定クラス。null なら本作用のランダム AI が入る
        // return : 生成されたランタイムユニット
        public virtual BattleUnit CreateRuntimeUnit(int aLevel, ICommandDecider aDecider = null)
        {
            var unit = new PPBattleUnit(mUnitId, DisplayName, CreateParameterSet(aLevel), CreatePPParameterSet(aLevel), mTypeAttribute)
            {
                CommandDecider = aDecider ?? new PPRandomAICommandDecider(),
                SourceDefinition = this,
            };
            foreach (var skill in mSkills)
            {
                if (skill != null)
                {
                    unit.Skills.Add(skill.CreateRuntimeSkill());
                }
            }
            return unit;
        }

        // 基礎ステータスへ成長倍率を掛けてパラメータ一式を組み立てる
        // 曲線の設定ミスでレベルアップにより弱くなるのを避けるため、倍率は最低 1 に丸める
        // aLevel : 評価するレベル
        // return : 成長を反映したパラメータ一式
        protected virtual ParameterSet CreateParameterSet(int aLevel)
        {
            var b = EvaluateStats(aLevel);
            return new ParameterSet(
                b.MaxHP, b.Attack, b.Defense, b.Speed
                );
        }

        // スキルやランタイムユニットを生成せず、表示と生成で同じ成長値（育成値）を評価する
        // きようさは基底の StatBlock に含まれないため EvaluateDexterity で別に評価する
        public virtual StatBlock EvaluateStats(int aLevel) => new()
        {
            MaxHP = EvaluateGrowthValue(mBaseStatBlock.MaxHP, mHpGrowth, aLevel),
            Attack = EvaluateGrowthValue(mBaseStatBlock.Attack, mAttackGrowth, aLevel),
            Defense = EvaluateGrowthValue(mBaseStatBlock.Defense, mDefenseGrowth, aLevel),
            Speed = EvaluateGrowthValue(mBaseStatBlock.Speed, mSpeedGrowth, aLevel),
        };

        // きようさの育成値を評価する。他の 4 能力と同じ丸め規則を使う
        // aLevel : 評価するレベル
        // return : 成長を反映したきようさ
        public virtual float EvaluateDexterity(int aLevel)
            => EvaluateGrowthValue(mExpandStatBlock.Dexterity, mDexterityGrowth, aLevel);

        // 基礎値へ成長倍率を掛けて育成値を求める。表示（ユニット作成ウィンドウ）と生成で共通の計算式
        // 曲線の設定ミスでレベルアップにより弱くなるのを避けるため、倍率は最低 1 に丸める
        // 曲線が未設定（null）の場合は倍率 1 として扱う
        // aBase : レベル 1 時点の基礎値
        // aCurve : 成長曲線（X = レベル, Y = 倍率）
        // aLevel : 評価するレベル
        // return : 四捨五入で整数に丸めた育成値
        public static float EvaluateGrowthValue(float aBase, AnimationCurve aCurve, int aLevel)
        {
            float rate = aCurve == null ? 1f : Mathf.Max(1f, aCurve.Evaluate(aLevel));
            return RoundGrowthValue(aBase * rate);
        }

        // 育成値を整数へ丸める。0.5 は 0 から遠い側へ丸める（2.5 → 3、-2.5 → -3）
        // Mathf.Round は偶数丸め（2.5 → 2）になるため使わない
        // aValue : 丸める値
        // return : 丸めた値
        public static float RoundGrowthValue(float aValue)
            => (float)Math.Round((double)aValue, MidpointRounding.AwayFromZero);

        // 追加パラメータ一式を組み立てる。きようさ以外はレベル成長の対象外
        // 行動回数上限は未設定のアセットで 0 になるため、下限 1 に丸めてから渡す
        // aLevel : きようさの成長曲線を評価するレベル
        // return : 生成された追加パラメータ一式
        protected virtual PPParameterSet CreatePPParameterSet(int aLevel)
            => new(mExpandStatBlock.AttackCost, Mathf.Max(1, mExpandStatBlock.ActionCount),
                mExpandStatBlock.SkillGaugeMax, mExpandStatBlock.CoinGaugeMax, EvaluateDexterity(aLevel));
    }
}
