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
    // 敵パーティの位置づけ。AI の忍耐係数（どれだけ粘ってリソースを溜めるか）の既定値を決める
    public enum PPPartyEncounterType
    {
        // 雑魚。溜めずに手数で押す
        Trash,
        // 強敵。標準的な粘り強さ
        Elite,
        // ボス。大技のためにじっくり溜める
        Boss,
        // 個別指定。係数をインスペクタで直接設定する
        Custom,
    }

    // パーティメンバー 1 体分の設定
    // ユニット定義の既定値をそのまま使うのが基本で、
    // 同じユニットを別の性格で出したい場合のみ上書き項目を使う
    // ロールは Inherit 以外なら上書き、それ以外はフラグで上書きの有無を切り替える
    [Serializable]
    public sealed class PPPartyMemberEntry
    {
        [Label("ユニット定義")] public PPUnitDefinition Unit;
        // 生成するレベル。成長曲線の評価に使う
        [Label("レベル")] public int Level = 1;

        // AI ロールの上書き。Inherit ならユニット定義の既定ロールを使う
        [Header("上書き")]
        [Label("ロール上書き")] public PPUnitRole RoleOverride = PPUnitRole.Inherit;
        [Label("行動スコアを上書きする?")] public bool IsOverrideActionScore = false;
        [Label("行動スコア上書き値")]public PPUnitActionScoreModifier ActionScoreOverride = new();
        [Label("知能を上書きする?")] public bool IsOverrideIntelligence = false;
        // 知能の上書き値（0〜1）。0 を指定した場合はプロファイルの値を継承する扱いになる
        [PercentLabel("知能上書き値", 0f, 1f, "継承")]public float IntelligenceOverride = 0.5f;
    }

    // パーティ編成の定義（ScriptableObject）
    // メンバー構成・リソース設定・AI の性格をまとめて持ち、
    // CreateRuntimeParty でランタイムの PPBattleParty を生成する
    // 敵の遭遇パターンを 1 アセット = 1 戦闘の単位で用意できる
    [CreateAssetMenu(fileName = "PPPartyDefinition", menuName = "Project-Pudding/Battle/PPPartyDefinition")]
    public class PPPartyDefinition : ScriptableObject
    {
        [Header("パーティ")]
        [Label("パーティメンバー", true)] public List<PPPartyMemberEntry> Members = new();
        [Label("リソース上限")] public int MaxResource = 100;
        [Label("リソース変換レート初期値")] public float BaseResourceConversionRate = 1f;

        // パーティの位置づけ。忍耐係数の既定値を決める
        [Header("AI")]
        [Label("パーティ種別")] public PPPartyEncounterType EncounterType = PPPartyEncounterType.Trash;
        // 種別が Custom のときに使う忍耐係数
        [Label("カスタム忍耐係数")]
        [EditCondition(nameof(IsCustomEncounterType), true, false)]
        public float CustomPatienceCoefficient = 1f;

        // カスタム忍耐係数の入力欄を出すかどうか
        protected bool IsCustomEncounterType
            => EncounterType == PPPartyEncounterType.Custom;

        // パーティ種別ごとの既定の忍耐係数を返す
        // ボスほど値が大きく、大技を撃つためにリソースを溜める判断をしやすくなる
        // aType : パーティ種別
        private static float DefaultCoefficientFor(PPPartyEncounterType aType)
            => aType switch
            {
                PPPartyEncounterType.Trash => 0.5f,
                PPPartyEncounterType.Elite => 1f,
                PPPartyEncounterType.Boss => 1.8f,
                _ => 1.0f,
            };

        // 実際に使う忍耐係数を解決する。Custom のときのみ手入力値を使う
        // return : 忍耐係数
        public float ResolvePatienceCoefficient()
            => EncounterType == PPPartyEncounterType.Custom ? CustomPatienceCoefficient : DefaultCoefficientFor(EncounterType);

        // この定義からランタイムのパーティを生成する
        // メンバーごとにユニットを生成し、ロール・スコア補正・知能の上書きを適用してから編成する
        // aSide : このパーティの陣営
        // aItems : 初期所持アイテム。null なら空のインベントリになる
        // return : 生成されたランタイムパーティ
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

        // 適用するロールを解決する。上書きが Inherit 以外ならそちらを優先する
        // aEntry : 対象のメンバー設定
        private static PPUnitRole ResolveRole(PPPartyMemberEntry aEntry)
        {
            if(aEntry.RoleOverride != PPUnitRole.Inherit) return aEntry.RoleOverride;
            return aEntry.Unit.DefaultRole;
        }

        // 適用する知能を解決する。上書き指定時のみ手入力値を 0～1 に丸めて使う
        // ここで 0 になった場合は、AI 側でプロファイルの値へフォールバックする
        // aEntry : 対象のメンバー設定
        private static float ResolveIntelligence(PPPartyMemberEntry aEntry)
        {
            if(aEntry.IsOverrideIntelligence) return Mathf.Clamp01(aEntry.IntelligenceOverride);
            return aEntry.Unit.DefaultIntelligence;
        }
    }
}
