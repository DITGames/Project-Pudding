/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPRuntimeTactics.cs
 * @author hqrse
 * @date 2026/08/11
 * @brief 戦術定義のランタイム側。進行状況を保持する
 * =====================================*/

namespace PPCore
{
    // 戦術定義に進行状況を持たせたランタイム側の実体
    // 戦術ベース AI の要になるクラスで、「今この戦術のどこまで実行したか」を思考をまたいで保持する
    // これが無いと、バフを付けてから大技を撃つような複数手順の戦術が
    // 1 回の思考で完了しなかった場合に毎回先頭からやり直され、永久にバフを掛け続けることになる
    // 進行位置は保持しつつ、再開時にはステップの達成済み判定で開始位置を決め直すため、
    // 途中でバフが切れていれば自然とバフのステップへ戻る
    public sealed class PPRuntimeTactics
    {
        // 参照する戦術定義
        public PPBattleTacticsDefinition Definition { get; }
        // プロファイルの戦術リスト内の位置。小さいほど優先度が高い
        public int Priority { get; }

        // 現在の進行位置。0 なら未着手
        public int StepIndex { get; private set; }
        // 現在ステップの解決結果。思考のたびに入れ替わる
        public PPTacticStepResolution CurrentResolution { get; private set; }
        // 直前に実行したステップが解決した対象。「直前ステップと同じ対象」の解決に使う
        public PPBattleUnit PreviousTarget { get; private set; }
        // 残クールタイム（ティック）
        public int RemainingCooldown { get; private set; }
        // 1 バトル 1 回の戦術を消化済みか
        public bool IsDoneOnce { get; private set; }
        // この思考で候補外になった理由。デバッグ表示用
        public PPTacticRejectReason LastRejectReason { get; private set; }
        // 待ち判定で見積もった「撃てるまでのティック数」。デバッグ表示用
        public float EstimatedWaitTicks { get; private set; }
        // 現在ステップが今すぐ実行できるか。false なら溜めて待つ判断になっている
        public bool IsAffordableNow { get; private set; }

        // 着手済みかどうか。進行中の戦術を優先して継続するかの判定に使う
        public bool IsInProgress => StepIndex > 0;
        // まだ実行していないステップが残っているか
        public bool HasRemainingStep => StepIndex < Definition.Steps.Count;

        // aDefinition : 参照する戦術定義
        // aPriority : プロファイルの戦術リスト内の位置
        public PPRuntimeTactics(PPBattleTacticsDefinition aDefinition, int aPriority)
        {
            Definition = aDefinition;
            Priority = aPriority;
        }

        // この思考での判定結果を記録する
        // aResolution : 現在ステップの解決結果。解決できなかった場合は null
        // aReason : 候補外になった理由。実行可能なら None
        // aEstimatedWaitTicks : 撃てるまでの見積もりティック数
        // aIsAffordableNow : 今すぐ実行できるか
        public void SetThinkResult(PPTacticStepResolution aResolution, PPTacticRejectReason aReason,
            float aEstimatedWaitTicks, bool aIsAffordableNow)
        {
            CurrentResolution = aResolution;
            LastRejectReason = aReason;
            EstimatedWaitTicks = aEstimatedWaitTicks;
            IsAffordableNow = aIsAffordableNow;
        }

        // 進行位置を指定のステップへ合わせる
        // 達成済みステップのスキップ結果を反映するために使う
        // aIndex : 開始するステップの位置
        public void SetStepIndex(int aIndex) => StepIndex = aIndex;

        // ステップを 1 つ消化して進める
        // 直前対象を更新するため、後続ステップの「直前ステップと同じ対象」がここを参照する
        // aResolution : 消化したステップの解決結果
        public void AdvanceStep(PPTacticStepResolution aResolution)
        {
            if (aResolution?.Target != null)
            {
                PreviousTarget = aResolution.Target;
            }
            StepIndex++;
        }

        // 完走・中断時の終了処理
        // クールタイムを開始し、1 バトル 1 回の戦術なら消化済みにして進行を巻き戻す
        // クールタイムを開始時ではなくここで数え始めるため、複数ティックにわたる長い戦術でも
        // 意図した間隔が確実に空く
        public void Complete()
        {
            if (Definition.IsDoOnce)
            {
                IsDoneOnce = true;
            }
            RemainingCooldown = Definition.CooldownTicks;
            ResetProgress();
        }

        // 進行だけを巻き戻す。クールタイムと 1 バトル 1 回の消化状況は触らない
        public void ResetProgress()
        {
            StepIndex = 0;
            CurrentResolution = null;
            PreviousTarget = null;
        }

        // ティックが 1 つ進んだときのクールタイム消化
        public void TickCooldown()
        {
            if (RemainingCooldown > 0) RemainingCooldown--;
        }

        // バトル開始時のリセット。進行・クールタイム・1 バトル 1 回の消化状況をすべて戻す
        public void ResetForBattle()
        {
            ResetProgress();
            RemainingCooldown = 0;
            IsDoneOnce = false;
            LastRejectReason = PPTacticRejectReason.None;
            EstimatedWaitTicks = 0f;
            IsAffordableNow = false;
        }
    }
}
