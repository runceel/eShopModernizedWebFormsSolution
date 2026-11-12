# 0001: Fluent UI Blazor と Interactive Server

- 状態: Accepted
- 記録日: 2026-11-12
- 関連: [OD-01](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/2)、[OD-08](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/9)

## 背景

現行 Web Forms の機能を Blazor に移行する。見た目は現行を再現せず、Fluent UI Blazor 5.0.1 を使用することが指定された。操作を伴う Fluent UI コンポーネントにはインタラクティブなレンダリングが必要である。

## 決定

`Microsoft.FluentUI.AspNetCore.Components` 5.0.1 を使用し、Interactive Server を基本のレンダリングモードとする。アプリ名は `Catalog manager` とする。特定のコンポーネントと .NET 11 の互換性は実装時に検証する。

## 検討した選択肢

- Static SSR を基本として必要な部分だけインタラクティブにする案を先に検討したが、Fluent UI の利用を踏まえて Interactive Server を基本とする判断に変更した。
- 画面ごとの詳細なコンポーネント構成は、この決定では固定しない。

## 結果

- 対話的な UI をサーバー側の実行モデルで構成する。
- 接続状態や再接続の扱いが必要になる。具体的な画面構成と互換性確認は未完了。

参考: [Fluent UI Blazor 5.0.1 の interactivity に関する説明](https://github.com/microsoft/fluentui-blazor/blob/v5.0.1/README.md#6-enable-interactivity)。
