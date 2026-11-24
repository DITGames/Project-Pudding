/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPStageProgress.cs
 * @author hqrse
 * @date 2026/11/21
 * @brief シーンをまたいで残るステージの進行状態と、始める前に選んだ内容
 * =====================================*/

using System.Collections.Generic;
using UnityEngine;
using CustomConsole;

namespace PPCore
{
    // 所持ユニット 1 体分。同じユニット定義を複数体持てるため、参照で見分ける
    public sealed class PPStageOwnedUnit
    {
        public readonly PPUnitDefinition Unit;
        public int Level;

        public PPStageOwnedUnit(PPUnitDefinition aUnit, int aLevel)
        {
            Unit = aUnit;
            Level = aLevel;
        }
    }

    // バトルの決着を進行状態へ反映した結果
    public enum PPStageBattleOutcome
    {
        // 勝って、マップに戻って続ける
        Continue,
        // 負けて、進行状態を捨てた
        Defeated,
        // ボスに勝ってステージをクリアし、進行状態を捨てた
        Cleared,
    }

    // マップからバトルのシーンへ渡す、これから戦うバトルの内容
    public sealed class PPStageBattleRequest
    {
        public readonly Vector2Int Node;
        public readonly PPStageNodeType Type;
        // 戦うバトル定義(ウェーブと敵拠点)
        public readonly PPBattleDefinition Battle;

        // マスの深さ。バトルのマスは 1 段目から始まるため、段の番号をそのまま使う
        public int Depth => Node.x;

        public PPStageBattleRequest(Vector2Int aNode, PPStageNodeType aType, PPBattleDefinition aBattle)
        {
            Node = aNode;
            Type = aType;
            Battle = aBattle;
        }
    }

    // タイトルからマップに入るまでの画面で選んだ内容。マップのシーンを開いたときに、これで進行状態を始める
    public sealed class PPStageStartSelection
    {
        /* 定数 */

        private const string LogTag = "StageMap";

        /* static 変数 */

        // 選んでいる途中の内容。null なら選んでいない（マップのシーンを単体で Play したときなど）
        private static PPStageStartSelection sCurrent;

        /* 状態 */

        // 空ならマップ定義の初期パーティを使う
        private readonly List<PPPartySaveUnitEntry> mInitialUnits = new();

        /* プロパティ */

        public static PPStageStartSelection Current => sCurrent;

        public IReadOnlyList<PPPartySaveUnitEntry> InitialUnits => mInitialUnits;
        // まだ選んでいなければ null。null のままマップのシーンを開くと、そのシーンのマップ定義で始める
        public PPMapDefinition Definition { get; private set; }
        // null ならマップ定義の設定でシードを決める
        public int? Seed { get; private set; }

        /* static 関数 */

        // 選び直すときは前の内容を捨てて新しく始める
        public static PPStageStartSelection Begin()
        {
            sCurrent = new PPStageStartSelection();
            return sCurrent;
        }

        public static void Clear() => sCurrent = null;

        /* 操作 */

        // 所持ユニットの並びはこの順で編成に入る
        public void SetInitialUnits(IEnumerable<PPPartySaveUnitEntry> aUnits)
        {
            mInitialUnits.Clear();
            var names = new List<string>();
            foreach (var entry in aUnits)
            {
                if (entry.Unit == null) continue;
                mInitialUnits.Add(entry);
                names.Add($"{entry.Unit.DisplayName} Lv{entry.Level}");
            }
            CustomConsoleLog.Log(LogTag, $"初期ユニットを選択: {string.Join("、", names)}");
        }

        public void SetStage(PPMapDefinition aDefinition, int? aSeed = null)
        {
            Definition = aDefinition;
            Seed = aSeed;
            CustomConsoleLog.Log(LogTag, $"ステージを選択: {(aDefinition != null ? aDefinition.name : "-")} シード={(aSeed.HasValue ? aSeed.Value.ToString() : "マップ定義の設定")}");
        }

        /* ヘルパー */

        // Play を始めるたびに空に戻す。ドメインの再読み込みを切っているため、前回の Play の状態が残るのを防ぐ
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => sCurrent = null;
    }

    // マップのシード・通過したマス・所持ユニット・編成など、シーンをまたいで残す進行状態
    // メモリ上だけに持ち、Play を始めるたびに空に戻す
    public sealed class PPStageProgress : IPPPartySaveSource
    {
        /* 定数 */

        // 編成できるユニットコストの合計の上限
        public const int MaxPartyCost = 100;
        // 編成の枠の数
        public const int MaxPartySize = PPPartyFactory.MaxMembers;
        private const string LogTag = "StageMap";

        /* static 変数 */

        // 進行中の状態。null ならステージに入っていない（バトルのシーンを単体で Play したときなど）
        private static PPStageProgress sCurrent;

        /* 状態 */

        public readonly PPMapDefinition Definition;
        public readonly int Seed;
        // バトルが終わった後に戻るシーンの名前
        public string ReturnSceneName;

        // 通過したマスを通った順に持つ。最後が現在地
        private readonly List<Vector2Int> mPath = new();
        private readonly List<PPStageOwnedUnit> mOwnedUnits = new();
        // 枠ごとの編成。添字は枠の番号(0 から)。空の枠は null
        private readonly PPStageOwnedUnit[] mFormation = new PPStageOwnedUnit[MaxPartySize];

        /* プロパティ */

        public static PPStageProgress Current => sCurrent;

        public IReadOnlyList<Vector2Int> Path => mPath;
        public IReadOnlyList<PPStageOwnedUnit> OwnedUnits => mOwnedUnits;
        // まだマスを選んでいなければ null
        public Vector2Int? CurrentNode => mPath.Count > 0 ? mPath[^1] : null;
        // バトルのシーンで戦う内容。バトルのマスを選んでから決着するまでの間だけ null 以外
        public PPStageBattleRequest PendingBattle { get; private set; }
        // 編成に 1 体もいないとバトルを始められない
        public bool CanEnterBattle
        {
            get
            {
                foreach (var owned in mFormation)
                {
                    if (owned != null) return true;
                }
                return false;
            }
        }

        /* static 関数 */

        // 新しく始める。初期ユニットを渡せばそれを、空ならマップ定義の初期パーティを所持ユニットと編成にする
        public static PPStageProgress Begin(PPMapDefinition aDefinition, int aSeed, IReadOnlyList<PPPartySaveUnitEntry> aInitialUnits = null)
        {
            sCurrent = new PPStageProgress(aDefinition, aSeed, aInitialUnits);
            CustomConsoleLog.Log(LogTag, $"ステージ開始: {aDefinition.name} シード={aSeed} 所持ユニット={sCurrent.mOwnedUnits.Count}");
            return sCurrent;
        }

        // 進行状態を捨てる。次にマップを開くと最初からになる
        public static void Clear() => sCurrent = null;

        /* 生成 */

        // 渡された初期ユニットは選んだ順に枠の前から置き、枠が埋まったら控えにする
        private PPStageProgress(PPMapDefinition aDefinition, int aSeed, IReadOnlyList<PPPartySaveUnitEntry> aInitialUnits)
        {
            Definition = aDefinition;
            Seed = aSeed;

            var initial = aDefinition.InitialParty;
            if (aInitialUnits != null && aInitialUnits.Count > 0)
            {
                foreach (var entry in aInitialUnits)
                {
                    if (entry.Unit == null) continue;
                    var owned = new PPStageOwnedUnit(entry.Unit, Mathf.Max(1, entry.Level));
                    mOwnedUnits.Add(owned);
                    PlaceInFirstEmpty(owned);
                }
                return;
            }

            if (initial.Count == 0)
            {
                CustomConsoleLog.Error(LogTag, $"マップ定義 {aDefinition.name} の「初期パーティ」が空です。ユニットを並べてください");
                return;
            }

            foreach (var entry in initial)
            {
                if (entry == null || entry.Unit == null) continue;

                var owned = new PPStageOwnedUnit(entry.Unit, Mathf.Max(1, entry.Level));
                mOwnedUnits.Add(owned);
                PlaceInFirstEmpty(owned);
            }
        }

        /* 問い合わせ */

        // 枠のユニット。空の枠や範囲外なら null
        public PPStageOwnedUnit GetMember(int aSlot) => aSlot >= 0 && aSlot < MaxPartySize ? mFormation[aSlot] : null;

        // 編成されている枠の番号(0 から)を返す。編成されていなければ null
        public int? FindSlot(PPStageOwnedUnit aUnit)
        {
            for (int i = 0; i < MaxPartySize; i++)
            {
                if (aUnit != null && mFormation[i] == aUnit) return i;
            }
            return null;
        }

        public bool IsVisited(Vector2Int aNode) => mPath.Contains(aNode);

        public IReadOnlyList<PPPartySaveUnitEntry> GetOwnedUnits()
        {
            var list = new List<PPPartySaveUnitEntry>(mOwnedUnits.Count);
            foreach (var owned in mOwnedUnits) list.Add(new PPPartySaveUnitEntry(owned.Unit, owned.Level));
            return list;
        }

        // 今の編成のユニットコストの合計
        public int GetPartyCost()
        {
            int total = 0;
            foreach (var owned in mFormation) total += GetCost(owned);
            return total;
        }

        // 枠にユニットを置いた(入れ替えた)後も、ユニットコストの合計が上限以内に収まるか
        public bool CanSetMember(int aSlot, PPStageOwnedUnit aUnit)
        {
            // 外すときと、編成済みのユニット同士の入れ替えは合計が増えない
            if (aUnit == null || FindSlot(aUnit).HasValue) return true;
            int after = GetPartyCost() - GetCost(GetMember(aSlot)) + GetCost(aUnit);
            return after <= MaxPartyCost;
        }

        // 今の編成から味方のパーティを作る。枠の順に並べ、空の枠は飛ばす
        public PPBattleParty CreateAllyParty()
        {
            var members = new List<PPPartyMemberEntry>(MaxPartySize);
            foreach (var owned in mFormation)
            {
                if (owned != null) members.Add(new PPPartyMemberEntry { Unit = owned.Unit, Level = owned.Level });
            }
            return PPPartyFactory.CreateFromMembers(members, BattleSide.Ally);
        }

        /* 編成 */

        // 枠にユニットを置く。ほかの枠にいたユニットなら、その枠と入れ替える。null を渡すと枠を空にする
        // ユニットコストの合計が上限を超える置き方はしない
        public void SetMember(int aSlot, PPStageOwnedUnit aUnit)
        {
            if (aSlot < 0 || aSlot >= MaxPartySize) return;
            if (aUnit != null && !mOwnedUnits.Contains(aUnit))
            {
                CustomConsoleLog.Error(LogTag, $"所持していないユニット {aUnit.Unit?.DisplayName} は編成できません");
                return;
            }
            if (!CanSetMember(aSlot, aUnit))
            {
                CustomConsoleLog.Warning(LogTag, $"ユニットコストの合計が上限 {MaxPartyCost} を超えるため、{aUnit.Unit?.DisplayName} を編成できません");
                return;
            }

            var previous = mFormation[aSlot];
            var from = FindSlot(aUnit);
            if (from.HasValue) mFormation[from.Value] = previous;
            mFormation[aSlot] = aUnit;
        }

        /* 所持ユニット */

        // 所持ユニットに加え、編成に空きがあり、コストの上限にも収まれば前から最初の空きの枠に入れる
        public PPStageOwnedUnit AddUnit(PPUnitDefinition aUnit, int aLevel)
        {
            var owned = new PPStageOwnedUnit(aUnit, Mathf.Max(1, aLevel));
            mOwnedUnits.Add(owned);
            PlaceInFirstEmpty(owned);
            CustomConsoleLog.Log(LogTag, $"ユニット獲得: {aUnit.DisplayName} Lv{owned.Level} 枠={(FindSlot(owned) is int slot ? (slot + 1).ToString() : "控え")}");
            return owned;
        }

        // 編成の前から最初の空きの枠に置く。空きがないか、置くとユニットコストの合計が上限を超えるなら控えのまま
        private void PlaceInFirstEmpty(PPStageOwnedUnit aUnit)
        {
            if (GetPartyCost() + GetCost(aUnit) > MaxPartyCost) return;
            for (int i = 0; i < MaxPartySize; i++)
            {
                if (mFormation[i] != null) continue;
                mFormation[i] = aUnit;
                return;
            }
        }

        public void LevelUp(PPStageOwnedUnit aUnit)
        {
            aUnit.Level++;
            CustomConsoleLog.Log(LogTag, $"強化: {aUnit.Unit.DisplayName} Lv{aUnit.Level}");
        }

        /* 進行 */

        // マスを通過したことにして、現在地をそのマスへ進める
        public void CompleteNode(Vector2Int aNode)
        {
            if (!mPath.Contains(aNode)) mPath.Add(aNode);
        }

        // バトルのマスに入る。決着するまでは現在地を進めない
        public void BeginBattle(PPStageBattleRequest aRequest) => PendingBattle = aRequest;

        // バトルの決着を反映する。勝てば現在地を進め、ボスに勝つか負けたら進行状態を捨てる
        // ボスに勝ったときは、このステージのクリアを記録して次のステージを選べるようにする
        public PPStageBattleOutcome ResolveBattle(BattleResultType aResult)
        {
            var request = PendingBattle;
            PendingBattle = null;
            if (request == null) return PPStageBattleOutcome.Continue;

            if (aResult != BattleResultType.Victory)
            {
                CustomConsoleLog.Log(LogTag, $"敗北: 進行状態を捨てる (結果={aResult})");
                Clear();
                return PPStageBattleOutcome.Defeated;
            }

            CompleteNode(request.Node);
            if (request.Type != PPStageNodeType.Boss) return PPStageBattleOutcome.Continue;

            PPStageClearRecord.MarkCleared(Definition);
            CustomConsoleLog.Log(LogTag, "ステージクリア: 進行状態を捨てる");
            Clear();
            return PPStageBattleOutcome.Cleared;
        }

        /* ヘルパー */

        // Play を始めるたびに空に戻す。ドメインの再読み込みを切っているため、前回の Play の状態が残るのを防ぐ
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay() => sCurrent = null;

        private static int GetCost(PPStageOwnedUnit aUnit) => aUnit != null && aUnit.Unit != null ? aUnit.Unit.UnitCost : 0;
    }
}
