# 0009: カスタマイズ初期データの選択と反映

- 状態: Accepted
- 記録日: 2026-11-16
- 関連: [OD-14](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/15)、[OD-04](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/5)、[OD-07](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/8)、[OD-10](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/11)

## 背景

ローカルの開発者・操作者は組み込み以外のカタログも投入できる必要がある。現行は CSV と画像 ZIP を使うが、CSV 欠落時の暗黙の組み込みデータへの切替や画像ディレクトリ全削除は、意図しないデータ・画像の消失を招く。DB と画像は別の永続化境界である。

## 決定

組み込みデータを既定とし、AppHost のローカル設定で現行形式の CSV・画像 ZIP を選ぶ。入力配置と相対パスの基準を明確にし、初回投入と明示的リセットで同じ設定境界を使う。設定変更は AppHost 再起動後に反映し、初期化済み DB の変更は確認付きリセットでのみ行う。

必要な入力の不足・不正は停止・通知し、組み込みデータへフォールバックしない。選択画像以外は削除せず、初回投入時に既存画像と衝突する場合や所有関係が不明な場合は変更せず停止する。リセットの検証・失敗時の保護は OD-04 の方針に従う。現在の規則は[技術スペック](../target-spec/01-technical-spec.md#起動時の初期化)と[品質・ローカル運用](../target-spec/03-quality-and-operations.md#db-リセット)を参照する。

## 検討した選択肢

- カスタマイズ機能を廃止する案は、ローカルで独自カタログを投入する用途を満たさない。
- 現行のフォールバックと画像全削除を維持する案は、入力ミスを隠し、既存画像を失う危険がある。
- 実行中の設定変更を即時反映する案は、初期化とリセットの設定一致を複雑にするため第 1 ゴールでは採用しない。

## 結果

- 現行形式を利用できる一方、必要な入力の検証と画像の保護が必要になる。
- 設定変更後は再起動が必要であり、通常の再起動は初期化済みカタログを変更しない。
- DB と画像を単一トランザクションと仮定せず、失敗・中断時の停止と復旧を実装・検証する。
