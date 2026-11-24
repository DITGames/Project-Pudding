/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPEncounterDefinition.cs
 * @author hqrse
 * @date 2026/11/23
 * @brief ステージ定義(1 回のバトルの中身。ウェーブ・敵拠点・テーマ・出てくる段)
 * =====================================*/

using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // 遭遇の戦い方の傾向。アセットへ数値で保存されるため、値は明示的に振り、追加するときは末尾へ足す
    public enum PPEncounterTheme
    {
        [InspectorName(PPEncounterThemeDefinition.NameNone)]
        None = 0,
        [InspectorName(PPEncounterThemeDefinition.NameSwarm)]
        Swarm = 1,
        [InspectorName(PPEncounterThemeDefinition.NameBrute)]
        Brute = 2,
        [InspectorName(PPEncounterThemeDefinition.NameAffliction)]
        Affliction = 3,
        [InspectorName(PPEncounterThemeDefinition.NameCombo)]
        Combo = 4,
        [InspectorName(PPEncounterThemeDefinition.NameDefense)]
        Defense = 5,
        [InspectorName(PPEncounterThemeDefinition.NameAttrition)]
        Attrition = 6,
    }

    // テーマの表示名をまとめた定義。表示名をハードコードせずここを参照する
    public static class PPEncounterThemeDefinition
    {
        /* 定数 */

        public const string NameNone = "なし";
        public const string NameSwarm = "群れ";
        public const string NameBrute = "強敵";
        public const string NameAffliction = "状態異常";
        public const string NameCombo = "連携";
        public const string NameDefense = "防衛";
        public const string NameAttrition = "消耗戦";

        /* 説明文 */

        public static string ToDisplayString(PPEncounterTheme aTheme)
            => aTheme switch
            {
                PPEncounterTheme.None => NameNone,
                PPEncounterTheme.Swarm => NameSwarm,
                PPEncounterTheme.Brute => NameBrute,
                PPEncounterTheme.Affliction => NameAffliction,
                PPEncounterTheme.Combo => NameCombo,
                PPEncounterTheme.Defense => NameDefense,
                PPEncounterTheme.Attrition => NameAttrition,
                _ => "",
            };
    }

    // ステージ定義。1 回のバトルで敵をどう出すか(ウェーブ)と敵拠点を持ち、マップのマスに候補として割り当てる
    // 難易度ポイントは、ウェーブに出てくるユニットの種類ごとのポイントを合計する
    [CreateAssetMenu(fileName = "PED_New", menuName = "Project-Pudding/Definition/PPEncounterDefinition")]
    public class PPEncounterDefinition : ScriptableObject
    {
        /* 定数 */

        private const string LogTag = "StageMap";

        /* インスペクタ設定 */

        [Header("ウェーブ")]
        [Label("ウェーブ")]
        [SerializeField] private PPWaveDefinition mWave = new();

        [Header("敵拠点")]
        [Label("敵の拠点")]
        [SerializeField] private PPBaseSettings mEnemyBase = new("敵の拠点", 20000f);

        // 味方拠点から敵拠点までの距離。バトルの始まりに道の長さとして使う
        [Header("道")]
        [Label("道の長さ")]
        [Min(10f)]
        [SerializeField] private float mFieldLength = 60f;

        [Header("分類")]
        [Label("テーマ")]
        [SerializeField] private PPEncounterTheme mTheme = PPEncounterTheme.None;
        // マップ定義の「エリート」の欄に置く遭遇か。欄と食い違うとマップの生成時に警告する
        [Label("エリート")]
        [SerializeField] private bool mIsElite = false;

        // 段は最下段を 0 として数える
        [Header("出てくる段")]
        [Label("最小の段")] [Min(0)]
        [SerializeField] private int mMinFloor = 0;
        [Label("最大の段")] [Min(0)]
        [SerializeField] private int mMaxFloor = 99;

        /* プロパティ */

        public PPWaveDefinition Wave => mWave;
        public PPBaseSettings EnemyBase => mEnemyBase;
        public float FieldLength => Mathf.Max(10f, mFieldLength);
        public PPEncounterTheme Theme => mTheme;
        public bool IsElite => mIsElite;
        public int MinFloor => mMinFloor;
        public int MaxFloor => mMaxFloor;
        // ウェーブに敵を出す設定が 1 つでもあるか
        public bool HasEnemies => mWave.CollectUnits().Count > 0;
        // ウェーブに出てくるユニットの種類ごとの難易度ポイントの合計。レベルは種類ごとの最も高いもの
        public int DifficultyPoints
        {
            get
            {
                int total = 0;
                foreach (var unit in mWave.CollectUnits()) total += unit.EvaluateDifficultyPoint(mWave.GetMaxLevel(unit));
                return total;
            }
        }

        /* Unity イベント */

#if UNITY_EDITOR
        // 最小の段が最大の段より大きいと、どの段にも出なくなるため警告する。値は書き換えない
        void OnValidate()
        {
            if (mMinFloor <= mMaxFloor) return;
            CustomConsoleLog.Warning(LogTag, $"ステージ定義 {name} の最小の段 {mMinFloor} が最大の段 {mMaxFloor} より大きいため、どの段にも出ません。「出てくる段」を直してください", this);
        }
#endif

        /* 問い合わせ */

        // 指定した段に出てくるか。ウェーブに敵が無ければ出さない
        public bool IsAvailableOnFloor(int aFloor) => HasEnemies && aFloor >= mMinFloor && aFloor <= mMaxFloor;
    }
}
