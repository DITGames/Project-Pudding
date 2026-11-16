/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file PPBattleCommandInputController.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief コマンド入力の管理（入力ステートの契約・選択中の文脈・コントローラ）
 * =====================================*/

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // コマンド入力の 1 段階を表すステート。PPBattleCommandInputController がスタックで積み下ろしする
    // Suspend は退避（Resume で復元できる）、Exit は破棄
    public interface IPPBattleInputState
    {
        // 初めてこのステートに入るときに呼ばれる。UI の表示と購読を行う
        void Enter();

        // 一つ先のステートから戻ってきたときに呼ばれる
        void Resume();

        // 一つ先へ進むため退避するときに呼ばれる。UI を一時的に隠す
        void Suspend();

        // 完全に破棄されるときに呼ばれる。購読解除と後始末を行う
        void Exit();
    }

    // コマンド入力の途中経過を保持するコンテキスト。各ステートは自分の担当分だけを書き込む
    // CommandBuilder は「対象さえ決まればコマンドを作れる」状態を表す
    public class PPBattleSelectionContext
    {
        /* 状態 */

        // 行動するユニット。ユニット選択で決まる
        public BattleUnit Unit;
        // 使用するスキル。スキル選択で決まる。通常攻撃なら null のまま
        public BattleSkill Skill;
        // 対象ユニット。対象選択で決まる
        public BattleUnit Target;
        // 対象を受け取ってコマンドを生成する関数。コマンド／スキル選択の時点で設定される
        public Func<BattleUnit, BattleCommandBase> CommandBuilder;
        // 選択中の行動のターゲット範囲。未確定なら null
        public TargetScope? TargetScope;

        /* 操作 */

        // すべての選択内容を破棄する。入力を最初から始めるときに使う
        public void Clear()
        {
            Unit = null;
            Skill = null;
            Target = null;
            CommandBuilder = null;
            TargetScope = null;
        }

        // 行動ユニットの選択だけを残して以降の選択を破棄する
        // コマンド選択まで戻ったときに、選び直しの対象がユニット以降だけになるようにする
        public void ClearSelectionKeepingUnit()
        {
            Skill = null;
            Target = null;
            CommandBuilder = null;
            TargetScope = null;
        }
    }

    // プレイヤーのコマンド入力を、スタックベースのステートマシンで管理するコントローラー
    // キャンセルはスタックを 1 段ポップし、空になれば入力を中断する。選択途中の状態は PPBattleSelectionContext に集約する
    public class PPBattleCommandInputController : MonoBehaviour
    {
        /* インスペクタ設定 */

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

        /* 状態 */

        // このティックの行動を仮押さえしている台帳。予約分を差し引いた残量でスキルの発動可否を出すために使う
        // 未設定なら実際のゲージ残量だけで判定される
        public PPUnitActionLedger ActionLedger { get; set; }
        // コマンドの投入先。Bind で受け取る
        private BattleManager mManager;
        // 入力ステートのスタック。先頭が現在アクティブなステート
        private readonly Stack<IPPBattleInputState> mStateStack = new();
        // 選択途中の内容（ユニット・スキル・対象・コマンド生成関数）を保持するコンテキスト
        private readonly PPBattleSelectionContext mContext = new();
        // 入力を開いているか。閉じた通知を 1 回の入力につき 1 回だけ出すために持つ
        private bool mIsInputOpen;

        /* プロパティ */

        // 入力を開いているか
        public bool IsInputOpen => mIsInputOpen;

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

        /* イベント */

        // コマンド確定時(行動ユニット, 確定したコマンド)。キュー投入前に発火する
        public event Action<BattleUnit, BattleCommandBase> OnCommandConfirmed;
        // コマンドをキューへ流し終えたとき。入力 1 サイクルの完了通知
        public event Action OnCommandFlushed;
        // 入力が閉じたとき。確定・中断のどちらでも発火する
        public event Action OnInputClosed;

        /* 生成 */

        // スキル既定のリゾルバに、プレイヤーが選んだ対象を焼き込んだリゾルバを組み立てる
        // 単体対象のリゾルバのみ差し替え、全体対象などはそのまま既定を返す
        public ITargetResolver BuildResolver(ITargetResolver aDefault, BattleUnit aTarget)
            => aTarget == null
                ? aDefault
                : aDefault switch
                {
                    SingleEnemyResolver => new SingleEnemyResolver(aTarget),
                    SingleAllyResolver => new SingleAllyResolver(aTarget),
                    _ => aDefault
                };

        /* Unity イベント */

        // 破棄時にキャンセル入力の購読を解除する
        private void OnDestroy()
        {
            if (mUIInputModule != null)
            {
                mUIInputModule.cancel.action.performed -= HandleCancelPerformed;
            }
        }

        /* 初期化・接続 */

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

        /* 操作 */

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
            mIsInputOpen = true;
            Push(new PPUnitSelectState(this));
            Time.timeScale = 0;
        }

        // 指定したユニットのスキル選択から入力を開始する。行動を積む直前に選び直させるときに使う
        // 既に入力を開いている場合は割り込まず false を返す
        public bool BeginSkillSelect(BattleUnit aUnit)
        {
            if (mManager == null || mManager.StateMachine.Current == BattleState.BattleEnd) return false;
            if (aUnit == null || mIsInputOpen) return false;

            mContext.Clear();
            mContext.Unit = aUnit;
            if (mInputStateBorder != null)
            {
                mInputStateBorder.gameObject.SetActive(true);
            }
            mIsInputOpen = true;
            Push(new PPSkillSelectState(this));
            Time.timeScale = 0;
            return true;
        }

        // 入力を中断する（バトル終了・対象の消滅・キャンセルなど）
        // スタックを空にし、UI の選択状態も解除する
        public void Abort()
        {
            bool wasOpen = ClearStack();
            if (EventSystem.current != null)
                EventSystem.current.SetSelectedGameObject(null);
            if (wasOpen) OnInputClosed?.Invoke();
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
            Flush();
        }

        // 入力 UI を閉じて、確定したコマンドを呼び出し側へ渡す
        // 実行もキュー投入もせず、予約として扱う
        private void Flush()
        {
            bool wasOpen = ClearStack();
            OnCommandFlushed?.Invoke();
            if (wasOpen) OnInputClosed?.Invoke();
        }

        // ステートスタックを空にし、入力中表示を消して timeScale を戻す。入力を開いていたかを返す
        // 積まれている全ステートに Exit を通すのでリソースは解放される
        private bool ClearStack()
        {
            bool wasOpen = mIsInputOpen;
            mIsInputOpen = false;

            while (mStateStack.Count > 0)
            {
                mStateStack.Pop().Exit();
            }

            if (mInputStateBorder != null)
            {
                mInputStateBorder.gameObject.SetActive(false);
            }

            Time.timeScale = 1;
            return wasOpen;
        }

        /* イベントハンドラ */

        // キャンセル入力を受けて 1 段戻る。入力中でなければ何もしない
        // ctx : Input System のコールバックコンテキスト
        private void HandleCancelPerformed(InputAction.CallbackContext ctx)
        {
            if (mStateStack.Count > 0)
            {
                Back();
            }
        }
    }
}
