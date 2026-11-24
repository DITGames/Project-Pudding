/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPCoinUnitBridge.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief プッシャーのコインとバトルの橋渡し(コインの中身の抽選と、落ちたコインからのユニット生成)
 * =====================================*/

using System.Collections.Generic;
using AttributeUtility;
using CustomConsole;
using UnityEngine;

namespace PPCore
{
    // コインに入れるユニットの抽選。パーティのユニットのコストの逆数の比で選ぶ(コストが低いほど出やすい)
    public class PPCoinUnitLottery
    {
        /* 内部型 */

        // 抽選の候補 1 体分。Rate は全体を 1 とした出現率
        public readonly struct Entry
        {
            public readonly PPUnitDefinition Unit;
            public readonly int Level;
            public readonly float Rate;

            public Entry(PPUnitDefinition aUnit, int aLevel, float aRate)
            {
                Unit = aUnit;
                Level = aLevel;
                Rate = aRate;
            }
        }

        /* 状態 */

        private readonly List<Entry> mEntries = new();

        /* プロパティ */

        public IReadOnlyList<Entry> Entries => mEntries;

        /* 生成 */

        // 定義を持つメンバーだけを候補にする。生成時のレベルもそのまま引き継ぐ
        public PPCoinUnitLottery(IEnumerable<BattleUnit> aMembers)
        {
            var units = new List<(PPUnitDefinition Unit, int Level, float Weight)>();
            float total = 0f;
            foreach (var member in aMembers)
            {
                if (member?.SourceDefinition is not PPUnitDefinition definition) continue;
                float weight = 1f / Mathf.Max(1, definition.UnitCost);
                units.Add((definition, member.Level, weight));
                total += weight;
            }
            foreach (var (unit, level, weight) in units) mEntries.Add(new Entry(unit, level, weight / total));
        }

        /* 操作 */

        // 出現率に従って 1 体選ぶ。候補が無ければ false
        public bool TryDraw(IRandomProvider aRandom, out Entry aEntry)
        {
            aEntry = default;
            if (mEntries.Count == 0) return false;

            float roll = aRandom.NextFloat();
            foreach (var entry in mEntries)
            {
                aEntry = entry;
                roll -= entry.Rate;
                if (roll < 0f) return true;
            }
            // 出現率の合計が丸めで 1 にわずかに届かないときは、最後の候補にする
            return true;
        }
    }

    // プッシャーのコインとバトルの橋渡し。台に出たコインへ抽選でユニットを入れ、
    // 落下口に落ちたコインのユニットを、バトル画面の味方拠点の近くに生み出す
    public class PPCoinUnitBridge : MonoBehaviour
    {
        /* インスペクタ設定 */

        [Header("プッシャー")]
        [Label("コインの置き場")]
        [SerializeField] private PPCoinPool mPool;
        // ここに落ちたコインからユニットが生まれる。台の外へ飛び出したものを消すだけの領域は入れない
        [Label("ユニットを生む落下口", true)]
        [SerializeField] private PPPusherDropZone[] mDropZones = new PPPusherDropZone[0];
        // コインに貼るユニットのアイコンを引く
        [Label("ビジュアルカタログ")]
        [SerializeField] private PPUnitVisualCatalog mUnitVisualCatalog;

        // ユニットの重さをこの範囲で 0〜1 に直し、コインの大きさと質量を軽い側〜重い側の間で決める
        [Header("重さ")]
        [Label("重さの下限")]
        [Min(0f)]
        [SerializeField] private float mMinWeight = 1f;
        [Label("重さの上限")]
        [Min(0f)]
        [SerializeField] private float mMaxWeight = 10f;
        // 大きさはコインのプレハブの何倍か
        [Label("コインの大きさ(軽い)")]
        [Min(0.1f)]
        [SerializeField] private float mLightScale = 1f;
        [Label("コインの大きさ(重い)")]
        [Min(0.1f)]
        [SerializeField] private float mHeavyScale = 1.6f;
        [Label("コインの質量(軽い)")]
        [Min(0.01f)]
        [SerializeField] private float mLightMass = 2f;
        [Label("コインの質量(重い)")]
        [Min(0.01f)]
        [SerializeField] private float mHeavyMass = 8f;

        [Header("生成")]
        // 味方拠点(道の左端)からの距離
        [Label("生まれる位置")]
        [Min(0f)]
        [SerializeField] private float mSpawnDistance = 2f;

        /* 状態 */

        private BattleManager mManager;
        private PPCoinUnitLottery mLottery;
        private IRandomProvider mRandom;
        private readonly List<GameObject> mCoinBuffer = new();

        /* プロパティ */

        public PPCoinUnitLottery Lottery => mLottery;

        /* Unity イベント */

        private void OnDestroy()
        {
            if (mPool != null) mPool.OnCoinSpawned -= Assign;
            foreach (var zone in mDropZones)
            {
                if (zone != null) zone.OnCoinDropped -= HandleDropped;
            }
        }

        /* 初期化・接続 */

        // 味方パーティから抽選を作り、台に出ているコインと、これから出るコインへ中身を入れる
        // 開始時に並べるコインの大きさも決まるよう、プッシャーの読み込みより先に呼ぶ
        public void Prepare(BattleParty aAllyParty, IRandomProvider aRandom)
        {
            mRandom = aRandom;
            mLottery = new PPCoinUnitLottery(aAllyParty.ActiveMembers);
            if (mLottery.Entries.Count == 0)
            {
                CustomConsoleLog.Warning("Battle", "味方パーティにユニットがいないため、コインにユニットを入れません", this);
            }
            if (mPool == null)
            {
                CustomConsoleLog.Error("Battle", "コインの置き場が未設定のため、コインからユニットを生めません。PPCoinUnitBridge の「コインの置き場」を割り当ててください", this);
                return;
            }

            mCoinBuffer.Clear();
            mPool.CollectActiveCoins(mCoinBuffer);
            foreach (var coin in mCoinBuffer) Assign(coin);
            mPool.OnCoinSpawned -= Assign;
            mPool.OnCoinSpawned += Assign;
        }

        // バトルの開始後、落下口に落ちたコインからユニットを生み出すようにする
        public void Bind(BattleManager aManager)
        {
            mManager = aManager;
            foreach (var zone in mDropZones)
            {
                if (zone == null) continue;
                zone.OnCoinDropped -= HandleDropped;
                zone.OnCoinDropped += HandleDropped;
            }
        }

        /* イベントハンドラ */

        // 台に出たコインへ、抽選で選んだユニットを入れる
        private void Assign(GameObject aCoin)
        {
            if (!aCoin.TryGetComponent<PPCoinContent>(out var content)) return;
            if (mLottery == null || !mLottery.TryDraw(mRandom, out var entry))
            {
                content.Clear();
                return;
            }
            float t = Mathf.InverseLerp(mMinWeight, mMaxWeight, entry.Unit.Weight);
            content.Set(entry.Unit, entry.Level, ResolveIcon(entry.Unit),
                Mathf.Lerp(mLightScale, mHeavyScale, t), Mathf.Lerp(mLightMass, mHeavyMass, t));
        }

        // 落下口に落ちたコインのユニットを、味方拠点の近くに生み出す
        private void HandleDropped(GameObject aCoin)
        {
            if (!aCoin.TryGetComponent<PPCoinContent>(out var content) || !content.HasUnit) return;
            var unit = content.Unit.CreateRuntimeUnit(content.Level);
            content.Clear();
            mManager?.SpawnFieldUnit(unit, BattleSide.Ally, mSpawnDistance);
        }

        /* ヘルパー */

        private Sprite ResolveIcon(PPUnitDefinition aUnit)
            => mUnitVisualCatalog != null ? mUnitVisualCatalog.Resolve(aUnit.UnitId)?.UnitIcon : null;
    }
}
