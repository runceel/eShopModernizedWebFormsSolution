# 2. 受け入れ基準（第 1 ゴール）

移行後アプリが満たすべき確認項目です。特記がない限り、`aspire run` 直後（初期データのみの状態）を前提とします。
KI 番号の項目は、OD-10 で「修正する」と決定した場合の期待値です。

## 起動・基盤（AC-RUN）

| ID | 確認内容 | 期待結果 |
|---|---|---|
| AC-RUN-01 | 前提ツールのみの環境でリポジトリルートから `aspire run` | AppHost が起動し、Aspire ダッシュボードの URL が表示される |
| AC-RUN-02 | ダッシュボードのリソース一覧 | PostgreSQL、DB、Web アプリが Running / Healthy |
| AC-RUN-03 | Web アプリの起動順 | DB の準備完了後に Web アプリが起動する（`WaitFor`） |
| AC-RUN-04 | 初回起動後の DB | スキーマが作成され、ブランド 5・タイプ 4・商品 12 件が登録されている |
| AC-RUN-05 | 再起動（`aspire run` を停止→再実行） | 初期データが重複登録されない。永続ボリューム採用時は前回の変更が残る（OD-07） |
| AC-RUN-06 | ダッシュボードの構造化ログ / トレース | 画面表示に対応するリクエストと DB クエリのトレースが確認できる |
| AC-RUN-07 | ソース上の資格情報 | DB パスワードなどの秘密情報がコミットされていない |

## 画面遷移・URL（AC-NAV）

| ID | 確認内容 | 期待結果 |
|---|---|---|
| AC-NAV-01 | `/` と `/Default` | 商品一覧 1 ページ目を表示 |
| AC-NAV-02 | `/Default/index/1/size/10` | 一覧 2 ページ目を表示 |
| AC-NAV-03 | `/Catalog/Create`, `/Catalog/Details/1`, `/Catalog/Edit/1`, `/Catalog/Delete/1` | 各画面を表示 |
| AC-NAV-04 | 各画面の Cancel / Back to list、ヘッダーロゴ | 一覧（`/`）へ戻る |
| AC-NAV-05 | 共通レイアウト | ヘッダーロゴ、ヒーロー「Catalog manager」、フッターを表示。タイトルは `{画面名} - Catalog manager ...`（サブタイトル表記は OD-11） |

## 商品一覧（AC-LIST）

| ID | 確認内容 | 期待結果 |
|---|---|---|
| AC-LIST-01 | `/` | Id 1〜10 の 10 件を Id 昇順で表示 |
| AC-LIST-02 | 列 | 画像, Name, Description, Brand, Type, Price, Picture name, Stock, Restock, Max stock, 操作（Edit / Details / Delete） |
| AC-LIST-03 | Id 1 の行 | `.NET Bot Black Hoodie` / `.NET` / `T-Shirt` / `$19.50` / `1.png` / 100 / 0 / 0、画像 `/Pics/1.png` |
| AC-LIST-04 | ページャー（1 ページ目） | 「Page 1 - 2」、全 12 件、Previous 非表示、Next 表示 |
| AC-LIST-05 | Next 押下 | Id 11, 12 の 2 件を表示、Previous 表示、Next 非表示 |
| AC-LIST-06 | 全件削除後 | 「No data was returned.」相当の表示 |
| AC-LIST-07 | `/Default/index/0/size/0`, `/Default/index/-1/size/10`（KI-02, KI-03） | エラーにならず既定値で表示（または 400） |

## 商品詳細（AC-DTL）

| ID | 確認内容 | 期待結果 |
|---|---|---|
| AC-DTL-01 | `/Catalog/Details/2` | 画像 `/Pics/2.png` と 9 項目を表示（Price は `$8.50`） |
| AC-DTL-02 | Edit リンク | `/Catalog/Edit/2` へ遷移 |
| AC-DTL-03 | `/Catalog/Details/9999`（KI-01） | 「見つかりません」表示または HTTP 404。例外画面にならない |
| AC-DTL-04 | 名前に `<script>alert(1)</script>` を含む商品を表示（KI-09） | スクリプトは実行されず文字列として表示 |

## 商品登録（AC-CRT）

| ID | 確認内容 | 期待結果 |
|---|---|---|
| AC-CRT-01 | 初期表示 | Price `0.00`、Stock / Restock / Max stock `0`、Brand・Type は先頭選択、画像は「Uploading images not allowed for this version.」 |
| AC-CRT-02 | 正常値で Create | 一覧へ戻り、新商品が末尾に表示。画像名は `dummy.png`、`OnReorder=false` |
| AC-CRT-03 | Name 空欄 | 「The Name field is required.」を表示し保存しない |
| AC-CRT-04 | Price `-1` / `1000001` | 価格のエラーメッセージを表示し保存しない |
| AC-CRT-05 | Stock / Restock / Max stock に `-1` / `10000001` / 非数値 | 各項目のエラーメッセージを表示し保存しない |
| AC-CRT-06 | Name 51 文字（KI-08） | 検証エラーを表示し保存しない |
| AC-CRT-07 | ブラウザの検証を回避して不正値を送信（KI-07） | サーバー側検証で拒否される |
| AC-CRT-08 | 採番 | 既存 ID と重複しない ID が付与される |

## 商品編集（AC-EDT）

| ID | 確認内容 | 期待結果 |
|---|---|---|
| AC-EDT-01 | `/Catalog/Edit/1` | 現在値が入力済み、Brand=`.NET`、Type=`T-Shirt` 選択済み、Picture name は読み取り専用 |
| AC-EDT-02 | 値を変更して Save | 一覧へ戻り変更が反映。画像名は変わらない |
| AC-EDT-03 | 検証 | 登録画面と同じ検証（AC-CRT-03〜07）が働く |
| AC-EDT-04 | `OnReorder=true` の商品を編集保存（KI-06） | `OnReorder` が `true` のまま |
| AC-EDT-05 | `/Catalog/Edit/9999`（KI-01） | 「見つかりません」表示または HTTP 404 |

## 商品削除（AC-DEL）

| ID | 確認内容 | 期待結果 |
|---|---|---|
| AC-DEL-01 | `/Catalog/Delete/3` | 「Are you sure you want to delete this?」と商品内容を表示 |
| AC-DEL-02 | Delete 押下 | 一覧へ戻り、該当商品が表示されない（DB から物理削除） |
| AC-DEL-03 | Cancel | 削除されず一覧へ戻る |
| AC-DEL-04 | `/Catalog/Delete/9999`（KI-01） | 「見つかりません」表示または HTTP 404 |

## 見た目（AC-UI）

| ID | 確認内容 | 期待結果 |
|---|---|---|
| AC-UI-01 | 配色・フォント | 見出し帯 `#00A69C`、主ボタン `#83D01B`、キャンセル `#E52638`、Montserrat フォント |
| AC-UI-02 | 価格表示 | `$` + 小数 2 桁（KI-12） |
| AC-UI-03 | 画像 | `/Pics/{PictureFileName}` から配信され、一覧はサムネイル（最大幅 120px） |
