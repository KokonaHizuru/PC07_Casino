# 本編のシーン進行・宴イベント

2026-10-18実装。TitleSceneのNewGameButtonから開始する。

## シーン順

TitleScene → OpeningScene → Scene_HightAndLow → HighCardEndingScene → PokerOpeningScene → Scene_Poker → PokerEndingScene → BlackjackOpeningScene → Scene_Blackjack → BlackjackEndingScene → EndingScene。

各OP・専用エンディングでは宴の終了を待って自動遷移する。各ゲームではNext Roundで続け、指定ラウンド数の最終精算・RoundEndedイベントまで終了したら自動で専用エンディングへ移る。勝ち・負け・引き分け・Foldはすべて1ラウンドとして数える。演出の失敗・Retryや資金不足待ちはラウンド数を進めない。

## Inspectorで設定する場所

| 設定 | 場所 | 初期値 |
|---|---|---|
| ラウンド数 | 各ゲームシーンのPresentationオブジェクトにあるCasinoRoundProgress / Rounds To Play | 5 |
| 専用エンディング | 同コンポーネント / Ending Scene | 対応する専用シーン |
| 基本BET | DuelSceneUI・PokerSceneUI・BlackjackSceneUI / Initial Bet | 10 |
| 本編の所持金 | PlayerData.SetDefaultData | 1000 |
| OP・EDラベルと遷移先 | 各ADVシーンのStoryAdv / StoryAdvScene | シーン専用ラベル・次シーン |

本編シーンはPreview ModeをOFFにして同じGameInstance.playerDataを共有する。所持金・フラグはシーン移行で初期化しない。New GameだけでPlayerData.ResetとBET変更のリセットを実行する。資金不足時の担保・補充システムは従来どおり外部接続が必要で、自動補充は行わない。

## イベントSOの自動登録

以下の**直下**に対応するPresentationEventを作成する。サブフォルダ内は対象外。

- Assets/MyAssets/ScriptableObject/Event/HightCard
- Assets/MyAssets/ScriptableObject/Event/Poker
- Assets/MyAssets/ScriptableObject/Event/Blackjack

命名は `Event_ゲーム名_再生タイミング_固有名`。例：`Event_HightCard_BeforeCardSelection_01`。

各Timingに1つずつ、合計56個の `Event_*_*_Default` を用意した。フラグ未指定・有効・Once Per Round OFF・Stage AfterVisualで、対応するラベル名を表示する。Actor / Action / Phase / Result / Qualificationは既存どおり条件を追加できる。1つのTimingが複数の対象者やカードで発生する場合は、その都度再生する。

エディタは追加・編集・削除・移動、コンパイル後、Play開始前、ビルド前にCasinoEventCatalogを更新する。配列はアセットパス順。各Directorがカタログを参照するため、手動でEvents配列へ追加する必要はない。実行中のフォルダ検索・自動登録は行わない。

手動更新：`Casino > Story > Rebuild Events and Scenarios`。既存のDirector.Events配列はカタログ未指定時の互換用として残している。

## フラグ条件と終了時の更新

- Flag Idが指定されている場合、接続中PlayerData.flag（FlagData）の値がtrueなら対象となる。未登録IDはfalse。
- 同じタイミングで条件に合うフラグ指定イベントを**すべて**選ぶ。フラグIDの重複は制限しない。
- 指定イベントが1つも該当しない場合のみ、条件に合うフラグ未指定イベントをすべて選ぶ。標準演出前後のStageをまたいで判定するため、指定イベントがBeforeVisual、既定イベントがAfterVisualでも二重再生しない。
- 再生対象はそのタイミングの入口で確定する。途中でフラグが変わっても、同じ通知の対象は増減しない。フラグの変更は次のタイミングから選択に反映する。
- Completion FlagsにFlag IdとValueの組を登録すると、そのイベントが正常終了した後に順番に反映する。空のIDは無視する。フラグ更新だけを行いたい場合はScenario Labelを空欄にできる。
- 失敗・中断中は終了フラグを更新しない。Retryでは完了済みのイベント・フラグ更新を繰り返さない。

例：イベント01のFlag Idを `CardSelect01` とし、Completion Flagsに `CardSelect01=false`、`CardSelect02=true` を設定する。次の選択タイミングではイベント02を選べる。

## ADV本文の編集

シナリオの正本は Assets/MyAdv/CasinoStory/ 内のゲーム別XLS。

- CasinoStory_HightCard.xls：19シート（OP・EDを含む）
- CasinoStory_Poker.xls：24シート（OP・EDを含む）
- CasinoStory_Blackjack.xls：19シート（OP・EDを含む）

1ラベルにつき1シート。シート名は31文字制限に合わせてHC・PK・BJの略称を使用し、Command列の正式ラベルは変更しない。
同フォルダ直下の CasinoStory_*.xls を自動収集する。ファイル・シートの追加や削除も反映する。旧 CasinoStory.xls は読み込まない。

先頭行は列名、Command列の `*ラベル名` で開始し、Text列に表示したい文章を入れ、最後に `EndScenario` を置く。仮の62ラベルはラベル名を1ページ表示する。

XLSの保存後、エディタが `.book.asset` を更新し、CasinoStory章をMyAdv.scenariosへ登録する。生成済みbookを直接編集しない。既存のMyAdvのExcelシナリオは維持している。

**SOの追加は自動登録されるが、新しいADV本文は自動生成しない。** 新しいScenario Labelの本文をXLS（または通常の宴シナリオ）へ追加する。SO追加時に編集済みの台詞を上書きしない設計。

## 宴の追加コマンド

| Command | Arg1 | Arg2 | 動作 |
|---|---|---|---|
| CasinoSetBaseBet | HightCard / Poker / Blackjack | 正の整数 | 対象ゲームの次ラウンドから基本BETを変更 |
| CasinoSetDealerRaise | 正の整数 | 空欄 | 待機中のディーラーRaise1回の追加額を指定 |
| CasinoSetFlag | フラグID | TRUE / FALSE | PlayerData.flagを即時更新 |

`HighCard` も `HightCard` の別名として利用可能。ブラックジャックの基本BETは正の偶数のみ。

### 「今回は勝負に出る」の例

HightCardイベントSOのTimingをBeforeAction、ActorをDealer、ActionをRaiseにする。専用ラベルのText列へ台詞を入れ、その後に `CasinoSetDealerRaise` / Arg1=`100` を記述してEndScenarioで終える。

ADV完了後、ディーラーが100を追加する。プレイヤーのCall額は実際の投入額の差分であり、100を支払う。資金不足ならその額で補充を待つ。レイズ指定は使用直後に消え、次回は通常額へ戻る。精算は実投入額から計算し、支払い済みBETを書き換えない。

このコマンドは**ディーラーがRaiseを選択済みのBeforeAction中だけ**使用できる。Raiseを強制する機能は今回追加していない。Checkのときにはこのイベント自体が発生しない。

基本BET変更は、進行中のInitialBet・Raise通常額・ポーカーPlay Bet・ブラックジャックDouble Downへ影響しない。OP内で指定すれば次に始まる最初のラウンドから有効。RoundStartedのADVではそのラウンドの金額は既に固定されているため、変更は次ラウンドから。

C#からは各SceneUI.SetBaseBet(amount)、シーンをまたぐ指定にはGameInstance.Instance.casinoStory.SetBaseBet(game, amount)、今回のRaiseにはHighCardDuelController.Game.SetCurrentDealerRaise(amount)を使用する。

## 検証

2026-10-18：通常進行・コマンド追加進行の両方でEndingSceneへの到達を確認。各ゲーム5ラウンド、全11シーンの順序、PlayerDataの共有、宴からのBET20→次ラウンド30への変更、一度限りのRaise100、終了後フラグ更新を実再生で検証済み。ルール回帰はハイカード86,476件、ブラックジャック626,204件、ポーカー1,618,469件のassertionが通過。

- Unity付属コンパイラでRuntime / Editorコードを検証。
- 3ゲームの既存ルール回帰テストを実行。ハイカードに100のRaise、差額Call、資金不足復帰、一度限りの解除、実額精算を追加。
- Tests/StoryFlow/CasinoStoryValidation.csは**コピーした検証専用プロジェクトのAssets/Editorへ配置するテスト**。本番Assetsへ置かない。
- Runでフラグ選択・更新・フォルダ直下の登録と削除を検証後、実際のNewGameButtonと宴を通して全シーン・各5ラウンド・PlayerDataの共有を検証する。
- RunCommandsはコピー側に検証用シナリオとSOを追加するため、必ず新しいコピーか元のテスト素材を復元したコピーで実行する。実際の宴コマンドによる基本BET変更・今回だけのRaise・終了フラグを検証する。

既存のTitleSceneとEndingSceneに関連して、SaveWindow.StartのsaveButtonObject未設定エラーが検出される。今回の本編進行は最後まで通るが、既存セーブUIの設定は別途修正が必要。
