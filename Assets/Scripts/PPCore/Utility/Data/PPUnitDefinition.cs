/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPUnitDefinition.cs
 * @author hqrse
 * @date 2026/06/25
 * @brief ユニット定義
 * =====================================*/

using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    // 本作固有の要素を追加したユニット定義
    // 基底の UnitDefinition に対して、属性・追加ステータス・
    // パーティ AI 用の既定値（ロール・スコア補正・知能）・レベル成長曲線を持つ
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

        // AI 上の既定ロール。Inherit ならパーティ側の設定に従う
        [Header("パーティAI")]
        [Label("既定ロール")][SerializeField]protected PPUnitRole mDefaultRole = PPUnitRole.Inherit;
        [Label("既定の行動スコア補正")][SerializeField]protected PPUnitActionScoreModifier mDefaultActionScore = new();
        // 既定の知能（0〜1）。0 ならパーティプロファイルの値を継承する
        [PercentLabel("既定の知能", 0f, 1f, "継承")][SerializeField]protected float mDefaultIntelligence = 0.5f;

        [Header("成長曲線 (X = レベル, Y = 倍率)")]
        [Label("HP成長曲線")][SerializeField]protected AnimationCurve mHpGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label("攻撃力成長曲線")][SerializeField]protected AnimationCurve mAttackGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label("防御力成長曲線")][SerializeField]protected AnimationCurve mDefenseGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        [Label("素早さ成長曲線")][SerializeField]protected AnimationCurve mSpeedGrowth = AnimationCurve.Linear(1, 1, 50, 3);

        public PPStatBlock ExpandStatBlock => mExpandStatBlock;
        public PPUnitRole DefaultRole => mDefaultRole;
        public PPTypeAttribute TypeAttribute => mTypeAttribute;
        public PPUnitActionScoreModifier ActionScoreModifier => mDefaultActionScore;
        // 既定の知能（0〜1）。0 は継承を表す
        public float DefaultIntelligence => mDefaultIntelligence;

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
        // return : 生成された追加パラメータ一式
        protected virtual PPParameterSet CreatePPParameterSet()
            => new(mExpandStatBlock.AttackCost);
    }
}
