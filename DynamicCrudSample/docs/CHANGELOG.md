# CHANGELOG

## 2026-02-27（エンティティ間ナビゲーション・状態復元・面パン強化）

### 追加

#### 1. `EntityLinkDefinition` に `filter` フィールドを追加（`Models/EntityMetadata.cs`）
- YAML の `links` セクションで `filter: { targetParam: sourceColumn }` を定義できるようにした
- `targetParam`：遷移先エンティティに渡すクエリパラメータ名
- `sourceColumn`：現在の行から取得するカラム名
- `filter` なし → エンティティレベルリンク（一覧上部に表示）
- `filter` あり → 行単位リンク（アクション列にボタン表示、行のIDを付与して遷移）

#### 2. 行単位ナビゲーションリンク（`Views/DynamicEntity/_List.cshtml`）
- アクション列に `filter` が設定されたリンクを行ごとにボタン表示（`btn-secondary btn-outline`）
- ボタン押下時、`returnUrl` に現在ページの全状態（検索・ソート・フィルタ・ページ・ページサイズ）を含めて遷移
- `currentStateUrl` を `Context.Request.QueryString` から構築することで HTMX 経由のフィルタ値も正確に保持
- エンティティレベルリンク（`filter` なし）は上部セクションに残す構成に変更

#### 3. 遷移元状態の復元とパンくずナビ（`Views/DynamicEntity/Index.cshtml`）
- `returnUrl` を `filter-form` の hidden input に追加し、HTMX 部分更新後も引き継ぎ
- `list-container` の `hx-get` に `hx-include="#filter-form"` を追加して初期ロード時もフィルタ状態を伝播
- 面パン（breadcrumb）に遷移元エンティティ名のリンクを追加（`ReturnDisplayName` → `ReturnUrl`）
- `returnUrl` が存在する場合「← 前の〇〇に戻る」ボタンを表示。状態を維持したまま一覧に戻れる

#### 4. `CreateForm` に `returnUrl` 引き継ぎ（`Views/DynamicEntity/Index.cshtml`、`Views/DynamicEntity/_Form.cshtml`）
- `New` ボタンの `hx-get` に `returnUrl` を追加し、モーダル経由の新規作成後も遷移元を保持
- ページモードフォームの hidden input に `returnUrl` を追加

#### 5. コントローラ側の `returnUrl` 対応（`Controllers/DynamicEntityController.cs`）
- `Index`・`ListPartial`・`Create`・`Edit`・`Delete` の各アクションに `returnUrl` パラメータを追加
- `DynamicListViewModel` に `ReturnUrl`・`ReturnEntity`・`ReturnDisplayName` フィールドを追加
- `CreateListViewModel` ヘルパーメソッドを新設し、`returnUrl` から遷移元エンティティを解析して表示名を自動解決
- `ExtractEntityFromReturnUrl` メソッドを新設。`returnUrl` のクエリ文字列から `entity` パラメータを抽出

#### 6. `customer.yml` — `invoices` リンクに行単位フィルタ追加
- `links.invoices.filter: { CustomerId: CustomerId }` を追加
- 顧客行の「Invoices →」ボタンで、該当顧客の請求書一覧に絞り込み遷移できるようになった

#### 7. `invoice.yml` — `CustomerId` フィルター追加
- `filters.CustomerId`（type: dropdown、expression: `Invoice.CustomerId`、foreignKey: customer）を追加
- `layout.filters.order` に `CustomerId` を先頭追加
- 顧客一覧から遷移した際、顧客名ドロップダウンが自動選択された状態でフィルタが適用される

### 修正

#### 1. `_List.cshtml` の Razor 構文エラー修正
- `@foreach` コードブロック内に誤って `@{ }` ネストが存在した問題を修正（`RZ1010` エラー）
- `@{` と閉じ `}` を除去し、C# 文を直接ブロック内に配置

### 検証結果
1. `dotnet build` 成功（0 エラー / 8 警告はすべて既存のnullable注釈警告）
2. 顧客一覧 → 「Invoices →」行リンク → 請求書一覧（該当顧客で絞り込み済み）の動作を確認
3. 面パン「Customer」リンク → 元のページ・フィルタ状態が完全復元されることを確認

---

## 2026-02-27

### Added
1. Added detailed Japanese file-level comments across custom `.cs` and `.cshtml` files.
2. Added detailed Japanese method-level comments in core backend files (`DynamicCrudRepository`, `DynamicEntityController`, `UserAuthService`).

### Fixed
1. Fixed Razor build issue caused by BOM before `@model` in `Views/Shared/Error.cshtml`.

### Added
1. Optional `count=false` query mode for large datasets to skip `COUNT(*)`.
2. Keyset pagination support via cursor for high-volume list views.

### Added
1. Enforced push workflow note: update modification records before every push.

### Added
1. Added an `isPublic` flag on entity metadata so each YAML definition can opt into appearing in the authenticated sidebar menu.

### Changed
1. Refactored dynamic form item access in [`Views/DynamicEntity/_Form.cshtml`](/Users/tt/Desktop/ws/ccc/DynamicCrudSample/Views/DynamicEntity/_Form.cshtml).
2. Replaced repeated runtime cast/try-catch field reads with safe dictionary access (`TryGetValue`).
3. Removed nullable-related build warnings from form rendering path.
4. Sidebar navigation now waits for login, iterates every YAML definition, and shows a `Public`/`Private` badge plus active-state styling based on the new `isPublic` metadata.
5. `Views/DynamicEntity/Index.cshtml` now renders breadcrumb navigation to keep the current entity context explicit.

### Verification
1. `dotnet build` succeeded with `0 warning / 0 error`.
