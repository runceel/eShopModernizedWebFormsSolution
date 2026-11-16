# 設計判断の記録

ADR は、複数の構成要素や将来の変更を拘束する判断について、その背景・選択肢・結果を記録する。移行先の現在の合意は [移行先仕様](../target-spec/00-scope-and-constraints.md) を参照する。

`Accepted` は利用者との設計合意を意味し、実装・動作確認済みを意味しない。判断を変更する場合は新しい ADR で置き換え、過去の判断理由を書き換えない。

| ADR | 状態 | 判断 |
|---|---|---|
| [0001](0001-blazor-rendering.md) | Accepted | Fluent UI Blazor と Interactive Server を採用する |
| [0002](0002-service-boundaries.md) | Accepted | Blazor Web と Catalog API を別サービスにする |
| [0003](0003-persistence-and-initialization.md) | Accepted | PostgreSQL の永続化、初期化と明示的リセットを採用する |
| [0004](0004-url-compatibility.md) | Accepted | 現行 URL を維持し、直接 `.aspx` アクセスも扱う |
| [0005](0005-solution-layout.md) | Accepted | ソリューション配置と共有永続化境界を定める |
| [0006](0006-test-strategy.md) | Accepted | 単体・実 DB 統合・構成・限定ブラウザーテストを併用する |
| [0007](0007-initialization-state.md) | Accepted | 初期化状態を永続化し、不明・中断時は安全に停止する |
| [0008](0008-product-concurrency.md) | Accepted | 古い更新を拒否し、競合時のドラフトを保護する |
| [0009](0009-customization-input.md) | Accepted | CSV・画像 ZIP を設定で選び、安全に反映する |
| [0010](0010-local-aspire-operations.md) | Accepted | ローカル readiness・観測・秘密分離・破壊操作確認を定める |
