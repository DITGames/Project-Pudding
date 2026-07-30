/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPPartyDefinition.cs
 * @author hqrse
 * @date 2026/07/23
 * @brief パーティ定義
 * =====================================*/
using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;

namespace PPCore
{
    /// <summary>
    /// 敵パーティの位置づけ。AI の忍耐係数（どれだけ粘ってリソースを溜めるか）の既定値を決める。
    /// </summary>
    public enum PPPartyEncounterType
    {
        /// <summary>雑魚。溜めずに手数で押す。</summary>
        Trash,
        /// <summary>強敵。標準的な粘り強さ。</summary>
        Elite,
        /// <summary>ボス。大技のためにじっくり溜める。</summary>
        Boss,
        /// <summary>個別指定。係数をインスペクタで直接設定する。</summary>
        Custom,
    }

    /// <summary>
    /// パーティメンバー 1 体分の設定。
    /// <para>
    /// ユニット定義の既定値をそのまま使うのが基本で、
    /// 同じユニットを別の性格で出したい場合のみ上書き項目を使う。
    /// ロールは Inherit 以外なら上書き、それ以外はフラグで上書きの有無を切り替える。
    /// </para>
    /// </summary>
    [Serializable]
    public sealed class PPPartyMemberEntry
    {
        /// <summary>元になるユニット定義。</summary>
        [Label("ユニット定義")] public PPUnitDefinition Unit;
        /// <summary>生成するレベル。成長曲線の評価に使う。</summary>
        [Label("レベル")] public int Level = 1;

        /// <summary>AI ロールの上書き。Inherit ならユニット定義の既定ロールを使う。</summary>
        [Header("上書き")]
        [Label("ロール上書き")] public PPUnitRole RoleOverride = PPUnitRole.Inherit;
        /// <summary>行動スコア補正を上書きするか。</summary>
        [Label("行動スコアを上書きする?")] public bool IsOverrideActionScore = false;
        /// <summary>行動スコア補正の上書き値。</summary>
        [Label("行動スコア上書き値")]public PPUnitActionScoreModifier ActionScoreOverride = new();
        /// <summary>知能を上書きするか。</summary>
        [Label("知能を上書きする?")] public bool IsOverrideIntelligence = false;
        /// <summary>知能の上書き値（0〜1）。</summary>
        [PercentLabel("知能上書き値")]public float IntelligenceOverride = 0.5f;
    }

    /// <summary>
    /// パーティ編成の定義（ScriptableObject）。
    /// <para>
    /// メンバー構成・リソース設定・AI の性格をまとめて持ち、
    /// <see cref="CreateRuntimeParty"/> でランタイムの <see cref="PPBattleParty"/> を生成する。
    /// 敵の遭遇パターンを 1 アセット = 1 戦闘の単位で用意できる。
    /// </para>
    /// </summary>
    [CreateAssetMenu(fileName = "PPPartyDefinition", menuName = "Project-Pudding/Battle/PPPartyDefinition")]
    public class PPPartyDefinition : ScriptableObject
    {
        /// <summary>編成するメンバー。</summary>
        [Header("パーティ")]
        [Label("パーティメンバー", true)] public List<PPPartyMemberEntry> Members = new();
        /// <summary>属性ごとのリソース上限。</summary>
        [Label("リソース上限")] public int MaxResource = 100;
        /// <summary>コイン 1 枚あたりのリソース変換係数の初期値。</summary>
        [Label("リソース変換レート初期値")] public float BaseResourceConversionRate = 1f;

        /// <summary>パーティの位置づけ。忍耐係数の既定値を決める。</summary>
        [Header("AI")]
        [Label("パーティ種別")] public PPPartyEncounterType EncounterType = PPPartyEncounterType.Trash;
        /// <summary>種別が Custom のときに使う忍耐係数。</summary>
        [Label("カスタム忍耐係数")]
        [EditCondition(nameof(IsCustomEncounterType), true, false)]
        public float CustomPatienceCoefficient = 1f;

        /// <summary>カスタム忍耐係数の入力欄を出すかどうか。</summary>
        protected bool IsCustomEncounterType
            => EncounterType == PPPartyEncounterType.Custom;

        /// <summary>
        /// パーティ種別ごとの既定の忍耐係数を返す。
        /// ボスほど値が大きく、大技を撃つためにリソースを溜める判断をしやすくなる。
        /// </summary>
        /// <param name="aType">パーティ種別。</param>
        private static float DefaultCoefficientFor(PPPartyEncounterType aType)
            => aType switch
            {
                PPPartyEncounterType.Trash => 0.5f,
                PPPartyEncounterType.Elite => 1f,
                PPPartyEncounterType.Boss => 1.8f,
                _ => 1.0f,
            };

        /// <summary>
        /// 実際に使う忍耐係数を解決する。Custom のときのみ手入力値を使う。
        /// </summary>
        /// <returns>忍耐係数。</returns>
        public float ResolvePatienceCoefficient()
            => EncounterType == PPPartyEncounterType.Custom ? CustomPatienceCoefficient : DefaultCoefficientFor(EncounterType);

        /// <summary>
        /// この定義からランタイムのパーティを生成する。
        /// メンバーごとにユニットを生成し、ロール・スコア補正・知能の上書きを適用してから編成する。
        /// </summary>
        /// <param name="aSide">このパーティの陣営。</param>
        /// <param name="aItems">初期所持アイテム。null なら空のインベントリになる。</param>
        /// <returns>生成されたランタイムパーティ。</returns>
        public PPBattleParty CreateRuntimeParty(BattleSide aSide,
            IReadOnlyDictionary<PPItemDefinition, int> aItems = null)
        {
            var units = new List<BattleUnit>();
            foreach (var entry in Members)
            {
                if(entry == null) continue;
                var unit = (PPBattleUnit)entry.Unit.CreateRuntimeUnit(entry.Level);
                unit.AssignedRole = ResolveRole(entry);
                unit.ScoreModifier = entry.IsOverrideActionScore ? entry.ActionScoreOverride : entry.Unit.ActionScoreModifier;
                unit.Intelligence = ResolveIntelligence(entry);
                units.Add(unit);
            }

            var party = new PPBattleParty(MaxResource, BaseResourceConversionRate, aSide, units, null, aItems)
            {
                PatienceCoefficient = ResolvePatienceCoefficient(),
            };
            return party;
        }

        /// <summary>
        /// 適用するロールを解決する。上書きが Inherit 以外ならそちらを優先する。
        /// </summary>
        /// <param name="aEntry">対象のメンバー設定。</param>
        private static PPUnitRole ResolveRole(PPPartyMemberEntry aEntry)
        {
            if(aEntry.RoleOverride != PPUnitRole.Inherit) return aEntry.RoleOverride;
            return aEntry.Unit.DefaultRole;
        }

        /// <summary>
        /// 適用する知能を解決する。上書き指定時のみ手入力値を 0～1 に丸めて使う。
        /// </summary>
        /// <param name="aEntry">対象のメンバー設定。</param>
        private static float ResolveIntelligence(PPPartyMemberEntry aEntry)
        {
            if(aEntry.IsOverrideIntelligence) return Mathf.Clamp01(aEntry.IntelligenceOverride);
            return aEntry.Unit.DefaultIntelligence;
        }
    }
}
