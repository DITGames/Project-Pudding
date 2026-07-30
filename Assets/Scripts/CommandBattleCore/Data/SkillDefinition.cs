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
    // スキルのマスターデータ（ScriptableObject）
    // インスペクタで設定した内容から、CreateRuntimeSkill でランタイムの BattleSkill を生成する
    // 定義（不変・アセット）とインスタンス（可変・戦闘中の状態を持つ）を分ける構成
    // 効果の中身は BuildEffect を派生側でオーバーライドして実装する
    // この基底クラス自体は効果を持たない
    public class SkillDefinition : ScriptableObject
    {
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
        // ダメージ量・回復量の基礎値になる
        [Label("スキルパワー")]
        [SerializeField] protected float mPower = 10f;
        [Label("クールタイム")]
        [SerializeField] protected int mMaxCooldown = 0;
        [Label("最大使用回数")]
        [SerializeField] protected int mMaxUsesPerBattle = 0;

        public string SkillId => mSkillId;
        public string DisplayName => mDisplayName;
        public string Description => mDescription;
        public TargetScope TargetScope => mTargetScope;

        // この定義からランタイムのスキルインスタンスを生成する
        // 生成物には自身への参照（SourceDefinition）を必ず設定する
        // 定義型から属性やコストを引く実装があるため、この設定を省略しないこと
        // return : クールダウン・使用回数を初期化済みのランタイムスキル
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
        // return : 効果本体のデリゲート（行動ユニット, 対象リスト, コンテキスト）
        protected virtual Action<BattleUnit, List<BattleUnit>, BattleContext> BuildEffect()
        {
            return null;
        }
    }
}
