# LiveCharacterMessage 使用方法

## しっぽの色

ManagerのTail Settingsで対象インデックスのOverride Tail ColorをONにし、Tail Colorを変更します（アルファも設定可能）。OFFでは従来どおりWindow画像の色を使います。
Play Modeで表示中のInspector変更は毎フレーム反映されます。色はしっぽだけに適用し、本文やWindowの色は変更しません。Target追従・固定配置のどちらでも使用できます。

```csharp
messageManager.SetTailColor(0, new Color(1f, 0.5f, 0.5f, 1f));
messageManager.ResetTailColor(0); // Window画像の色へ戻す
```

非表示中にも設定できます。再表示やメッセージのリセットで色設定は失われません。色の上書きを解除するにはResetTailColorを呼んでください。

Canvas配下のLiveCharacterMessageManagerの子にLiveCharacterMessage.prefabを必要な数だけ配置します。
非アクティブの子も取得します。インデックスはHierarchy順の0始まりです（別のManager配下は除外）。
開始時はすべて非表示です。子を追加・削除・並べ替えた場合はRefreshMessages()を呼んでください。
RefreshMessages()は既存の表示をすべてリセットし、インデックスを再構築します。

```csharp
// 0番の吹き出し。表示完了後も残ります。
messageManager.ShowMessage(0, "こんにちは！", () => Debug.Log("表示完了"));
// しっぽを下辺の左寄りへ。位置は0～1（左右の辺では下～上）。
messageManager.SetTailPosition(0, LiveCharacterMessage.TailSide.Bottom, 0.25f);
messageManager.ResetMessage(0);
messageManager.ResetAllMessages();
```

- MessageCountで選択可能な要素数を取得できます。無効なインデックスは警告し、表示要求はfalseを返します。
- 同じインデックスへの再表示は旧表示を非表示・リセットして新しい文字列を先頭から表示します。他の枠は残ります。
- リセット、再表示、無効化で中断した古いコールバックは呼びません。完了コールバックは1回だけ呼びます。
- 空文字列やCharacter Intervalが0の場合はShowMessage内で即座に完了します。
- Managerを無効化すると全メッセージをリセットします。
- LiveCharacterMessageのMessage欄に設定した文字列はShowMessage(index)で表示できます。
- Character Intervalは1文字の表示間隔（初期値0.04秒）。初期設定ではTime.timeScaleに依存しません。
- Paddingは左右それぞれ／上下それぞれの余白。Maximum Widthで折り返し、高さは全文に合わせて拡張します。
- 文字送り中にサイズが揺れないよう、最初に全文でサイズを決めます。TMPのタグは文字送りの対象になりません。
- ManagerのTail Settingsは子と同じインデックスで設定します。未設定分は下辺中央です。
- しっぽは実行時にWindowの子に三角形として生成し、Window画像の色を使います。
- Window/TMPのRectTransformを本機能が制御するため、同じ対象にContentSizeFitter等を併用しないでください。

## 確認

Unity 6000.2.6f2の参照DLLとRoslynで3つのスクリプトのコンパイルを確認済み。
Unity Play Modeでの表示・コールバックの実行確認は未実施です。
確認時は、複数枠同時表示、途中で同じ枠を再表示、個別／全体リセット、長文・改行・空文字、しっぽ4方向を確認してください。

## オブジェクトへしっぽを向ける

ManagerのTail SettingsのTargetに、キャラクターや頭位置などのTransformを指定します。
Targetだけ設定すれば、表示中はLateUpdateで辺・付け根・方向・長さを自動調整します。Side/Positionの調整は不要です。
Tail SettingsのTail Length Offsetで長さだけを補正できます。自動計算した基準長さから、-20で20短く、+20で20長くします（WindowのローカルUI単位）。方向と付け根は変わりません。
先にTarget Tail Max Lengthで基準長さを制限し、その後にオフセットを加算します。例えばTargetまで300・上限120の場合、-20で100、+20で140になります。最終長さは上限を超える場合があります。
Targetを基準に手前／先へ調整したい場合は上限を0にしてください。補正後の長さが0以下ならしっぽだけ隠します。Target未指定時はこの補正を使いません。Inspectorの変更はPlay Modeで表示中の吹き出しに反映されます（編集モードのプレビューには未対応）。
Target Offsetはワールド座標の補正値です。UI対象は対象Canvasのカメラ、ワールド対象はTarget Camera（省略時Camera.main）で画面座標へ変換します。
Screen Space Camera / World SpaceのCanvasにはWorld Cameraを設定してください。
同じCanvas内のTargetはカメラを介さず位置を計算します。
追従時の基準長さはLiveCharacterMessageのTarget Tail Max Length（初期値120）を上限に自動調整します。0にするとTargetまでの距離を基準にします。
Tail Size Xは付け根の幅、Tail Size YはTarget未指定時の固定長です。Tail Corner Paddingは自動配置で角を避ける余白です。
Target指定中に投影できない場合（カメラが未設定、対象がカメラの背後など）は、Sideへ切り替えずしっぽだけ隠します。
対象が吹き出し内部にある場合はしっぽだけ隠します。対象が破棄された場合は固定位置へ戻ります。

```csharp
messageManager.SetTailTarget(0, characterHead);
messageManager.SetTailLengthOffset(0, -20f); // 先端をTargetの手前へ
// ワールド座標のオフセットと撮影カメラも指定可能
messageManager.SetTailTarget(0, character.transform, new Vector3(0f, 1.5f, 0f), characterCamera);
// 追従を終了し、下辺の中央に戻す
messageManager.SetTailPosition(0, LiveCharacterMessage.TailSide.Bottom, 0.5f);
// または直前の固定位置に戻す
messageManager.SetTailTarget(0, null);
```
