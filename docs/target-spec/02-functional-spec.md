# 移行先仕様: 画面・機能

見た目を除いて [現行画面仕様](../current-spec/02-screens.md) と [現行業務ルール](../current-spec/03-business-logic.md) を維持する。下記は合意済みの範囲と明示的な例外であり、未記載の挙動を変更してよいという意味ではない。

## 画面と URL

既存 URL にアクセスした際は、ブラウザ上の URL を変更せず対応する画面または画像を返す。商品 ID を含む URL は、その ID の商品を対象とする。

| URL | 機能 |
|---|---|
| `/`, `/Default`, `/Default.aspx` | 商品一覧 |
| `/Default/index/{index}/size/{size}` | ページ指定の一覧。`index` は 0 始まり |
| `/Catalog/Create`, `/Catalog/Create.aspx` | 商品登録 |
| `/Catalog/Details/{id}` | 商品詳細 |
| `/Catalog/Edit/{id}` | 商品編集 |
| `/Catalog/Delete/{id}` | 商品削除確認 |
| `/Catalog/Details.aspx`, `/Catalog/Edit.aspx`, `/Catalog/Delete.aspx` | ID のない直接アクセス。HTTP 500 を返し、内部例外やスタックトレースを表示しない |
| `/About.aspx`, `/Contact.aspx` | 現行の静的ページの内容を表示する。ナビゲーションに新たなリンクを追加することは求めない |
| `/Pics/{name}` | 商品画像 |

API 用 URL は別途設計する。存在しない商品 ID や不正なページ指定など、上記以外の異常時の詳細は、現行挙動を基準に [OD-10](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/11) で個別に扱う。互換性の判断は [ADR 0004](../adr/0004-url-compatibility.md)。

## 共通表示・遷移

- アプリ名は `Catalog manager` とし、Fluent UI の見た目を採用する。現行の色・レイアウト・フォントの再現は不要。
- 現行の表示項目、操作名、メッセージ、画面遷移は、明記した安全性・データ保護上の例外を除き維持する。
- 数値・日時の入力と表示は、現行の `en-US` カルチャを基準とする。
- ヘッダーから一覧へ戻れる。フッターには Web ホストのマシン名と、その利用者のセッション開始時刻を表示する。
- 登録・編集・削除の成功後、および Cancel / Back to list から一覧へ戻る。詳細から対象商品の編集に進める。

## 商品一覧

- 商品を ID 昇順に表示する。既定は 0 ページ目、1 ページ 10 件。
- 画像、Name、Description、Brand、Type、Price、Picture name、Stock、Restock、Max stock と、Edit / Details / Delete の操作を表示する。
- ページ数・件数と Previous / Next を表示する。商品が 0 件の場合は `No data was returned.` を表示する。
- 組み込み初期データでは 1 ページ目に ID 1〜10、2 ページ目に ID 11〜12 を表示する。CSV を選択した場合はそのデータを表示する。

## 商品詳細・登録・編集・削除

| 画面 | 操作と表示 |
|---|---|
| 詳細 | 対象商品の画像と Name、Description、Brand、Type、Price、Picture name、Stock、Restock、Max stock を表示する |
| 登録 | 上記の入力項目のうち画像はアップロード不可。新規商品の画像名は `dummy.png`、`OnReorder` は `false` |
| 編集 | 現在値を表示し、画像名は編集不可。画面にない `OnReorder` は更新時も既存値を保持する |
| 削除 | 確認文と商品内容を表示し、Delete で商品を物理削除する。Cancel では削除しない |

登録時の Price は `0.00`、Stock / Restock / Max stock は `0`、Brand / Type は先頭項目を初期選択する。編集時は既存の商品・ブランド・タイプの値を表示する。ブランド・タイプの管理画面や画像アップロード機能は追加しない。

## 入力・検証

現行の入力項目、正常値の範囲、既存の検証メッセージを基準とする。クライアント側だけでなくサーバー側でも検証し、不正な入力を保存しない。

| 項目 | 現行を基準とする内容 |
|---|---|
| Name | 必須。空欄時は `The Name field is required.` |
| Description | 任意 |
| Brand / Type | 既存の選択肢から指定する |
| Price | 0〜1,000,000。現行メッセージは `The Price must be a positive number with maximum two decimals between 0 and 1 million.` |
| Stock / Restock / Max stock | 整数の 0〜10,000,000。現行の各項目のメッセージを基準とする |

価格メッセージの「maximum two decimals」と、実際に現行 Validator / DB が受け付ける精度は同一とは限らない。小数桁・空欄・DB 制約違反などの細部は現行挙動を確認し、[OD-10](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/11) で決める。文字列を表示する際は HTML として実行されないようにする。

## 同時更新

複数の利用者が同じ商品を編集した場合、先に保存された後の古い内容による更新は検知して拒否し、利用者に競合を通知する。他の利用者の変更は上書きしない。競合の通知文、再読込・再入力の操作と API 応答は [OD-13](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/14) で決定する。
