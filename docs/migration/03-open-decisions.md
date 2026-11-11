# 3. 未確定の設計判断

実装着手前に決定が必要な事項です。「推奨案」は本ドキュメント作成時点の提案であり、決定ではありません。
決定したら「決定」列を更新してください。

| ID | 論点 | 選択肢 | 推奨案 | 決定 |
|---|---|---|---|---|
| OD-01 | Blazor のレンダリングモード | (a) Static SSR + フォーム送信（Enhanced navigation）／(b) Interactive Server／(c) Interactive WebAssembly / Auto | (a) を基本とし、必要な箇所のみ (b)。現行の PostBack モデルに近く、DB を Web アプリ内から直接扱える | 未 |
| OD-02 | プロジェクト構成 | (a) Web 1 プロジェクトにサービス・データアクセスを同居／(b) API と Web を分離（Catalog API + Blazor フロント） | (a)。第 1 ゴールは現行同等の再現で、分離の必要がない | 未 |
| OD-03 | 商品 ID の採番 | (a) PostgreSQL シーケンス + EF Core HiLo（現行踏襲）／(b) IDENTITY 列 | (b)。欠番の考え方は変わらず実装が簡素。外部連携で ID 体系の互換が必要なら (a) | 未 |
| OD-04 | スキーマ作成・初期データ投入方式 | (a) EF Core Migrations を起動時に適用 + Seed／(b) `EnsureCreated` + Seed／(c) 別のマイグレーション用ワーカーを AppHost に追加 | (c) または (a)。将来のデプロイを考えると Migrations を採用 | 未 |
| OD-05 | モックデータモード（`UseMockData`）の扱い | (a) 廃止（PostgreSQL のみ）／(b) インメモリ実装として残す | (a)。Aspire で PostgreSQL が自動起動するため不要。テストはテスト用の仕組みで代替 | 未 |
| OD-06 | フッターのセッション情報（マシン名・セッション開始時刻） | (a) 廃止／(b) 同等情報を表示（サーバー名・初回アクセス時刻）／(c) アプリのバージョン等に置換 | (a) または (c)。業務機能ではなく情報露出もあるため | 未 |
| OD-07 | PostgreSQL データの永続化 | (a) コンテナボリュームで永続化／(b) 起動ごとに初期化 | (a)。開発中の確認データを保持できる。初期化手順を README に記載 | 未 |
| OD-08 | UI フレームワーク | (a) Bootstrap 4.3.1 を継続して見た目を厳密に維持／(b) Bootstrap 5 へ更新し独自 CSS を調整 | (b)。jQuery 依存をなくせる。受け入れ基準 AC-UI の範囲で見た目を維持 | 未 |
| OD-09 | PostgreSQL の物理名 | (a) 現行どおり PascalCase（引用符付き識別子）／(b) スネークケース（`catalog`, `catalog_brand_id`） | (b)。PostgreSQL の慣習に合わせる | 未 |
| OD-10 | 既知の不具合（KI-01〜20）の修正範囲 | 各項目で再現 / 修正 | [01-target-requirements](01-target-requirements.md) 1.6 の項目を修正、それ以外は現行どおり | 未 |
| OD-11 | 画面上の名称 | 「Catalog manager (Web Forms)」の `(Web Forms)` を維持／`(Blazor)` 等に変更 | `(Blazor)` に変更 | 未 |
| OD-12 | 認証・認可 | (a) 第 1 ゴールでは導入しない（現行どおり匿名）／(b) 導入 | (a)。デプロイ段階で Entra ID 等を検討 | 未 |
| OD-13 | 同時更新制御 | (a) 現行どおり後勝ち／(b) PostgreSQL の `xmin` 等で楽観的同時実行制御 | (a)（第 1 ゴール）。必要になれば (b) | 未 |
| OD-14 | カスタマイズデータ（`UseCustomizationData`、CSV / ZIP） | (a) 廃止／(b) CSV 投入機能を移植 | (a)。既定では未使用で、テスト用データを含む | 未 |
| OD-15 | 未使用資産（About / Contact / Mobile Master / ViewSwitcher） | 移植する / しない | 移植しない | 未 |
| OD-16 | 移行後のソリューション配置 | `eShopModernizedWebFormsSolution` リポジトリのルート直下に `src/` を作成 | 推奨どおり | 未 |
| OD-17 | 自動テスト | (a) サービス層の単体テスト／(b) Aspire.Hosting.Testing による結合テスト／(c) Playwright 等の E2E | (a) + (b) を第 1 ゴールに含める | 未 |
