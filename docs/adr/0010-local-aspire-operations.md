# 0010: ローカル Aspire の準備完了・観測・破壊操作の境界

- 状態: Accepted
- 記録日: 2026-11-16
- 関連: [OD-20 #21](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/21)、[OD-04 #5](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/5)、[OD-07 #8](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/8)、[OD-12 #13](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/13)、[OD-14 #15](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/15)、[OD-17 #18](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/18)

## 背景

第 1 ゴールは .NET 11・Blazor・Aspire・PostgreSQL をローカル `aspire run` で起動する。未認証のため同一 PC に限定し、開発データを保持してテストの破壊操作から分離する。初期化や DB・画像の復旧に失敗した状態を、プロセス生存や以前の成功記録だけで利用可能としてはならない。旧ログの相関 ID 不出力は推定であり実測されていない。

## 決定

- AppHost は DB の健康状態、今回の初期化成功、API/Web readiness の依存を管理する。サービスも実行中・単独起動時の状態を確認し、失敗・不明時は通常操作を止める。
- Web、Dashboard、管理用入口はループバックに限定する。API・DB は必要なサービス間通信だけを許可し、LAN やクラウドへ公開しない。localhost 限定・未認証でも CSRF 保護を行う。
- ServiceDefaults / OpenTelemetry とローカル Dashboard で状態・ログ・トレースを観測する。標準 Activity / W3C TraceContext を使い、秘密値や CSV 内容はログ・trace に送らない。独自相関 ID を要件にしない。
- 資格情報は secret parameter と開発用 User Secrets／環境変数に分離し、必要なサービスだけに渡す。必要な値を解決できなければ失敗として通知する。
- リセットの確認は Dashboard と CLI の両経路で実行側が検証する必須入力とし、対象・消去範囲の不一致や確認欠落時は fail-closed とする。Dashboard の表示だけを確認保証にしない。
- Docker を標準確認経路とする。Windows・macOS・Linux の上流サポートをアプリ実証済みと混同せず、.NET 11、CLI、コンテナ、ブラウザーの具体的な互換性は実装時に確認する。

現在の規則と検証条件は[技術スペック](../target-spec/01-technical-spec.md#ローカル起動の依存関係od-20)、[品質・ローカル運用](../target-spec/03-quality-and-operations.md#local-aspire-operations)、[受け入れ基準](../target-spec/04-acceptance-criteria.md#local-aspire-scenarios)に定める。

## 検討した選択肢

- Running 状態や固定待ち時間を準備完了とする案は、接続不能・初期化途中を見分けられないため採用しない。
- 旧ファイルログ形式と独自 GUID を再現する案は、標準 HTTP 計装・ログ相関との二重管理になるため採用しない。クラウド監視・履歴保管もローカル第一目標を超える。
- Dashboard の確認ダイアログだけに頼る案は、CLI・直接実行で同じ確認を保証できないため採用しない。
- 上流の対応表だけで複数のコンテナランタイムを実証済みとする案は、上流対応とアプリ実証を混同するため採用しない。

## 結果

- 初期化前・復旧不明時の通常操作と、確認なしの破壊操作を防ぎ、標準 trace からサービスを横断して調査できる。
- readiness の業務状態、HTTP 外の計装、対象に対応する確認と排他・復旧の実装、秘密非露出の検証が必要になる。Dashboard は永続監査・公開環境用監視を代替しない。
- 実装・パッケージ互換性、OS 別起動、コマンド入力の伝達、障害注入と相関／秘密非露出は後続検証である。Accepted は設計記録の完了であり実装完了ではない。
