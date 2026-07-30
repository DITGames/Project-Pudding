/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleCommandInputController.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief コマンド入力のコントローラー
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace PPCore
{
    // プレイヤーのコマンド入力を、スタックベースのステートマシンで管理するコントローラー
    // ユニット選択 → コマンド選択 → スキル選択 → 対象選択、と IPPBattleInputState を
    // 積み上げていき、確定した時点でコマンドを組み立てて BattleManager のキューへ流す
    // キャンセル（戻る）はスタックを 1 段ポップすることで表現され、空になれば入力自体を中断する
    // 選択途中の状態（選択中ユニット・スキル・対象）は自身では持たず PPBattleSelectionContext に集約する
    // 各ステートはこのコントローラー経由で View やコンテキストへアクセスする
    public class PPBattleCommandInputController : MonoBehaviour
    {
        [Label("ユニットビューバインダー")]
        [SerializeField] private PPBattleUnitViewBinder mViewBinder;
        [Label("コマンドメニュー")]
        [SerializeField] private PPBattleCommandMenuView mCommandMenu;
        [Label("スキルメニュー")]
        [SerializeField] private PPBattleSkillMenuView mSKillMenu;
        [Label("詳細ビュー")]
        [SerializeField] private PPBattleDetailMenuView mDetailMenu;
        [Label("戻る")]
        [SerializeField] private Button mBackButton;
        [Label("UI入力モジュール")]
        [SerializeField] private InputSystemUIInputModule mUIInputModule;
        [Label("入力ステートボーダー")]
        [SerializeField] private Image mInputStateBorder;

        // コマンドの投入先。Bind で受け取る
        private BattleManager mManager;
        // 入力ステートのスタック。先頭が現在アクティブなステート
        private readonly Stack<IPPBattleInputState> mStateStack = new();
        // 選択途中の内容（ユニット・スキル・対象・コマンド生成関数）を保持するコンテキスト
        private readonly PPBattleSelectionContext mContext = new();

        // コマンド確定時(行動ユニット, 確定したコマンド)。キュー投入前に発火する
        public event Action<BattleUnit, BattleCommandBase> OnCommandConfirmed;
        // コマンドをキューへ流し終えたとき。入力 1 サイクルの完了通知
        public event Action OnCommandFlushed;

        // バインドされているバトルマネージャ
        public BattleManager Manager => mManager;
        // ユニットとビューの対応付け。各ステートが対象選択に使う
        public PPBattleUnitViewBinder ViewBinder => mViewBinder;
        // コマンドメニューのビュー
        public PPBattleCommandMenuView CommandMenu => mCommandMenu;
        // スキルメニューのビュー
        public PPBattleSkillMenuView SkillMenu => mSKillMenu;
        // ユニット詳細のビュー
        public PPBattleDetailMenuView DetailMenu => mDetailMenu;
        // 選択途中の状態を保持するコンテキスト
        public PPBattleSelectionContext Context => mContext;

        // バトルマネージャを紐づけ、戻るボタンとキャンセル入力を購読する
        // aManager : 確定したコマンドの投入先
        public void Bind(BattleManager aManager)
        {
            mManager = aManager;
            if (mBackButton != null)
            {
                mBackButton.onClick.AddListener(Back);
            }

            if (mUIInputModule != null)
            {
                mUIInputModule.cancel.action.performed += HandleCancelPerformed;
            }
        }

        // 破棄時にキャンセル入力の購読を解除する
        private void OnDestroy()
        {
            if (mUIInputModule != null)
            {
                mUIInputModule.cancel.action.performed -= HandleCancelPerformed;
            }
        }

        // キャンセル入力を受けて 1 段戻る。入力中でなければ何もしない
        // ctx : Input System のコールバックコンテキスト
        private void HandleCancelPerformed(InputAction.CallbackContext ctx)
        {
            if (mStateStack.Count > 0)
            {
                Back();
            }
        }

        // コマンド入力を開始する。選択内容とステートスタックを初期化し、ユニット選択から始める
        // 入力中はプッシャー側の物理を止めるため timeScale を 0 にする
        public void BeginCommandInput()
        {
            if (mManager == null || mManager.StateMachine.Current == BattleState.BattleEnd)
                return;
            mContext.Clear();
            ClearStack();
            if (mInputStateBorder != null)
            {
                mInputStateBorder.gameObject.SetActive(true);
            }
            Push(new PPUnitSelectState(this));
            Time.timeScale = 0;
        }

        // 入力を中断する（バトル終了・対象の消滅・キャンセルなど）
        // スタックを空にし、UI の選択状態も解除する
        public void Abort()
        {
            ClearStack();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
        }

        // 次のステートへ進む。現在のステートを Suspend してから新しいステートを積んで Enter する
        // aNext : 積むステート
        public void Push(IPPBattleInputState aNext)
        {
            if(mStateStack.Count > 0) mStateStack.Peek().Suspend();
            mStateStack.Push(aNext);
            aNext.Enter();
        }

        // ひとつ前のステートへ戻る。現在のステートを Exit して捨て、下のステートを Resume する
        // 戻り先が無くなった場合は入力自体を中断する
        public void Back()
        {
            // ひとつ前のコマンドに戻す
            if (mStateStack.Count > 0)
            {
                mStateStack.Pop().Exit();
                if(mStateStack.Count > 0)
                {
                    mStateStack.Peek().Resume();
                    return;
                }
            }

            Abort();
        }

        // 選択内容を確定し、コマンドを生成してキューへ流す
        // コマンドを組み立てられない状態（生成関数未設定など）なら何もしない
        public void Confirm()
        {
            var command = mContext.CommandBuilder?.Invoke(mContext.Target);
            if (command == null) return;

            OnCommandConfirmed?.Invoke(mContext.Unit, command);
            Flush(command);
        }

        // 入力 UI を閉じてコマンドをバトルマネージャのキューへ投入する
        // aCommand : 投入するコマンド
        private void Flush(BattleCommandBase aCommand)
        {
            ClearStack();
            mManager.EnqueueCommand(aCommand);
            OnCommandFlushed?.Invoke();
        }

        // ステートスタックを空にし、入力中表示を消して timeScale を戻す
        // 積まれている全ステートに Exit を通すのでリソースは解放される
        private void ClearStack()
        {
            while (mStateStack.Count > 0)
            {
                mStateStack.Pop().Exit();
            }

            if (mInputStateBorder != null)
            {
                mInputStateBorder.gameObject.SetActive(false);
            }

            Time.timeScale = 1;
        }

        // スキル既定のリゾルバに、プレイヤーが選んだ対象を焼き込んだリゾルバを組み立てる
        // 単体対象のリゾルバのみ差し替え、全体対象などはそのまま既定を返す
        // aDefault : スキルが持つ既定のターゲットリゾルバ
        // aTarget : プレイヤーが選択した対象。未選択なら null
        // return : 実行時に使用するターゲットリゾルバ
        public ITargetResolver BuildResolver(ITargetResolver aDefault, BattleUnit aTarget)
            => aTarget == null
                ? aDefault
                : aDefault switch
                {
                    SingleEnemyResolver => new SingleEnemyResolver(aTarget),
                    SingleAllyResolver => new SingleAllyResolver(aTarget),
                    _ => aDefault
                };
    }
}
