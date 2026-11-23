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
        public readonly PPPartyDefinition Enemy;
        // 敵の元になった遭遇。ボスでは null
        public readonly PPEncounterDefinition Encounter;

        public PPStageBattleRequest(Vector2Int aNode, PPStageNodeType aType, PPPartyDefinition aEnemy, PPEncounterDefinition aEncounter = null)
        {
            Node = aNode;
            Type = aType;
            Enemy = aEnemy;
            Encounter = aEncounter;
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
        public PPStageMapDefinition Definition { get; private set; }
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

        public void SetStage(PPStageMapDefinition aDefinition, int? aSeed = null)
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

        private const string LogTag = "StageMap";
        // 編成の配置。編成とコイン配布の順番（Center → Right → Left）で並べる
        private static readonly PPPartyPosition[] Positions =
            { PPPartyPosition.Center, PPPartyPosition.Right, PPPartyPosition.Left };

        /* static 変数 */

        // 進行中の状態。null ならステージに入っていない（バトルのシーンを単体で Play したときなど）
        private static PPStageProgress sCurrent;

        /* 状態 */

        public readonly PPStageMapDefinition Definition;
        public readonly int Seed;
        // バトルが終わった後に戻るシーンの名前
        public string ReturnSceneName;

        // 通過したマスを通った順に持つ。最後が現在地
        private readonly List<Vector2Int> mPath = new();
        private readonly List<PPStageOwnedUnit> mOwnedUnits = new();
        // 配置ごとの編成。添字は PPPartyPosition の値。空の配置は null
        private readonly PPStageOwnedUnit[] mFormation = new PPStageOwnedUnit[Positions.Length];

        /* プロパティ */

        public static PPStageProgress Current => sCurrent;

        public IReadOnlyList<Vector2Int> Path => mPath;
        public IReadOnlyList<PPStageOwnedUnit> OwnedUnits => mOwnedUnits;
        // まだマスを選んでいなければ null
        public Vector2Int? CurrentNode => mPath.Count > 0 ? mPath[^1] : null;
        // バトルのシーンで戦う内容。バトルのマスを選んでから決着するまでの間だけ null 以外
        public PPStageBattleRequest PendingBattle { get; private set; }
        // 中央に誰かいないとバトルを始められない
        public bool CanEnterBattle => GetMember(PPPartyPosition.Center) != null;

        /* static 関数 */

        // 新しく始める。初期ユニットを渡せばそれを、空ならマップ定義の初期パーティを所持ユニットと編成にする
        public static PPStageProgress Begin(PPStageMapDefinition aDefinition, int aSeed, IReadOnlyList<PPPartySaveUnitEntry> aInitialUnits = null)
        {
            sCurrent = new PPStageProgress(aDefinition, aSeed, aInitialUnits);
            CustomConsoleLog.Log(LogTag, $"ステージ開始: {aDefinition.name} シード={aSeed} 所持ユニット={sCurrent.mOwnedUnits.Count}");
            return sCurrent;
        }

        // 進行状態を捨てる。次にマップを開くと最初からになる
        public static void Clear() => sCurrent = null;

        /* 生成 */

        // 渡された初期ユニットは選んだ順に Center → Right → Left へ置き、4 体目からは控えにする
        private PPStageProgress(PPStageMapDefinition aDefinition, int aSeed, IReadOnlyList<PPPartySaveUnitEntry> aInitialUnits)
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

            if (initial == null)
            {
                CustomConsoleLog.Error(LogTag, $"マップ定義 {aDefinition.name} の「初期パーティ」が未設定です。味方のパーティ定義を割り当ててください");
                return;
            }

            foreach (var position in Positions)
            {
                var entry = initial.GetMember(position);
                if (entry == null || entry.Unit == null) continue;

                var owned = new PPStageOwnedUnit(entry.Unit, Mathf.Max(1, entry.Level));
                mOwnedUnits.Add(owned);
                mFormation[(int)position] = owned;
            }
        }

        /* 問い合わせ */

        public PPStageOwnedUnit GetMember(PPPartyPosition aPosition) => mFormation[(int)aPosition];

        // 編成されている配置を返す。編成されていなければ null
        public PPPartyPosition? FindPosition(PPStageOwnedUnit aUnit)
        {
            foreach (var position in Positions)
            {
                if (aUnit != null && mFormation[(int)position] == aUnit) return position;
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

        // 今の編成から味方のパーティを作る
        public PPBattleParty CreateAllyParty()
        {
            return PPPartyFactory.CreateFromMembers(aPosition =>
            {
                var owned = GetMember(aPosition);
                return owned == null ? null : new PPPartyMemberEntry { Unit = owned.Unit, Level = owned.Level };
            }, BattleSide.Ally);
        }

        /* 編成 */

        // 配置にユニットを置く。ほかの配置にいたユニットなら、その配置と入れ替える。null を渡すと配置を空にする
        public void SetMember(PPPartyPosition aPosition, PPStageOwnedUnit aUnit)
        {
            if (aUnit != null && !mOwnedUnits.Contains(aUnit))
            {
                CustomConsoleLog.Error(LogTag, $"所持していないユニット {aUnit.Unit?.DisplayName} は編成できません");
                return;
            }

            var previous = mFormation[(int)aPosition];
            var from = FindPosition(aUnit);
            if (from.HasValue) mFormation[(int)from.Value] = previous;
            mFormation[(int)aPosition] = aUnit;
        }

        /* 所持ユニット */

        // 所持ユニットに加え、編成に空きがあれば Center → Right → Left の順で最初の空きに入れる
        public PPStageOwnedUnit AddUnit(PPUnitDefinition aUnit, int aLevel)
        {
            var owned = new PPStageOwnedUnit(aUnit, Mathf.Max(1, aLevel));
            mOwnedUnits.Add(owned);
            PlaceInFirstEmpty(owned);
            CustomConsoleLog.Log(LogTag, $"ユニット獲得: {aUnit.DisplayName} Lv{owned.Level} 配置={FindPosition(owned)?.ToString() ?? "控え"}");
            return owned;
        }

        // 編成の Center → Right → Left のうち最初の空きに置く。空きがなければ控えのまま
        private void PlaceInFirstEmpty(PPStageOwnedUnit aUnit)
        {
            foreach (var position in Positions)
            {
                if (mFormation[(int)position] != null) continue;
                mFormation[(int)position] = aUnit;
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
    }
}
