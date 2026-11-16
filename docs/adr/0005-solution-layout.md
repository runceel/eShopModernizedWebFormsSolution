# 0005: ソリューションと永続化の配置

- 状態: Accepted
- 記録日: 2026-11-12
- 関連: [OD-16](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/17)、[OD-02](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/3)

## 背景

移行先では Web、API、独立した初期化サービスの境界と Aspire によるルートからの起動を維持しながら、API と初期化サービスが同じスキーマと Migrations を使う必要がある。

## 決定

ルートにソリューション、`src/` に実行単位と共通ライブラリ、`tests/` にテストを配置する。DB モデルと Migrations は API と初期化サービスが参照する共有永続化ライブラリに置き、Web からは参照しない。AppHost は一つとし、ルートから `aspire run` で起動できる構成にする。具体的な配置と依存関係は[技術スペック](../target-spec/01-technical-spec.md#ソリューションとプロジェクトの配置)を参照する。

## 検討した選択肢

- Web と API を同一プロジェクトにする案は、合意済みのサービス境界に反するため採用しない。
- Migrations を API 内に置き、初期化サービスから API を参照する案は依存方向が不明瞭になる。DB の共有部分のみ分離する。
- 全プロジェクトをルート直下に置く案より、`src/` と `tests/` の分離を採用する。

## 結果

- 独立したサービス境界を保ちつつ、DB スキーマの重複を避ける。
- ライブラリとソリューションの管理が必要になる。共有 DTO や追加レイヤーは必要性が確認されるまで固定しない。
- プロジェクトの作成と CLI による起動・依存関係の実証は後続作業である。
