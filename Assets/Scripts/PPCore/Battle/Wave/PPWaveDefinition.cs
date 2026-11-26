/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPWaveDefinition.cs
 * @author hqrse
 * @date 2026/11/24
 * @brief ウェーブの定義(段階の一覧)と、その目安(敵の数・長さ・無限)・点検と誤り
 * =====================================*/

using System.Collections.Generic;
using AttributeUtility;
using UnityEngine;

namespace PPCore
{
    // ウェーブの定義の誤り 1 件。画面で印を付けられるよう、どの段階の誤りかを添える
    public class PPWaveProblem
    {
        /* プロパティ */

        public string Message { get; }
        // 誤りのある段階の識別子。段階に結び付かなければ null
        public string StageId { get; }

        /* 生成 */

        public PPWaveProblem(string aMessage, string aStageId = null)
        {
            Message = aMessage;
            StageId = aStageId;
        }
    }

    // バトル定義が持つウェーブ。段階を並べ、段階ごとに出す敵・始まる条件・行き先を持つ
    [System.Serializable]
    public class PPWaveDefinition
    {
        /* 定数 */

        // 出し終えるまでの秒数をすぐ(0 秒)とみなす幅
        private const float InstantEpsilon = 0.0001f;

        /* インスペクタ設定 */

        [Label("段階", true)]
        [SerializeField] private List<PPWaveStage> mStages = new();

        /* プロパティ */

        public IReadOnlyList<PPWaveStage> Stages => mStages;

        /* 生成 */

        public PPWaveDefinition() { }

        public PPWaveDefinition(IEnumerable<PPWaveStage> aStages) => mStages = new List<PPWaveStage>(aStages);

        /* 問い合わせ */

        // 識別子の段階の番号(0 から)。見つからなければ -1
        public int IndexOf(string aId)
        {
            if (string.IsNullOrEmpty(aId)) return -1;
            for (int i = 0; i < mStages.Count; i++)
            {
                if (mStages[i] != null && mStages[i].Id == aId) return i;
            }
            return -1;
        }

        // 識別子の段階。見つからなければ null
        public PPWaveStage FindStage(string aId)
        {
            int index = IndexOf(aId);
            return index >= 0 ? mStages[index] : null;
        }

        // その段階を行き先にしている段階があるか
        public bool HasIncoming(PPWaveStage aStage)
        {
            foreach (var stage in mStages)
            {
                if (stage == null) continue;
                foreach (var next in stage.Nexts)
                {
                    if (next != null && next.TargetId == aStage.Id) return true;
                }
            }
            return false;
        }

        // 出てくるユニットの種類。同じユニットは 1 度だけ並べる
        public List<PPUnitDefinition> CollectUnits()
        {
            var units = new List<PPUnitDefinition>();
            foreach (var stage in mStages)
            {
                if (stage == null) continue;
                foreach (var spawn in stage.Spawns)
                {
                    if (spawn?.Unit != null && !units.Contains(spawn.Unit)) units.Add(spawn.Unit);
                }
            }
            return units;
        }

        // 各段階を 1 回ずつ出したときの敵の数(無限の行は数えない)
        public int CountFiniteEnemies()
        {
            int count = 0;
            foreach (var stage in mStages)
            {
                if (stage != null) count += stage.FiniteEnemyCount;
            }
            return count;
        }

        // 数が 0(無限)の行を持つ段階があるか
        public bool HasInfinite()
        {
            foreach (var stage in mStages)
            {
                if (stage != null && stage.IsInfinite) return true;
            }
            return false;
        }

        // 長さの目安(秒)。時間の条件だけで進んだとして、各段階を最初に 1 度出し終えるまで。全滅・被弾・HP でしか始まらない段階は数えない
        public float EstimateLength()
        {
            var startTimes = EstimateStartTimes();
            float length = 0f;
            for (int i = 0; i < mStages.Count; i++)
            {
                if (startTimes[i] < float.PositiveInfinity) length = Mathf.Max(length, startTimes[i] + mStages[i].SpawnDuration);
            }
            return length;
        }

        // 段階ごとの、時間の条件だけで進んだときに最初に始まる時刻。始まらなければ +∞
        public float[] EstimateStartTimes()
        {
            var starts = new float[mStages.Count];
            var armed = new float[mStages.Count];
            var done = new bool[mStages.Count];
            for (int i = 0; i < mStages.Count; i++)
            {
                starts[i] = float.PositiveInfinity;
                armed[i] = mStages[i] != null && mStages[i].WaitsFromBattleStart ? 0f : float.PositiveInfinity;
            }
            // 始まる時刻の早い順に確定させる(行き先は始まり・出し終えより後にしか待ち始めないため、早い順で決まる)
            while (true)
            {
                int best = -1;
                float bestTime = float.PositiveInfinity;
                for (int i = 0; i < mStages.Count; i++)
                {
                    if (done[i] || armed[i] == float.PositiveInfinity) continue;
                    float time = armed[i] + GetTimeOnlyWait(mStages[i]);
                    if (time < bestTime)
                    {
                        best = i;
                        bestTime = time;
                    }
                }
                if (best < 0) break;
                done[best] = true;
                starts[best] = bestTime;
                foreach (var next in mStages[best].Nexts)
                {
                    int target = IndexOf(next?.TargetId);
                    if (target < 0 || done[target] || next.Timing == PPWaveNextTiming.Wiped) continue;
                    if (next.Timing == PPWaveNextTiming.Finished && mStages[best].IsInfinite) continue;
                    float armTime = next.Timing == PPWaveNextTiming.Finished ? bestTime + mStages[best].SpawnDuration : bestTime;
                    armed[target] = Mathf.Min(armed[target], armTime);
                }
            }
            return starts;
        }

        // 時間の条件だけを見たときの、待ち始めてから始まるまでの秒数。条件が無ければ 0、時間の条件が無ければ +∞
        private static float GetTimeOnlyWait(PPWaveStage aStage)
        {
            if (aStage.StartConditions.Count == 0) return 0f;
            float wait = float.PositiveInfinity;
            foreach (var condition in aStage.StartConditions)
            {
                if (condition != null && condition.Kind == PPWaveStartKind.Elapsed) wait = Mathf.Min(wait, condition.Seconds);
            }
            return wait;
        }

        /* 説明文 */

        // 段階の名前。名前が空なら番号、見つからなければ「(見つからない段階)」
        public string GetStageName(string aId)
        {
            int index = IndexOf(aId);
            if (index < 0) return "(見つからない段階)";
            return string.IsNullOrEmpty(mStages[index].Name) ? $"段階 {index + 1}" : mStages[index].Name;
        }

        // 「番号 名前」の形。見つからなければ「(見つからない段階)」
        public string GetStageLabel(string aId)
        {
            int index = IndexOf(aId);
            return index < 0 ? "(見つからない段階)" : $"{index + 1} {GetStageName(aId)}";
        }

        // 時間の条件の起点の言い方。バトル開始からだけ待つ段階は「バトル開始」、行き先にされるだけなら「前の段階」、両方なら「待ち始め」
        public string GetWaitOriginText(PPWaveStage aStage)
        {
            bool hasIncoming = HasIncoming(aStage);
            if (aStage.WaitsFromBattleStart) return hasIncoming ? "待ち始め" : "バトル開始";
            return "前の段階";
        }

        // 段階の一覧に出す、始まり方の要約。前の段階へ戻る行き先があれば「→ 3 援軍へ戻る」を足す
        public string DescribeStart(PPWaveStage aStage)
        {
            string text;
            if (aStage.StartConditions.Count == 0)
            {
                text = aStage.WaitsFromBattleStart ? (HasIncoming(aStage) ? "バトル開始・前の段階のすぐあと" : "バトル開始") : "前の段階のすぐあと";
            }
            else
            {
                var parts = new List<string>();
                foreach (var condition in aStage.StartConditions) parts.Add(condition.Describe(this, aStage, true));
                text = string.Join(" か ", parts);
                if (aStage.WaitsFromBattleStart) text = $"バトル開始から {text}";
            }
            if (!aStage.WaitsFromBattleStart && !HasIncoming(aStage)) text += "(どこからも始まらない)";

            int own = IndexOf(aStage.Id);
            foreach (var next in aStage.Nexts)
            {
                int target = IndexOf(next?.TargetId);
                if (target >= 0 && target <= own) return $"{text} → {GetStageLabel(next.TargetId)}へ戻る";
            }
            return text;
        }

        /* 評価・判定 */

        // 定義の誤りを文章にして aProblems に足す。値は直さない
        public void Validate(List<string> aProblems)
        {
            var problems = new List<PPWaveProblem>();
            Validate(problems);
            foreach (var problem in problems) aProblems.Add(problem.Message);
        }

        // 定義の誤りを、どの段階の誤りかを添えて aProblems に足す。値は直さない
        public void Validate(List<PPWaveProblem> aProblems)
        {
            var ids = new HashSet<string>();
            bool hasEntry = false;
            for (int i = 0; i < mStages.Count; i++)
            {
                var stage = mStages[i];
                if (stage == null) continue;
                string label = $"段階 {i + 1}「{stage.Name}」";
                if (string.IsNullOrEmpty(stage.Id)) aProblems.Add(new PPWaveProblem($"{label}の識別子が空です。段階を消して足し直してください"));
                else if (!ids.Add(stage.Id)) aProblems.Add(new PPWaveProblem($"{label}の識別子が他の段階と重なっています。ウェーブエディタで複製し直してください", stage.Id));
                if (stage.WaitsFromBattleStart) hasEntry = true;

                for (int s = 0; s < stage.Spawns.Count; s++)
                {
                    if (stage.Spawns[s]?.Unit == null) aProblems.Add(new PPWaveProblem($"{label}: 出す敵の {s + 1} 行目のユニットが選ばれていません", stage.Id));
                }
                foreach (var condition in stage.StartConditions)
                {
                    if (condition == null || condition.Kind != PPWaveStartKind.StageWiped) continue;
                    var target = FindStage(condition.StageId);
                    if (target == null) aProblems.Add(new PPWaveProblem($"{label}: 始まる条件「全滅」の対象の段階が見つかりません", stage.Id));
                    else if (target.IsInfinite) aProblems.Add(new PPWaveProblem($"{label}: 始まる条件の「{GetStageLabel(target.Id)}」は数が無限の敵を出すため、全滅しません", stage.Id));
                }
                for (int n = 0; n < stage.Nexts.Count; n++)
                {
                    var next = stage.Nexts[n];
                    if (next == null) continue;
                    if (FindStage(next.TargetId) == null) aProblems.Add(new PPWaveProblem($"{label}: 行き先 {n + 1} の段階が見つかりません", stage.Id));
                    if (stage.IsInfinite && next.Timing != PPWaveNextTiming.Started) aProblems.Add(new PPWaveProblem($"{label}: 数が無限の敵を出すため、出し終えることも全滅することもありません。行き先 {n + 1} は「出し始めたら」にしてください", stage.Id));
                }
            }
            if (mStages.Count > 0 && !hasEntry) aProblems.Add(new PPWaveProblem("「バトル開始から待つ」段階が 1 つもないため、どの段階も始まりません"));

            if (hasEntry)
            {
                var reached = CollectReachableStages();
                foreach (var stage in mStages)
                {
                    if (stage != null && !reached.Contains(stage)) aProblems.Add(new PPWaveProblem($"段階「{GetStageName(stage.Id)}」は、バトル開始から行き先をたどっても届かないため始まりません", stage.Id));
                }
            }

            var looped = FindInstantLoopStage(false);
            if (looped != null)
            {
                aProblems.Add(new PPWaveProblem($"段階「{GetStageName(looped.Id)}」を通る繰り返しに待ちがないため、1 フレームに 1 回ずつ始まり続けます。時間の条件などを足してください", looped.Id));
                return;
            }
            var mayLoop = FindInstantLoopStage(true);
            if (mayLoop != null)
            {
                aProblems.Add(new PPWaveProblem($"段階「{GetStageName(mayLoop.Id)}」を通る繰り返しは、条件によっては待ちがなくなり、1 フレームに 1 回ずつ始まり続けます(待ち始めた時点で満たしている拠点の HP の条件や、すぐ出し終える段階を通るとき)。時間の条件などを足してください", mayLoop.Id));
            }
        }

        // バトル開始から待つ段階から、行き先をたどって届く段階
        private HashSet<PPWaveStage> CollectReachableStages()
        {
            var reached = new HashSet<PPWaveStage>();
            var stack = new Stack<PPWaveStage>();
            foreach (var stage in mStages)
            {
                if (stage != null && stage.WaitsFromBattleStart && reached.Add(stage)) stack.Push(stage);
            }
            while (stack.Count > 0)
            {
                foreach (var next in stack.Pop().Nexts)
                {
                    var target = FindStage(next?.TargetId);
                    if (target != null && reached.Add(target)) stack.Push(target);
                }
            }
            return reached;
        }

        // 待ちを含まない行き先だけでできた繰り返しにある段階を 1 つ返す。無ければ null
        // aIncludesPossible なら、条件によってはその場で進む行き先・その場で始まる段階も待ちの無いものとして数える
        private PPWaveStage FindInstantLoopStage(bool aIncludesPossible)
        {
            // 0 = 未訪問, 1 = たどっている途中, 2 = 調べ終えた
            var marks = new Dictionary<PPWaveStage, int>();
            foreach (var stage in mStages)
            {
                if (stage == null || marks.ContainsKey(stage)) continue;
                var found = VisitInstant(stage, marks, aIncludesPossible);
                if (found != null) return found;
            }
            return null;
        }

        private PPWaveStage VisitInstant(PPWaveStage aStage, Dictionary<PPWaveStage, int> aMarks, bool aIncludesPossible)
        {
            aMarks[aStage] = 1;
            foreach (var next in aStage.Nexts)
            {
                var target = FindStage(next?.TargetId);
                if (target == null || !IsInstantNext(aStage, next, aIncludesPossible) || !CanStartInstantly(target, aIncludesPossible)) continue;
                int mark = aMarks.GetValueOrDefault(target);
                if (mark == 1) return target;
                if (mark == 0)
                {
                    var found = VisitInstant(target, aMarks, aIncludesPossible);
                    if (found != null) return found;
                }
            }
            aMarks[aStage] = 2;
            return null;
        }

        // 段階が始まったその場で進む行き先か。必ず進むのは条件の無い「出し始めたら」
        // aIncludesPossible なら、条件付きの行き先と、始まったフレームのうちに出し終える段階(敵が無いか、どの行も 1 体・最初の待ち 0)の「出し終えたら」も数える
        private static bool IsInstantNext(PPWaveStage aStage, PPWaveNext aNext, bool aIncludesPossible)
        {
            bool isInstantFinish = aNext.Timing == PPWaveNextTiming.Finished && !aStage.IsInfinite && aStage.SpawnDuration <= InstantEpsilon;
            bool isInstantTiming = aNext.Timing == PPWaveNextTiming.Started || (aIncludesPossible && isInstantFinish);
            return isInstantTiming && (aNext.Condition == PPWaveNextCondition.None || aIncludesPossible);
        }

        // 待ち始めたその場で始まり得る段階か。条件が無いか、0 秒の時間の条件があれば必ず始まる
        // aIncludesPossible なら、拠点の HP の条件も数える。「1 度だけ」の条件は繰り返しを続けられないため数えない
        private static bool CanStartInstantly(PPWaveStage aStage, bool aIncludesPossible)
        {
            if (aStage.StartConditions.Count == 0) return true;
            foreach (var condition in aStage.StartConditions)
            {
                if (condition == null || condition.IsOnce) continue;
                if (condition.Kind == PPWaveStartKind.Elapsed && condition.Seconds <= 0f) return true;
                if (aIncludesPossible && condition.Kind == PPWaveStartKind.BaseHp) return true;
            }
            return false;
        }
    }
}
