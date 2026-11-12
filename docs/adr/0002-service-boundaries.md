# 0002: Web と Catalog API の分離

- 状態: Accepted
- 記録日: 2026-11-12
- 関連: [OD-02](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/3)、[OD-16](https://github.com/runceel/eShopModernizedWebFormsSolution/issues/17)

## 背景

第 1 ゴールでは Aspire で一式をローカル起動する。画面、カタログ操作、永続化の責務境界を明確にする必要がある。

## 決定

Blazor Web と Catalog API を別サービスとし、Web は HTTP/JSON REST の API 経由でカタログデータを操作する。API が PostgreSQL へアクセスし、Web は DB に直接接続しない。スキーマ適用と初期データ投入には別の初期化サービスを設ける。

## 検討した選択肢

Web プロジェクトに画面・サービス・データアクセスを同居させる案と、Web・API を分離する案があった。後者を利用者が選択した。

## 結果

- カタログ操作と DB 接続の境界は API に集約される。
- ローカルでも複数サービスの起動順序、通信と障害通知を考慮する。
- エンドポイント、エラー応答、プロジェクトの名前や配置は別途決定する。
