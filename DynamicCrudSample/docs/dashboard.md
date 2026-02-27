# Dashboard 設計・実装ドキュメント

## 概要

`config/dashboard.yml` に統計・グラフ定義を書くだけで、Dashboard ページに統計カードとグラフが表示されます。コードの変更は不要です。

- **統計カード**: クリックするとエンティティ一覧へ遷移
- **グラフ**: Chart.js 4 を使用（棒・折れ線・ドーナツ・円グラフ）
- **データ**: DB から集計（COUNT / SUM / AVG）

---

## YAML 設定リファレンス

### stats セクション（統計カード）

```yaml
stats:
  - label: Total Revenue        # 表示ラベル（デフォルト言語）
    labelI18n:                  # ロケール別ラベル（省略可）
      en-US: Total Revenue
      zh-CN: 总收入
      ja-JP: 総売上
    entity: invoice             # entities.yml で定義したエンティティキー
    aggregate: sum              # count / sum / avg
    column: Total               # sum / avg の場合は必須
    filter: "Total > 0"         # WHERE 句（省略可）
    icon: "💰"                  # アイコン絵文字（省略可）
    color: badge-success        # DaisyUI バッジカラークラス（省略可）
```

| フィールド | 型 | 必須 | 説明 |
|-----------|-----|:----:|------|
| `label` | string | ✅ | デフォルト言語のラベル |
| `labelI18n` | map | — | ロケール別ラベル（`en-US` / `zh-CN` / `ja-JP`） |
| `entity` | string | ✅ | `entities.yml` のエンティティキー名 |
| `aggregate` | string | ✅ | `count` / `sum` / `avg` |
| `column` | string | ※ | `sum` / `avg` 時必須 |
| `filter` | string | — | SQL WHERE 句 |
| `icon` | string | — | 絵文字 |
| `color` | string | — | DaisyUI バッジカラークラス |

---

### charts セクション（グラフ）

```yaml
charts:
  # ── 折れ線グラフ（月別売上）────────────────────────────────────
  - title: Monthly Revenue
    titleI18n:
      en-US: Monthly Revenue
      ja-JP: 月別売上推移
    type: line                              # bar / line / doughnut / pie
    entity: invoice
    valueAggregate: sum
    valueColumn: Total
    groupExpression: "strftime('%Y-%m', InvoiceDate)"  # GROUP BY 式
    orderBy: label                          # label / value（既定: value）
    orderDir: asc                           # asc / desc（既定: desc）
    limit: 24
    colorBg: "rgba(99, 102, 241, 0.15)"
    colorBorder: "rgba(99, 102, 241, 1)"

  # ── ドーナツグラフ（ジャンル別）────────────────────────────────
  - title: Tracks by Genre
    type: doughnut
    entity: track
    valueAggregate: count
    labelJoinEntity: genre          # FK 先エンティティ（JOIN してラベルを取得）
    labelJoinKey: GenreId           # 現テーブルの FK カラム
    labelJoinDisplay: Name          # JOIN 先の表示カラム
    orderBy: value
    orderDir: desc
    limit: 10
    colors:                         # doughnut / pie 用カラーリスト
      - "rgba(99, 102, 241, 0.85)"
      - "rgba(16, 185, 129, 0.85)"
      # ...

  # ── 棒グラフ（国別）────────────────────────────────────────────
  - title: Top 10 Countries by Invoices
    type: bar
    entity: invoice
    valueAggregate: count
    groupExpression: BillingCountry # カラム名をそのまま指定
    orderBy: value
    orderDir: desc
    limit: 10
    colorBg: "rgba(16, 185, 129, 0.7)"
    colorBorder: "rgba(16, 185, 129, 1)"
```

| フィールド | 型 | 必須 | 説明 |
|-----------|-----|:----:|------|
| `title` | string | ✅ | グラフタイトル |
| `titleI18n` | map | — | ロケール別タイトル |
| `type` | string | ✅ | `bar` / `line` / `doughnut` / `pie` |
| `entity` | string | ✅ | エンティティキー |
| `valueAggregate` | string | ✅ | `count` / `sum` / `avg` |
| `valueColumn` | string | ※ | `sum` / `avg` 時必須 |
| `groupExpression` | string | ※ | GROUP BY 式（JOIN 未使用時必須） |
| `labelJoinEntity` | string | — | FK JOIN で取得するラベルの元エンティティ |
| `labelJoinKey` | string | — | 現テーブルの FK カラム名 |
| `labelJoinDisplay` | string | — | JOIN 先の表示カラム名 |
| `orderBy` | string | — | `label` / `value`（既定: `value`） |
| `orderDir` | string | — | `asc` / `desc`（既定: `desc`） |
| `limit` | int | — | 取得件数（既定: 10） |
| `filter` | string | — | SQL WHERE 句 |
| `colorBg` | string | — | 背景色（単色用） |
| `colorBorder` | string | — | 枠線色（単色用） |
| `colors` | list | — | `doughnut`/`pie` 用カラーリスト |

---

## アーキテクチャ

```
起動時（Singleton）
config/dashboard.yml ──→ DashboardConfigProvider ──→ DashboardConfig
                                                         ├── Stats[]
                                                         └── Charts[]

GET /Dashboard/Index
        │
        ├─ BuildStatsAsync()
        │     foreach stat:
        │       SQL = COUNT(*) / SUM(col) / AVG(col)
        │       + WHERE filter
        │       → ExecuteScalarAsync → FormatScalar → DashboardStatViewModel
        │           EntityUrl = /DynamicEntity/Index?entity=xxx
        │
        └─ BuildChartsAsync()
              foreach chart:
                SQL = SELECT {group} as label, {aggregate} as value
                      FROM {table}
                      [JOIN {joinTable} j ON ...]
                      [WHERE {filter}]
                      GROUP BY {group}
                      ORDER BY {col} {dir} LIMIT {n}
                → QueryAsync → labels[] + values[] → JSON serialize
                → DashboardChartViewModel

        DashboardViewModel { Stats[], Charts[] }
                │
                ▼
        Views/Dashboard/Index.cshtml
          ├── stat cards (<a> link → entity list)
          └── <canvas> × N
                │
                ▼
        @section Scripts
          Chart.js 4.4.3 (CDN)
          new Chart(ctx, { type, labels, values, colors, ... })
```

---

## グラフのSQL生成ルール

### シンプル GROUP BY（`groupExpression` 使用）

```sql
SELECT {groupExpression} AS label, {valueExpr} AS value
FROM {Table}
[WHERE {filter}]
GROUP BY {groupExpression}
ORDER BY {orderBy} {orderDir}
LIMIT {limit}
```

例（月別売上）:
```sql
SELECT strftime('%Y-%m', InvoiceDate) AS label, SUM(Total) AS value
FROM Invoice
GROUP BY strftime('%Y-%m', InvoiceDate)
ORDER BY label ASC
LIMIT 24
```

### FK JOIN（`labelJoinEntity` 使用）

```sql
SELECT j.{LabelJoinDisplay} AS label, {valueExpr} AS value
FROM {Table}
JOIN {JoinTable} j ON {Table}.{LabelJoinKey} = j.{JoinPK}
[WHERE {filter}]
GROUP BY j.{LabelJoinDisplay}
ORDER BY {orderBy} {orderDir}
LIMIT {limit}
```

例（ジャンル別トラック数）:
```sql
SELECT j.Name AS label, COUNT(*) AS value
FROM Track
JOIN Genre j ON Track.GenreId = j.GenreId
GROUP BY j.Name
ORDER BY value DESC
LIMIT 10
```

---

## ファイル構成

```
DynamicCrudSample/
├── config/
│   └── dashboard.yml                     # stats + charts 定義
├── Models/
│   └── DashboardConfig.cs                # DashboardConfig / DashboardStatDefinition
│                                         # DashboardChartDefinition
├── Services/
│   └── DashboardConfigProvider.cs        # IDashboardConfigProvider + 実装（Singleton）
├── Controllers/
│   └── DashboardController.cs            # DashboardStatViewModel / DashboardChartViewModel
│                                         # DashboardViewModel / DashboardController
└── Views/
    └── Dashboard/
        └── Index.cshtml                  # カードグリッド + Chart.js 初期化
```

---

## デフォルトグラフ一覧

| グラフ | 種別 | エンティティ | 集計 | 備考 |
|-------|------|------------|------|------|
| Monthly Revenue | `line` | invoice | SUM(Total) | strftime 月別・24ヶ月 |
| Tracks by Genre | `doughnut` | track | COUNT | Genre JOIN |
| Top 10 Countries by Invoices | `bar` | invoice | COUNT | BillingCountry 別 |
| Top 10 Artists by Albums | `bar` | album | COUNT | Artist JOIN |

---

## デフォルト統計カード一覧（12種）

| カード | 集計 | アイコン |
|-------|------|--------|
| Artists | COUNT | 🎵 |
| Albums | COUNT | 💿 |
| Tracks | COUNT | 🎸 |
| Genres | COUNT | 🎼 |
| Media Types | COUNT | 📀 |
| Playlists | COUNT | 📋 |
| Customers | COUNT | 👥 |
| Employees | COUNT | 🧑‍💼 |
| Invoices | COUNT | 📄 |
| Invoice Lines | COUNT | 🧾 |
| Total Revenue | SUM(Total) | 💰 |
| Avg Invoice | AVG(Total) | 📊 |

---

## 新しいグラフを追加する手順

`config/dashboard.yml` の `charts` セクションにエントリを追加するだけです。

```yaml
charts:
  # 既存グラフ ...

  # 追加例: メディアタイプ別トラック数（円グラフ）
  - title: Tracks by Media Type
    type: pie
    entity: track
    valueAggregate: count
    labelJoinEntity: mediatype
    labelJoinKey: MediaTypeId
    labelJoinDisplay: Name
    orderBy: value
    orderDir: desc
    limit: 5
    colors:
      - "rgba(99, 102, 241, 0.85)"
      - "rgba(16, 185, 129, 0.85)"
      - "rgba(245, 158, 11, 0.85)"
      - "rgba(239, 68, 68, 0.85)"
      - "rgba(59, 130, 246, 0.85)"
```

アプリを再起動すると新しいグラフが表示されます（コード変更不要）。

---

## 制約と注意事項

| 項目 | 説明 |
|------|------|
| エンティティ存在確認 | 存在しない entity を指定した場合はスキップ |
| SQL エラー | 集計・グラフクエリが失敗した場合はスキップ |
| `filter` のセキュリティ | SQL に直接埋め込まれます。YAML のアクセス権を適切に管理してください |
| SQLite と SQL Server の互換性 | `groupExpression` に `strftime` を使うと SQLite 専用になります。SQL Server の場合は `FORMAT(col, 'yyyy-MM')` など方言に合わせた式を使用してください |
| 数値フォーマット | `sum` / `avg` は `"N2"` 書式（例: `1,234.56`）で表示 |
| Chart.js バージョン | 4.4.3（CDN）。オフライン環境では `wwwroot/js/` にダウンロードして参照先を変更してください |
