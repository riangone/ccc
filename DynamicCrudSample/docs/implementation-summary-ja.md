# DynamicCrudSample 実装変更詳細（日本語）

## 1. 概要
このドキュメントは、現時点までに本プロジェクトへ適用した実装変更を整理したものです。
対象プロジェクト:
`/Users/tt/Desktop/ws/ccc/DynamicCrudSample`

## 2. プロジェクト基盤
1. .NET 10 MVC プロジェクトを生成し、動作可能な構成へ再編成。
2. 動的CRUD用のコントローラ、サービス、モデル、ビューを実体化。
3. HTMXによる部分更新フローを実装。
4. UIにDaisyUIを適用。

主なファイル:
- `Program.cs`
- `Controllers/DynamicEntityController.cs`
- `Services/DynamicCrudRepository.cs`
- `Models/EntityMetadata.cs`

## 3. Filter/Form機能拡張
### 3.1 Filter対応
対応タイプ:
1. `dropdown`
2. `checkbox`（複数値をIN検索）
3. `multi-select`（複数値をIN検索）
4. `range`（`*_min` / `*_max`）
5. `date-range`（`*_from` / `*_to`）

### 3.2 Form対応
対応タイプ:
1. `text`
2. `email`
3. `textarea`
4. `int`
5. `decimal`
6. `double`
7. `bool`
8. `date`
9. `datetime`
10. `select`（`options`）
11. `radio`（`options`）

主なファイル:
- `Views/DynamicEntity/_FilterControl.cshtml`
- `Views/DynamicEntity/_Form.cshtml`
- `Views/DynamicEntity/_List.cshtml`
- `Services/ValueConverter.cs`
- `Services/DynamicCrudRepository.cs`

## 4. Form表示モードの二系統化
1. Create/Edit のモーダル表示対応。
2. Create/Edit のページ遷移モード対応。
3. モーダル保存成功時は一覧を更新し、モーダルを閉じる。
4. バリデーションエラー時はモーダル内に再表示。

追加アクション:
1. `CreateForm` / `EditForm`（モーダル）
2. `CreatePage` / `EditPage`（ページ遷移）

主なファイル:
- `Controllers/DynamicEntityController.cs`
- `Views/DynamicEntity/Index.cshtml`
- `Views/DynamicEntity/FormPage.cshtml`
- `Views/DynamicEntity/_Form.cshtml`

## 5. Chinook DB導入
1. デフォルト接続先を `chinook.db` に変更。
2. 起動時、DBが存在しない場合はChinook SQLiteを自動ダウンロード。
3. Chinookテーブル中心で動的CRUDを検証。

主なファイル:
- `Data/DbInitializer.cs`
- `appsettings.json`

## 6. 認証とユーザー管理
1. Cookie認証を追加。
2. `AppUser` テーブルを追加。
3. `AdminOnly` ポリシーを追加。
4. ログイン、ログアウト、アクセス拒否ページを追加。
5. ユーザー管理画面（一覧、新規、編集）を追加。
6. `DynamicEntityController` を認証必須化。

初期管理者:
1. UserName: `admin`
2. Password: `Admin@123`

主なファイル:
- `Controllers/AccountController.cs`
- `Controllers/UsersController.cs`
- `Models/Auth/AppUser.cs`
- `Services/Auth/UserAuthService.cs`
- `Views/Account/Login.cshtml`
- `Views/Users/Index.cshtml`
- `Views/Users/Edit.cshtml`

## 7. 多言語対応
対応言語:
1. `en-US`
2. `zh-CN`
3. `ja-JP`

実装内容:
1. RequestLocalization有効化。
2. 言語切替コントローラ追加。
3. Layoutに言語切替UI追加。
4. 共通文言をRESX管理。

主なファイル:
- `Program.cs`
- `Controllers/LocalizationController.cs`
- `Views/Shared/_Layout.cshtml`
- `Localization/SharedResource.cs`
- `Resources/Localization.SharedResource.en-US.resx`
- `Resources/Localization.SharedResource.zh-CN.resx`
- `Resources/Localization.SharedResource.ja-JP.resx`

## 8. ログ強化
1. Serilog導入。
2. コンソールログと日次ローテーションファイルログを有効化。
3. HTTPリクエストログを有効化。
4. リポジトリ内にSQL実行ログを追加。
5. DB監査ログ `AuditLog` を追加。
6. 認証、ユーザー管理、CRUD操作を監査ログへ記録。

主なファイル:
- `DynamicCrudSample.csproj`
- `Program.cs`
- `Services/DynamicCrudRepository.cs`
- `Services/Auth/AuditLogService.cs`

## 9. ページ単位YAML化とYAML主導UI
### 9.1 ページごとYAML
`config/entities/*.yml` の分割構成へ変更。

作成済みファイル:
1. `config/entities/customer.yml`
2. `config/entities/employee.yml`
3. `config/entities/artist.yml`
4. `config/entities/album.yml`
5. `config/entities/genre.yml`
6. `config/entities/track.yml`
7. `config/entities/invoice.yml`

### 9.2 YAML内多言語
新規対応:
1. `displayNameI18n`
2. `labelI18n`（columns/forms/filters）

### 9.3 YAML内レイアウト
新規対応:
1. `layout.forms.columns`
2. `layout.forms.order`
3. `layout.filters.columns`
4. `layout.filters.order`

モデルと読み込み拡張:
- `Models/EntityMetadata.cs`
- `Services/EntityMetadataProvider.cs`

ビュー反映:
- `Views/DynamicEntity/Index.cshtml`
- `Views/DynamicEntity/_Form.cshtml`
- `Views/DynamicEntity/_FilterControl.cshtml`
- `Views/DynamicEntity/_List.cshtml`
- `Views/DynamicEntity/FormPage.cshtml`

## 10. 不具合修正履歴
### 10.1 ログイン後 NullReferenceException
事象:
- `_FilterControl` 内で外部キー候補を `dict["Id"]` 前提で参照。
- Chinookの参照先では主キー名が `Id` 以外のテーブルがあるため例外発生。

対応:
1. `GetAllForEntityAsync` で主キーを `AS Id` として返却。
2. `_FilterControl` 側にnullガードを追加。

対象:
- `Services/DynamicCrudRepository.cs`
- `Views/DynamicEntity/_FilterControl.cshtml`

### 10.2 分割YAMLパースエラー
事象:
- `expression` 内のシングルクォートを含む値がYAML解釈で失敗。

対応:
1. 対象 `expression` をダブルクォートで囲って修正。

対象:
- `config/entities/customer.yml`
- `config/entities/invoice.yml`

## 11. 現在の確認状況
1. 最新ビルドは成功。
2. ログイン後の主要ページ確認:
   - `customer`: 200
   - `album`: 200
   - `track`: 200
   - `invoice`: 200
3. 言語切替後の表示確認:
   - `ja-JP` で 200
4. ログ出力確認:
   - `logs/app-YYYYMMDD.log`
   - `AuditLog` テーブル記録

## 13. 追加改善履歴（最新）
### 13.1 ページネーションリンク数の最適化
1. ページ番号リンクを最大5件表示へ変更。
2. 現在ページ中心のウィンドウ表示に変更。

対象:
- `Views/DynamicEntity/_List.cshtml`

### 13.2 レイアウト再設計（左オーバーレイメニュー）
1. ヘッダーを簡素化。
2. ページ一覧を左サイドの開閉メニューへ移動。
3. メニューはオーバーレイ方式で、本文幅に影響しない設計へ変更。
4. 開閉操作:
   - ヘッダーのハンバーガーで開く
   - メニュー内の閉じるボタンで閉じる
   - オーバーレイクリックで閉じる

対象:
- `Views/Shared/_Layout.cshtml`

### 13.3 右側ナビUI調整（DaisyUI参考）
1. 右側を「ドロップダウン中心」構成へ再設計。
2. 検索入力は削除。
3. 言語切替をテキストではなくアイコン（国旗）ボタンへ変更。

対象:
- `Views/Shared/_Layout.cshtml`

### 13.4 CRUDコア改善（安全性・共通化・整合性）
1. SQL安全化:
   - メタデータ検証（table/key/column/join/expression/filter）を追加。
   - 危険トークンを拒否。
   - 識別子・式を許可制に制限。
2. クエリ構築共通化:
   - `GetAllAsync` / `CountAsync` の重複ロジックを統合。
   - `BuildFromClause` / `BuildWhere` / `AppendWhere` を追加。
3. 取引整合性:
   - CRUD + Audit を同一トランザクションで実行。
   - `Insert/Update/Delete` に `IDbTransaction` 対応追加。

対象:
- `Services/DynamicCrudRepository.cs`
- `Controllers/DynamicEntityController.cs`
- `Services/Auth/IAuditLogService.cs`
- `Services/Auth/AuditLogService.cs`

### 13.5 Users管理のトランザクション化
1. `UsersController` の Create/Edit で User更新とAudit記録を同一トランザクション化。
2. `IUserAuthService` と `UserAuthService` を接続/トランザクション受け取り対応に拡張。

対象:
- `Controllers/UsersController.cs`
- `Services/Auth/IUserAuthService.cs`
- `Services/Auth/UserAuthService.cs`

### 13.6 認証可用性改善
1. `AccountController` で監査ログ書き込み失敗がログイン/ログアウト成功を阻害しないよう修正。
2. 監査失敗時は警告ログのみ記録して処理継続。

対象:
- `Controllers/AccountController.cs`

## 15. UI改善（パンくず多段化・ボタン位置・エンティティ選択ピッカー）

### 15.1 パンくず多段チェーン

ページ遷移のたびに `returnUrl` クエリパラメータが入れ子になる構造を活用し、コントローラー側で `BuildBreadcrumbChain()` が再帰的に遡ることで全遷移履歴を `BreadcrumbItem` リストとして生成します。

```
Customer 一覧 → Invoice 一覧（returnUrl=Customer） → Track 一覧（returnUrl=Invoice の URL）
↓ パンくず表示
Home / Customer / Invoice / Track（現在）
```

対象ファイル:
- `Controllers/DynamicEntityController.cs`（`BuildBreadcrumbChain`、`BreadcrumbItem`、`BreadcrumbChain` プロパティ追加）
- `Views/DynamicEntity/Index.cshtml`（パンくずをタイトル上方に移動）
- `Views/DynamicEntity/FormPage.cshtml`（多段パンくず対応）
- `Views/DynamicEntity/_List.cshtml`（EditPage リンクへ `returnUrl` 引き渡し）
- `Views/_ViewImports.cshtml`（`using DynamicCrudSample.Controllers` 追加）

### 15.2 Newボタンをタイトル左側へ配置

```
変更前: [                タイトル ] [ New ] [ New Page ]
変更後: [ New ] [ New Page ] [ タイトル              ]
```

対象ファイル:
- `Views/DynamicEntity/Index.cshtml`

### 15.3 エンティティ選択ピッカー

フォームやフィルターの外部キー項目で、ドロップダウンの代わりに別エンティティの一覧モーダルを開いて行を選択できます。

**YAMLによる設定方法:**

```yaml
# フォームフィールド — 単一選択
ArtistId:
  type: int
  foreignKey:
    entity: artist
    displayColumn: Name
    picker: true        # ドロップダウン→ピッカーモーダルへ

# フォームフィールド — 複数選択（カンマ区切りで保存）
Tags:
  type: string
  foreignKey:
    entity: tag
    displayColumn: Name
    multiPicker: true

# フィルター — 単一ピッカー
ArtistId:
  type: entity-picker
  foreignKey:
    entity: artist
    displayColumn: Name

# フィルター — 複数ピッカー
GenreId:
  type: entity-multi-picker
  foreignKey:
    entity: genre
    displayColumn: Name
```

**動作フロー:**
1. フォーム/フィルターの「Browse...」ボタンをクリック
2. 全ページ共通の `#entity-picker-modal` が開く（DaisyUI dialog）
3. HTMX が `GET /DynamicEntity/PickerList?entity=...&search=...&page=...` を呼び出し、テーブルを表示
4. 検索ボックスへの入力でインクリメンタル検索（デバウンス 300ms）
5. テーブル行クリックで選択
   - 単一選択: 値をセットしてモーダルを閉じる
   - 複数選択: チップとして追加。モーダルは「Done」ボタンで閉じる
6. 選択済みチップの「✕」で個別削除可能

**追加ファイル:**
- `Views/DynamicEntity/_Picker.cshtml`（ピッカー用テーブル + ページングパーシャル）

**変更ファイル:**
- `Models/EntityMetadata.cs`（`ForeignKeyDefinition.Picker` / `MultiPicker` 追加）
- `Controllers/DynamicEntityController.cs`（`PickerList` アクション、`PickerViewModel` 追加）
- `Views/DynamicEntity/_Form.cshtml`（`picker`/`multiPicker` 分岐追加）
- `Views/DynamicEntity/_FilterControl.cshtml`（`entity-picker`/`entity-multi-picker` タイプ追加）
- `Views/Shared/_Layout.cshtml`（ピッカーモーダル HTML + JS 関数群追加）

## 12. YAML定義テンプレート
```yaml
entities:
  entity_key:
    table: TableName
    key: PrimaryKey
    displayName: Entity Name
    displayNameI18n:
      en-US: Entity Name
      zh-CN: 实体名
      ja-JP: エンティティ名
    softDelete: false
    paging:
      pageSize: 10
      mode: numbered
    layout:
      forms:
        columns: 2
        order: [Field1, Field2]
      filters:
        columns: 3
        order: [Filter1, Filter2]
    joins: []
    columns:
      Field1:
        type: string
        label: Field 1
        labelI18n:
          en-US: Field 1
          zh-CN: 字段1
          ja-JP: 項目1
        searchable: true
        sortable: true
    forms:
      Field1:
        type: string
        label: Field 1
        labelI18n:
          en-US: Field 1
          zh-CN: 字段1
          ja-JP: 項目1
        editable: true
    filters:
      Field1:
        type: dropdown
        label: Field 1
        labelI18n:
          en-US: Field 1
          zh-CN: 字段1
          ja-JP: 項目1
        options: [A, B, C]
```

## 14. 運用ルール（更新）
1. 今後、**push を行う前に必ず修改记录（`docs/CHANGELOG.md`）を更新する**。
2. 変更内容、影響範囲、検証結果（最低1つ）を記録してから push する。
