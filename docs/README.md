# eShop Catalog manager モダナイゼーション ドキュメント

レガシー ASP.NET Web Forms アプリ **eShopLegacyWebForms**（リポジトリ `runceel/eShopLegacyWebFormsSolution`）を
**.NET 11 + Blazor + .NET Aspire + PostgreSQL** へ移行するためのドキュメント群です。

- 第 1 ゴール: ローカルで `aspire run` により一式（Web アプリ + PostgreSQL）が起動し、現行と同等の業務機能が使えること
- 第 2 ゴール（次ステップ、本ドキュメントの対象外）: クラウドへのデプロイ

## 記述ルール

各記述は次の 3 種類のどれかを明示して区別します。

| ラベル | 意味 |
|---|---|
| **[現行]** | 現行ソースコードから確認した事実。根拠ファイルを併記する |
| **[要件]** | 移行先で満たすべき要件（現行仕様の維持、またはユーザー指定の移行条件） |
| **[未確定]** | 実装前に決める必要がある設計判断。`migration/03-open-decisions.md` で管理する |

根拠ファイルのパスは、特記がない限りレガシーリポジトリの `src/eShopLegacyWebForms/` からの相対パスです。
現行仕様はソースコードの静的解析に基づきます。実行して確認していない推定には「（推定・要実機確認）」と付記します。

## 目次

### 現行仕様（As-Is）

1. [システム概要・アーキテクチャ](current-spec/01-overview.md)
2. [画面仕様](current-spec/02-screens.md)
3. [業務ロジック・サービス仕様](current-spec/03-business-logic.md)
4. [データ仕様（モデル・DB・初期データ）](current-spec/04-data.md)
5. [実行環境・構成・外部依存](current-spec/05-runtime-and-config.md)
6. [既知の不具合・特異挙動・リスク](current-spec/06-known-issues.md)

### 移行（To-Be）

1. [移行先要件と対応方針](migration/01-target-requirements.md)
2. [受け入れ基準](migration/02-acceptance-criteria.md)
3. [未確定の設計判断](migration/03-open-decisions.md)

## 調査対象

- レガシーリポジトリ: `runceel/eShopLegacyWebFormsSolution`（コミット `df71279`）
- 調査範囲: ソリューション内の全 C# / ASPX / 設定ファイル / 初期データ。サードパーティの JS/CSS ライブラリ本体は対象外
