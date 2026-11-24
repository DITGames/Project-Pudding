/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageMapDefinition.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief ステージマップの定義
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // マップに出てくる遭遇の候補 1 件分。出てくる段は遭遇が持ち、マップごとの出やすさは重みで決める
    [Serializable]
    public sealed class PPStageEncounterEntry
    {
        [Label("遭遇")] public PPEncounterDefinition Encounter;
        [Label("重み")] [Min(0f)] public float Weight = 1f;

        // 指定した段に出てくる候補か
        public bool IsAvailable(int aFloor) => Encounter != null && Weight > 0f && Encounter.IsAvailableOnFloor(aFloor);
    }

    // 1 つのマップの形・マスの出現・敵の分布・ユニット獲得・開始時のパーティをまとめた定義
    // 同じ定義と同じシードからは、いつも同じマップが作られる
    [CreateAssetMenu(fileName = "PPStageMapDefinition", menuName = "Project-Pudding/Definition/PPStageMapDefinition")]
    public class PPStageMapDefinition : ScriptableObject
    {
        /* 定数 */

        private const string LogTag = "StageMap";

        /* インスペクタ設定 */

        // ステージ選択の画面に出す内容。表示名が空ならアセット名を出す
        [Header("表示")]
        [Label("表示名")]
        [SerializeField] private string mDisplayName;
        [Label("サムネイル")]
        [SerializeField] private Sprite mThumbnail;
        [Label("説明")] [TextArea(2, 5)]
        [SerializeField] private string mDescription;

        [Header("マップ形状")]
        [Label("列数")] [Min(1)]
        [SerializeField] private int mWidth = 7;
        // ボスの段を含まない段数
        [Label("段数")] [Min(2)]
        [SerializeField] private int mHeight = 15;
        // 最下段の始点から最上段へ引く経路の本数。多いほど枝分かれが増える
        [Label("経路の本数")] [Min(1)]
        [SerializeField] private int mPathCount = 6;

        [Header("マスの出現")]
        [Label("通常戦の重み")] [Min(0f)]
        [SerializeField] private float mBattleWeight = 60f;
        [Label("エリートの重み")] [Min(0f)]
        [SerializeField] private float mEliteWeight = 16f;
        [Label("強化の重み")] [Min(0f)]
        [SerializeField] private float mUpgradeWeight = 14f;
        [Label("ユニット獲得の重み")] [Min(0f)]
        [SerializeField] private float mRecruitWeight = 10f;
        // この段より下には、エリート・強化・ユニット獲得を出さない
        [Label("特殊なマスが出始める段")] [Min(1)]
        [SerializeField] private int mSpecialMinFloor = 5;
        // この段は全マスがユニット獲得になる。-1 なら決め打ちしない
        [Label("ユニット獲得が必ず出る段")] [Min(-1)]
        [SerializeField] private int mRecruitFloor = 8;
        // ボスの直前の段のマス
        [Label("最上段のマス")]
        [SerializeField] private PPStageNodeType mTopFloorType = PPStageNodeType.Upgrade;

        // 通常敵にはエリートでない遭遇を、エリートにはエリートの遭遇を置く
        [Header("敵")]
        [Label("通常敵", true)]
        [SerializeField] private List<PPStageEncounterEntry> mNormalEncounters = new();
        // 空ならエリートのマスは出さない
        [Label("エリート", true)]
        [SerializeField] private List<PPStageEncounterEntry> mEliteEncounters = new();
        [Label("ボス")]
        [SerializeField] private PPPartyDefinition mBoss;

        // このステージのバトルで道の両端に置く拠点
        [Header("拠点")]
        [Label("味方の拠点")]
        [SerializeField] private PPBaseSettings mAllyBase = new("味方の拠点", 20000f);
        [Label("敵の拠点")]
        [SerializeField] private PPBaseSettings mEnemyBase = new("敵の拠点", 20000f);

        [Header("ユニット獲得")]
        [Label("候補", true)]
        [SerializeField] private List<PPUnitDefinition> mRecruitCandidates = new();
        [Label("画面に出す数")] [Min(1)]
        [SerializeField] private int mRecruitOfferCount = 3;
        [Label("加わるときのレベル")] [Min(1)]
        [SerializeField] private int mRecruitLevel = 1;

        [Header("開始")]
        // 初回はこの定義にいるユニットを所持ユニットとし、編成もこの配置にする
        [Label("初期パーティ")]
        [SerializeField] private PPPartyDefinition mInitialParty;
        [Label("シードを固定する")]
        [SerializeField] private bool mIsFixedSeed = false;
        [Label("シード")]
        [EditCondition(nameof(mIsFixedSeed), true, false)]
        [SerializeField] private int mSeed = 12345;

        /* プロパティ */

        public string DisplayName => string.IsNullOrEmpty(mDisplayName) ? name : mDisplayName;
        public Sprite Thumbnail => mThumbnail;
        public string Description => mDescription;
        public int Width => mWidth;
        public int Height => mHeight;
        public int PathCount => mPathCount;
        public float BattleWeight => mBattleWeight;
        public float EliteWeight => mEliteWeight;
        public float UpgradeWeight => mUpgradeWeight;
        public float RecruitWeight => mRecruitWeight;
        public int SpecialMinFloor => mSpecialMinFloor;
        public int RecruitFloor => mRecruitFloor;
        public PPStageNodeType TopFloorType => mTopFloorType;
        public IReadOnlyList<PPStageEncounterEntry> NormalEncounters => mNormalEncounters;
        public IReadOnlyList<PPStageEncounterEntry> EliteEncounters => mEliteEncounters;
        public PPPartyDefinition Boss => mBoss;
        public PPBaseSettings AllyBase => mAllyBase;
        public PPBaseSettings EnemyBase => mEnemyBase;
        public IReadOnlyList<PPUnitDefinition> RecruitCandidates => mRecruitCandidates;
        public int RecruitOfferCount => mRecruitOfferCount;
        public int RecruitLevel => mRecruitLevel;
        public PPPartyDefinition InitialParty => mInitialParty;

        /* Unity イベント */

#if UNITY_EDITOR
        // インスペクタで編集した時点で、欄と遭遇の「エリート」の食い違いを警告する
        void OnValidate()
        {
            foreach (var message in FindMismatchedEncounters())
            {
                CustomConsoleLog.Warning(LogTag, message, this);
            }
        }
#endif

        /* 問い合わせ */

        // 新しく始めるときのシードを返す。固定しない場合は毎回違う値になる
        public int ResolveSeed() => mIsFixedSeed ? mSeed : new System.Random().Next();

        // 通常敵の欄にエリートの遭遇が、エリートの欄にエリートでない遭遇が置かれていれば、その警告文を返す。無ければ空
        public List<string> FindMismatchedEncounters()
        {
            var messages = new List<string>();
            CollectMismatched(mNormalEncounters, false, "通常敵", messages);
            CollectMismatched(mEliteEncounters, true, "エリート", messages);
            return messages;
        }

        private void CollectMismatched(List<PPStageEncounterEntry> aEntries, bool aIsElite, string aListName, List<string> aMessages)
        {
            foreach (var entry in aEntries)
            {
                if (entry == null || entry.Encounter == null || entry.Encounter.IsElite == aIsElite) continue;
                aMessages.Add($"マップ定義 {name} の「{aListName}」にある遭遇 {entry.Encounter.name} は、エリートの設定が{(aIsElite ? "オフ" : "オン")}です。遭遇の「エリート」か、置く欄を直してください");
            }
        }
    }
}
