# 0006: 第 1 ゴールの自動テスト層

- 状態: Accepted
- 記録日: 2026-11-12
- 関連: [OD-17](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/18)、[OD-07](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/8)

## 背景

Interactive Server の画面互換性、実 PostgreSQL の永続化、独立初期化と破壊的リセットの安全性は、単一のテスト層では十分に確認できない。移行先には引き継ぐテストツール標準がない。

## 決定

単体、実 PostgreSQL を使う API 統合、実 AppHost を使う構成・運用、限定したブラウザーテストを併用する。xUnit、Testcontainers for .NET、Aspire.Hosting.Testing、Playwright for .NET を採用方針とし、テストは実行ごとに隔離した使い捨ての PostgreSQL・データボリューム・画像領域を使う。開発用の永続資源を破壊操作や並列テストに共有しない。テストの責務は[品質・ローカル運用](../target-spec/03-quality-and-operations.md#テストと対象外)に定める。

## 検討した選択肢

- インメモリ DB のみでは PostgreSQL の Migrations、採番、永続性を検証できない。
- ブラウザー E2E のみで全組合せを確認すると時間と障害切り分けの負担が大きいため、重要な画面・通信経路に絞り、境界値と故障を下位層に置く。
- 全テストで開発用の永続領域を共有すると、リセットや並列実行で利用者のデータを破壊し得るため採用しない。

## 結果

- スキーマ・サービス連携・ユーザー操作を適切な境界で確認でき、アプリにモックモードを追加しない。
- コンテナ、ブラウザー、故障注入の準備とテスト資源の隔離が必要になる。
- ツールの .NET 11 等との互換性、テスト実装・実行、未決定仕様の具体化は後続作業である。
