# AGENTS.md

このファイルは、Codex をはじめとする AI アシスタントが本リポジトリで作業する際のガイドです。

## プロジェクト概要

**Project-Pudding**（WabisabiAndons）は Unity 6 製のゲームプロジェクト。
**コインプッシャー（物理）** と **コマンドバトル（リアルタイム進行）** を接続したハイブリッド構成が中核。
プッシャー台から落ちたコインが各ユニットのコインゲージへ 1 枚ずつ配られ、行動コスト分溜まったユニットから行動する。ターンやティックの概念は持たない。

- Unity: `6000.3.17f1`（`ProjectSettings/ProjectVersion.txt`）
- レンダーパイプライン: URP 17.3.0
- 入力: **新 Input System のみ**（`activeInputHandler: 1`。`UnityEngine.Input` の旧 API は使用不可）
- 主要シーン: `Assets/Scenes/Check/S_BattleCheck.unity`（新しいプッシャーとバトルを組み込んだシーン。CLI 検証もこのシーンで行う）、`Assets/Scenes/Prototype/` 配下（`Prototype_#1.unity` はビルド対象。旧プッシャーの構成のまま変更しない）

## ビルド・実行

Unity Editor でシーンを開いて Play するのが唯一の実行手段。CLI ビルドスクリプトやテストは未整備。

- `Assets/`, `Packages/`, `ProjectSettings/` のみがバージョン管理対象（`Library/`, `Temp/`, `Build/` は ignore）
- **`.asmdef` は 1 つも存在しない**。全ランタイムコードが `Assembly-CSharp`、`Editor/` 配下が `Assembly-CSharp-Editor` にコンパイルされる
- テストアセンブリは未作成（`com.unity.test-framework` は導入済みだが未使用）
- CI / lint / formatter の設定なし。検証はエディタ上のコンパイル通過と Play テストで行う

## ディレクトリ構成

```
Assets/
├── Scripts/
│   ├── CommandBattleCore/   # 汎用コマンドバトル基盤（プロジェクト非依存）
│   ├── PPCore/              # Project-Pudding 固有の拡張レイヤ
│   ├── CustomConsole/       # タグ付きログ + 専用コンソールウィンドウ
│   ├── RecentAssetsWindow/  # 最近開いたアセットのエディタ拡張
│   └── *.cs                 # 旧プッシャーの初期プロトタイプ（Coin*, PusherTableController 等）。Prototype のシーン専用
├── GameData/                # ScriptableObject 実データ（AI 条件・エフェクト・カタログ・パーティ）
├── Content/Sample/          # サンプルユニット / スキル定義アセット
├── Prefab/, Scenes/, Texture/, Settings/, PysicsMaterials/
Packages/, ProjectSettings/
```

## アーキテクチャ

### 2 層構造：CommandBattleCore → PPCore

**この分離は本リポジトリで最も重要な設計上の約束。**

- `CommandBattleCore`（namespace `CommandBattleCore`）
  汎用のコマンドバトルエンジン。Project-Pudding のゲーム固有仕様（属性・コイン・パーティ AI 等）を **一切知らない**。
- `PPCore`（namespace `PPCore`、型は `PP` プレフィックス）
  上記を継承・拡張した本作固有レイヤ。`PPBattleUnit : BattleUnit`、`PPBattleParty : BattleParty`、`PPBattleRules : BattleRules`、`PPSkillDefinition : SkillDefinition` のように必ず基底を継承して拡張する。

新機能を追加するときは「汎用バトル機能か、Project-Pudding 固有か」をまず判断すること。
固有仕様を `CommandBattleCore` 側に持ち込まない。逆に汎用的な仕組み（新しいリアクショントリガー、ターゲット解決など）は `CommandBattleCore` に置いて `PPCore` から使う。

### バトル進行

`BattleManager`（`CommandBattleCore/Core/BattleManager.cs`）がすべての中心。

- `BattleContext` が 1 バトル分の状態を保持（`AllyParty` / `EnemyParty` / `Rules` / `Clock` / `Timers` / `Reward` / `Environment`）
- `BattleStateMachine` の `BattleState`: `BattleStart → CommandInput → ActionExecution → ResultCheck → BattleEnd`
- コマンドは `ActionQueue` に積んで `ExecuteNextCommand()` / `ExecuteAllCommands()` で実行（演出は待たない）
- `AdvanceTime()` がバトル専用の時計を進め、期限の来たタイマーを発火する。状態異常（発生間隔 × 回数）とスキルのクールタイムはこの時計で進む
- 行動は `PPActionScheduler` が積む。コインが配られたときと行動の演出が終わったときに、コインゲージが行動コスト分溜まったユニットを調べ、「選択中のスキルが発動可能ならそのスキル、そうでなければ通常攻撃」を 1 ユニット 1 行動ずつ実行する
- **拡張ポイントはすべて差し替え可能なインターフェース**として `BattleRules` / `BattleManager` に生えている：
  `IHitResolver`, `ICriticalResolver`, `IRandomProvider`, `ICastValidator`, `ITargetFilter`, `IDeadTargetPolicy`, `IPPActionOrderResolver`, `IBattleResultChecker`, `IBattleLogger`
- リアクション（反撃等）は `IBattleReaction` + `ReactionTrigger` で、`DispatchReactions()` がキュー先頭へ割り込む。`MaxReactionPerEvent` で暴走を抑止
- 挙動を観測する側は `BattleManager` の C# `event`（`OnDamageTaken`, `OnUnitDefeated`, `OnBattleEnded`, `OnStatsEffectAdded` …）を購読する。View / ブリッジ層はここに繋ぐ

エントリポイントは `PPCore/Battle/Core/PPBattleRunner.cs`。バトルの組み立て方（パーティ生成 → Rules 設定 → View / 入力の Bind → 進行役と AI の接続 → コインとチャージ用アイテムの橋渡しの Bind）はこのファイルを参照するのが最短。

### データ定義（ScriptableObject）

マスターデータはすべて ScriptableObject。`CreateRuntimeUnit()` / `CreateRuntimeSkill()` でランタイムインスタンスを生成し、生成物は `SourceDefinition` に定義アセットへの参照を保持する（AI が `SourceDefinition is PPSkillDefinition` で判定するため、**この参照設定を省略しない**）。

- `UnitDefinition` → `PPUnitDefinition`（属性・成長曲線・既定ロール・既定知能を追加。`CreateRuntimeUnit(int aLevel)` でレベル成長）
- `SkillDefinition` → `PPSkillDefinition`（抽象）→ `PPAttackSkillDefinition` / `PPHealSkillDefinition` / `PPEffectCureSkillDefinition`
- `PPEffectDefinition` → `PPStatusEffectDefinition`（毒など）/ `PPParameterEffectDefinition`（バフデバフ）
- `PPPartyAIProfileDefinition`（敵 AI の性格）、`PPPartyDefinition`、`PPItemDefinition`、`PPUnitVisualDefinition` / `PPSkillVisualDefinition`
- `PPCatalogAsset<T>` 派生（`PPSkillCatalog` 等）が ID → アセットの解決を担う。ID 重複は `Debug.LogError` で検出される

`CreateAssetMenu` の menuName は **`Project-Pudding/<カテゴリ>/<型名>`** で統一（`Definition` / `Skill` / `Effect` / `Catalog` / `AI` / `Battle`）。`CommandBattleCore` 側の基底型のみ `CommandBattleCore/...` を使う。

### 属性とリソース

- `PPTypeAttribute`: `Normal / Fire / Water / Earth / Shine / Dark`（`TypeCount = 6`、`Normal` が基準リソース = `BaseIndex`）
- `PPBattleResourcePool` が属性ごとの `ResourceParameter` を保持。スキルの `PPResourceCost` を `CanPay` / `TryPay` で消費
- プッシャー → バトルの接続は `PPCoinResourceBridge`：`IPPCoinGainNotifier`（実装は `PPPusherDropZone`）の `OnCoinGained` を購読し、`IPPCoinResourceConverter` と `PPBattleParty.CoinConversionRate` を経由して、生存ユニットのコインゲージへ 1 枚ずつ順番に配る。**物理側とバトル側を直接結合させず、必ずこのブリッジを挟む**

### プッシャー

`Assets/Prefab/Pusher/PR_PusherField.prefab` が一式（本体 `PR_CoinPusher`・落下検出・コイン投入・チャージ用アイテムの出現）。味方用と敵用を 1 つずつシーンに置く。

- `PPPusherPlate` … 押し出し板。奥の台の下から手前へ出入りして、床のコインを押し出す
- `PPPusherDropZone` … 落下検出。床の端を越えたコインとチャージ用アイテムを拾って通知する。通知しない設定のものは、台の外へ出たものを消すだけ
- `PPCoinDispenser` … コインの投入。味方は左右の矢印キー、敵は一定間隔の自動投入。落ちたコインは所持コインへ戻る
- `PPChargeItemSpawner` / `PPPusherChargeItem` … チャージ用アイテム。中身（誰のスキルゲージへ入るか）は出現時に `PPChargeItemBridge` が決め、落ちたときに同じブリッジがスキルゲージを溜める
- 敵のプッシャーは離れた場所に置き、専用のカメラで RenderTexture へ映してワイプ表示する。`PPBattleRunner` の「敵のプッシャーを使う」を切ると一式を無効にし、「敵ゲージ供給」で設定した量で代用する
### ユニット AI

`PPUnitAIStrategist`（`IPPPartyCommandStrategist`）が、ユニットごとの判断ツリー `PPUnitBehaviourTree` を評価して次の行動を決める。一定間隔では思考しない。

処理の流れ：
1. バトル開始時に `ThinkAll()`、以後はユニットの行動（演出）が終わるたびに `PPActionScheduler.OnActionFinished` から `Think(unit)` が呼ばれる
2. `PPPartyAIContext.Capture()` でパーティ状況をスナップショットし、ツリーを評価する（個別 → ロール既定 → 全体既定の順で引く）
3. 結果をその場で実行せず、ユニットの「選択中のスキル」（`SelectedSkill`）と「狙う相手」（`DesignatedTarget`）として予約する。待機と通常攻撃はスキルの選択を外す
4. コインゲージが溜まると、プレイヤー操作のユニットと同じく `PPActionScheduler` が予約どおりに行動を積む

- 時間の経過は「そのユニットが何回行動したか」（`PPUnitAIBlackboard.ActionCount`）で数える。判断の維持・狙いの固定・ノードの間隔・「直近の出来事」は行動回数で設定する
- 上位のスキルまでの距離は「あと何回通常攻撃すれば届くか」（`PPUnitGaugeForecast.EstimateAttacksToReach`）で測る。クールタイムなど秒で決まるものと比べるときだけ、直近の 1 回の行動に掛かった秒数（`SecondsPerAction`）で換算する
- オートバトル（`PPBattleRunner` の設定、T キーで切り替え）は、味方にも同じ AI を当てる
- AI のチューニングは基本的にコードではなくツリーのアセット（`Assets/GameData/AI/UnitBehaviourTree/`）で行う。ノードと条件は `[SerializeReference]` の通常クラスで、`Window` のツリーエディタから編集する

乱数は必ず `aContext.Rules.RandomProvider`、またはユニット自身の `ResolveRandom()` を経由する。AI ロジック内で `UnityEngine.Random` を直接使わない。
### UI・入力

`PPBattleCommandInputController` がスタックベースのステートマシンで入力を管理する。

- `IPPBattleInputState` 実装：`PPCommandSelectState`（ターゲット / スキル / 解除 / 詳細）→ `PPUnitSelectState` → `PPDesignateTargetState`（狙う敵の指定）または `PPSkillSelectState` → `PPTargetSelectState`（味方が対象のスキルだけ）。`PPUnitDetailViewState` はユニット選択の次
- 入力中はバトル専用の時計（`BattleContext.Clock`）を止める。確定した内容はその場で実行せず、ユニットの「選択中のスキル」「狙う相手」として持ち、コインゲージが溜まったときに `PPActionScheduler` が使う
- `Enter / Resume / Suspend / Exit` のライフサイクル。メニュー系は `PPBattleMenuStateBase`、ユニット選択系は `PPBattleUnitPickerStateBase` を継承する
- 選択途中の状態は `PPBattleSelectionContext` が保持
- View 側は `PPBattleUnitViewBinder` が `BattleUnit` ↔ `PPBattleUnitView` を対応付ける。表示データは `IPPUnitStatusSource` / `IPPSkillStatusSource` / `IPPItemStatusSource` 経由で供給する

### エディタ拡張

- `LabelAttribute` … インスペクタ表示名を日本語化。`PropertyDrawer` は `CommandBattleCore/Editor/LabelAttributeDrawer.cs`
- `EditConditionAttribute` … UE5 の EditCondition/EditConditionHides 相当の条件付き表示
- `CustomConsoleLog` + `Window > Custom Console` … タグ・拡張ログレベル（Verbose/Critical）・送信元オブジェクト付きログ。バトル/AI のトレースはこれを使う
- `Window > Recent Assets` … 最近開いたアセット履歴
- `PPConditionPickerPopup` / `PPConditionTreeView` / `PPConditionAssetFactory` … AI 条件アセットの生成・選択 UI

## コーディング規約

既存コードから読み取れる規約。**新規ファイルもこれに合わせること。**

**ファイルヘッダ**（全 `CommandBattleCore` / `PPCore` ファイルに付与）:

```csharp
/* =====================================
 * Copyright WabisabiAndons. All rights reserved.
 * @file <FileName>.cs
 * @author UserName
 * @date YYYY/MM/DD
 * @brief <日本語1行説明>
 * =====================================*/
```

`Copyright` はプロジェクトの権利表記に合わせる（本プロジェクトは `WabisabiAndons`）。`@author` の `UserName` はそのファイルを新規作成した人の名前に置き換える。

**命名**

| 対象 | 規則 | 例 |
|---|---|---|
| private / protected フィールド | `m` プレフィックス + PascalCase | `mProfile`, `mBattleManager` |
| メソッド引数 | `a` プレフィックス + PascalCase | `aContext`, `aUnit`, `aTargets` |
| public プロパティ / メソッド | PascalCase | `ResourcePool`, `PlanActions` |
| PPCore の型 | `PP` プレフィックス必須 | `PPBattleUnit`, `PPSkillCommand` |
| ローカル変数 | camelCase | `snap`, `candidates` |

- `Label` / `InspectorName` の表示文字列は **日本語**
- `[SerializeField] private` + `[Label("表示名")]` をセットで書く。インスペクタに素の英語フィールド名を出さない
- 日本語表示文字列は再利用されるものを `PPBattleUtilityDefinition` / `PPTypeAttributeDefinition` の `const` に集約している。ロール名・属性名はハードコードせずこの定数を使う
- 拡張ポイントは `virtual` / `protected` を積極的に付ける（`BattleManager` の各メソッド、AI のスコア計算群）
- switch 式・`??=`・パターンマッチなど C# 9+ の記法を通常どおり使ってよい

## ログ運用

バトル/AI のトレースなど詳細なログを出す場合は `CustomConsoleLog`（`Assets/Scripts/CustomConsole/CustomConsoleLog.cs`）経由で出力し、`Window > Custom Console` でのタグ・レベルによる絞り込みを効かせる。**本節のルールは新規に書くログ出力コードに適用する。既存の `Debug.Log` / `Debug.LogError` 呼び出し（プッシャー系プロトタイプ含む）の置き換えは対象外**とし、「既知の課題・注意点」の運用方針（触る必要が生じたときに対処する）に準ずる。

- タグには機能領域を表す**大分類のみ**を指定する（サブタグ・階層構造は用いない）。バトル進行・パーティ AI・UI・入力など、本ファイルのアーキテクチャ節に登場する機能領域に対応する名前を付ける。大分類の正式な一覧は定めない
- 検証・デバッグ用の一時的なログは、大分類タグの末尾にアンダースコア区切りで `_Verify` を付けて区別する（例: `CustomConsoleLog.Verbose("Battle_Verify", $"...")`）
- ログレベル（`Verbose` / `Log` / `Warning` / `Error` / `Critical`）はタグ運用と独立した軸であり、状況に応じて自由に選択してよい
- ログメッセージの言語は日本語・英語どちらでもよい（既存の `BattleManager` のバトルログは英語）

## Unity 固有の注意点

- **`.meta` ファイルは必ずコミットする**。ファイル追加・移動・削除時に対応する `.meta` の追随を忘れない
- `.gitattributes` は Unity 公式テンプレート準拠。`*.unity` / `*.asset` / `*.prefab` は `unityyamlmerge`、モデル・音声・画像は Git LFS 対象
- シーンとプレハブは YAML マージが必要なため、**手動で競合解決しない**。競合したら片方を採用するかエディタで作業をやり直す
- `Editor/` フォルダ配下のみが Editor アセンブリ。ランタイムスクリプトから `UnityEditor` を参照する場合は `#if UNITY_EDITOR` で囲む
- ScriptableObject のフィールド名を変更するとシリアライズ済みアセットの値が失われる。`[FormerlySerializedAs]` を使うか、既存アセットの再設定を明示する

## Git 運用

- デフォルトブランチ: `main`。直 push せず PR 経由でマージする
- ブランチ名: `feat/<領域>/<機能名>_<連番>` / `fix/<領域>/<機能名>_<連番>`（例 `feat/Battle/PartyAI_03`, `fix/Utility/RecentAssetWindowFix_01`）
- コミットメッセージ: `feat : <概要>` / `fix : <概要>`（コロンの前後にスペース）。概要は英語の機能名または日本語のどちらでもよい
- AI エージェントは指定されたブランチ上で作業し、そのブランチへ push する

## 既知の課題・注意点

作業中に遭遇し得る、既存の未整理箇所。**ついでに直すのではなく、触る必要が生じたときに対処する。**

- **ランタイムスクリプトに `UnityEditor` の未使用 using が混入している**（`PPCore/UI/SlotListComponent.cs`, `PPCore/Battle/StatusEffect/PPEffectDefinition.cs`, `PPCore/Battle/AI/PPPartyAIProfileDefinition.cs`, `CommandBattleCore/Commands/ActionQueue.cs`）。エディタ上では通るがプレイヤービルドで壊れる。該当ファイルを編集する際は削除する
- **初期プロトタイプのプッシャー系スクリプトが Shift-JIS 保存**（`CoinSpawner.cs`, `CoinGenerator.cs` 等）。UTF-8 前提のツールで開くと `[Header]` の日本語が文字化けする。編集時は UTF-8 に変換してから文字列を直す
- `Assets/Scenes/Prototype/` 配下のシーンと、それが使う `Assets/Scripts/*.cs` 直下の旧プッシャー・`SamplePusherBattleRunner` は変更しない。旧コードは命名規約・ヘッダコメント規約の対象外で、namespace も持たない
- 物理は一定間隔（FixedUpdate、0.02 秒）で進める設定にしている。描画ごとに進める設定へ戻すと、処理が一瞬止まったときにプッシャーのコインが弾け飛ぶ
- `PPCore/Battle/AI/Conditions/NewMonoBehaviourScript.cs` はデバッグ用の残骸
- `PPSkillDefinition.BuildEffectWithEntries()` が定義されているが `CreateRuntimeSkill()` から呼ばれておらず、`mEffectEntries` のエフェクト付与が実際には走っていない
