/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file UnitDefinition.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief ユニットのマスターデータ
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // ユニットのマスターデータ（ScriptableObject）。基礎ステータスと所持スキルの定義を持つ
    // CreateRuntimeUnit でランタイムの BattleUnit を生成し、スキルも同時にランタイム化する
    [CreateAssetMenu(menuName = "Project-Pudding/Definition/UnitDefinition", fileName = "NewUnit")]
    public class UnitDefinition : ScriptableObject
    {
        /* インスペクタ設定 */

        [Header("ユニット")]
        [Label("ユニットID")]
        [SerializeField] protected string mUnitId;
        [Label("表示名")]
        [SerializeField] protected string mDisplayName;

        [Header("詳細")]
        [Label("ステータス")]
        [SerializeField] protected StatBlock mBaseStatBlock;
        [Label("使用可能スキル", true)]
        [SerializeField] protected List<SkillDefinition> mSkills = new();

        /* プロパティ */

        public string UnitId => mUnitId;
        public string DisplayName => mDisplayName;
        public StatBlock BaseStatBlock => mBaseStatBlock;
        public List<SkillDefinition> Skills => mSkills;

        /* 生成 */

        // この定義からランタイムのユニットインスタンスを生成する
        // パラメータを組み立て、所持スキルもランタイム化して持たせる（null は読み飛ばす）
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

        // 基礎ステータスからランタイムのパラメータ一式を組み立てる
        // 追加パラメータを持たせる場合は派生側でオーバーライドする
        protected virtual ParameterSet CreateParameterSet() =>
            new(mBaseStatBlock.MaxHP, mBaseStatBlock.Attack, mBaseStatBlock.Defense, mBaseStatBlock.Speed);
    }
}
