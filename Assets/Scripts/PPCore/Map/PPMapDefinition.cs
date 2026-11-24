/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPMapDefinition.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief マップの定義
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // マップに出てくるバトル定義の候補 1 件分。出てくる段はバトル定義が持ち、マップごとの出やすさは重みで決める
    [Serializable]
    public sealed class PPMapBattleEntry
    {
        [FormerlySerializedAs("Encounter")]
        [Label("バトル定義")] public PPBattleDefinition Battle;
        [Label("重み")] [Min(0f)] public float Weight = 1f;

        // 指定した段に出てくる候補か
        public bool IsAvailable(int aFloor) => Battle != null && Weight > 0f && Battle.IsAvailableOnFloor(aFloor);
    }

    // 1 つのマップの形・マスの出現・敵の分布・ユニット獲得・開始時のパーティをまとめた定義
    // 同じ定義と同じシードからは、いつも同じマップが作られる
    [CreateAssetMenu(fileName = "PMD_New", menuName = "Project-Pudding/Definition/PPMapDefinition")]
    public class PPMapDefinition : ScriptableObject
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
        [SerializeField] private PPMapNodeType mTopFloorType = PPMapNodeType.Upgrade;

        // 通常敵にはエリートでないバトル定義を、エリートにはエリートのバトル定義を置く
        [Header("敵")]
        [Label("通常敵", true)]
        [FormerlySerializedAs("mNormalEncounters")]
        [SerializeField] private List<PPMapBattleEntry> mNormalBattles = new();
        // 空ならエリートのマスは出さない
        [Label("エリート", true)]
        [FormerlySerializedAs("mEliteEncounters")]
        [SerializeField] private List<PPMapBattleEntry> mEliteBattles = new();
        [Label("ボス")]
        [SerializeField] private PPBattleDefinition mBoss;
        // マスが深いほど敵を強くする。割り振れるレベルは (基底レベル - 1) + 深さごとに足すレベル を四捨五入したもので、
        // ウェーブに出てくる敵の種類へ割り振る
        [Header("敵の強さ")]
        [Label("基底レベル")]
        [Min(1)]
        [SerializeField] private int mBaseLevel = 1;
        // 横軸が深さ(バトルのマスは 1 段目から)、縦軸がその深さで足すレベル。既定は深さ 1 で 0、1 段深くなるごとに +1
        [Label("深さごとに足すレベル")]
        [SerializeField] private AnimationCurve mDepthLevelCurve = AnimationCurve.Linear(1f, 0f, 30f, 29f);

        // このマップのバトルで道の左端に置く拠点。敵の拠点はバトル定義が持つ
        [Header("拠点")]
        [Label("味方の拠点")]
        [SerializeField] private PPBaseSettings mAllyBase = new("味方の拠点", 20000f);

        [Header("ユニット獲得")]
        [Label("候補", true)]
        [SerializeField] private List<PPUnitDefinition> mRecruitCandidates = new();
        [Label("画面に出す数")] [Min(1)]
        [SerializeField] private int mRecruitOfferCount = 3;
        [Label("加わるときのレベル")] [Min(1)]
        [SerializeField] private int mRecruitLevel = 1;

        [Header("開始")]
        // タイトルで選んだ内容が無いときは、ここに並べたユニットを所持ユニットとし、この順に編成へ入れる
        [Label("初期パーティ", true)]
        [SerializeField] private List<PPPartyMemberEntry> mInitialMembers = new();
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
        public PPMapNodeType TopFloorType => mTopFloorType;
        public IReadOnlyList<PPMapBattleEntry> NormalBattles => mNormalBattles;
        public IReadOnlyList<PPMapBattleEntry> EliteBattles => mEliteBattles;
        public PPBattleDefinition Boss => mBoss;
        public int BaseLevel => Mathf.Max(1, mBaseLevel);
        public AnimationCurve DepthLevelCurve => mDepthLevelCurve;
        public PPBaseSettings AllyBase => mAllyBase;
        public IReadOnlyList<PPUnitDefinition> RecruitCandidates => mRecruitCandidates;
        public int RecruitOfferCount => mRecruitOfferCount;
        public int RecruitLevel => mRecruitLevel;
        public IReadOnlyList<PPPartyMemberEntry> InitialParty => mInitialMembers;

        /* Unity イベント */

#if UNITY_EDITOR
        // インスペクタで編集した時点で、欄とバトル定義の「エリート」の食い違いを警告する
        void OnValidate()
        {
            foreach (var message in FindMismatchedBattles())
            {
                CustomConsoleLog.Warning(LogTag, message, this);
            }
            if (mInitialMembers.Count > PPPartyFactory.MaxMembers)
            {
                CustomConsoleLog.Warning(LogTag, $"マップ定義 {name} の初期パーティが {mInitialMembers.Count} 体あります。{PPPartyFactory.MaxMembers + 1} 体目からは控えになります", this);
            }
        }
#endif

        /* 問い合わせ */

        // 深さ aDepth のマスで、出てくる敵の種類へ割り振るレベル。(基底レベル - 1) + 深さごとに足すレベル を四捨五入し、0 未満にはしない
        public int GetLevelPoints(int aDepth)
        {
            float add = mDepthLevelCurve != null && mDepthLevelCurve.length > 0 ? mDepthLevelCurve.Evaluate(aDepth) : Mathf.Max(0, aDepth - 1);
            return Mathf.Max(0, Mathf.RoundToInt(BaseLevel - 1 + add));
        }

        // 新しく始めるときのシードを返す。固定しない場合は毎回違う値になる
        public int ResolveSeed() => mIsFixedSeed ? mSeed : new System.Random().Next();

        // 通常敵の欄にエリートのバトル定義が、エリートの欄にエリートでないバトル定義が置かれていれば、その警告文を返す。無ければ空
        public List<string> FindMismatchedBattles()
        {
            var messages = new List<string>();
            CollectMismatched(mNormalBattles, false, "通常敵", messages);
            CollectMismatched(mEliteBattles, true, "エリート", messages);
            return messages;
        }

        private void CollectMismatched(List<PPMapBattleEntry> aEntries, bool aIsElite, string aListName, List<string> aMessages)
        {
            foreach (var entry in aEntries)
            {
                if (entry == null || entry.Battle == null || entry.Battle.IsElite == aIsElite) continue;
                aMessages.Add($"マップ定義 {name} の「{aListName}」にあるバトル定義 {entry.Battle.name} は、エリートの設定が{(aIsElite ? "オフ" : "オン")}です。バトル定義の「エリート」か、置く欄を直してください");
            }
        }
    }
}
