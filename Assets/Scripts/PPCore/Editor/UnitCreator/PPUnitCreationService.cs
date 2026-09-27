/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitCreationService.cs
 * @author hqrse
 * @date 2026/09/09
 * @brief 関連定義の生成・登録と失敗時の復元
 * =====================================*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PPCore
{
    public static class PPUnitCreationService
    {
        // 同一要求の再送は結果を返し、別要求や古い入力からは生成しない
        public static string[] Create(PPUnitCreationDraft aDraft, int aRevision, string aRequestId)
        {
            if (string.IsNullOrWhiteSpace(aRequestId)) throw new ArgumentException("生成要求IDが必要です。");
            // 編集モードの下書きは元アセットへの書き戻しになる。新規作成の生成・復元処理は通らない
            if (aDraft.IsEditing) return Save(aDraft, aRevision, aRequestId);
            if (aDraft.IsCreated && aDraft.RequestId == aRequestId && aDraft.CreatedRevision == aRevision)
                return aDraft.CreatedPaths;
            aDraft.RequireRevision(aRevision);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Playを停止してから生成してください。");
            var validation = PPUnitCreationValidator.Validate(aDraft);
            if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Errors));
            var catalogBackups = new Dictionary<ScriptableObject, string>();
            foreach (var catalog in new ScriptableObject[] { aDraft.UnitCatalog, aDraft.VisualCatalog, aDraft.SkillCatalog }.Where(aCatalog => aCatalog != null))
            {
                if (!AssetDatabase.IsOpenForEdit(catalog)) throw new InvalidOperationException("カタログが書き込み可能ではありません: " + catalog.name);
                catalogBackups.Add(catalog, EditorJsonUtility.ToJson(catalog));
            }
            var created = new Dictionary<string, string>();
            var transient = new List<UnityEngine.Object>();
            var folder = PPUnitCreationValidator.OutputFolder(aDraft);
            string folderGuid = null;
            try
            {
                folderGuid = AssetDatabase.CreateFolder(aDraft.Folder, aDraft.Unit.UnitId);
                if (string.IsNullOrEmpty(folderGuid) || AssetDatabase.GUIDToAssetPath(folderGuid) != folder)
                    throw new IOException("生成フォルダーを作成できませんでした。");
                var unit = UnityEngine.Object.Instantiate(aDraft.Unit);
                transient.Add(unit);
                unit.name = aDraft.Unit.UnitId;
                var visual = ScriptableObject.CreateInstance<PPUnitVisualDefinition>();
                transient.Add(visual);
                visual.UnitId = unit.UnitId;
                visual.UnitIcon = aDraft.Icon;
                visual.UnitPortrait = aDraft.Portrait;
                var paths = validation.Paths;
                SaveNew(unit, paths[0], created);
                SaveNew(visual, paths[1], created);
                var settings = ScriptableObject.CreateInstance<PPUnitCreationSettings>();
                transient.Add(settings);
                settings.name = "作成設定";
                settings.Initialize(aDraft.MaxLevel, aDraft.Scales);
                AssetDatabase.AddObjectToAsset(settings, unit);
                var index = 2;
                if (aDraft.NewAI)
                {
                    var ai = ScriptableObject.CreateInstance<PPUnitAIProfileDefinition>();
                    transient.Add(ai);
                    SaveNew(ai, paths[index++], created);
                    var so = new SerializedObject(unit);
                    so.FindProperty("mAIProfile").objectReferenceValue = ai;
                    so.ApplyModifiedPropertiesWithoutUndo();
                }
                foreach (var draftSkill in aDraft.NewSkills)
                {
                    var skill = UnityEngine.Object.Instantiate(draftSkill);
                    transient.Add(skill);
                    SaveNew(skill, paths[index++], created);
                    for (var i = 0; i < unit.Skills.Count; i++)
                        if (unit.Skills[i] == draftSkill) unit.Skills[i] = skill;
                }
                AddToCatalog(aDraft.UnitCatalog, unit);
                AddToCatalog(aDraft.VisualCatalog, visual);
                foreach (var skill in unit.Skills.OfType<PPSkillDefinition>()) AddToCatalog(aDraft.SkillCatalog, skill);
                foreach (var obj in transient) EditorUtility.SetDirty(obj);
                foreach (var path in paths) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(path));
                foreach (var catalog in catalogBackups.Keys) AssetDatabase.SaveAssetIfDirty(catalog);
                Invalidate(aDraft);
                aDraft.RecordCreation(aRequestId, paths.ToArray());
                return aDraft.CreatedPaths;
            }
            catch (Exception exception)
            {
                var recoveryErrors = new List<string>();
                foreach (var entry in catalogBackups)
                {
                    try
                    {
                        EditorJsonUtility.FromJsonOverwrite(entry.Value, entry.Key);
                        EditorUtility.SetDirty(entry.Key);
                        AssetDatabase.SaveAssetIfDirty(entry.Key);
                    }
                    catch (Exception restoreError) { recoveryErrors.Add("カタログ復元失敗: " + AssetDatabase.GetAssetPath(entry.Key) + " / " + restoreError.Message); }
                }
                Invalidate(aDraft);
                // GUIDも照合し、この処理が所有しているファイルだけを取り除く
                foreach (var entry in created.Reverse())
                {
                    try
                    {
                        if (AssetDatabase.AssetPathToGUID(entry.Key) != entry.Value || !AssetDatabase.DeleteAsset(entry.Key))
                            recoveryErrors.Add("残存アセット: " + entry.Key);
                    }
                    catch (Exception deleteError) { recoveryErrors.Add("残存アセット: " + entry.Key + " / " + deleteError.Message); }
                }
                if (!string.IsNullOrEmpty(folderGuid))
                {
                    var ownedFolder = AssetDatabase.GUIDToAssetPath(folderGuid);
                    if (!string.IsNullOrEmpty(ownedFolder) && Directory.Exists(ownedFolder) && !Directory.EnumerateFileSystemEntries(ownedFolder).Any())
                        if (!AssetDatabase.DeleteAsset(ownedFolder)) recoveryErrors.Add("空フォルダー残存: " + ownedFolder);
                }
                throw new InvalidOperationException(exception.Message + (recoveryErrors.Count > 0 ? "\n手動確認が必要です:\n" + string.Join("\n", recoveryErrors) : "\n今回の生成物とカタログ変更を復元しました。"), exception);
            }
            finally
            {
                foreach (var obj in transient)
                    if (obj != null && !EditorUtility.IsPersistent(obj)) UnityEngine.Object.DestroyImmediate(obj);
            }
        }

        // 編集モードの保存。元ユニット・既存ビジュアルはオブジェクトを差し替えず値だけを書き写すため、GUID・localFileId・位置は変わらない
        // 新規に作るのは AI・スキル・（既存がなければ）ビジュアルだけで、失敗時に削除するのもそれらに限る。書き換えたアセットは事前の内容へ戻す
        // 同じ保存要求IDの再送は記録済みの結果を返し、新しい要求IDなら何度でも保存できる
        private static string[] Save(PPUnitCreationDraft aDraft, int aRevision, string aRequestId)
        {
            if (aDraft.SavedRequestId == aRequestId)
            {
                if (aDraft.SavedRevision == aRevision) return aDraft.SavedPaths;
                throw new InvalidOperationException("この保存要求IDは別の更新番号で使用済みです。新しい要求IDを指定してください。");
            }
            aDraft.RequireRevision(aRevision);
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Playを停止してから保存してください。");
            var validation = PPUnitCreationValidator.Validate(aDraft);
            if (!validation.IsValid) throw new InvalidOperationException(string.Join("\n", validation.Errors));
            var plan = PPUnitCreationValidator.EditPlan(aDraft);
            var source = aDraft.Source;
            var visual = aDraft.SourceVisual;
            var settings = AssetDatabase.LoadAllAssetsAtPath(plan.UnitPath).OfType<PPUnitCreationSettings>().FirstOrDefault();
            // 書き換える既存アセットは、失敗時に戻すため内容を控える
            var backups = new Dictionary<UnityEngine.Object, string>();
            foreach (var target in new UnityEngine.Object[] { source, visual, settings, aDraft.UnitCatalog, aDraft.VisualCatalog, aDraft.SkillCatalog }.Where(aTarget => aTarget != null))
            {
                if (!AssetDatabase.IsOpenForEdit(target)) throw new InvalidOperationException("アセットが書き込み可能ではありません: " + AssetDatabase.GetAssetPath(target));
                backups[target] = EditorJsonUtility.ToJson(target);
            }
            var created = new Dictionary<string, string>();
            var transient = new List<UnityEngine.Object>();
            var savedSkills = new Dictionary<PPSkillDefinition, PPSkillDefinition>();
            PPUnitAIProfileDefinition ai = null;
            PPUnitCreationSettings addedSettings = null;
            try
            {
                var edited = UnityEngine.Object.Instantiate(aDraft.Unit);
                transient.Add(edited);
                if (plan.AIPath != null)
                {
                    ai = ScriptableObject.CreateInstance<PPUnitAIProfileDefinition>();
                    transient.Add(ai);
                    SaveNew(ai, plan.AIPath, created);
                    var aiSo = new SerializedObject(edited);
                    aiSo.FindProperty("mAIProfile").objectReferenceValue = ai;
                    aiSo.ApplyModifiedPropertiesWithoutUndo();
                }
                for (var i = 0; i < aDraft.NewSkills.Count; i++)
                {
                    var draftSkill = aDraft.NewSkills[i];
                    var skill = UnityEngine.Object.Instantiate(draftSkill);
                    transient.Add(skill);
                    SaveNew(skill, plan.SkillPaths[i], created);
                    savedSkills[draftSkill] = skill;
                    for (var index = 0; index < edited.Skills.Count; index++)
                        if (edited.Skills[index] == draftSkill) edited.Skills[index] = skill;
                }
                if (plan.NewVisual)
                {
                    visual = ScriptableObject.CreateInstance<PPUnitVisualDefinition>();
                    transient.Add(visual);
                    visual.UnitId = source.UnitId;
                    SaveNew(visual, plan.VisualPath, created);
                }
                if (visual != null)
                {
                    var visualSo = new SerializedObject(visual);
                    visualSo.FindProperty(nameof(PPUnitVisualDefinition.UnitIcon)).objectReferenceValue = aDraft.Icon;
                    visualSo.FindProperty(nameof(PPUnitVisualDefinition.UnitPortrait)).objectReferenceValue = aDraft.Portrait;
                    visualSo.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(visual);
                }
                CopyInto(edited, source);
                if (settings == null)
                {
                    addedSettings = ScriptableObject.CreateInstance<PPUnitCreationSettings>();
                    transient.Add(addedSettings);
                    addedSettings.name = "作成設定";
                    addedSettings.Initialize(aDraft.MaxLevel, aDraft.Scales);
                    AssetDatabase.AddObjectToAsset(addedSettings, source);
                }
                else
                {
                    settings.Initialize(aDraft.MaxLevel, aDraft.Scales);
                    EditorUtility.SetDirty(settings);
                }
                AddToCatalog(aDraft.UnitCatalog, source);
                if (visual != null) AddToCatalog(aDraft.VisualCatalog, visual);
                foreach (var skill in source.Skills.OfType<PPSkillDefinition>()) AddToCatalog(aDraft.SkillCatalog, skill);
                EditorUtility.SetDirty(source);
                foreach (var obj in transient.Where(EditorUtility.IsPersistent)) EditorUtility.SetDirty(obj);
                AssetDatabase.SaveAssetIfDirty(source);
                if (visual != null) AssetDatabase.SaveAssetIfDirty(visual);
                foreach (var path in created.Keys) AssetDatabase.SaveAssetIfDirty(AssetDatabase.LoadMainAssetAtPath(path));
                foreach (var catalog in new ScriptableObject[] { aDraft.UnitCatalog, aDraft.VisualCatalog, aDraft.SkillCatalog }.Where(aCatalog => aCatalog != null))
                    AssetDatabase.SaveAssetIfDirty(catalog);
                Invalidate(aDraft);
            }
            catch (Exception exception)
            {
                var recoveryErrors = new List<string>();
                foreach (var entry in backups)
                {
                    try
                    {
                        EditorJsonUtility.FromJsonOverwrite(entry.Value, entry.Key);
                        EditorUtility.SetDirty(entry.Key);
                        AssetDatabase.SaveAssetIfDirty(entry.Key);
                    }
                    catch (Exception restoreError) { recoveryErrors.Add("復元失敗: " + AssetDatabase.GetAssetPath(entry.Key) + " / " + restoreError.Message); }
                }
                if (addedSettings != null && EditorUtility.IsPersistent(addedSettings))
                {
                    try
                    {
                        AssetDatabase.RemoveObjectFromAsset(addedSettings);
                        AssetDatabase.SaveAssetIfDirty(source);
                    }
                    catch (Exception removeError) { recoveryErrors.Add("作成設定の除去失敗: " + plan.UnitPath + " / " + removeError.Message); }
                }
                Invalidate(aDraft);
                // 今回新しく作ったファイルだけを GUID を照合して取り除く（元ユニット・既存ビジュアルは対象にならない）
                foreach (var entry in created.Reverse())
                {
                    try
                    {
                        if (AssetDatabase.AssetPathToGUID(entry.Key) != entry.Value || !AssetDatabase.DeleteAsset(entry.Key))
                            recoveryErrors.Add("残存アセット: " + entry.Key);
                    }
                    catch (Exception deleteError) { recoveryErrors.Add("残存アセット: " + entry.Key + " / " + deleteError.Message); }
                }
                throw new InvalidOperationException(exception.Message + (recoveryErrors.Count > 0 ? "\n手動確認が必要です:\n" + string.Join("\n", recoveryErrors) : "\n今回の変更を元に戻しました。"), exception);
            }
            finally
            {
                foreach (var obj in transient)
                    if (obj != null && !EditorUtility.IsPersistent(obj)) UnityEngine.Object.DestroyImmediate(obj);
            }
            // 書き戻しの成功後に下書きをそろえる。ここで失敗しても書き戻し済みのアセットは消さない
            var paths = plan.AllPaths.ToArray();
            aDraft.RecordSave(aRequestId, aRevision, paths, visual, ai, savedSkills);
            return paths;
        }

        // 値だけを書き写し、オブジェクト自体は差し替えない。m_Script と非表示の項目（名前など）は元のまま残す
        private static void CopyInto(UnityEngine.Object aFrom, UnityEngine.Object aTo)
        {
            var from = new SerializedObject(aFrom);
            var to = new SerializedObject(aTo);
            var property = from.GetIterator();
            for (var enter = true; property.NextVisible(enter); enter = false)
                if (property.name != "m_Script") to.CopyFromSerializedProperty(property);
            to.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SaveNew(ScriptableObject aObject, string aPath, Dictionary<string, string> aCreated)
        {
            if (File.Exists(aPath) || File.Exists(aPath + ".meta")) throw new IOException("生成先が衝突しました: " + aPath);
            AssetDatabase.CreateAsset(aObject, aPath);
            var guid = AssetDatabase.AssetPathToGUID(aPath);
            if (string.IsNullOrEmpty(guid)) throw new IOException("アセット保存に失敗しました: " + aPath);
            aCreated.Add(aPath, guid);
        }

        private static void AddToCatalog<T>(PPCatalogAsset<T> aCatalog, T aItem) where T : ScriptableObject
        {
            if (aCatalog == null) return;
            var so = new SerializedObject(aCatalog);
            var items = so.FindProperty("mItems");
            for (var i = 0; i < items.arraySize; i++)
                if (items.GetArrayElementAtIndex(i).objectReferenceValue == aItem) return;
            items.InsertArrayElementAtIndex(items.arraySize);
            items.GetArrayElementAtIndex(items.arraySize - 1).objectReferenceValue = aItem;
            so.ApplyModifiedPropertiesWithoutUndo();
            aCatalog.Invalidate();
        }

        private static void Invalidate(PPUnitCreationDraft aDraft)
        {
            aDraft.UnitCatalog?.Invalidate();
            aDraft.VisualCatalog?.Invalidate();
            aDraft.SkillCatalog?.Invalidate();
        }
    }
}
