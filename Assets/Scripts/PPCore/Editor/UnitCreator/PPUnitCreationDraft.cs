/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPUnitCreationDraft.cs
 * @author hqrse
 * @date 2026/09/09
 * @brief ユニット作成の永続下書きと編集用アクセサー
 * =====================================*/

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AttributeUtility;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace PPCore
{
    public sealed class PPUnitCreationDraft : ScriptableObject
    {
        [Label("下書きID")][SerializeField] private string mDraftId;
        [Label("更新番号")][SerializeField] private int mRevision;
        [Label("ユニット")][SerializeField] private PPUnitDefinition mUnit;
        [Label("アイコン")][SerializeField] private Sprite mIcon;
        [Label("保存先")][SerializeField] private string mFolder = "Assets/GameData";
        [Label("最大レベル")][SerializeField] private int mMaxLevel = 50;
        [Label("表示レベル")][SerializeField] private int mPreviewLevel = 25;
        [Label("軸基準値")][SerializeField] private float[] mScales = { 1000, 100, 100, 100 };
        [Label("新規AIを作成")][SerializeField] private bool mNewAI;
        [Label("新規スキル")][SerializeField] private List<PPSkillDefinition> mNewSkills = new();
        [Label("ユニットカタログ")][SerializeField] private PPUnitCatalog mUnitCatalog;
        [Label("ビジュアルカタログ")][SerializeField] private PPUnitVisualCatalog mVisualCatalog;
        [Label("スキルカタログ")][SerializeField] private PPSkillCatalog mSkillCatalog;
        [Label("生成要求ID")][SerializeField] private string mRequestId;
        [Label("生成時の更新番号")][SerializeField] private int mCreatedRevision = -1;
        [Label("生成結果")][SerializeField] private string[] mCreatedPaths = Array.Empty<string>();
        [Label("生成GUID")][SerializeField] private string[] mCreatedGuids = Array.Empty<string>();
        [Label("生成ユニットID")][SerializeField] private string mCreatedUnitId;

        public string DraftId => mDraftId;
        public int Revision => mRevision;
        public PPUnitDefinition Unit => mUnit;
        public Sprite Icon { get => mIcon; set => mIcon = value; }
        public string Folder { get => mFolder; set => mFolder = value; }
        public int MaxLevel => mMaxLevel;
        public int PreviewLevel { get => mPreviewLevel; set => mPreviewLevel = value; }
        public float[] Scales => mScales;
        public bool NewAI { get => mNewAI; set => mNewAI = value; }
        public List<PPSkillDefinition> NewSkills => mNewSkills;
        public PPUnitCatalog UnitCatalog { get => mUnitCatalog; set => mUnitCatalog = value; }
        public PPUnitVisualCatalog VisualCatalog { get => mVisualCatalog; set => mVisualCatalog = value; }
        public PPSkillCatalog SkillCatalog { get => mSkillCatalog; set => mSkillCatalog = value; }
        public string RequestId => mRequestId;
        public int CreatedRevision => mCreatedRevision;
        public string[] CreatedPaths => mCreatedPaths;
        public string[] CreatedGuids => mCreatedGuids;
        public string CreatedUnitId => mCreatedUnitId;
        public bool IsCreated => mCreatedPaths.Length > 0;
        // 成長する能力値の一覧。きようさだけは基礎値が追加ステータス側（mExpandStatBlock）にある
        public static readonly string[] StatNames = { "HP", "攻撃力", "防御力", "素早さ", "きようさ" };
        public static readonly string[] BaseFields = { "mBaseStatBlock.MaxHP", "mBaseStatBlock.Attack", "mBaseStatBlock.Defense", "mBaseStatBlock.Speed", "mExpandStatBlock.Dexterity" };
        public static readonly string[] CurveFields = { "mHpGrowth", "mAttackGrowth", "mDefenseGrowth", "mSpeedGrowth", "mDexterityGrowth" };
        // レーダーチャートに載せる軸の数（HP・攻撃力・防御力・素早さ）。チャート基準値（Scales）もこの数だけ持つ
        public const int ChartAxisCount = 4;

        // 初期アセットを作らず、編集専用インスタンスを組み立てる
        public static PPUnitCreationDraft Create()
        {
            var draft = CreateInstance<PPUnitCreationDraft>();
            draft.mDraftId = Guid.NewGuid().ToString("N");
            draft.mUnit = CreateInstance<PPUnitDefinition>();
            var so = new SerializedObject(draft.Unit);
            so.FindProperty("mDisplayName").stringValue = "新しいユニット";
            so.FindProperty("mBaseStatBlock.MaxHP").floatValue = 100;
            so.FindProperty("mBaseStatBlock.Attack").floatValue = 10;
            so.FindProperty("mBaseStatBlock.Defense").floatValue = 10;
            so.FindProperty("mBaseStatBlock.Speed").floatValue = 10;
            so.FindProperty("mExpandStatBlock.ActionCount").intValue = 1;
            so.FindProperty("mExpandStatBlock.SkillGaugeMax").floatValue = 100;
            so.FindProperty("mExpandStatBlock.CoinGaugeMax").floatValue = 100;
            so.FindProperty("mExpandStatBlock.Dexterity").floatValue = PPStatBlock.DefaultDexterity;
            so.ApplyModifiedPropertiesWithoutUndo();
            return draft;
        }

        // UIとMCPの両方で古い更新を拒否する
        public void RequireRevision(int aRevision)
        {
            if (aRevision != mRevision) throw new InvalidOperationException($"更新競合: 期待 {aRevision} / 現在 {mRevision}");
            if (IsCreated) throw new InvalidOperationException("この下書きは生成済みです。新しい下書きを作成してください。");
        }

        // 編集確定後だけ番号を進め、再コンパイルに備えて保存する
        public void Commit()
        {
            mRevision++;
            PPUnitDraftStore.Save(this);
        }

        // 生成結果は同じ要求の再送へ返すために保持する
        public void RecordCreation(string aRequestId, string[] aPaths)
        {
            mRequestId = aRequestId;
            mCreatedRevision = mRevision;
            mCreatedPaths = aPaths;
            mCreatedGuids = aPaths.Select(AssetDatabase.AssetPathToGUID).ToArray();
            mCreatedUnitId = Unit.UnitId;
            try { PPUnitDraftStore.Save(this); }
            catch
            {
                mRequestId = null;
                mCreatedRevision = -1;
                mCreatedPaths = Array.Empty<string>();
                mCreatedGuids = Array.Empty<string>();
                mCreatedUnitId = null;
                throw;
            }
        }

        // 変更要求は独立したコピーへ適用し、全項目の解釈に成功してから置き換える
        public PPUnitCreationDraft CreateWorkingCopy()
        {
            var copy = Instantiate(this);
            copy.mUnit = Instantiate(Unit);
            copy.mNewSkills = new List<PPSkillDefinition>();
            foreach (var skill in mNewSkills)
            {
                var cloned = Instantiate(skill);
                copy.mNewSkills.Add(cloned);
                for (var i = 0; i < copy.Unit.Skills.Count; i++)
                    if (copy.Unit.Skills[i] == skill) copy.Unit.Skills[i] = cloned;
            }
            return copy;
        }

        // 基礎値と終端実値を変更し、途中の曲線形状を維持する
        public void SetStat(int aIndex, float aInitial, float aFinal)
        {
            var so = new SerializedObject(Unit);
            var curve = GetCurve(aIndex);
            var oldEnd = curve.Evaluate(MaxLevel);
            var ratio = aInitial > 0 ? aFinal / aInitial : 1;
            curve = PPUnitGrowthTemplate.Rescale(curve, MaxLevel, MaxLevel, oldEnd, ratio);
            so.FindProperty(BaseFields[aIndex]).floatValue = aInitial;
            so.FindProperty(CurveFields[aIndex]).animationCurveValue = curve;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 時間軸だけを伸縮し、実数値と接線形状を保つ
        public void SetMaxLevel(int aLevel)
        {
            if (aLevel < 1 || aLevel > 100000) throw new ArgumentOutOfRangeException(nameof(aLevel), "最大レベルは1〜100000です。");
            var so = new SerializedObject(Unit);
            for (var i = 0; i < StatNames.Length; i++)
            {
                var curve = GetCurve(i);
                so.FindProperty(CurveFields[i]).animationCurveValue = PPUnitGrowthTemplate.Rescale(curve, mMaxLevel, aLevel, curve.Evaluate(mMaxLevel), curve.Evaluate(mMaxLevel));
            }
            so.ApplyModifiedPropertiesWithoutUndo();
            mMaxLevel = aLevel;
            mPreviewLevel = Mathf.Clamp(mPreviewLevel, 1, mMaxLevel);
        }

        public AnimationCurve GetCurve(int aIndex) => new SerializedObject(Unit).FindProperty(CurveFields[aIndex]).animationCurveValue;
        public float GetInitial(int aIndex) => new SerializedObject(Unit).FindProperty(BaseFields[aIndex]).floatValue;
        public float GetFinal(int aIndex) => GetInitial(aIndex) * Mathf.Max(1, GetCurve(aIndex).Evaluate(MaxLevel));

        // カスタム曲線の始点と終点の時刻を固定する
        public void SetCurve(int aIndex, AnimationCurve aCurve)
        {
            var curve = PPUnitGrowthTemplate.Anchor(aCurve, MaxLevel);
            var so = new SerializedObject(Unit);
            so.FindProperty(CurveFields[aIndex]).animationCurveValue = curve;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // 新規スキルだけを下書きの所有物として追加する
        public PPSkillDefinition AddNewSkill()
        {
            var skill = CreateInstance<PPSkillDefinition>();
            skill.name = "新規スキル";
            mNewSkills.Add(skill);
            Unit.Skills.Add(skill);
            return skill;
        }
    }

    public static class PPUnitDraftStore
    {
        private const string DirectoryPath = "UserSettings/UnitCreator";
        private static readonly Dictionary<string, PPUnitCreationDraft> mLoaded = new();
        public static string LastId => EditorPrefs.GetString(Application.dataPath + "/UnitCreator/LastId", "");

        // 内部シリアライザーに所有オブジェクトを渡し、既存アセット参照は外部参照として残す
        public static void Save(PPUnitCreationDraft aDraft)
        {
            Directory.CreateDirectory(DirectoryPath);
            var objects = new List<UnityEngine.Object> { aDraft, aDraft.Unit };
            objects.AddRange(aDraft.NewSkills.Where(aSkill => aSkill != null));
            var path = GetPath(aDraft.DraftId);
            var temporary = path + ".tmp";
            InternalEditorUtility.SaveToSerializedFileAndForget(objects.ToArray(), temporary, true);
            if (File.Exists(path)) File.Replace(temporary, path, null);
            else File.Move(temporary, path);
            if (mLoaded.TryGetValue(aDraft.DraftId, out var previous) && previous != null && previous != aDraft)
            {
                foreach (var skill in previous.NewSkills) if (skill != null) UnityEngine.Object.DestroyImmediate(skill);
                if (previous.Unit != null) UnityEngine.Object.DestroyImmediate(previous.Unit);
                UnityEngine.Object.DestroyImmediate(previous);
            }
            mLoaded[aDraft.DraftId] = aDraft;
            EditorPrefs.SetString(Application.dataPath + "/UnitCreator/LastId", aDraft.DraftId);
        }

        // ウィンドウが閉じていてもIDから復元できる
        public static PPUnitCreationDraft Load(string aId)
        {
            if (mLoaded.TryGetValue(aId, out var draft) && draft != null) return draft;
            var path = GetPath(aId);
            if (!File.Exists(path)) throw new InvalidOperationException("下書きが見つかりません: " + aId);
            var objects = InternalEditorUtility.LoadSerializedFileAndForget(path);
            draft = objects.OfType<PPUnitCreationDraft>().FirstOrDefault();
            if (draft == null || draft.Unit == null) throw new InvalidOperationException("下書きを復元できません: " + path);
            mLoaded[aId] = draft;
            return draft;
        }

        private static string GetPath(string aId)
        {
            if (!Guid.TryParseExact(aId, "N", out _)) throw new ArgumentException("不正な下書きIDです。");
            return DirectoryPath + "/" + aId + ".asset";
        }
    }
}
