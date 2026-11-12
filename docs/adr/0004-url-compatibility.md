# 0004: 現行 URL と直接アクセスの互換性

- 状態: Accepted
- 記録日: 2026-11-12
- 関連: [OD-18](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/19)、[OD-10](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/11)

## 背景

現行アプリにはルーティングされた URL のほか、直接アクセスできる `.aspx` URL と商品画像 URL がある。移行先では見た目を除く現行挙動を維持することが求められている。

## 決定

既存のブラウザ URL と `/Pics/{name}` を維持し、URL を変更せずに対応する画面・画像を返す。About / Contact の直接アクセスも維持する。

ID のない詳細・編集・削除の直接 `.aspx` URL は HTTP 500 を維持する。ただし、サーバー内部の例外やスタックトレースを利用者に表示しない。初期商品 ID 1〜12 の URL は、組み込み初期データの同じ商品に対応させる。

## 検討した選択肢

新しい URL への移行や、ID のない直接アクセスを別のエラーステータスに変えることも可能だが、利用者は既存 URL と該当する HTTP 500 の維持を選択した。

## 結果

- 既存リンクと初期商品への参照を維持するため、ルーティングに互換性の層が必要になる。
- HTTP 500 は維持するが、内部エラー情報の露出は維持しない。
- 既存 SQL Server の実データは第 1 ゴールで移行しないため、実データ由来の ID と内容の対応は別途検討が必要。
