/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitDefinition.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニット定義
 * =====================================*/

using System;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // ユニット定義。属性・ロール・追加ステータス・レベル成長曲線・ビヘイビアツリーを持つ
    // 成長は AnimationCurve の倍率で表現し、成長後の値は四捨五入で整数に丸める
    [CreateAssetMenu(fileName = "PPBattleUnitDefinition", menuName = "Project-Pudding/Definition/PPUnitDefinition")]
    public class PPUnitDefinition : UnitDefinition
    {
        /* インスペクタ設定 */

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
        [Label("ロール")]
        [SerializeField]protected PPUnitRole mRole = PPUnitRole.Balance;

        // このユニットの思考設定。AI はユニット単位で判断するため、ツリーもユニットに紐づく
        // 未設定のユニットは ロール既定 → 全体既定 の順に解決したツリーで動く（PPUnitAIDefaultsDefinition.ResolveTree）
        [Header("AI")]
        [Label("ビヘイビアツリー")]
        [SerializeField]protected PPUnitBehaviourTree mBehaviourTree;

        [Header("成長曲線 (X = レベル, Y = 倍率)")]
        [Label(PPUnitAbilityDefinition.NameHp + PPUnitAbilityDefinition.GrowthCurveSuffix)][SerializeField]protected AnimationCurve mHpGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label(PPUnitAbilityDefinition.NameStrength + PPUnitAbilityDefinition.GrowthCurveSuffix)][SerializeField]protected AnimationCurve mAttackGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label(PPUnitAbilityDefinition.NameGuard + PPUnitAbilityDefinition.GrowthCurveSuffix)][SerializeField]protected AnimationCurve mDefenseGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label(PPUnitAbilityDefinition.NameAgility + PPUnitAbilityDefinition.GrowthCurveSuffix)][SerializeField]protected AnimationCurve mSpeedGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label(PPUnitAbilityDefinition.NameDexterity + PPUnitAbilityDefinition.GrowthCurveSuffix)][SerializeField]protected AnimationCurve mDexterityGrowth = AnimationCurve.Linear(1, 1, 50, 3);

        /* プロパティ */

        public PPStatBlock ExpandStatBlock => mExpandStatBlock;
        public PPTypeAttribute TypeAttribute => mTypeAttribute;
        public PPUnitRole Role => mRole;
        public PPUnitBehaviourTree BehaviourTree => mBehaviourTree;

        /* static 関数 */

        // 育成値を整数へ丸める。0.5 は 0 から遠い側へ丸める（2.5 → 3、-2.5 → -3）
        // Mathf.Round は偶数丸め（2.5 → 2）になるため使わない
        public static float RoundGrowthValue(float aValue)
            => (float)Math.Round((double)aValue, MidpointRounding.AwayFromZero);

        // 基礎値へ成長倍率を掛けて育成値を求める。表示と生成で共通の計算式
        // 倍率は最低 1 に丸め、曲線が未設定の場合は倍率 1 として扱う
        public static float EvaluateGrowthValue(float aBase, AnimationCurve aCurve, int aLevel)
        {
            float rate = aCurve == null ? 1f : Mathf.Max(1f, aCurve.Evaluate(aLevel));
            return RoundGrowthValue(aBase * rate);
        }

        /* 生成 */

        // レベル 1 でランタイムユニットを生成する。基底のシグネチャに合わせた入口
        public override BattleUnit CreateRuntimeUnit(ICommandDecider aDecider = null)
            => CreateRuntimeUnit(1, aDecider);

        // レベルを指定してランタイムユニットを生成する
        // 成長後のパラメータと追加パラメータを組み立て、所持スキルもランタイム化して持たせる
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
        protected virtual ParameterSet CreateParameterSet(int aLevel)
        {
            var b = EvaluateStats(aLevel);
            return new ParameterSet(
                b.MaxHP, b.Attack, b.Defense, b.Speed
                );
        }

        // 追加パラメータ一式を組み立てる。きようさ以外はレベル成長の対象外
        // 行動回数上限は未設定のアセットで 0 になるため、下限 1 に丸めてから渡す
        protected virtual PPParameterSet CreatePPParameterSet(int aLevel)
            => new(mExpandStatBlock.AttackCost, Mathf.Max(1, mExpandStatBlock.ActionCount),
                mExpandStatBlock.SkillGaugeMax, mExpandStatBlock.CoinGaugeMax, EvaluateDexterity(aLevel));

        /* 評価・判定 */

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
        public virtual float EvaluateDexterity(int aLevel)
            => EvaluateGrowthValue(mExpandStatBlock.Dexterity, mDexterityGrowth, aLevel);
    }
}
