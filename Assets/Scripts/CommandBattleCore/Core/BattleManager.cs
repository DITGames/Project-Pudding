/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file BattleManager.cs
 * @author hqrse
 * @date 2026/06/13
 * @brief バトル進行のコアクラス
 * =====================================*/
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CommandBattleCore
{
    public class BattleManager
    {
        // 1バトル当たりの情報を持つコンテキスト
        public BattleContext Context { get; private set; }
        // バトルステート
        public BattleStateMachine StateMachine { get; } = new();
        // 行動キュー
        public ActionQueue ActionQueue { get; } = new ActionQueue();
        // スキル演出プレゼンター
        public IBattlePresenter Presenter { get; set; }
        // 進行中の演出スキップ用。各コマンド演出ごとに作り直す
        public CancellationTokenSource PresentationCts { get; private set; }
        // リザルトチェッククラス
        public IBattleResultChecker ResultChecker { get; set; } = new DefaultBattleResultChecker();
        // バトルログクラス
        public IBattleLogger Logger { get; set; } = new DefaultBattleLogger();
        // ターンごとの行動順並び変えクラス
        public ITurnOrderResolver TurnOrderResolver { get; set; } = new SpeedTurnOrderResolver();
        // 1イベント当たりのリアクション上限
        public int MaxReactionPerEvent { get; set; } = 1;
        // トリガー処理で予約済みのリアクション総数
        private int mReactionsThisEvent = 0;
        private bool mInReactionDispatch = false;
        
        // ステート変更時(バトル状態)
        public event Action<BattleState> OnStateChanged;
        // ターン開始時など(ターン番号)
        public event Action<int> OnTickStarted;
        // ターン終了時など(ターン番号)
        public event Action<int> OnTickEnded;
        // コマンド実行直前(行動ユニット, 実行コマンド)
        public event Action<BattleUnit, BattleCommandBase> OnPreCommand;
        // コマンド実行直後(行動ユニット, 実行コマンド)
        public event Action<BattleUnit, BattleCommandBase> OnPostCommand;
        // コマンド追加時(行動ユニット, 実行コマンド)
        public event Action<BattleUnit, BattleCommandBase> OnCommandQueued;
        // コマンド実行時(行動ユニット, 実行コマンド)
        public event Action<BattleUnit, BattleCommandBase> OnCommandExecuted;
        // ダメージ時(対象ユニット, 値)
        public event Action<BattleUnit, float> OnDamageTaken;
        // 攻撃結果決定時(攻撃情報)
        public event Action<DamageInfo> OnDamageResolved;
        // 回復時(対象ユニット, 値)
        public event Action<BattleUnit, float> OnHealed;
        // ユニット撃破時(対象ユニット)
        public event Action<BattleUnit> OnUnitDefeated;
        // ユニット入れ替え時(退避ユニット, 参戦ユニット)
        public event Action<BattleUnit, BattleUnit> OnUnitSwapped;
        // ステータスエフェクト追加時(対象ユニット, エフェクト)
        public event Action<BattleUnit, StatusEffect> OnStatsEffectAdded;
        // ステータスエフェクト除去時(対象ユニット, エフェクト)
        public event Action<BattleUnit, StatusEffect> OnStatsEffectRemoved;
        // ステータスエフェクトスタック時(対象ユニット, エフェクト)
        public event Action<BattleUnit,  StatusEffect> OnStatusEffectStacked;
        // バトル終了時(リザルト)
        public event Action<BattleResult> OnBattleEnded;

        public Func<float> TimeProvider { get; set; } = () => 0f;

        public BattleManager()
        {
            StateMachine.OnStateChanged += (_, next) => OnStateChanged?.Invoke(next);
        }

        // コンテキストを渡してバトル開始
        public void StartBattle(BattleContext aContext)
        {
            Context = aContext ?? throw new ArgumentNullException(nameof(aContext));
            StateMachine.TransitionTo(BattleState.BattleStart);

            SubscribeParty(aContext.AllyParty);
            SubscribeParty(aContext.EnemyParty);

            foreach (var party in new[]{Context.AllyParty, Context.EnemyParty})
                foreach (var unit in party.ActiveMembers.Concat(party.ReserveMembers))
                    foreach (var skill in unit.Skills)
                        skill.ResetForBattle();

            aContext.AllyParty.OnSwapped += HandleSwap;
            aContext.EnemyParty.OnSwapped += HandleSwap;
        }

        // パーティイベントのサブスクライブ
        private void SubscribeParty(BattleParty aParty)
        {
            foreach (var unit in aParty.ActiveMembers) SubscribeUnit(unit);
            foreach (var unit in aParty.ReserveMembers) SubscribeUnit(unit);
        }

        // ユニットイベントのサブスクライブ
        private void SubscribeUnit(BattleUnit aUnit)
        {
            aUnit.OnDamaged += (u, dmg) =>
            {
                OnDamageTaken?.Invoke(u, dmg);
                Log(BattleLogType.Damage, null, u, $"{u.DisplayName} took {dmg} damage.");
            };
            aUnit.OnHealed += (u, amt) =>
            {
                OnHealed?.Invoke(u, amt);
                DispatchReactions(new ReactionContext(
                    ReactionTrigger.OnHealed, u, u));
                Log(BattleLogType.Heal, null, u, $"{u.DisplayName} recovered {amt} HP.");
            };
            aUnit.OnDefeated += u =>
            {
                OnUnitDefeated?.Invoke(u);
                DispatchReactions(new ReactionContext(
                    ReactionTrigger.OnUnitDefeated, null, u));
                Log(BattleLogType.UnitDefeated, null, u, $"{u.DisplayName} was defeated.");
                CheckBattleResult();
            };
            aUnit.OnStatusEffectAdded += (u, e) =>
            {
                OnStatsEffectAdded?.Invoke(u, e);
                DispatchReactions(new ReactionContext(
                    ReactionTrigger.OnStatusAdded, null, u));
                Log(BattleLogType.StatusEffect, null, u, $"{u.DisplayName} gained {e.DisplayName}.");
            };
            aUnit.OnStatusEffectRemoved += (u, e) =>
            {
                OnStatsEffectRemoved?.Invoke(u, e);
                Log(BattleLogType.StatusEffect, null, u, $"{u.DisplayName} lost {e.DisplayName}.");
            };
            aUnit.OnStatusEffectStacked += (u, e) =>
            {
                OnStatusEffectStacked?.Invoke(u, e);
                Log(BattleLogType.StatusEffect, null, u, $"{u.DisplayName}'s {e.DisplayName} stacked ({e.CurrentStacks}).");
            };
            aUnit.OnDamageResolved += info =>
            {
                OnDamageResolved?.Invoke(info);
                DispatchReactions(new ReactionContext(
                    ReactionTrigger.OnDamaged, info.Source, info.Target, info));
            };
        }

        // 入れ替えイベント
        private void HandleSwap(BattleUnit aOut, BattleUnit aIn)
        {
            OnUnitSwapped?.Invoke(aOut, aIn);
            Log(BattleLogType.Swap, aOut, aIn, $"{aOut.DisplayName} swapped out for {aIn.DisplayName}.");
        }

        // コマンドをキューに積む
        // 積むタイミングは採用先による
        public void EnqueueCommand(BattleCommandBase aCommand)
        {
            ActionQueue.Enqueue(aCommand);
            OnCommandQueued?.Invoke(aCommand.Source, aCommand);
            Log(BattleLogType.Action, aCommand.Source, null, $"{aCommand.Source.DisplayName} queued {aCommand.GetType().Name}.");
        }
        
        // キューの先頭のコマンドを実行する
        public bool ExecuteNextCommand()
        {
            if(StateMachine.Current == BattleState.BattleEnd) return false;
            if (!ActionQueue.TryDequeue(out var command)) return false;

            // 実行ステートに遷移
            StateMachine.TransitionTo(BattleState.ActionExecution);
            OnPreCommand?.Invoke(command.Source, command);

            // オブジェクトが生存 + 行動不可の制限がかかっていなければ実行する
            // 麻痺などの確率による行動制限がある場合は別で判別する必要がありそう
            if (command.Source.IsAlive && (command.Source.CurrentRestrictions & ActionRestriction.CannotAct) == 0)
            {
                command.Execute(Context);
                OnCommandExecuted?.Invoke(command.Source, command);
            }
            
            // 行動回数の消費
            command.Source.Actions.Consume();
            OnPostCommand?.Invoke(command.Source, command);

            // ステートマシンをリザルトチェックに移行
            // 演出を入れる場合演出終了をリッスンしてそのあとにチェックするほうがよさそう
            StateMachine.TransitionTo(BattleState.ResultCheck);
            CheckBattleResult();

            if (StateMachine.Current != BattleState.BattleEnd)
            {
                StateMachine.TransitionTo(BattleState.CommandInput);
            }

            return true;
        }

        // キューが空になるまで連続実行
        // ターン制向けのユーティリティ
        // 演出待ちがないので注意
        public void ExecuteAllCommands()
        {
            while (StateMachine.Current != BattleState.BattleEnd && ExecuteNextCommand())
            {
                
            }
        }
        
        // async版 単発キュー実行
        public async ValueTask ExecuteNextCommandAsync(CancellationToken aCt = default)
        {
            if (StateMachine.Current == BattleState.BattleEnd) return;
            if(!ActionQueue.TryDequeue(out var command)) return;
            
            StateMachine.TransitionTo(BattleState.ActionExecution);
            
            // コマンド実行前イベントの通知
            OnPreCommand?.Invoke(command.Source, command);
            
            if (command.Source.IsAlive && (command.Source.CurrentRestrictions & ActionRestriction.CannotAct) == 0)
            {
                // コマンド実行前演出
                if (Presenter != null)
                {
                    await SafePlaySkillPresentation(Presenter.PlayPreExecute(command, Context, aCt));
                }
                
                // 実際のコマンド実行
                command.Execute(Context);
                OnCommandExecuted?.Invoke(command.Source, command);

                // コマンド実行後演出
                if (Presenter != null)
                {
                    await SafePlaySkillPresentation(Presenter.PlayPostExecute(command, Context, aCt));
                }
            }
            
            // コマンド実行後イベントの通知
            command.Source.Actions.Consume();
            OnPostCommand?.Invoke(command.Source, command);
            
            StateMachine.TransitionTo(BattleState.ResultCheck);
            CheckBattleResult();
            if(StateMachine.Current != BattleState.BattleEnd)
                StateMachine.TransitionTo(BattleState.CommandInput);
        }
        
        // async版 全キュー実行
        public async ValueTask ExecuteAllCommandAsync(CancellationToken aCt = default)
        {
            while (StateMachine.Current != BattleState.BattleEnd && ActionQueue.Count > 0)
            {
                await ExecuteNextCommandAsync(aCt);
            }
        }
        
        // 進行中のスキル演出をスキップする
        public void SkipCurrentPresentation() => PresentationCts?.Cancel();

        // スキル演出実行
        private static async ValueTask SafePlaySkillPresentation(ValueTask aPlay)
        {
            try
            {
                await aPlay;
            }
            catch (System.OperationCanceledException)
            {
                // スキップされた場合。演出は中断
            }
        }
        
        // リアクショントリガー発生
        private void DispatchReactions(ReactionContext aContext)
        {
            bool outermost = !mInReactionDispatch;
            mInReactionDispatch = true;
            try
            {
                foreach (var unit in EnumerateAllAliveUnits())
                {
                    if (mReactionsThisEvent >= MaxReactionPerEvent) break;
                    foreach (var reaction in unit.Reactions.ToArray())
                    {
                        if (reaction.Trigger != aContext.Trigger) continue;
                        if (!unit.IsAlive) break;
                        if (!reaction.ShouldReact(unit, aContext, Context)) continue; // ユニットのリアクションが条件を満たすのかセルフチェック

                        var cmd = reaction.BuildReaction(unit, aContext, Context);
                        if (cmd == null) continue;

                        ActionQueue.EnqueueFront(cmd); // 先頭に割り込み
                        mReactionsThisEvent++;
                    }
                }
            }
            finally
            {
                if (outermost)
                {
                    mInReactionDispatch = false;
                    mReactionsThisEvent = 0;
                }
            }
        }

        private IEnumerable<BattleUnit> EnumerateAllAliveUnits()
        {
            foreach (var unit in Context.AllyParty.GetAliveActiveMembers()) yield return unit;
            foreach (var unit in Context.EnemyParty.GetAliveActiveMembers()) yield return unit;
        }
        
        // ターン経過処理
        // ターン制の場合はそのままターンごとに進めてATBなどの場合は1秒ごとなどで進める
        public void AdvanceTick()
        {
            OnTickEnded?.Invoke(Context.TurnCount);
            DispatchReactions(new ReactionContext(
                ReactionTrigger.OnTurnEnded, null, null, null, Context));
            
            TickParty(Context.AllyParty);
            TickParty(Context.EnemyParty);
            
            CheckBattleResult();
            if (StateMachine.Current == BattleState.BattleEnd) return;

            Context.TurnCount++;
            OnTickStarted?.Invoke(Context.TurnCount);
            DispatchReactions(new ReactionContext(
                ReactionTrigger.OnTurnStarted, null, null, null, Context));
        }

        // パーティの状態更新
        private void TickParty(BattleParty aParty)
        {
            foreach(var unit in aParty.GetAliveActiveMembers())
            {
                unit.TickStatusEffects(Context);
                unit.Actions.ResetForTurn();   // ゲーム性によって呼び出しタイミングを変えるべき
                foreach (var skill in unit.Skills)
                {
                    skill.TickCooldown();
                }
            }
        }
        
        // 行動準取得
        public List<BattleUnit> GetTurnOrder() => TurnOrderResolver.ResolveOrder(Context);

        // 勝敗チェック
        public BattleResult CheckBattleResult()
        {
            if(Context == null || StateMachine.Current == BattleState.BattleEnd) return null;

            var result = ResultChecker.CheckResult(Context);
            if (result.Type != BattleResultType.InProgress)
            {
                EndBattle(result);
            }

            return result;
        }

        // バトル終了
        private void EndBattle(BattleResult aResult)
        {
            ActionQueue.Clear();
            StateMachine.TransitionTo(BattleState.BattleEnd);
            Log(aResult.Type == BattleResultType.Escaped ? BattleLogType.Escape : BattleLogType.Custom, null, null,
                $"Battle ended: {aResult.Type}");
            OnBattleEnded?.Invoke(aResult);
        }

        // ログヘルパー
        private void Log(BattleLogType aType, BattleUnit aUnit, BattleUnit aTarget, string aDescription)
        {
            Logger?.Log(new BattleLogEntry(aType, aUnit, aTarget, aDescription, TimeProvider()));
        }
    }
}