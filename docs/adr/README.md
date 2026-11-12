# 設計判断の記録

ADR は、複数の構成要素や将来の変更を拘束する判断について、その背景・選択肢・結果を記録する。移行先の現在の合意は [移行先仕様](../target-spec/00-scope-and-constraints.md) を参照する。

`Accepted` は利用者との設計合意を意味し、実装・動作確認済みを意味しない。判断を変更する場合は新しい ADR で置き換え、過去の判断理由を書き換えない。

| ADR | 状態 | 判断 |
|---|---|---|
| [0001](0001-blazor-rendering.md) | Accepted | Fluent UI Blazor と Interactive Server を採用する |
| [0002](0002-service-boundaries.md) | Accepted | Blazor Web と Catalog API を別サービスにする |
| [0003](0003-persistence-and-initialization.md) | Accepted | PostgreSQL の永続化、初期化と明示的リセットを採用する |
| [0004](0004-url-compatibility.md) | Accepted | 現行 URL を維持し、直接 `.aspx` アクセスも扱う |
