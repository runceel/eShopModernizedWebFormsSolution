# 移行先仕様: 目的・前提・対象範囲

> この文書群は合意した移行先の仕様です。設計判断の完了は実装・実行・受け入れ確認の完了を意味しません。

## 基本方針

- **確定:** 見た目を除き、現行の画面・操作・業務上の挙動を維持する。現行の基準は [現行仕様](../current-spec/01-overview.md) とする。
- **確定:** 移行先は .NET 11、Blazor、Aspire、PostgreSQL を使用する。UI には Fluent UI Blazor 5.0.1 を使う。
- **確定:** 第 1 ゴールはローカルで `aspire run` により一式が起動し、カタログ管理が利用できること。クラウドへのデプロイは次の段階とする。
- **実装・検証待ち:** 合意した設計をコードへ反映し、受け入れ条件で確認する。未決定の実装詳細は各仕様書に明記し、Issue の状態を実装完了の根拠にしない。

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

## 設計判断と実装確認の境界

OD-01（#2）は PR [#25](https://github.com/runceel/eShopModernizedWebFormsSolution/pull/25) で `main` に統合済み。以下の OD-02〜OD-21（#3〜#22）の設計判断も文書化済みだが、実装・互換性確認・ランタイム試験は未完了であり、Issue が閉じていることも実装完了を意味しない。

| Issue | 合意済みの判断 | 実装・検証で残ること |
|---|---|---|
| [OD-02 #3](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/3) | Web と HTTP/JSON Catalog API を分離し、Web は DB に直接接続しない | API の具体的な契約とサービス間接続 |
| [OD-03 #4](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/4) | 商品 IDENTITY を使い、組み込み商品 ID 1〜12 を維持する。明示 ID 投入後に採番を調整する | 初期化・リセット後や再起動後の採番を実 DB で確認 |
| [OD-04 #5](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/5) | 独立初期化サービス、永続化する初期化状態、失敗時の停止、明示的リセット | 状態記録・Migrations・画像配置・リセットの排他と復旧を実装・検証 |
| [OD-05 #6](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/6) | アプリのモックモードを設けず、テストは層に応じた代替または隔離 PostgreSQL を使う | テスト実装、テスト資源の隔離とツール互換性 |
| [OD-06 #7](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/7) | Blazor Web ホスト名と UI セッション開始時刻を表示する | セッションの実装と画面間・新規セッションの確認 |
| [OD-07 #8](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/8) | 通常停止・再起動を越えて DB と画像を保持する | 名前付きボリュームと画像領域、全削除後の再起動を確認 |
| [OD-08 #9](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/9) | Fluent UI Blazor 5.0.1 を UI 基盤にする。Bootstrap を継続せず、必要な独自 CSS/JS を個別評価する | .NET 11 との互換性、画面・依存の検証 |
| [OD-09 #10](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/10) | PostgreSQL の物理名を小文字 `snake_case` とし、制約名に接頭辞規約を使う | EF Core マッピング・Migration・実 DB メタデータを確認 |
| [OD-10 #11](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/11) | KI-01〜20 を「修正」「現行挙動を維持」「第1ゴール対象外/後続判断」に分類。KI-10 のローカル CSRF は修正する | [既知事項の処置](05-known-issues-policy.md)の確認条件を実装・検証 |
| [OD-11 #12](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/12) | 共通アプリ名・画面タイトル・見出しの既存表記を維持する | 全 URL・画像等の可視箇所を確認 |
| [OD-12 #13](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/13) | 第1ゴールは同一 PC の localhost に限定し、ログインなし。LAN/クラウドへ公開しない | Web/Dashboard/API の到達範囲と CSRF を検証。公開時の認証等は別設計 |
| [OD-13 #14](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/14) | 楽観的競合検知、古い更新拒否、ドラフト保持と明示的破棄確認 | API 契約・原子的更新・画面操作を実装して確認 |
| [OD-14 #15](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/15) | AppHost 設定で現行形式 CSV・画像 ZIP を選択し、再起動後の明示的リセットで反映 | パス解決、入力検証、画像安全性、リセットを確認 |
| [OD-15 #16](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/16) | About/Contact と画像・編集機能は維持。未使用と調査された Web Forms 資産自体は移植しない | 個別の外部カスタマイズ・実運用利用は未確認 |
| [OD-16 #17](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/17) | ルートの `.slnx`、`src/`、`tests/` と共有 `CatalogData` を設ける | プロジェクト作成、ルートからの起動・依存関係を検証 |
| [OD-17 #18](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/18) | 単体・実 PostgreSQL API 統合・AppHost 構成・限定ブラウザーテストを併用する | テスト実装、xUnit/Testcontainers/Aspire.Hosting.Testing/Playwright の互換性と実行 |
| [OD-18 #19](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/19) | 既存の正常 URL・画像 URL・直接アクセスを維持し、リダイレクトしない | HTTP とブラウザーで URL・内容・ステータスを確認 |
| [OD-19 #20](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/20) | 見た目以外の現行機能・表示・操作を維持し、合意済み安全例外を適用 | [機能範囲](02-functional-spec.md#第-1-ゴールの適用範囲と確認)に従い照合 |
| [OD-20 #21](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/21) | readiness 依存、localhost、標準ログ/trace、秘密分離、Dashboard/CLI の fail-closed 確認（[ADR 0010](../adr/0010-local-aspire-operations.md)） | OS/版別起動、入力伝達、故障・相関・非露出の実証 |
| [OD-21 #22](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/22) | PostgreSQL の型・制約・物理名と初期化後の整合性確認を定める | Migration と DB メタデータを照合。SQL Server 実データ移行は対象外 |
