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
    // ユニットのマスターデータ（ScriptableObject）
    // 基礎ステータスと所持スキルの定義を持ち、CreateRuntimeUnit でランタイムの BattleUnit を生成する
    // スキルも同時にランタイム化されるため、同じ定義から作った 2 体はクールダウンなどの状態を共有しない
    [CreateAssetMenu(menuName = "CommandBattleCore/UnitDefinition", fileName = "NewUnit")]
    public class UnitDefinition : ScriptableObject
    {
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

        public string UnitId => mUnitId;
        public string DisplayName => mDisplayName;
        public StatBlock BaseStatBlock => mBaseStatBlock;
        public List<SkillDefinition> Skills => mSkills;

        // この定義からランタイムのユニットインスタンスを生成する
        // パラメータを組み立て、所持スキルもそれぞれランタイム化して持たせる
        // スキルリストに null が混ざっていても読み飛ばす
        // aDecider : コマンド決定クラス。null ならランダム AI が入る
        // return : 生成されたランタイムユニット
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
        // return : 生成されたパラメータ一式
        protected virtual ParameterSet CreateParameterSet() =>
            new(mBaseStatBlock.MaxHP, mBaseStatBlock.Attack, mBaseStatBlock.Defense, mBaseStatBlock.Speed);
    }
}
