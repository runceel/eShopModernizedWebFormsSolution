# 移行先仕様: 目的・前提・対象範囲

> この文書群は合意した移行先の仕様です。実装や動作確認が完了したことを示すものではありません。

## 基本方針

- **確定:** 見た目を除き、現行の画面・操作・業務上の挙動を維持する。現行の基準は [現行仕様](../current-spec/01-overview.md) とする。
- **確定:** 移行先は .NET 11、Blazor、Aspire、PostgreSQL を使用する。UI には Fluent UI Blazor 5.0.1 を使う。
- **確定:** 第 1 ゴールはローカルで `aspire run` により一式が起動し、カタログ管理が利用できること。クラウドへのデプロイは次の段階とする。
- **未決定:** 合意済み事項の実現方法や個別の例外は、下記の Issue で継続して検討する。Issue が開いていることは、記載済みの合意が未確定であることを意味しない。

## 第 1 ゴールの範囲

- 商品一覧・ページング・詳細・登録・編集・削除、既存 URL と画像 URL、フッターのセッション情報。
- 既存の組み込み初期データ、および設定で選択する CSV・画像 ZIP による初期化。直接アクセス可能な About・Contact も URL と内容を維持する。
- PostgreSQL の永続化、起動時の初期化、利用者が明示的に実行する DB リセット。
- ローカル利用時の入力検証、データ保護、同時更新の検知。

既存 SQL Server の実データ移行、公開環境の認証・認可、クラウドデプロイは第 1 ゴールの対象外とする。初期データの ID を維持しても、既存の実データが移行済みになるわけではない。

## 現行からの合意済みの変更

| 項目 | 移行先の扱い |
|---|---|
| 外観・表示名 | Fluent UI のデザインを優先し、アプリ名は `Catalog manager` とする。色やレイアウトの再現は求めない |
| UI・サービス | Interactive Server を基本とし、Blazor Web と Catalog API を別サービスにする |
| データ保存 | アプリのモックモードを廃止し、カタログデータは永続化する PostgreSQL に統一する。画像本体はファイルで管理する |
| 商品 ID | 新規商品は PostgreSQL IDENTITY を使用する。組み込み初期商品の ID 1〜12 は維持する |
| 同時更新 | 現行の後勝ちではなく、競合を検知して通知し、他者の変更を上書きしない |
| 安全性・データ保護 | 出力エンコードとサーバー側検証を行い、編集時の `OnReorder` 消失を防ぐ。内部例外やスタックトレースを画面に出さない |
| 初期化とリセット | CSV・画像 ZIP を選択時、必要なファイルが不足・不正なら初期化を中止する。明示的な確認付きの DB リセットを追加する |
| ID のない商品 `.aspx` URL | 詳細・編集・削除の直接 URL は現行どおり HTTP 500 とする。ただし内部情報は表示しない |

上記以外の既知の不具合は一律に変更すると決めていない。[現行の既知事項](../current-spec/06-known-issues.md) と [OD-10](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/11) を参照する。

## 検討事項の対応状況

各 Issue は継続中。以下の「合意済み」と「残る検討」を分けて扱う。

| Issue | 合意済み | 主に残る検討 |
|---|---|---|
| [OD-01 #2](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/2) | Interactive Server を基本とする | 各画面の実装・.NET 11 との動作確認 |
| [OD-02 #3](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/3) | Web と HTTP/JSON API を分離する | API の具体的な契約 |
| [OD-03 #4](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/4) | 商品 IDENTITY、組み込み初期 ID の維持 | 初期投入後の採番調整方法 |
| [OD-04 #5](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/5) | Migrations、別の初期化サービス、明示的なリセット | 初期化判定、リセットの同期・復旧方法 |
| [OD-05 #6](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/6) | アプリのモックモードは廃止する | テスト用の代替手段 |
| [OD-06 #7](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/7) | Web ホスト名とセッション開始時刻を表示する | セッションの実現方法 |
| [OD-07 #8](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/8) | 通常の停止・再起動で DB を保持する | 保存先の指定と整理手順 |
| [OD-08 #9](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/9) | Fluent UI Blazor 5.0.1 を使用する | コンポーネントの選定と互換性確認 |
| [OD-09 #10](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/10) | 論理データを維持する | PostgreSQL の物理命名 |
| [OD-10 #11](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/11) | セキュリティ・データ保護上の不具合を修正する | その他の KI ごとの扱い |
| [OD-11 #12](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/12) | アプリ名は `Catalog manager` とする | ページごとの細部の表記 |
| [OD-12 #13](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/13) | 第 1 ゴールのローカル利用ではログインなし | 公開・デプロイ時の認証・認可 |
| [OD-13 #14](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/14) | 同時更新を検知し、上書きせず通知する | 競合時の API 契約と画面操作 |
| [OD-14 #15](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/15) | CSV・画像 ZIP を初期化とリセットで利用できる | 設定方法やファイル配置 |
| [OD-15 #16](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/16) | About・Contact の直接 URL と内容を維持する | その他の未使用資産の扱い |
| [OD-16 #17](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/17) | Web、API、初期化サービスを分離する | プロジェクト名とディレクトリ配置 |
| [OD-17 #18](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/18) | [受け入れ基準](04-acceptance-criteria.md) を確認する | 自動化の範囲とツール |
| [OD-18 #19](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/19) | 既存 URL、画像 URL、直接 `.aspx` URL を維持する | その他の異常時の詳細 |
| [OD-19 #20](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/20) | 基本機能と現行挙動を維持する | 合意済み例外以外の個別確認 |
| [OD-20 #21](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/21) | Aspire のローカル起動とリセットコマンド | リソース名、コマンド引数、監視・対応環境 |
| [OD-21 #22](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/22) | PostgreSQL、EF Core・Npgsql、画像ファイルを使用する | 物理スキーマの詳細、将来の実データ移行 |
