/* =====================================
 * Copyright hqrse. All rights reserved.
 * @file PPBattleSkillMenuView.cs
 * @author hqrse
 * @date 2026/06/30
 * @brief バトル中のスキル一メニュー
 * =====================================*/

using System;
using System.Collections.Generic;
using CommandBattleCore;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using AttributeUtility;

namespace PPCore
{
    // ユニットの所持スキルをアイコンボタンとして横一列に並べるメニュー
    // 表示するたびにボタンを生成し直し、閉じるときに破棄する
    // ユニットごとにスキル数が違うため、使い回さず作り直す方針
    public class PPBattleSkillMenuView : MonoBehaviour
    {
        [Label("スキルボタンプレハブ")]
        [SerializeField] private PPBattleCommandButton mButtonPrefab;
        [Label("スキルボタン表示領域")]
        [SerializeField] private RectTransform mContent;
        [Label("戻るボタン")]
        [SerializeField] private Button mBackButton;
        [Label("詳細ボタン")]
        [SerializeField] private Button mDetailButton;
        [Label("スキルカタログ")]
        [SerializeField] private PPSkillVisualCatalog mIconCatalog;

        // スキルが選択されたときに発火する
        public event Action<BattleSkill> OnSkillSelected;
        // 戻るが押されたときに発火する
        public event Action OnBackRequested;
        // 詳細確認が押されたときに発火する
        public event Action OnDetailRequested;

        // 生成済みのスキルボタン。閉じるときにまとめて破棄する
        private readonly List<PPBattleCommandButton> mSkillButtons = new();
        // 生成済みのスキルボタンに対応するステータスソース。閉じるときにまとめて購読解除する
        private readonly List<IPPSkillStatusSource> mSkillSources = new();

        // ユニットの所持スキル分のボタンを生成して表示する
        // 各ボタンには発動可否を判定できるステータスソースを渡すため、
        // リソース不足のスキルは自動的に押せない状態になる
        // aUnit : スキルを表示する対象ユニット
        // aContext : 発動可否の判定に使うバトルコンテキスト
        // aDirection : 入場演出の向き。Down なら上から、Up なら下から現れる
        public void Show(BattleUnit aUnit, BattleContext aContext, PPBattleTransitionDirection aDirection)
        {
            if (mContent == null)
            {
                Debug.LogWarning("mContent is null");
                return;
            }

            Clear(aDirection);
            gameObject.SetActive(true);

            PPBattleCommandButton firstBtn = null;
            foreach (var skill in aUnit.Skills)
            {
                var btn = Instantiate(mButtonPrefab, mContent);
                // 複製直後は全ボタンが同名になり階層上で見分けがつかないため、対象スキルが分かる名前を付ける
                btn.gameObject.name = $"SkillButton_{skill.SkillId}";
                var src = new PPBattleSkillStatusSource(skill, aUnit, aContext);
                // カタログ未設定、または該当アイコン未登録のどちらもアイコンなしとして扱う
                var icon = mIconCatalog != null
                    ? mIconCatalog.Resolve(skill.SkillId)?.SkillIcon
                    : null;
                btn.Setup(icon, src.Cost.ToString(), () => OnSkillSelected?.Invoke(skill));
                btn.SetInteractable(src.IsCastable);
                // リソース変動でコスト表示・押下可否を追従させる
                src.Changed += () =>
                {
                    btn.SetContent(icon, src.Cost.ToString());
                    btn.SetInteractable(src.IsCastable);
                };
                mSkillButtons.Add(btn);
                mSkillSources.Add(src);

                // 初期フォーカス設定
                firstBtn ??= btn;
            }

            // 直後にフォーカスを当てるため、レイアウトの反映を次フレームまで待たない
            LayoutRebuilder.ForceRebuildLayoutImmediate(mContent);

            // レイアウト確定後の定位置を基準に、入場演出を開始する
            foreach (var btn in mSkillButtons)
            {
                btn.PlayEnter(aDirection);
            }

            mBackButton.onClick.AddListener(RaiseBack);
            if (mDetailButton != null) mDetailButton.onClick.AddListener(RaiseDetail);

            // 初期フォーカスを設定する(スキルがない場合はBackButtonにフォーカス)
            var focus = firstBtn != null ? firstBtn.FocusTarget : mBackButton.gameObject;
            EventSystem.current.SetSelectedGameObject(focus);
        }

        // メニューを閉じ、生成したボタンを破棄する
        // aDirection : 退場演出の向き。Down なら下へ、Up なら上へ抜ける
        public void Hide(PPBattleTransitionDirection aDirection)
        {
            Clear(aDirection);
        }

        // 戻る操作を外部へ通知する
        private void RaiseBack()
        {
            OnBackRequested?.Invoke();
        }

        // 詳細確認操作を外部へ通知する
        private void RaiseDetail()
        {
            OnDetailRequested?.Invoke();
        }

        // 戻る・詳細ボタンの購読を解除し、生成済みのスキルボタンとステータスソースをすべて退場演出付きで破棄する
        // 表示のたびに作り直すため、開く前と閉じるときの両方から呼ばれる
        // ルートを即座に非アクティブにすると子ボタンの退場コルーチンが Unity 側で強制停止し、
        // フェードアウトが再生されないまま破棄もされず残ってしまうため、非アクティブ化はしない
        // （退場後は子が居なくなるだけで、見た目にも入力にも影響しない）
        // aDirection : 退場演出の向き
        private void Clear(PPBattleTransitionDirection aDirection)
        {
            mBackButton.onClick.RemoveListener(RaiseBack);
            if (mDetailButton != null) mDetailButton.onClick.RemoveListener(RaiseDetail);
            foreach (var btn in mSkillButtons)
            {
                if(btn == null) continue;
                // mContent（ContentSizeFitter付き）に残したままだと、全ボタン退場でコンテナが
                // 収縮した瞬間に基準位置がずれてワープして見えるため、自身のルートへ退避させてから消す
                btn.PlayExit(aDirection, transform, () => Destroy(btn.gameObject));
            }
            mSkillButtons.Clear();

            // 供給元がリソースを購読している場合があるため、そちらの Dispose も通しておく
            foreach (var src in mSkillSources)
            {
                (src as IDisposable)?.Dispose();
            }
            mSkillSources.Clear();
        }
    }
}
