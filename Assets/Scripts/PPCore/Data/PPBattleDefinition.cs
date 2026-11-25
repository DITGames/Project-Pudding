/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleDefinition.cs
 * @author hqrse
 * @date 2026/11/23
 * @brief バトル定義(1 回のバトルの中身。ウェーブ・敵拠点・道の長さ・出てくる段)
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;
using AttributeUtility;
using CustomConsole;

namespace PPCore
{
    // バトル定義。1 回のバトルで敵をどう出すか(ウェーブのノードと線)・敵拠点・道の長さを持ち、マップのマスに候補として割り当てる
    // 難易度ポイントは、ウェーブに出てくるユニットの種類ごとのポイントを合計する
    [CreateAssetMenu(fileName = "PBD_New", menuName = "Project-Pudding/Definition/PPBattleDefinition")]
    public class PPBattleDefinition : ScriptableObject
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

        // マップ定義の「エリート」の欄に置くバトル定義か。欄と食い違うとマップの生成時に警告する
        [Header("分類")]
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
        // 最小の段が最大の段より大きいときと、ウェーブのノードや線に誤りがあるときに警告する。値は書き換えない
        void OnValidate()
        {
            if (mMinFloor > mMaxFloor)
            {
                CustomConsoleLog.Warning(LogTag, $"バトル定義 {name} の最小の段 {mMinFloor} が最大の段 {mMaxFloor} より大きいため、どの段にも出ません。「出てくる段」を直してください", this);
            }
            if (mWave == null) return;
            var problems = new List<string>();
            mWave.Validate(problems);
            foreach (var problem in problems) CustomConsoleLog.Warning(LogTag, $"バトル定義 {name} のウェーブ: {problem}", this);
        }
#endif

        /* 問い合わせ */

        // 指定した段に出てくるか。ウェーブに敵が無ければ出さない
        public bool IsAvailableOnFloor(int aFloor) => HasEnemies && aFloor >= mMinFloor && aFloor <= mMaxFloor;
    }
}
