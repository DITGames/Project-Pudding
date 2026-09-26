/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitCreationValidator.cs
 * @author hqrse
 * @date 2026/09/09
 * @brief 下書きの値・参照・保存先の共通検証
 * =====================================*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CommandBattleCore;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    public sealed class PPUnitCreationValidation
    {
        public List<string> Errors { get; } = new();
        public List<string> Warnings { get; } = new();
        public List<string> Paths { get; } = new();
        public bool IsValid => Errors.Count == 0;
    }

    public static class PPUnitCreationValidator
    {
        public static bool IsFinite(float aValue) => !float.IsNaN(aValue) && !float.IsInfinity(aValue);

        // ファイル名として使えないIDは置換せず、入力へ返す
        public static bool IsSafeId(string aId) => !string.IsNullOrWhiteSpace(aId)
            && aId == aId.Trim() && aId != "." && aId != ".." && !aId.EndsWith(".")
            && aId.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && !aId.Contains('/') && !aId.Contains('\\')
            && !new[] { "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9", "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9" }.Contains(aId.Split('.')[0].ToUpperInvariant());

        public static string OutputFolder(PPUnitCreationDraft aDraft) => aDraft.Folder + "/" + aDraft.Unit.UnitId;

        // 検証は警告とエラーを分け、UIとMCPで同じ生成予定を返す
        public static PPUnitCreationValidation Validate(PPUnitCreationDraft aDraft)
        {
            var result = new PPUnitCreationValidation();
            var unit = aDraft.Unit;
            if (aDraft.IsCreated) result.Errors.Add("この下書きは生成済みです。");
            if (!IsSafeId(unit.UnitId)) result.Errors.Add("ユニットIDは有効なファイル名として入力してください。");
            if (string.IsNullOrWhiteSpace(unit.DisplayName)) result.Errors.Add("表示名を入力してください。");
            var folderValid = (aDraft.Folder == "Assets" || aDraft.Folder.StartsWith("Assets/", StringComparison.Ordinal))
                && !aDraft.Folder.Split('/').Any(aPart => aPart == "." || aPart == ".." || string.IsNullOrWhiteSpace(aPart))
                && !aDraft.Folder.Contains('\\') && AssetDatabase.IsValidFolder(aDraft.Folder);
            if (!folderValid) result.Errors.Add("保存先にはAssets配下の既存フォルダーを指定してください。");
            if (folderValid && IsSafeId(unit.UnitId))
            {
                var folder = OutputFolder(aDraft);
                if (Directory.Exists(folder) || File.Exists(folder) || File.Exists(folder + ".meta")) result.Errors.Add("生成先が既に存在します: " + folder);
                result.Paths.Add(folder + "/" + unit.UnitId + ".asset");
                result.Paths.Add(folder + "/" + unit.UnitId + "_Visual.asset");
                if (aDraft.NewAI) result.Paths.Add(folder + "/" + unit.UnitId + "_AI.asset");
                for (var i = 0; i < aDraft.NewSkills.Count; i++)
                    result.Paths.Add(folder + "/Skill_" + i + ".asset");
            }
            if (LoadAll<UnitDefinition>().Any(aUnit => aUnit.UnitId == unit.UnitId)) result.Errors.Add("既存ユニットとIDが重複しています。");
            if (LoadAll<PPUnitVisualDefinition>().Any(aVisual => aVisual.UnitId == unit.UnitId)) result.Errors.Add("既存ビジュアルとIDが重複しています。");
            if (aDraft.MaxLevel < 1 || aDraft.MaxLevel > 100000 || aDraft.PreviewLevel < 1 || aDraft.PreviewLevel > aDraft.MaxLevel)
                result.Errors.Add("レベルの範囲が不正です（最大レベル1〜100000）。");
            for (var i = 0; i < PPUnitCreationDraft.StatNames.Length; i++) ValidateCurve(aDraft, i, result);
            var expand = unit.ExpandStatBlock;
            if (expand.ActionCount < 1) result.Errors.Add("行動回数は1以上です。");
            if (new[] { expand.AttackCost, expand.SkillGaugeMax, expand.CoinGaugeMax }.Any(aValue => !IsFinite(aValue) || aValue < 0))
                result.Errors.Add("コスト・ゲージ上限は有限の0以上の値が必要です。");
            if (expand.AttackCost > expand.CoinGaugeMax) result.Errors.Add("通常攻撃コストがコインゲージ上限を超えています。");
            ValidateReference(aDraft.Icon, "アイコン", result);
            if (aDraft.Icon == null) result.Warnings.Add("アイコンが未設定です。");
            if (aDraft.NewAI) result.Warnings.Add("空のAIプロファイルを作成します。生成後にUnit AI Treeで設定してください。");
            else
            {
                ValidateReference(unit.AIProfile, "AIプロファイル", result);
                if (unit.AIProfile == null || unit.AIProfile.Root == null) result.Warnings.Add("AIのルートが未設定です。");
            }
            ValidateReference(aDraft.UnitCatalog, "ユニットカタログ", result);
            ValidateReference(aDraft.VisualCatalog, "ビジュアルカタログ", result);
            ValidateReference(aDraft.SkillCatalog, "スキルカタログ", result);
            if (aDraft.UnitCatalog == null) result.Warnings.Add("ユニットはカタログへ登録されません。");
            if (aDraft.VisualCatalog == null) result.Warnings.Add("ビジュアルはカタログへ登録されません。IDから表示を解決するには登録が必要です。");
            var skillIds = new HashSet<string>();
            var existingSkills = LoadAll<SkillDefinition>().ToArray();
            foreach (var skill in unit.Skills)
            {
                if (skill == null) { result.Errors.Add("スキルに未設定・参照切れがあります。"); continue; }
                if (string.IsNullOrWhiteSpace(skill.SkillId) || !skillIds.Add(skill.SkillId)) result.Errors.Add("スキルIDが空欄または重複しています: " + skill.SkillId);
                if (string.IsNullOrWhiteSpace(skill.DisplayName)) result.Errors.Add("スキルの表示名が空欄です。");
                if (skill is PPSkillDefinition ownedSkill && aDraft.NewSkills.Contains(ownedSkill))
                {
                    if (existingSkills.Any(aSkill => aSkill.SkillId == skill.SkillId)) result.Errors.Add("新規スキルIDが既存と重複しています: " + skill.SkillId);
                }
                else ValidateReference(skill, "スキル", result);
                var so = new SerializedObject(skill);
                ValidateMissingReferences(so, result);
                if (so.FindProperty("mMaxCooldown").intValue < 0 || so.FindProperty("mMaxUsesPerBattle").intValue < 0)
                    result.Errors.Add("スキルのクールタイム・最大使用回数は0以上です: " + skill.DisplayName);
                if (skill is PPSkillDefinition ppSkill)
                {
                    var effects = so.FindProperty("mSkillEffects");
                    if (effects.arraySize == 0) result.Warnings.Add("効果未設定のスキル: " + skill.DisplayName);
                    for (var index = 0; index < effects.arraySize; index++)
                        if (effects.GetArrayElementAtIndex(index).managedReferenceValue == null) result.Errors.Add("スキル効果に参照切れがあります: " + skill.DisplayName);
                    var cost = so.FindProperty("mSkillGaugeCost").floatValue;
                    if (!IsFinite(cost) || cost < 0 || cost > expand.SkillGaugeMax) result.Errors.Add("スキルコストが不正、またはゲージ上限を超えています: " + skill.DisplayName);
                }
            }
            if (aDraft.NewSkills.Any(aSkill => aSkill == null || !unit.Skills.Contains(aSkill))) result.Errors.Add("新規スキルの下書き一覧と割り当てが一致しません。");
            ValidateMissingReferences(new SerializedObject(aDraft), result);
            ValidateMissingReferences(new SerializedObject(unit), result);
            return result;
        }

        private static void ValidateCurve(PPUnitCreationDraft aDraft, int aIndex, PPUnitCreationValidation aResult)
        {
            var label = PPUnitCreationDraft.StatNames[aIndex];
            var initial = aDraft.GetInitial(aIndex);
            var curve = aDraft.GetCurve(aIndex);
            if (!IsFinite(initial) || initial < 0) aResult.Errors.Add(label + ": 初期値は有限の0以上の値が必要です。");
            if (aIndex == 0 && initial == 0) aResult.Warnings.Add("HPが0のユニットは戦闘開始時点で戦闘不能になります。");
            // チャートに載らない能力値（きようさ）は基準値を持たない
            if (aIndex < PPUnitCreationDraft.ChartAxisCount && (!IsFinite(aDraft.Scales[aIndex]) || aDraft.Scales[aIndex] <= 0)) aResult.Errors.Add(label + ": チャート基準値は正の有限値が必要です。");
            if (curve == null || curve.length < (aDraft.MaxLevel == 1 ? 1 : 2)) { aResult.Errors.Add(label + ": 曲線のキーが不足しています。"); return; }
            var keys = curve.keys;
            if (keys[0].time != 1 || keys[0].value != 1 || keys[keys.Length - 1].time != aDraft.MaxLevel)
                aResult.Errors.Add(label + ": 曲線の始点・終点が不正です。");
            if (keys.Any(aKey => !IsFinite(aKey.time) || !IsFinite(aKey.value) || aKey.value < 1 || float.IsNaN(aKey.inTangent) || float.IsNaN(aKey.outTangent) || !IsFinite(aKey.inWeight) || !IsFinite(aKey.outWeight) || aKey.time < 1 || aKey.time > aDraft.MaxLevel))
                aResult.Errors.Add(label + ": キーの範囲・倍率・接線が不正です。");
            var end = initial * Mathf.Max(1, curve.Evaluate(aDraft.MaxLevel));
            var previous = initial;
            var decreases = false;
            var overshoots = false;
            for (var level = 1; level <= Mathf.Clamp(aDraft.MaxLevel, 1, 100000); level++)
            {
                var value = initial * Mathf.Max(1, curve.Evaluate(level));
                if (!IsFinite(value)) { aResult.Errors.Add(label + ": 評価結果が有限値ではありません。"); break; }
                decreases |= value < previous - 0.0001f;
                overshoots |= value > end + 0.0001f;
                previous = value;
            }
            if (decreases) aResult.Warnings.Add(label + ": 途中レベルで値が減少します。");
            if (overshoots) aResult.Warnings.Add(label + ": 途中レベルで最大レベル時の値を超えます。");
        }

        private static void ValidateReference(UnityEngine.Object aObject, string aLabel, PPUnitCreationValidation aResult)
        {
            if (aObject != null && (!EditorUtility.IsPersistent(aObject) || string.IsNullOrEmpty(AssetDatabase.GetAssetPath(aObject))))
                aResult.Errors.Add(aLabel + ": 保存済みのアセットを選択してください。");
        }

        private static void ValidateMissingReferences(SerializedObject aObject, PPUnitCreationValidation aResult)
        {
            var property = aObject.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                    aResult.Errors.Add("参照切れ: " + property.propertyPath);
                if (property.propertyType == SerializedPropertyType.Float && !IsFinite(property.floatValue))
                    aResult.Errors.Add("有限値ではありません: " + property.propertyPath);
            }
        }

        // サブアセットの定義もID重複の確認対象に含める
        private static IEnumerable<T> LoadAll<T>() where T : UnityEngine.Object => AssetDatabase.FindAssets("t:" + typeof(T).Name)
            .Select(AssetDatabase.GUIDToAssetPath).Distinct().SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<T>();
    }
}
