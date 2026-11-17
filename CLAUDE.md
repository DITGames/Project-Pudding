# CLAUDE.md

このファイルは、Claude Code をはじめとする AI アシスタントが本リポジトリで作業する際のガイドです。

## プロジェクト概要

**Project-Pudding**（WabisabiAndons）は Unity 6 製のゲームプロジェクト。
**コインプッシャー（物理）** と **コマンドバトル（ターン/ティック制）** を接続したハイブリッド構成が中核。
プッシャー台から落ちたコインがバトルの行動リソースへ変換され、そのリソースを消費してスキルを撃つ。

- Unity: `6000.3.17f1`（`ProjectSettings/ProjectVersion.txt`）
- レンダーパイプライン: URP 17.3.0
- 入力: **新 Input System のみ**（`activeInputHandler: 1`。`UnityEngine.Input` の旧 API は使用不可）
- 主要シーン: `Assets/Scenes/Prototype/Prototype_#1.unity`（ビルド対象）、`BattleSample.unity` / `BattleSceneSample.unity`（バトル検証用）

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
│   ├── PPCore/              # バトルシステム本体（進行・ユニット・スキル・状態異常・AI・入力・View・データ定義）
│   ├── AttributeUtility/    # Label / EditCondition などインスペクタ用属性
│   ├── CustomConsole/       # タグ付きログ + 専用コンソールウィンドウ
│   ├── RecentAssetsWindow/  # 最近開いたアセットのエディタ拡張
│   └── *.cs                 # プッシャー側の初期プロトタイプ（Coin*, PusherTableController 等）
├── GameData/                # ScriptableObject 実データ（AI 条件・エフェクト・カタログ・パーティ）
├── Content/Sample/          # サンプルユニット / スキル定義アセット
├── Prefab/, Scenes/, Texture/, Settings/, PysicsMaterials/
Packages/, ProjectSettings/
```

## アーキテクチャ

### PPCore の構成

バトルシステムは `PPCore`（namespace `PPCore`）の **単一レイヤ**。別プロジェクトでの再利用は想定しない。
Project-Pudding の仕様（属性・コイン・ゲージ等）をどの型にも直接書いてよい。

```
PPCore/
├── Battle/
│   ├── Core/          # 進行（BattleManager / BattleContext / PPBattleRules / PPBattleRunner / ティック内行動の収集と順序 / ログ）
│   ├── Command/       # コマンド（攻撃・スキル等）とユニット単位のコマンド決定
│   ├── Judge/         # 命中・会心・発動可否・ダメージ情報・ダメージ計算
│   ├── Targeting/     # 対象範囲・ターゲットリゾルバ・死亡対象の代替
│   ├── Reaction/      # リアクション（反撃等）
│   ├── Units/         # ユニット・パーティ・スキルのランタイム、パラメータ、共通の列挙と表示名定数
│   ├── Skill/         # スキルタグ・スキル効果定義・インスペクタ用属性
│   ├── StatusEffect/  # ステータスエフェクト本体・振る舞い・持続条件・スタック消費ルール
│   ├── Integration/   # プッシャー → バトルのコイン橋渡し
│   ├── AI/            # パーティ行動計画・ビヘイビアツリー（Tree/）・条件（Conditions/）
│   ├── Input/         # コマンド入力のコントローラと各ステート
│   ├── View/          # バトル UI の View とステータスソース
│   └── Sample/        # サンプルランナー
├── Data/              # ユニット・スキル・パーティ等の定義アセット型（ScriptableObject）
├── Catalog/           # ID → アセットの解決
├── Settings/          # ランタイム設定
└── Editor/            # ピッカー・ユニット作成ツール
```

**ファイルの切り方**

- 役割が近い小さな型（interface と実装、基底と派生、定義とランタイム、同じ分類の条件やノード）は 1 ファイルにまとめる。目安は 1 ファイル 500 行前後まで
- **`MonoBehaviour` / `ScriptableObject` / `EditorWindow` は 1 クラス 1 ファイル**（ファイル名 = クラス名）。Unity がファイルと型を対応付けるため、他のファイルへ混ぜない。逆に、これらのファイルへ補助の通常クラスや interface を同居させるのは問題ない
- `[SerializeReference]` で保持される通常クラス（スキル効果定義・振る舞い定義・AI 条件・ツリーノード等）は**クラス名と namespace を変えない**。アセットに型名で保存されているため、変える場合は `[MovedFrom]` が必要

**型の命名と継承の現状**

- `BattleManager`, `BattleUnit`, `StatusEffect`, `Parameter` など一部の型は `PP` プレフィックスを持たない
- `PPBattleUnit : BattleUnit`、`PPBattleParty : BattleParty`、`PPBattleRules : BattleRules`、`PPSkillDefinition : SkillDefinition` のような基底・派生のペアがあり、同じファイルに並んでいる
- 新しく追加する型は `PP` プレフィックスを付ける

### バトル進行

`BattleManager`（`PPCore/Battle/Core/BattleManager.cs`）がすべての中心。

- `BattleContext` が 1 バトル分の状態を保持（`AllyParty` / `EnemyParty` / `Rules` / `TurnCount` / `Reward` / `Environment`）
- `BattleStateMachine` の `BattleState`: `BattleStart → CommandInput → ActionExecution → ResultCheck → BattleEnd`
- コマンドは `ActionQueue` に積んで `ExecuteNextCommand()` / `ExecuteAllCommands()` で実行（演出は待たない）
- `AdvanceTime()` がバトル専用の時計を進め、期限の来たタイマーを発火する。状態異常（発生間隔 × 回数）とスキルのクールタイムはこの時計で進む
- `AdvanceTick()` は `TurnCount` を進めて `OnTickEnded` / `OnTickStarted` を通知するだけで、状態異常は進めない
- **拡張ポイントはすべて差し替え可能なインターフェース**として `BattleRules` / `BattleManager` に生えている：
  `IHitResolver`, `ICriticalResolver`, `IRandomProvider`, `ICastValidator`, `ITargetFilter`, `IDeadTargetPolicy`, `ITurnOrderResolver`, `IBattleResultChecker`, `IBattleLogger`
- リアクション（反撃等）は `IBattleReaction` + `ReactionTrigger` で、`DispatchReactions()` がキュー先頭へ割り込む。`MaxReactionPerEvent` で暴走を抑止
- 挙動を観測する側は `BattleManager` の C# `event`（`OnDamageTaken`, `OnUnitDefeated`, `OnBattleEnded`, `OnStatsEffectAdded` …）を購読する。View / ブリッジ層はここに繋ぐ

エントリポイントの実例は `PPCore/Battle/Sample/SamplePusherBattleRunner.cs` と `PPCore/Battle/Core/PPBattleRunner.cs`。バトルの組み立て方（パーティ生成 → Rules 設定 → Strategist 注入 → View / 入力 / コイン橋渡しの Bind → AI ドライバとティックのコルーチン起動）はこのファイルを参照するのが最短。

### データ定義（ScriptableObject）

マスターデータはすべて ScriptableObject。`CreateRuntimeUnit()` / `CreateRuntimeSkill()` でランタイムインスタンスを生成し、生成物は `SourceDefinition` に定義アセットへの参照を保持する（AI が `SourceDefinition is PPSkillDefinition` で判定するため、**この参照設定を省略しない**）。

- `UnitDefinition` → `PPUnitDefinition`（属性・成長曲線・既定ロール・既定知能を追加。`CreateRuntimeUnit(int aLevel)` でレベル成長）
- `SkillDefinition` → `PPSkillDefinition`（抽象）→ `PPAttackSkillDefinition` / `PPHealSkillDefinition` / `PPEffectCureSkillDefinition`
- `PPEffectDefinition` → `PPStatusEffectDefinition`（毒など）/ `PPParameterEffectDefinition`（バフデバフ）
- `PPPartyAIProfileDefinition`（敵 AI の性格）、`PPPartyDefinition`、`PPItemDefinition`、`PPUnitVisualDefinition` / `PPSkillVisualDefinition`
- `PPCatalogAsset<T>` 派生（`PPSkillCatalog` 等）が ID → アセットの解決を担う。ID 重複は `Debug.LogError` で検出される

`CreateAssetMenu` の menuName は **`Project-Pudding/<カテゴリ>/<型名>`** で統一（`Definition` / `Skill` / `Effect` / `Catalog` / `AI` / `Battle`）。

### 属性とリソース

- `PPTypeAttribute`: `Normal / Fire / Water / Earth / Shine / Dark`（`TypeCount = 6`、`Normal` が基準リソース = `BaseIndex`）
- `PPBattleResourcePool` が属性ごとの `ResourceParameter` を保持。スキルの `PPResourceCost` を `CanPay` / `TryPay` で消費
- プッシャー → バトルの接続は `PPCoinResourceBridge`：`IPPCoinGainNotifier`（実装例 `CoinDropCounter`）の `OnCoinGained` を購読し、`IPPCoinResourceConverter` と `PPBattleParty.CoinConversionRate` を経由してリソースプールへ加算する。**物理側とバトル側を直接結合させず、必ずこのブリッジを挟む**

### パーティ AI

`PPPartyAIStrategistBase`（`IPPPartyCommandStrategist`）がパーティ単位で行動計画（`PPPartyPlan`）を立て、`PPEnemyAIDriver` が `ThinkInterval` 秒ごとにコルーチンで駆動する。

処理の流れ：
1. `PPPartyAIContext.Capture()` でパーティ状況をスナップショット
2. `PPIncomTrendTracker` でリソース増加トレンドをサンプリング
3. `PPPartyAISituationRule` の条件（`PPPartyConditionValidator` 派生の ScriptableObject 群）を評価して状況スコアを解決
4. ユニットごとに行動候補 `PPActionCandidate` を生成し、ロール別重み × 状況スコア × コスト効率でスコアリング
5. `Intelligence` に応じたノイズを載せて選択（知能が低いほど最適解を外す）
6. 「待って溜めた方が良いか」を `EvaluateWaitForUnit()` で判定
7. `PPResourceBudget` で実リソースを優先度順に確保し `MaxActionsPerTick` まで採用

AI のチューニングは基本的に **コードではなく `PPPartyAIProfileDefinition` アセット**（`Assets/GameData/AI/PartyAIProfile/`）で行う。条件アセットは `Assets/GameData/AI/Conditions/` 配下に、`PPConditionMenuAttribute(path, folderPath)` が指すツリー位置に沿って `PPConditionAssetFactory` が自動生成する。新しい条件クラスを足すときは `PPPartyConditionValidator` を継承し、`PPConditionMenuAttribute` と `CreateAssetMenu` を必ず付ける。

乱数は必ず `aContext.Rules.RandomProvider`（`NextInt` / `NextFloat`）を経由する。AI ロジック内で `UnityEngine.Random` を直接使わない。

### UI・入力

`PPBattleCommandInputController` がスタックベースのステートマシンで入力を管理する。

- `IPPBattleInputState` 実装：`PPCommandSelectState`（ターゲット / スキル / 解除 / 詳細）→ `PPUnitSelectState` → `PPDesignateTargetState`（狙う敵の指定）または `PPSkillSelectState` → `PPTargetSelectState`（味方が対象のスキルだけ）。`PPUnitDetailViewState` はユニット選択の次
- 入力中はバトル専用の時計（`BattleContext.Clock`）を止める。確定した内容はその場で実行せず、ユニットの「選択中のスキル」「狙う相手」として持ち、コインゲージが溜まったときに `PPActionScheduler` が使う
- `Enter / Resume / Suspend / Exit` のライフサイクル。メニュー系は `PPBattleMenuStateBase`、ユニット選択系は `PPBattleUnitPickerStateBase` を継承する
- 選択途中の状態は `PPBattleSelectionContext` が保持
- View 側は `PPBattleUnitViewBinder` が `BattleUnit` ↔ `PPBattleUnitView` を対応付ける。表示データは `IPPUnitStatusSource` / `IPPSkillStatusSource` / `IPPItemStatusSource` 経由で供給する

### エディタ拡張

- `LabelAttribute` … インスペクタ表示名を日本語化。属性と `PropertyDrawer` は `Assets/Scripts/AttributeUtility/` 配下
- `EditConditionAttribute` … UE5 の EditCondition/EditConditionHides 相当の条件付き表示
- `CustomConsoleLog` + `Window > Custom Console` … タグ・拡張ログレベル（Verbose/Critical）・送信元オブジェクト付きログ。バトル/AI のトレースはこれを使う
- `Window > Recent Assets` … 最近開いたアセット履歴
- `PPConditionPickerPopup` / `PPConditionTreeView` / `PPConditionAssetFactory` … AI 条件アセットの生成・選択 UI

## コーディング規約

既存コードから読み取れる規約。**新規ファイルもこれに合わせること。**

**ファイルヘッダ**（全 `PPCore` ファイルに付与）:

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
| PPCore の型 | `PP` プレフィックス必須（`BattleManager` など一部の既存型を除く） | `PPBattleUnit`, `PPSkillCommand` |
| ローカル変数 | camelCase | `snap`, `candidates` |

- `Label` / `InspectorName` の表示文字列は **日本語**
- `[SerializeField] private` + `[Label("表示名")]` をセットで書く。インスペクタに素の英語フィールド名を出さない
- 日本語表示文字列は再利用されるものを `PPBattleUtilityDefinition` / `PPTypeAttributeDefinition` の `const` に集約している。ロール名・属性名はハードコードせずこの定数を使う
- **コメントは 1〜2 行にまとめる**。何をするか・なぜそうするかの要点だけを書き、引数や戻り値の列挙、変更・移行の経緯は書かない
- 拡張ポイントは `virtual` / `protected` を積極的に付ける（`BattleManager` の各メソッド、AI のスコア計算群）
- switch 式・`??=`・パターンマッチなど C# 9+ の記法を通常どおり使ってよい

**メンバーの並び順**

- 変数（定数・フィールド・プロパティ・イベント）を先に、関数を後に置く。入れ子の型は先頭の `/* 内部型 */` に置く
- 変数も関数もカテゴリごとにまとめ、`/* カテゴリ名 */` で区切る。カテゴリ内は public → internal → protected → private の順
- `/* */` はカテゴリの区切り専用。型・変数・関数の説明と、関数本体の中のコメントは `//`
- static な変数は `/* static 変数 */` として、定数の次（インスタンスの変数より前）に置く
- public / internal の static 関数は `/* static 関数 */` として関数の先頭に置く。private の static 関数は `/* ヘルパー */` に置く
- コンストラクタは static 関数の次のカテゴリ `/* 生成 */` に置く
- static クラスは全メンバーが static のため、上の 2 つの区分は使わず通常のカテゴリで分ける
- 特定のカテゴリに属さない汎用の補助関数は `/* ヘルパー */` として末尾にまとめる。あるカテゴリ専用の private 関数はそのカテゴリの末尾に置く
- インスペクタに出るフィールドは `/* インスペクタ設定 */` にまとめ、アクセス順ではなく現在の並び（`[Header]` の区分）を保つ
- カテゴリ名は型の内容に合わせて付ける。特に合う名前が無ければ次の語彙を使う
  - 変数: `定数` / `static 変数` / `インスペクタ設定` / `状態` / `プロパティ` / `イベント`
  - 関数: `static 関数` / `生成` / `Unity イベント` / `初期化・接続` / `操作` / `評価・判定` / `問い合わせ` / `表示` / `描画` / `説明文` / `イベントハンドラ` / `ヘルパー`
- メンバーが 3 つ以下、またはカテゴリが 1 つしかない型は区切りを省く
- static フィールドの初期化子が他の static フィールドを参照する場合は、参照先を先に置く（C# は記述順に初期化する）

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

- `Assets/Scripts/*.cs` 直下のプッシャー系スクリプト群は命名規約・ヘッダコメント規約の対象外の旧コード。namespace も持たない
- `PP` プレフィックスを持たない型の改名と、基底・派生ペアの 1 クラス化は未着手（「PPCore の構成」節を参照）
- 本ファイルの「データ定義」「属性とリソース」「パーティ AI」「エディタ拡張」節には、現在のコードと合わない記述が残っている（例: AI 条件は ScriptableObject ではなく `[SerializeReference]` の通常クラスで、ユニット AI はビヘイビアツリー `PPUnitBehaviourTree` で動く）。該当領域を触るときはコードを正とする
