/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitDefinition.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニット定義
 * =====================================*/

using CommandBattleCore;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // 本作固有の要素を追加したユニット定義
    // 基底の UnitDefinition に対して、属性・追加ステータス・レベル成長曲線・AI プロファイルを持つ
    // 成長は「レベルごとの実数値テーブル」ではなく AnimationCurve の倍率で表現する
    // 基礎ステータスに倍率を掛けるだけで済み、成長カーブをインスペクタ上で視覚的に調整できる
    [CreateAssetMenu(fileName = "PPBattleUnitDefinition", menuName = "Project-Pudding/Definition/PPUnitDefinition")]
    public class PPUnitDefinition : UnitDefinition
    {
        [Header("ユニット拡張")]
        [Label("ステータス")] // スキル前提なら消す
        [SerializeField]protected PPStatBlock mExpandStatBlock;
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
            var unit = new PPBattleUnit(mUnitId, DisplayName, CreateParameterSet(aLevel), CreatePPParameterSet(), mTypeAttribute)
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
            var b = mBaseStatBlock;
            return new ParameterSet(
                b.MaxHP * Mathf.Max(1f, mHpGrowth.Evaluate(aLevel)),
                b.Attack * Mathf.Max(1f, mAttackGrowth.Evaluate(aLevel)),
                b.Defense * Mathf.Max(1f, mDefenseGrowth.Evaluate(aLevel)),
                b.Speed * Mathf.Max(1f, mSpeedGrowth.Evaluate(aLevel))
                );
        }

        // 追加パラメータ一式を組み立てる。こちらはレベル成長の対象外
        // 行動回数上限は未設定のアセットで 0 になるため、下限 1 に丸めてから渡す
        // return : 生成された追加パラメータ一式
        protected virtual PPParameterSet CreatePPParameterSet()
            => new(mExpandStatBlock.AttackCost, Mathf.Max(1, mExpandStatBlock.ActionCount),
                mExpandStatBlock.SkillGaugeMax, mExpandStatBlock.CoinGaugeMax);
    }
}
