# eShop Catalog manager 現行仕様

ASP.NET Web Forms アプリ **eShopLegacyWebForms**（リポジトリ `runceel/eShopLegacyWebFormsSolution`）の
現行の機能、画面、データ、設定、およびコードから読み取れる特異な挙動をまとめています。
移行先の要件や方式はこの資料に含めず、[GitHub Issues](https://github.com/runceel/eShopModernizedWebFormsSolution/issues) で後日検討します。

根拠ファイルのパスは、特記がない限りレガシーリポジトリの `src/eShopLegacyWebForms/` からの相対パスです。
現行仕様はソースコードの静的解析に基づきます。実行して確認していない推定には「（推定・要実機確認）」と付記します。

## 目次

1. [システム概要・アーキテクチャ](current-spec/01-overview.md)
2. [画面仕様](current-spec/02-screens.md)
3. [業務ロジック・サービス仕様](current-spec/03-business-logic.md)
4. [データ仕様（モデル・DB・初期データ）](current-spec/04-data.md)
5. [実行環境・構成・外部依存](current-spec/05-runtime-and-config.md)
6. [既知の不具合・特異挙動・リスク](current-spec/06-known-issues.md)

## 調査対象

- レガシーリポジトリ: `runceel/eShopLegacyWebFormsSolution`（コミット `df71279`）
- 調査範囲: ソリューション内の全 C# / ASPX / 設定ファイル / 初期データ。サードパーティの JS/CSS ライブラリ本体は対象外
