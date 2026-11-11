# 3. 業務ロジック・サービス仕様（現行）

## 3.1 サービスインターフェース

**[現行]** 根拠: `Services/ICatalogService.cs`

| 操作 | 入力 | 出力 | 利用画面 |
|---|---|---|---|
| `GetCatalogItemsPaginated(pageSize, pageIndex)` | ページサイズ、0 始まりページ番号 | `PaginatedItemsViewModel<CatalogItem>` | 一覧 |
| `FindCatalogItem(id)` | 商品 ID | `CatalogItem`（ブランド・タイプ込み）または `null` | 詳細 / 編集 / 削除 |
| `GetCatalogBrands()` | — | 全ブランド | 登録 / 編集 |
| `GetCatalogTypes()` | — | 全タイプ | 登録 / 編集 |
| `CreateCatalogItem(item)` | 新規商品（ID なし） | なし（ID を採番して item に設定） | 登録 |
| `UpdateCatalogItem(item)` | 全項目を持つ商品 | なし | 編集 |
| `RemoveCatalogItem(item)` | 削除対象（取得済みインスタンス） | なし | 削除 |

すべて同期メソッド。トランザクションは `SaveChanges` 単位の暗黙トランザクションのみ。

## 3.2 ページング（`ViewModel/PaginatedItemsViewModel.cs`）

| プロパティ | 値 |
|---|---|
| `ActualPage` | 要求された `pageIndex`（補正なし） |
| `ItemsPerPage` | 要求された `pageSize`（補正なし） |
| `TotalItems` | 全商品件数（`long`） |
| `TotalPages` | `(int)Math.Ceiling((decimal)TotalItems / pageSize)` |
| `Data` | `Id` 昇順で `Skip(pageSize*pageIndex).Take(pageSize)` |

DB モードでは件数取得とページ取得は別クエリ。ブランド・タイプは `Include` で同時取得。

## 3.3 DB モード（`Services/CatalogService.cs`）

- `FindCatalogItem`: `Include(CatalogBrand).Include(CatalogType).FirstOrDefault(Id == id)`
- `GetCatalogBrands` / `GetCatalogTypes`: 全件 `ToList()`（並び順指定なし）
- `CreateCatalogItem`: `CatalogItemHiLoGenerator` で ID を採番 → `Add` → `SaveChanges`
- `UpdateCatalogItem`: 渡されたインスタンスを `EntityState.Modified` にして全列 UPDATE（差分更新ではない）。楽観的同時実行制御なし（後勝ち）
- `RemoveCatalogItem`: 同一リクエストの DbContext で取得済みのエンティティを `Remove` → `SaveChanges`
- `Dispose`: DbContext を破棄

### 商品 ID の採番（HiLo、`Models/CatalogItemHiLoGenerator.cs`）

**[現行]**

- DB シーケンス `catalog_hilo`（`START WITH 1 INCREMENT BY 10`）を利用
- アプリ内で 10 個単位のブロックを確保: ブロック先頭で `SELECT NEXT VALUE FOR catalog_hilo` を実行し、その値 `n` を返す。以降 9 回は `n+1 … n+9` をメモリから払い出す
- ジェネレーターはアプリ全体で Singleton、`lock` で排他
- アプリ再起動で未使用の残りブロックは破棄されるため ID は欠番になる（例: 初期データ 1〜12 → 再起動後の新規登録は 21 から）
- 商品テーブルの `Id` は DB 側で自動採番しない（`DatabaseGeneratedOption.None`）

## 3.4 モックモード（`Services/CatalogServiceMock.cs`）

**[現行]** `Web.config` の `UseMockData=true`（**既定値**）で有効。

- 初期データは `PreconfiguredData` の 12 商品・5 ブランド・4 タイプ（[04-data](04-data.md) 参照）
- データは Singleton インスタンスのメモリ上の `List` に保持。全ユーザーで共有され、アプリ再起動で初期状態に戻る
- 一覧取得のたびにブランド・タイプのナビゲーションプロパティを補完する
- `CreateCatalogItem`: `Id = 既存最大 Id + 1`
- `UpdateCatalogItem`: 同じ Id の要素を新インスタンスで置換
- `RemoveCatalogItem`: リストから参照一致で削除
- 文字列長などの DB 制約は適用されない
- スレッドセーフではない

## 3.5 業務ルールまとめ

| ID | ルール | 根拠 | 区分 |
|---|---|---|---|
| BR-01 | 商品名は必須 | 画面 Validator / DB `NOT NULL` | [現行] |
| BR-02 | 商品名は最大 50 文字（DB 制約のみ。画面では未検証） | `CatalogDBContext` | [現行] |
| BR-03 | 価格は 0〜1,000,000（画面）。DB は `decimal(18,2)` | 画面 Validator / EF 規約 | [現行] |
| BR-04 | 在庫・再発注点・最大在庫は 0〜10,000,000 の整数 | 画面 Validator / `CatalogItem` 属性 | [現行] |
| BR-05 | 在庫・再発注点・最大在庫の大小関係はチェックしない | — | [現行] |
| BR-06 | 新規商品の画像名は常に `dummy.png`。画像アップロード不可 | `CatalogItem.DefaultPictureName`, Create 画面 | [現行] |
| BR-07 | 画像名は編集不可 | Edit 画面 | [現行] |
| BR-08 | `OnReorder`（再発注中）は画面から入力・表示しない | 全画面 | [現行] |
| BR-09 | ブランド・タイプは画面から追加・編集・削除できない（参照のみ） | — | [現行] |
| BR-10 | 商品は物理削除 | `RemoveCatalogItem` | [現行] |
| BR-11 | 一覧は Id 昇順 | `GetCatalogItemsPaginated` | [現行] |

`CatalogItem` のデータ注釈（`[Range]`, `[RegularExpression]` 等）は Web Forms のモデルバインディングを使っていないため、画面の検証には使われていない。ただし EF6 の `SaveChanges` 時検証では評価される（推定・要実機確認）。
