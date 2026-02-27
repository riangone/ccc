# CHANGELOG

## 2026-02-27（Dashboard・URLリセット修正・パンくず修正）

### 追加

#### 1. Dashboard 画面（`Controllers/DashboardController.cs`、`Views/Dashboard/Index.cshtml`）

アプリのトップページを **Dashboard** に変更しました。
YAML 設定（`config/dashboard.yml`）で定義した統計情報を DB から集計してカード形式で表示します。

**統計定義 YAML（`config/dashboard.yml`）**

```yaml
stats:
  - label: Artists
    labelI18n: { en-US: Artists, zh-CN: 艺术家, ja-JP: アーティスト }
    entity: artist
    aggregate: count       # count / sum / avg
    icon: "🎵"
    color: badge-primary

  - label: Total Revenue
    entity: invoice
    aggregate: sum
    column: Total          # sum / avg の場合に必須
    icon: "💰"
    color: badge-success
```

| フィールド | 説明 |
|-----------|------|
| `entity` | entities.yml で定義したエンティティキー |
| `aggregate` | `count` / `sum` / `avg` |
| `column` | `sum` / `avg` の対象カラム |
| `filter` | WHERE 句（任意）|
| `icon` | アイコン絵文字 |
| `color` | DaisyUI バッジカラークラス |

**新規ファイル:**
- `Models/DashboardConfig.cs`（`DashboardConfig` / `DashboardStatDefinition` モデル）
- `Services/DashboardConfigProvider.cs`（`IDashboardConfigProvider` + 実装）
- `Controllers/DashboardController.cs`（集計クエリ実行）
- `Views/Dashboard/Index.cshtml`（統計カード表示）
- `config/dashboard.yml`（デフォルト統計定義）

#### 2. サイドバーに Dashboard リンク追加（`Views/Shared/_Layout.cshtml`）

サイドメニューの最上部に Dashboard リンクを追加しました。
現在 Dashboard 画面のとき `active` スタイルが適用されます。

### 修正

#### 1. URL リセットバグ（"New Page" ボタン）

**原因**: `Index.cshtml` の "New Page" ボタンの `returnUrl` は `entity` だけのシンプルな URL で、HTMX によるフィルター更新後も更新されなかった。

**修正**: "New Page" ボタンを `Index.cshtml` から `_List.cshtml` に移動しました。
`_List.cshtml` は HTMX によって毎回再描画されるため、常に最新の `currentReturnUrl`（検索・ソート・フィルター状態を含む）を使用します。

#### 2. 保存後 URL リセットバグ（Create / Edit POST）

**原因**: ページモードで保存成功後に `return RedirectToAction(nameof(Index), new { entity })` と状態なしの基本 URL にリダイレクトしていた。

**修正**: `returnUrl` が存在する場合はそこにリダイレクト、なければ基本 Index にフォールバック。

```csharp
// before
return RedirectToAction(nameof(Index), new { entity });

// after
return Redirect(returnUrl ?? Url.Action(nameof(Index), new { entity })!);
```

影響: `DynamicEntityController.Create` (POST) / `DynamicEntityController.Edit` (POST)

#### 3. Cancel ボタン URL リセット（`_Form.cshtml`）

ページモードの Cancel ボタンが `returnUrl` を無視して基本 Index に遷移していた問題を修正しました。
`Context.Request.Query["returnUrl"]` が存在する場合はそこに遷移します。

#### 4. パンくずリスト重複バグ（`FormPage.cshtml`）

**原因**: `BuildBreadcrumbChain(returnUrl)` が既にエンティティのパンくずを生成しているにもかかわらず、`FormPage.cshtml` でエンティティリンクをハードコードしていたため「Customer / Customer / Edit」のような重複が発生していた。

**修正**: パンくずチェーンが存在する場合（returnUrl あり）はハードコードのリンクを省略し、パンくずチェーンが空の場合（直接ナビゲーション時）のみ表示するよう変更。

```razor
@if (breadcrumbs.Count == 0)
{
    <li><a href="@Url.Action("Index", ...)">@Model.Meta.GetDisplayName()</a></li>
}
```

#### 5. パンくず "Home" → "Dashboard" への変更

`Index.cshtml` / `FormPage.cshtml` のパンくず最上位を `Home` から `Dashboard` に変更しました。

### 変更

#### デフォルトルートを Dashboard に変更（`Program.cs`）

```csharp
// before
app.MapControllerRoute(..., pattern: "{controller=DynamicEntity}/{action=Index}/{id?}");

// after
app.MapControllerRoute(..., pattern: "{controller=Dashboard}/{action=Index}/{id?}");
```

---

## 2026-02-27（SQL Server対応・全Chinook YAML・UXバグ修正・フック＆確認ダイアログ）

### 追加

#### 1. SQL Server 方言サポート（`Services/Dialect/`）

データベースプロバイダーごとにページング SQL を切り替える `ISqlDialect` 抽象を導入しました。

| ファイル | 説明 |
|----------|------|
| `Services/Dialect/ISqlDialect.cs` | `AppendNumberedPagination` / `AppendKeysetPagination` / `ConcatOperator` インターフェース |
| `Services/Dialect/SqliteDialect.cs` | `LIMIT @PageSize OFFSET @Offset` による実装 |
| `Services/Dialect/SqlServerDialect.cs` | `ORDER BY ... OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY`。ORDER BY が未指定の場合は主キーで自動補完 |

`Program.cs` の `DatabaseProvider` 設定（`"sqlite"` / `"sqlserver"`）に応じて DI に登録されます。

```json
// appsettings.json
{
  "DatabaseProvider": "sqlite",           // "sqlserver" に変えるだけで切り替え
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=chinook.db",
    "SqlServer": "Server=localhost;Database=Chinook;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

#### 2. SQL Server 用 DB 初期化（`Data/DbInitializer.cs`）

`DatabaseProvider` が `sqlserver` の場合、SQLite の Chinook ダウンロードをスキップし、SQL Server 向け DDL（`IF NOT EXISTS` / `INT IDENTITY(1,1)` 構文）で `AppUser` / `AuditLog` テーブルを作成します。

#### 3. EntityMetadataProvider — プロバイダー別 YAML ディレクトリ（`Services/EntityMetadataProvider.cs`）

- `sqlserver` 時: `config/entities-sqlserver/` を**先に**読み込み、不足エンティティを `config/entities/` で補完
- `sqlite` 時: `config/entities/` のみ読み込み（従来通り）

差分だけを `entities-sqlserver/` に置けばよいため、重複が最小化されます。

#### 4. Chinook 全テーブル YAML（`config/entities/`）

| ファイル | テーブル | 追加内容 |
|----------|----------|----------|
| `mediatype.yml` | MediaType | 一覧・新規・編集・Track へのリンク |
| `playlist.yml` | Playlist | 一覧・新規・編集 |
| `invoiceline.yml` | InvoiceLine | Invoice / Track FK ピッカー対応、Unit Price 範囲フィルター |

#### 5. SQL Server 向け差分 YAML（`config/entities-sqlserver/`）

SQLite と異なる点（文字列連結 `||` → `+`）のみを上書きします。

| ファイル | 変更箇所 |
|----------|----------|
| `customer.yml` | `SupportRepName.expression` を `e.LastName + ', ' + e.FirstName` に変更 |
| `invoice.yml` | `CustomerName.expression` を `c.LastName + ', ' + c.FirstName` に変更 |

#### 6. 確認ダイアログ・前処理・後処理フック（`Services/Hooks/`、`Models/EntityMetadata.cs`）

YAML の `confirmation` / `hooks` セクションで、作成・更新操作に確認ダイアログと前後処理を追加できます（詳細は `docs/confirmation-and-hooks.md` 参照）。

**新規ファイル:**
- `Services/Hooks/EntityHookContext.cs`（`CrudOperation` / `HookResult` / `EntityHookContext`）
- `Services/Hooks/IEntityHook.cs`（フックインターフェース）
- `Services/Hooks/IEntityHookRegistry.cs`（レジストリインターフェース）
- `Services/Hooks/EntityHookRegistry.cs`（DI 経由の名前→実装マップ）
- `Services/Hooks/SampleHooks.cs`（4 種のサンプル実装）

#### 7. リンクラベルの多言語対応（`Models/EntityMetadata.cs`）

`EntityLinkDefinition` に `LabelI18n` プロパティと `GetLabel()` メソッドを追加。
`_List.cshtml` のリンク表示を `link.Label` から `link.GetLabel()` に変更しました。

```yaml
links:
  invoices:
    label: Invoices
    labelI18n: { en-US: Invoices, zh-CN: 发票, ja-JP: 請求書 }
    targetEntity: invoice
    filter: { CustomerId: CustomerId }
```

#### 8. フォームフィールドパーシャル抽出（`Views/DynamicEntity/_FormField.cshtml`）

ページモードとモーダルモードで重複していたフィールド描画 HTML を `_FormField.cshtml` パーシャルビューに切り出し、両モードから `Html.PartialAsync` で参照するよう変更しました。

### 修正

#### 1. フォームフィールド消去バグ（バリデーション・フックエラー時）

`DynamicFormViewModel` に `SubmittedValues` （`Dictionary<string, string?>`）パラメータを追加。バリデーションエラーやフックキャンセル時にも送信値でフォームを再描画するようにしました。

**修正前**: エラー時に `item = null` で VM を組み立てていたためフィールドが空になる。
**修正後**: 送信フォーム値 `form` を `SubmittedValues` として渡し、フィールド値を復元。

影響ファイル: `Controllers/DynamicEntityController.cs`、`Views/DynamicEntity/_Form.cshtml`、`Views/DynamicEntity/_FormField.cshtml`

#### 2. HTMX 確認ダイアログと前処理フックの競合

**根本原因**: HTMX フォームの `submit` イベントで `evt.preventDefault()` を呼んでも HTMX は XHR を送信してしまう。そのため確認ダイアログ表示中にサーバーへリクエストが送られ、フックエラーと確認ダイアログが同時に発生していた。

**修正方法**:
- モーダルモードの `<form>` に `hx-confirm="@(confirmMsg ?? "")"` 属性を付与
- `_Layout.cshtml` の `htmx:confirm` イベントハンドラでカスタムダイアログを表示し、OK 後に `evt.detail.issueRequest(true)` を呼ぶ
- `hx-confirm=""` の場合（確認なし）はダイアログをスキップして即座にリクエスト発行

```javascript
document.body.addEventListener('htmx:confirm', function (evt) {
    var msg = evt.detail.question;
    if (!msg) {
        evt.preventDefault();
        evt.detail.issueRequest(true);  // 確認なし→即リクエスト
        return;
    }
    evt.preventDefault();
    showConfirmDialog(msg, function () { evt.detail.issueRequest(true); });
});
```

#### 3. Razor ビルドエラー修正（`_Form.cshtml`）

`else {}` ブロック内に誤って `@{}` を入れ子にした問題（`RZ1010`）と、`<form>` タグの属性エリアで `@Html.Raw()` を使った問題（`RZ1031`）を修正しました。

### 検証結果

1. `dotnet build` 成功（0 エラー）
2. SQLite モードでの動作変更なし（LIMIT / OFFSET は従来通り）
3. `entities-sqlserver/` の YAML が SQLite フォールバックと正しくマージされることを確認（設計ベース）
4. フォームフィールドがバリデーションエラー後も送信値を保持することを確認（設計ベース）

---

## 2026-02-27（UI改善：パンくず多段化・Newボタン位置変更・エンティティ選択ピッカー）

### 追加

#### 1. パンくずの多段チェーン対応（`Controllers/DynamicEntityController.cs`）
- `BuildBreadcrumbChain(returnUrl)` ヘルパーメソッドを追加
- `returnUrl` クエリパラメータが入れ子になっていることを利用し、再帰的に遡って全遷移履歴を抽出
- 例: Customer → Invoice → Track と遷移すると `Home / Customer / Invoice / Track（現在）` が自動生成される
- `DynamicListViewModel` に `BreadcrumbChain` プロパティを追加
- `DynamicFormViewModel` に `BreadcrumbChain` プロパティを追加
- `CreatePage`・`EditPage` アクションに `returnUrl` パラメータを追加し、フォームページでもパンくずを表示

#### 2. パンくずをタイトルの上方に配置（`Views/DynamicEntity/Index.cshtml`、`Views/DynamicEntity/FormPage.cshtml`）
- パンくず `<nav>` を h1 タイトルより前に移動
- `Model.BreadcrumbChain` を使って全遷移履歴をパンくずリンクとして表示
- `Index.cshtml` の「New Page」ボタンに `returnUrl` を追加（現在のエンティティ一覧URLを引き渡し）
- `FormPage.cshtml` も同様の多段パンくず表示に刷新

#### 3. Newボタンをタイトルの左側に配置（`Views/DynamicEntity/Index.cshtml`）
- 従来: タイトル左寄せ・ボタン右寄せ（`justify-between`）
- 変更後: `[New] [New Page] [h1 タイトル]` の水平並び（`flex items-center gap-3`）

#### 4. エンティティ選択ピッカー（新機能）

**モデル（`Models/EntityMetadata.cs`）**
- `ForeignKeyDefinition` に `Picker: bool`（単一選択）・`MultiPicker: bool`（複数選択）プロパティを追加

**コントローラー（`Controllers/DynamicEntityController.cs`）**
- `PickerList` アクションを追加：HTMX からピッカーテーブル用パーシャルを返す
- `PickerViewModel` レコードを追加（Entity, Meta, Items, TargetField, DisplayColumn, Multi, Search, Page, PageSize, HasMore）
- `BreadcrumbItem` レコードを追加

**ビュー（`Views/DynamicEntity/_Form.cshtml`）**
- `foreignKey.picker: true` の場合、ドロップダウンの代わりに「テキスト表示入力 + Browse ボタン + hidden input」を描画
- `foreignKey.multiPicker: true` の場合、チップ（バッジ）形式で複数選択された値を表示。「+ Browse」で追加、各チップの✕で削除

**ビュー（`Views/DynamicEntity/_FilterControl.cshtml`）**
- `type: entity-picker`：単一選択ピッカーフィルター（Browse ボタン + hidden input + 選択済み表示 + クリアボタン）
- `type: entity-multi-picker`：複数選択ピッカーフィルター（チップ表示 + Browse ボタン + Clear ボタン）

**新規ファイル（`Views/DynamicEntity/_Picker.cshtml`）**
- ピッカーモーダルのコンテンツ（テーブル + ページング）を描画するパーシャルビュー
- テーブル行クリックで選択（行 `data-picker-id` / `data-picker-label` 属性から JS が値を取得）

**レイアウト（`Views/Shared/_Layout.cshtml`）**
- 全ページ共通のエンティティ選択ピッカーモーダル `#entity-picker-modal` を追加
- ピッカー操作 JS 関数群を追加:
  - `openEntityPicker(btn)` — Browse ボタンから設定を読み取ってモーダルを開く
  - `loadPickerContent(search, page)` — HTMX で `PickerList` を呼んでテーブルを更新
  - `entityPickerSearch(value)` — デバウンス300ms 付きインクリメンタル検索
  - `loadPickerPage(page)` — ページング
  - `pickerSelectFromRow(row)` — 行クリック時の単一/複数選択処理
  - `removePickerChip(fieldName, id, el)` — チップ削除
  - `clearPickerValue(fieldName)` — フォームピッカーのクリア
  - `clearPickerFilterValue(fieldName)` — フィルターピッカーのクリア

**ビューインポート（`Views/_ViewImports.cshtml`）**
- `@using DynamicCrudSample.Controllers` を追加（`BreadcrumbItem` 型をビューで直接参照できるように）

#### 5. `_List.cshtml` の「Edit Page」リンクに `returnUrl` を追加
- `EditPage` リンクに `returnUrl = currentReturnUrl` を追加し、フォームページ側でもパンくずが正しく構築されるように対応

#### 6. デモ用 YAML 設定を更新（`config/entities.yml`）
- `album.forms.ArtistId` — `foreignKey.picker: true` を追加（ピッカー単一選択デモ）
- `invoice.forms.CustomerId` — `foreignKey.picker: true` を追加（多数レコードからのピッカー選択デモ）
- `album.filters.ArtistId` — `type: dropdown` → `type: entity-picker` に変更（フィルターピッカーデモ）

### 検証結果
1. `dotnet build` 成功（0 エラー / 8 警告はすべて既存の nullable 注釈警告）
2. Customer → Invoice → Track の3段遷移でパンくずが `Home / Customer / Invoice / Track` と正しく表示されることを確認（設計ベース）
3. Album フォームの ArtistId フィールドでピッカーモーダルが開き、選択値が hidden input へセットされる動作を設計確認
4. `_ViewImports.cshtml` への `using` 追加でビュー内の `BreadcrumbItem` 型参照エラーが解消

---

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
