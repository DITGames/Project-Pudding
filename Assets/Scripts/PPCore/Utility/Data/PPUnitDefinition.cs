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
    /// <summary>
    /// 本作固有の要素を追加したユニット定義。
    /// <para>
    /// 基底の <see cref="UnitDefinition"/> に対して、属性・追加ステータス・
    /// パーティ AI 用の既定値（ロール・スコア補正・知能）・レベル成長曲線を持つ。
    /// </para>
    /// <para>
    /// 成長は「レベルごとの実数値テーブル」ではなく <see cref="AnimationCurve"/> の倍率で表現する。
    /// 基礎ステータスに倍率を掛けるだけで済み、成長カーブをインスペクタ上で視覚的に調整できる。
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "PPBattleUnitDefinition", menuName = "Project-Pudding/Definition/PPUnitDefinition")]
    public class PPUnitDefinition : UnitDefinition
    {
        /// <summary>本作固有の追加ステータス（通常攻撃コストなど）。</summary>
        [Header("ユニット拡張")]
        [Label("ステータス")] // スキル前提なら消す
        [SerializeField]protected PPStatBlock mExpandStatBlock;
        /// <summary>ユニットの属性。弱点・耐性の判定に使う。</summary>
        [Label("属性")]
        [SerializeField]protected PPTypeAttribute mTypeAttribute = PPTypeAttribute.Normal;

        /// <summary>AI 上の既定ロール。Inherit ならパーティ側の設定に従う。</summary>
        [Header("パーティAI")]
        [Label("既定ロール")][SerializeField]protected PPUnitRole mDefaultRole = PPUnitRole.Inherit;
        /// <summary>AI のスコアリングに掛かる既定の個体差補正。</summary>
        [Label("既定の行動スコア補正")][SerializeField]protected PPUnitActionScoreModifier mDefaultActionScore = new();
        /// <summary>既定の知能。-1 ならパーティプロファイルの値を継承する。</summary>
        [Label("既定の知能")][SerializeField][Range(-1,100)]protected float mDefaultIntelligence = 50f;

        /// <summary>最大HPの成長曲線（X = レベル, Y = 基礎値への倍率）。</summary>
        [Header("成長曲線 (X = レベル, Y = 倍率)")]
        [Label("HP成長曲線")][SerializeField]protected AnimationCurve mHpGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        /// <summary>攻撃力の成長曲線。</summary>
        [Label("攻撃力成長曲線")][SerializeField]protected AnimationCurve mAttackGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        /// <summary>防御力の成長曲線。</summary>
        [Label("防御力成長曲線")][SerializeField]protected AnimationCurve mDefenseGrowth = AnimationCurve.Linear(1, 1, 50, 3);
        /// <summary>素早さの成長曲線。</summary>
        [Label("素早さ成長曲線")][SerializeField]protected AnimationCurve mSpeedGrowth = AnimationCurve.Linear(1, 1, 50, 3);

        /// <summary>追加ステータス。</summary>
        public PPStatBlock ExpandStatBlock => mExpandStatBlock;
        /// <summary>既定の AI ロール。</summary>
        public PPUnitRole DefaultRole => mDefaultRole;
        /// <summary>ユニットの属性。</summary>
        public PPTypeAttribute TypeAttribute => mTypeAttribute;
        /// <summary>既定の行動スコア補正。</summary>
        public PPUnitActionScoreModifier ActionScoreModifier => mDefaultActionScore;
        /// <summary>既定の知能。</summary>
        public float DefaultIntelligence => mDefaultIntelligence;

        /// <summary>
        /// レベル 1 でランタイムユニットを生成する。基底のシグネチャに合わせた入口。
        /// </summary>
        /// <param name="aDecider">コマンド決定クラス。null なら本作用のランダム AI が入る。</param>
        /// <returns>生成されたランタイムユニット。</returns>
        public override BattleUnit CreateRuntimeUnit(ICommandDecider aDecider = null)
            => CreateRuntimeUnit(1, aDecider);

        /// <summary>
        /// レベルを指定してランタイムユニットを生成する。
        /// 成長後のパラメータと追加パラメータを組み立て、所持スキルもランタイム化して持たせる。
        /// </summary>
        /// <param name="aLevel">生成するレベル。成長曲線の評価に使う。</param>
        /// <param name="aDecider">コマンド決定クラス。null なら本作用のランダム AI が入る。</param>
        /// <returns>生成されたランタイムユニット。</returns>
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

        /// <summary>
        /// 基礎ステータスへ成長倍率を掛けてパラメータ一式を組み立てる。
        /// 曲線の設定ミスでレベルアップにより弱くなるのを避けるため、倍率は最低 1 に丸める。
        /// </summary>
        /// <param name="aLevel">評価するレベル。</param>
        /// <returns>成長を反映したパラメータ一式。</returns>
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

        /// <summary>
        /// 追加パラメータ一式を組み立てる。こちらはレベル成長の対象外。
        /// </summary>
        /// <returns>生成された追加パラメータ一式。</returns>
        protected virtual PPParameterSet CreatePPParameterSet()
            => new(mExpandStatBlock.AttackCost);
    }
}
