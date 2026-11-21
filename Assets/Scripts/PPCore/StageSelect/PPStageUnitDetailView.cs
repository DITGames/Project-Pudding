/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageUnitDetailView.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージセレクトのユニット詳細画面
 * =====================================*/

using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // ユニットのアイコン・名前・レベル・属性・ロール・ステータス・スキルを出す画面
    // 編成画面と仲間選択画面から開き、詳細の入力かキャンセルで閉じる
    public class PPStageUnitDetailView : MonoBehaviour
    {
        /* 定数 */

        // 消費ゲージの無いスキルに出す文字
        private const string FreeCostText = "消費なし";

        /* インスペクタ設定 */

        [Header("入力")]
        [Label("閉じる入力", true)]
        [SerializeField] private InputActionReference[] mCloseActions = new InputActionReference[0];

        [Header("表示")]
        [Label("画面")]
        [SerializeField] private GameObject mPanel;
        [Label("ユニットの見た目カタログ")]
        [SerializeField] private PPUnitVisualCatalog mUnitVisualCatalog;
        [Label("アイコン")]
        [SerializeField] private Image mIcon;
        [Label("名前")]
        [SerializeField] private TMP_Text mName;
        // レベル・属性・ロール
        [Label("情報")]
        [SerializeField] private TMP_Text mInfo;
        // ステータスの名前を縦に並べる文字。右の数値と行をそろえる
        [Label("ステータスの名前")]
        [SerializeField] private TMP_Text mStatNames;
        [Label("ステータスの数値")]
        [SerializeField] private TMP_Text mStatValues;
        [Label("スキルを並べる場所")]
        [SerializeField] private RectTransform mSkillContainer;
        [Label("スキルの行のプレハブ")]
        [SerializeField] private PPStageSkillRow mSkillRowPrefab;
        // スキルが 1 つも無いときに出す文字。未設定なら出さない
        [Label("スキルが無いときの文字")]
        [SerializeField] private TMP_Text mNoSkillText;

        /* 状態 */

        private readonly List<PPStageSkillRow> mSkillRows = new();
        private int mOpenedFrame = -1;

        /* プロパティ */

        public bool IsOpen { get; private set; }
        // 最後に閉じたフレーム。閉じたキーで元の画面まで動かないよう、元の画面がそのフレームの入力を捨てる
        public int ClosedFrame { get; private set; } = -1;
        // 今出しているユニットの名前。検証や表示の確認に使う
        public string ShownName => mName != null ? mName.text : string.Empty;

        /* Unity イベント */

        void Awake()
        {
            if (mPanel != null) mPanel.SetActive(false);
        }

        // 開いたフレームは、開いたキーで閉じないよう入力を受け付けない
        void Update()
        {
            if (!IsOpen || Time.frameCount == mOpenedFrame) return;

            foreach (var action in mCloseActions)
            {
                if (action == null || !action.action.WasPressedThisFrame()) continue;
                Close();
                return;
            }
        }

        /* 操作 */

        public void Open(PPUnitDefinition aUnit, int aLevel)
        {
            IsOpen = true;
            mOpenedFrame = Time.frameCount;
            foreach (var action in mCloseActions)
            {
                if (action != null) action.action.Enable();
            }
            Show(aUnit, aLevel);
            if (mPanel != null) mPanel.SetActive(true);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            ClosedFrame = Time.frameCount;
            if (mPanel != null) mPanel.SetActive(false);
        }

        /* 表示 */

        private void Show(PPUnitDefinition aUnit, int aLevel)
        {
            var icon = PPStageUnitDisplay.ResolveIcon(mUnitVisualCatalog, aUnit);
            if (mIcon != null)
            {
                mIcon.sprite = icon;
                mIcon.enabled = icon != null;
            }
            if (mName != null) mName.text = aUnit.DisplayName;
            if (mInfo != null) mInfo.text = $"Lv {aLevel}   |   {PPStageUnitDisplay.ColoredAttribute(aUnit)}   |   {PPStageUnitDisplay.RoleName(aUnit)}";

            var stats = aUnit.EvaluateStats(aLevel);
            if (mStatNames != null)
            {
                mStatNames.text = string.Join("\n", PPUnitAbilityDefinition.NameHp, PPUnitAbilityDefinition.NameStrength,
                    PPUnitAbilityDefinition.NameGuard, PPUnitAbilityDefinition.NameAgility, PPUnitAbilityDefinition.NameDexterity);
            }
            if (mStatValues != null)
            {
                mStatValues.text = $"{stats.MaxHP:0}\n{stats.Attack:0}\n{stats.Defense:0}\n{stats.Speed:0}\n{aUnit.EvaluateDexterity(aLevel):0}";
            }
            ShowSkills(aUnit);
        }

        // スキルの行を、ユニットの持つスキルの数にそろえて書き直す
        private void ShowSkills(PPUnitDefinition aUnit)
        {
            var skills = new List<SkillDefinition>();
            foreach (var skill in aUnit.Skills)
            {
                if (skill != null) skills.Add(skill);
            }

            while (mSkillRows.Count < skills.Count) mSkillRows.Add(Instantiate(mSkillRowPrefab, mSkillContainer));
            for (int i = 0; i < mSkillRows.Count; i++)
            {
                bool isUsed = i < skills.Count;
                mSkillRows[i].gameObject.SetActive(isUsed);
                if (isUsed) mSkillRows[i].Set(skills[i].DisplayName, skills[i].Description, FormatCost(skills[i]));
            }
            if (mNoSkillText != null) mNoSkillText.gameObject.SetActive(skills.Count == 0);
        }

        /* ヘルパー */

        private static string FormatCost(SkillDefinition aSkill)
            => aSkill is PPSkillDefinition ppSkill && !ppSkill.IsFreeCost ? $"ゲージ {ppSkill.SkillGaugeCost:0}" : FreeCostText;
    }
}
