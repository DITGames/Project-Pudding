/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file UnitDefinition.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ユニットのマスターデータ
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;

namespace CommandBattleCore
{
    /// <summary>
    /// ユニットのマスターデータ（ScriptableObject）。
    /// <para>
    /// 基礎ステータスと所持スキルの定義を持ち、<see cref="CreateRuntimeUnit"/> で
    /// ランタイムの <see cref="BattleUnit"/> を生成する。
    /// スキルも同時にランタイム化されるため、同じ定義から作った 2 体は
    /// クールダウンなどの状態を共有しない。
    /// </para>
    /// </summary>
    [CreateAssetMenu(menuName = "CommandBattleCore/UnitDefinition", fileName = "NewUnit")]
    public class UnitDefinition : ScriptableObject
    {
        /// <summary>ユニットID。カタログでの解決キーになる。</summary>
        [Header("ユニット")]
        [Label("ユニットID")]
        [SerializeField] protected string mUnitId;
        /// <summary>UI 表示名。</summary>
        [Label("表示名")]
        [SerializeField] protected string mDisplayName;

        /// <summary>基礎ステータス。</summary>
        [Header("詳細")]
        [Label("ステータス")]
        [SerializeField] protected StatBlock mBaseStatBlock;
        /// <summary>このユニットが使用できるスキルの定義。</summary>
        [Label("使用可能スキル", true)]
        [SerializeField] protected List<SkillDefinition> mSkills = new();

        /// <summary>ユニットID。</summary>
        public string UnitId => mUnitId;
        /// <summary>UI 表示名。</summary>
        public string DisplayName => mDisplayName;
        /// <summary>基礎ステータス。</summary>
        public StatBlock BaseStatBlock => mBaseStatBlock;
        /// <summary>使用可能スキルの定義リスト。</summary>
        public List<SkillDefinition> Skills => mSkills;

        /// <summary>
        /// この定義からランタイムのユニットインスタンスを生成する。
        /// パラメータを組み立て、所持スキルもそれぞれランタイム化して持たせる。
        /// スキルリストに null が混ざっていても読み飛ばす。
        /// </summary>
        /// <param name="aDecider">コマンド決定クラス。null ならランダム AI が入る。</param>
        /// <returns>生成されたランタイムユニット。</returns>
        public virtual BattleUnit CreateRuntimeUnit(ICommandDecider aDecider = null)
        {
            var unit = new BattleUnit(mUnitId, mDisplayName, CreateParameterSet())
            {
                CommandDecider = aDecider ?? new RandomAICommandDecider(),
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
        /// 基礎ステータスからランタイムのパラメータ一式を組み立てる。
        /// 追加パラメータを持たせる場合は派生側でオーバーライドする。
        /// </summary>
        /// <returns>生成されたパラメータ一式。</returns>
        protected virtual ParameterSet CreateParameterSet() =>
            new(mBaseStatBlock.MaxHP, mBaseStatBlock.Attack, mBaseStatBlock.Defense, mBaseStatBlock.Speed);
    }
}
