# eShop Catalog manager ドキュメント

ASP.NET Web Forms アプリ **eShopLegacyWebForms**（リポジトリ `runceel/eShopLegacyWebFormsSolution`）の
現行仕様と、移行先について合意した仕様を分けて管理します。
移行先仕様は実装・動作確認の完了を意味しません。未決定事項は [GitHub Issues](https://github.com/runceel/eShopModernizedWebFormsSolution/issues) で検討します。

## 現行仕様

1. [システム概要・アーキテクチャ](current-spec/01-overview.md)
2. [画面仕様](current-spec/02-screens.md)
3. [業務ロジック・サービス仕様](current-spec/03-business-logic.md)
4. [データ仕様（モデル・DB・初期データ）](current-spec/04-data.md)
5. [実行環境・構成・外部依存](current-spec/05-runtime-and-config.md)
6. [既知の不具合・特異挙動・リスク](current-spec/06-known-issues.md)

根拠ファイルのパスは、特記がない限りレガシーリポジトリの `src/eShopLegacyWebForms/` からの相対パスです。
現行仕様はソースコードの静的解析に基づきます。実行して確認していない推定には「（推定・要実機確認）」と付記します。

## 移行先仕様

1. [目的・前提・対象範囲](target-spec/00-scope-and-constraints.md)
2. [技術スペック](target-spec/01-technical-spec.md)
3. [画面・機能仕様](target-spec/02-functional-spec.md)
4. [品質・ローカル運用](target-spec/03-quality-and-operations.md)
5. [受け入れ基準](target-spec/04-acceptance-criteria.md)

重要な設計判断の背景と選択肢は [ADR](adr/README.md) に記録します。

## 調査対象

- レガシーリポジトリ: `runceel/eShopLegacyWebFormsSolution`（コミット `df71279`）
- 調査範囲: ソリューション内の全 C# / ASPX / 設定ファイル / 初期データ。サードパーティの JS/CSS ライブラリ本体は対象外
