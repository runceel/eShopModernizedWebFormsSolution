# 5. 実行環境・構成・外部依存（現行）

## 5.1 開発・実行環境

**[現行]**

| 項目 | 内容 | 根拠 |
|---|---|---|
| OS | Windows（IIS / IIS Express 前提） | `.vsconfig`, csproj |
| IDE | Visual Studio 2017 以降（Web, IIS Express, LocalDB コンポーネント） | `.vsconfig`, `eShopLegacyWebForms.sln` |
| ビルド | MSBuild + `packages.config`（NuGet パッケージは `packages/` に復元） | csproj |
| Web サーバー | IIS Express `http://localhost:50586/`（HTTPS なし） | csproj |
| DB | SQL Server LocalDB `(localdb)\MSSQLLocalDB`（DB モード時のみ必要） | `Web.config` |

## 5.2 構成設定（`Web.config`）

| キー | 既定値 | 説明 |
|---|---|---|
| `connectionStrings/CatalogDBContext` | `Data Source=(localdb)\MSSQLLocalDB; Initial Catalog=Microsoft.eShopOnContainers.Services.CatalogDb; Integrated Security=True; MultipleActiveResultSets=True;` | Windows 統合認証 |
| `appSettings/UseMockData` | `true` | `true`: メモリ上のモックデータ、`false`: DB |
| `appSettings/UseCustomizationData` | `false` | DB 初期化時に CSV / ZIP を使うか |
| `system.web/compilation@debug` | `true`（Release 変換で削除） | |
| `system.web/httpRuntime@requestValidationMode` | `2.0` | Create / Edit は `ValidateRequest=false` |
| `system.web/sessionState@mode` | `InProc` | |
| `system.web/globalization` | `culture="en-US" uiCulture="en-US"` | 数値・日付の書式と解析に影響 |

`Web.Release.config` は `debug` 属性の削除のみ。`Web.Debug.config` に実質的な変換はない。

## 5.3 ロギング（log4net）

**[現行]** 根拠: `log4Net.xml`, `Properties/AssemblyInfo.cs`（`XmlConfigurator(ConfigFile = "log4net.xml")`）

| 項目 | 内容 |
|---|---|
| レベル | `ALL` |
| 出力先 | `logFiles\myapp.log`（ローリング、10MB × 最大 5 世代） |
| レイアウト | `%date [%thread] %property{activity} %level %logger - %property{requestinfo}%newline%message%newline%newline` |
| 出力内容 | 各リクエスト開始時の Debug ログ、各画面表示時の INFO ログ（`Now loading... ...`） |

レイアウトは `activity` を参照しているが、`Global.asax.cs` で設定しているプロパティ名は `activityid` のため、相関 ID はログに出力されない（推定・要実機確認）。

## 5.4 テレメトリ（Application Insights）

**[現行]** Application Insights Web SDK 2.9.1 と HTTP モジュール（`ApplicationInsightsWebTracking`, `TelemetryCorrelationHttpModule`）が組み込まれているが、`ApplicationInsights.config` に InstrumentationKey が無いため送信されない。

## 5.5 セッション

**[現行]** InProc セッションに `MachineName` と `SessionStartTime` のみ保存し、フッター表示に使用（業務データは保存しない）。

## 5.6 外部依存

| 依存 | 用途 | 必須性 |
|---|---|---|
| SQL Server LocalDB | 商品データの永続化 | DB モード時のみ |
| ファイルシステム（アプリフォルダ） | 商品画像、ログ、`Setup/*` | 必須 |
| Application Insights | テレメトリ | 実質未使用 |

外部 API 呼び出し、メッセージング、メール送信、キャッシュサーバー、認証基盤への依存はない。

## 5.7 主要 NuGet パッケージ

| パッケージ | バージョン |
|---|---|
| EntityFramework | 6.2.0 |
| Autofac / Autofac.Web | 4.9.1 / 4.0.0 |
| log4net | 2.0.10 |
| Microsoft.ApplicationInsights.* | 2.9.1 |
| Microsoft.AspNet.Web.Optimization | 1.1.3 |
| Microsoft.AspNet.FriendlyUrls | 1.0.2 |
| Microsoft.AspNet.ScriptManager.* / MS Ajax / Modernizr / Respond | — |
| bootstrap | 4.3.1 |
| jQuery / popper.js | 3.5.0 / 1.14.3 |
| Newtonsoft.Json | 12.0.1 |
