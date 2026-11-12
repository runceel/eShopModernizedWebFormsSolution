# 0003: 永続化、初期化と明示的なリセット

- 状態: Accepted
- 記録日: 2026-11-12
- 関連: [OD-03](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/4)、[OD-04](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/5)、[OD-05](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/6)、[OD-07](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/8)、[OD-14](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/15)

## 背景

現行は既定で共有インメモリデータを使い、DB モードでは初回作成時にデータを投入する。移行先では PostgreSQL を単一のカタログデータ保存先としつつ、意図的に全商品を削除した状態を再起動後も維持する必要がある。

## 決定

- カタログデータは PostgreSQL に永続化し、アプリのモックモードを設けない。画像本体はファイルで管理する。
- EF Core Migrations と別の初期化サービスを用い、未初期化 DB に設定されたデータを投入する。商品が 0 件という理由だけで再投入しない。
- 新規商品は IDENTITY を使い、組み込み初期商品 ID 1〜12 と、その後の ID の一意性を維持する。
- 組み込みデータ、または CSV・画像 ZIP を設定で選ぶ。必要な入力が不足・不正なら初回初期化もリセットも中止する。
- Aspire の明示的なコマンドでリセットできるようにする。実行時点の有効な設定を使い、確認・入力検証後にデータを再投入する。
- リセット中は通常操作を停止する。選択した画像以外は残し、途中で失敗した場合は開始前の DB・画像への復旧を試みる。

## 検討した選択肢

- インメモリモードを残す代わりに、カタログの保存先を PostgreSQL に統一する。
- 商品が 0 件になったら自動再投入する案ではなく、未初期化時のみ投入し、必要時に明示的にリセットする。
- 商品画像も PostgreSQL に保存する案ではなく、画像本体はファイルとして保持する。

## 結果

- 再起動後も変更を保持できる一方、初期化済み状態を正しく識別する必要がある。
- リセットは破壊的なため、確認、対象の特定、実行中の停止と失敗の通知が必要になる。
- DB と画像の間の同期・復旧方式や、プロセス中断時の具体的な復旧手順は未決定。単一トランザクションでの原子性は前提としない。

参考: [Aspire のカスタムリソースコマンド](https://aspire.dev/fundamentals/custom-resource-commands/)、[aspire resource](https://aspire.dev/reference/cli/commands/aspire-resource/)。
