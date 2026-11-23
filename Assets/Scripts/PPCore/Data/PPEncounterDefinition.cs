/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPEncounterDefinition.cs
 * @author hqrse
 * @date 2026/11/23
 * @brief 1 回の戦闘で出てくる敵の組み合わせ（遭遇）の定義
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

    // 遭遇の定義。敵の編成は敵パーティ定義を参照し、難易度ポイントは編成から合計する
    [CreateAssetMenu(fileName = "PED_New", menuName = "Project-Pudding/Definition/PPEncounterDefinition")]
    public class PPEncounterDefinition : ScriptableObject
    {
        /* 定数 */

        // 難易度ポイントを合計する配置の並び（編成と同じ Center → Right → Left）
        public static readonly PPPartyPosition[] Positions =
            { PPPartyPosition.Center, PPPartyPosition.Right, PPPartyPosition.Left };
        private const string LogTag = "StageMap";

        /* インスペクタ設定 */

        [Header("敵")]
        [Label("敵パーティ定義")]
        [SerializeField] private PPPartyDefinition mParty;

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

        public PPPartyDefinition Party => mParty;
        public PPEncounterTheme Theme => mTheme;
        public bool IsElite => mIsElite;
        public int MinFloor => mMinFloor;
        public int MaxFloor => mMaxFloor;
        // 編成の各ユニットの難易度ポイントの合計
        public int DifficultyPoints
        {
            get
            {
                int total = 0;
                foreach (var position in Positions) total += GetDifficultyPoint(position);
                return total;
            }
        }

        /* Unity イベント */

#if UNITY_EDITOR
        // 最小の段が最大の段より大きいと、どの段にも出なくなるため警告する。値は書き換えない
        void OnValidate()
        {
            if (mMinFloor <= mMaxFloor) return;
            CustomConsoleLog.Warning(LogTag, $"遭遇 {name} の最小の段 {mMinFloor} が最大の段 {mMaxFloor} より大きいため、どの段にも出ません。「出てくる段」を直してください", this);
        }
#endif

        /* 問い合わせ */

        // 指定した段に出てくるか。敵パーティ定義が無ければ出さない
        public bool IsAvailableOnFloor(int aFloor) => mParty != null && aFloor >= mMinFloor && aFloor <= mMaxFloor;

        // 配置 1 つ分の難易度ポイント。その配置にユニットがいなければ 0
        public int GetDifficultyPoint(PPPartyPosition aPosition)
        {
            var entry = mParty != null ? mParty.GetMember(aPosition) : null;
            if (entry == null || entry.Unit == null) return 0;
            return entry.Unit.EvaluateDifficultyPoint(entry.Level);
        }
    }
}
