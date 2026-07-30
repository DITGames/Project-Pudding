/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file SkillDefinition.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief スキルのマスターデータ
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;

namespace CommandBattleCore
{
    /// <summary>
    /// スキルのマスターデータ（ScriptableObject）。
    /// <para>
    /// インスペクタで設定した内容から、<see cref="CreateRuntimeSkill"/> で
    /// ランタイムの <see cref="BattleSkill"/> を生成する。
    /// 定義（不変・アセット）とインスタンス（可変・戦闘中の状態を持つ）を分ける構成。
    /// </para>
    /// <para>
    /// 効果の中身は <see cref="BuildEffect"/> を派生側でオーバーライドして実装する。
    /// この基底クラス自体は効果を持たない。
    /// </para>
    /// </summary>
    public class SkillDefinition : ScriptableObject
    {
        /// <summary>スキルID。カタログでの解決キーになる。</summary>
        [Header("スキル")]
        [Label("スキルID")]
        [SerializeField] protected string mSkillId;
        /// <summary>UI 表示名。</summary>
        [Label("表示名")]
        [SerializeField] protected string mDisplayName;
        /// <summary>UI に出す説明文。</summary>
        [TextArea]
        [Label("説明")]
        [SerializeField] protected string mDescription;

        /// <summary>既定のターゲット範囲。実行時にリゾルバへ変換される。</summary>
        [Header("詳細")]
        [Label("ターゲット選択")]
        [SerializeField] protected TargetScope mTargetScope = TargetScope.SingleEnemy;
        /// <summary>スキルの威力。ダメージ量・回復量の基礎値になる。</summary>
        [Label("スキルパワー")]
        [SerializeField] protected float mPower = 10f;
        /// <summary>クールタイムのターン数。0 ならクールダウンなし。</summary>
        [Label("クールタイム")]
        [SerializeField] protected int mMaxCooldown = 0;
        /// <summary>1 戦闘あたりの最大使用回数。0 なら無制限。</summary>
        [Label("最大使用回数")]
        [SerializeField] protected int mMaxUsesPerBattle = 0;

        /// <summary>スキルID。</summary>
        public string SkillId => mSkillId;
        /// <summary>UI 表示名。</summary>
        public string DisplayName => mDisplayName;
        /// <summary>説明文。</summary>
        public string Description => mDescription;
        /// <summary>既定のターゲット範囲。</summary>
        public TargetScope TargetScope => mTargetScope;

        /// <summary>
        /// この定義からランタイムのスキルインスタンスを生成する。
        /// 生成物には自身への参照（SourceDefinition）を必ず設定する。
        /// 定義型から属性やコストを引く実装があるため、この設定を省略しないこと。
        /// </summary>
        /// <returns>クールダウン・使用回数を初期化済みのランタイムスキル。</returns>
        public virtual BattleSkill CreateRuntimeSkill()
        {
            var skill = new BattleSkill(mSkillId, mDisplayName, mTargetScope.CreateResolver(), BuildEffect());
            skill.SourceDefinition = this;
            skill.MaxCooldown = mMaxCooldown;
            skill.MaxUsesPerBattle = mMaxUsesPerBattle;
            skill.ResetForBattle();
            return skill;
        }

        /// <summary>
        /// スキル実行時の効果を生成する。派生側でオーバーライドして実装する。
        /// 基底実装は null を返すため、この型のまま使うと何も起こらないスキルになる。
        /// </summary>
        /// <returns>効果本体のデリゲート（行動ユニット, 対象リスト, コンテキスト）。</returns>
        protected virtual Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return null;
        }
    }
}
