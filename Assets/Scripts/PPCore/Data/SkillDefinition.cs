/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file SkillDefinition.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief スキルのマスターデータ
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;

namespace PPCore
{
    // スキルのマスターデータ（ScriptableObject）。CreateRuntimeSkill でランタイムの BattleSkill を生成する
    // 効果の中身は BuildEffect を派生側でオーバーライドして実装する
    public class SkillDefinition : ScriptableObject
    {
        /* インスペクタ設定 */

        [Header("スキル")]
        [Label("スキルID")]
        [SerializeField] protected string mSkillId;
        [Label("表示名")]
        [SerializeField] protected string mDisplayName;
        [TextArea]
        [Label("説明")]
        [SerializeField] protected string mDescription;

        // 実行時にリゾルバへ変換される
        [Header("詳細")]
        [Label("ターゲット選択")]
        [SerializeField] protected TargetScope mTargetScope = TargetScope.SingleEnemy;

        [Label("クールタイム")]
        [SerializeField] protected int mMaxCooldown = 0;
        [Label("最大使用回数")]
        [SerializeField] protected int mMaxUsesPerBattle = 0;

        /* プロパティ */

        public string SkillId => mSkillId;
        public string DisplayName => mDisplayName;
        public string Description => mDescription;
        public TargetScope TargetScope => mTargetScope;

        /* 生成 */

        // この定義からランタイムのスキルインスタンスを生成する
        // 生成物には自身への参照（SourceDefinition）を必ず設定する
        public virtual BattleSkill CreateRuntimeSkill()
        {
            var skill = new BattleSkill(mSkillId, mDisplayName, mTargetScope.CreateResolver(), BuildEffect());
            skill.SourceDefinition = this;
            skill.MaxCooldown = mMaxCooldown;
            skill.MaxUsesPerBattle = mMaxUsesPerBattle;
            skill.ResetForBattle();
            return skill;
        }

        // スキル実行時の効果を生成する。派生側でオーバーライドして実装する
        // 基底実装は null を返すため、この型のまま使うと何も起こらないスキルになる
        protected virtual Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return null;
        }
    }
}
