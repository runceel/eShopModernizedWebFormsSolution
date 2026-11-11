# 6. 既知の不具合・特異挙動・リスク（現行）

現行コードから読み取れる、仕様として明文化されていない挙動の一覧です。

| ID | 事象 | 根拠 | 影響 |
|---|---|---|---|
| KI-01 | 存在しない `id` で詳細・編集・削除を開くと `NullReferenceException`（HTTP 500） | `Catalog/*.aspx.cs` | 利用者にスタックトレース表示 |
| KI-02 | 一覧で `size=0` を指定すると 0 除算で例外 | `PaginatedItemsViewModel` | HTTP 500 |
| KI-03 | 一覧で負の `index` を指定すると例外（DB モード）または不正表示 | `GetCatalogItemsPaginated` | HTTP 500 |
| KI-04 | 最終ページを超える `index` では空表示（「No data was returned.」）で Previous のみ表示 | `Default.aspx.cs` | 軽微 |
| KI-05 | ページャー文言 `Showing {size} of ...` は実件数ではなくページサイズを表示 | `Default.aspx` | 最終ページで件数が不正確 |
| KI-06 | 編集保存時、画面に無い `OnReorder` が常に `false` で上書きされる | `Edit.aspx.cs` | 再発注フラグが消える |
| KI-07 | サーバー側検証が実質無効。保存処理は `Page.IsValid` ではなく `ModelState.IsValid`（Web Forms ではモデルバインディング未使用のため常に true）を見ている。JS 無効時や改ざんで不正値が送信されると `decimal.Parse` / `int.Parse` 例外や範囲外値の保存が起こる | `Create.aspx.cs`, `Edit.aspx.cs` | データ不整合・HTTP 500 |
| KI-08 | 商品名 51 文字以上は画面検証を通過し、DB モードで保存時例外（モックでは保存される） | `CatalogDBContext` | HTTP 500 |
| KI-09 | 詳細・削除画面は `<asp:Label Text='<%# ... %>'>` で HTML エンコードせずに出力。Create / Edit は `ValidateRequest=false` のため、保存した HTML/スクリプトが実行される（格納型 XSS） | `Details.aspx`, `Delete.aspx` | セキュリティ |
| KI-10 | 認証・認可なし、CSRF 対策は ViewState のみ | 全体 | セキュリティ |
| KI-11 | 同時更新の検知なし（後勝ち） | `UpdateCatalogItem` | データ上書き |
| KI-12 | 価格表示の小数桁がモード依存（DB: `19.50`、モック: `19.5`）。通貨記号 `$` は CSS で付与 | `Default.aspx`, `site.css` | 表示差異 |
| KI-13 | 商品 ID はアプリ再起動ごとに最大 9 個の欠番が発生（HiLo） | `CatalogItemHiLoGenerator` | 軽微 |
| KI-14 | log4net レイアウトのプロパティ名不一致で相関 ID が出力されない | `log4Net.xml`, `Global.asax.cs` | ログ品質 |
| KI-15 | `UseCustomizationData=true` で DB 初期化すると `Pics` フォルダの全ファイルを削除して ZIP を展開する | `CatalogDBInitializer` | 画像消失の恐れ |
| KI-16 | CSV のブランド名解決失敗時のエラーメッセージが「type=... does not exist in catalogTypes」と誤っている | `CatalogDBInitializer` | 軽微 |
| KI-17 | モックモードはスレッドセーフでなく、全ユーザーでデータを共有 | `CatalogServiceMock` | 開発用途限定 |
| KI-18 | DB 既存時はスキーマ変更が反映されない（マイグレーションなし） | `CreateDatabaseIfNotExists` | 運用 |
| KI-19 | フッターにサーバーのマシン名を表示（情報露出） | `Site.Master.cs` | 軽微 |
| KI-20 | 未使用資産: `About.aspx`, `Contact.aspx`, `Site.Mobile.Master`, `ViewSwitcher.ascx`, `PictureUri`, `GetBrands/GetTypes`（Edit） | 各ファイル | なし |
