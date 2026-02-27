# Dashboard 設計・実装ドキュメント

## 概要

`config/dashboard.yml` に統計定義を書くだけで、DB から集計した数値を Dashboard ページにカード形式で表示できます。コードの変更は不要です。

---

## YAML 設定リファレンス（`config/dashboard.yml`）

```yaml
stats:
  - label: Artists             # 表示ラベル（デフォルト言語）
    labelI18n:                 # ロケール別ラベル（省略可）
      en-US: Artists
      zh-CN: 艺术家
      ja-JP: アーティスト
    entity: artist             # entities.yml で定義したエンティティキー
    aggregate: count           # count / sum / avg
    icon: "🎵"                # アイコン絵文字（省略可）
    color: badge-primary       # DaisyUI バッジカラークラス（省略可）

  - label: Total Revenue
    entity: invoice
    aggregate: sum
    column: Total              # sum / avg の場合は必須
    icon: "💰"
    color: badge-success

  - label: Avg Track Length
    entity: track
    aggregate: avg
    column: Milliseconds
    filter: "Milliseconds > 0" # WHERE 句（省略可）
    icon: "⏱️"
    color: badge-info
```

### 設定フィールド一覧

| フィールド | 型 | 必須 | 説明 |
|-----------|-----|:----:|------|
| `label` | string | ✅ | デフォルト言語のラベル |
| `labelI18n` | map | — | ロケール別ラベル。現在のロケールが見つかれば優先表示 |
| `entity` | string | ✅ | `entities.yml` のエンティティキー名（テーブル情報を参照） |
| `aggregate` | string | ✅ | `count` / `sum` / `avg` のいずれか |
| `column` | string | ※ | `sum` / `avg` 時は必須。集計するカラム名 |
| `filter` | string | — | SQL の WHERE 句（例: `IsDeleted = 0`） |
| `icon` | string | — | 絵文字または文字。stat-figure として右上に表示 |
| `color` | string | — | DaisyUI バッジカラークラス（例: `badge-primary`） |

### サポートする集計種別

| `aggregate` | 生成 SQL | 用途例 |
|-------------|----------|--------|
| `count` | `SELECT COUNT(*) FROM {Table}` | 件数表示 |
| `sum` | `SELECT COALESCE(SUM({Column}), 0) FROM {Table}` | 売上合計・数量合計 |
| `avg` | `SELECT COALESCE(AVG({Column}), 0) FROM {Table}` | 平均単価・平均評価 |

`filter` が指定されている場合は `WHERE {Filter}` を末尾に追加します。

---

## 処理フロー

```
起動時（Singleton 初期化）
    config/dashboard.yml を読み込み → DashboardConfig オブジェクト化
            │
            ▼
GET /Dashboard/Index
    DashboardConfigProvider.GetConfig() → DashboardConfig.Stats[]
            │
            ▼
    foreach stat in Stats:
        1. _meta.TryGet(stat.Entity) で EntityMetadata 取得（なければスキップ）
        2. aggregate に応じて SQL を組み立て
        3. filter が指定されていれば WHERE 句を追加
        4. db.ExecuteScalarAsync<object>(sql)
        5. 型変換（decimal/double → "N2" 書式）
        6. DashboardStatViewModel に追加
            │
            ▼
    View("Index", stats)
            │
            ▼
    Views/Dashboard/Index.cshtml
        DaisyUI stat コンポーネントでカード表示
```

---

## ファイル構成

```
DynamicCrudSample/
├── config/
│   └── dashboard.yml                     # 統計定義 YAML
├── Models/
│   └── DashboardConfig.cs                # DashboardConfig / DashboardStatDefinition
├── Services/
│   └── DashboardConfigProvider.cs        # IDashboardConfigProvider + 実装
├── Controllers/
│   └── DashboardController.cs            # 集計クエリ実行・ViewModel 組み立て
└── Views/
    └── Dashboard/
        └── Index.cshtml                  # 統計カード表示
```

---

## DI 登録（`Program.cs`）

```csharp
builder.Services.AddSingleton<IDashboardConfigProvider, DashboardConfigProvider>();
```

`DashboardConfigProvider` は起動時に YAML を一度だけ読み込む Singleton です。

---

## 統計カードの見た目（DaisyUI stat コンポーネント）

```
┌─────────────────────────────────┐
│  🎵                             │
│  アーティスト          (icon)   │
│  275                            │
│  [Artists] (badge)              │
└─────────────────────────────────┘
```

DaisyUI の `stat` / `stat-title` / `stat-value` / `stat-figure` / `stat-desc` クラスを使用しています。

---

## 新しい統計を追加する手順

1. `config/dashboard.yml` に新しいエントリを追加するだけです

```yaml
stats:
  # 既存の統計 ...

  # 追加例: プレイリスト数
  - label: Playlists
    labelI18n:
      en-US: Playlists
      zh-CN: 播放列表
      ja-JP: プレイリスト
    entity: playlist
    aggregate: count
    icon: "📋"
    color: badge-warning
```

2. アプリを再起動すると新しいカードが表示されます（コード変更不要）

---

## デフォルト統計一覧（`config/dashboard.yml` 初期値）

| カード | エンティティ | 集計 | アイコン |
|-------|------------|------|--------|
| Artists | artist | COUNT | 🎵 |
| Albums | album | COUNT | 💿 |
| Tracks | track | COUNT | 🎸 |
| Customers | customer | COUNT | 👥 |
| Invoices | invoice | COUNT | 📄 |
| Total Revenue | invoice | SUM(Total) | 💰 |
| Employees | employee | COUNT | 🧑‍💼 |

---

## 制約と注意事項

| 項目 | 説明 |
|------|------|
| entity の存在確認 | 存在しない entity を指定した場合はその統計をスキップします |
| SQL エラー | 集計クエリが失敗した場合はスキップします（Dashboard 全体はクラッシュしません） |
| filter のセキュリティ | `filter` は SQL に直接埋め込まれます。YAML ファイルのアクセス権を適切に管理してください |
| sum/avg の column 省略 | `column` が未指定の場合はその統計をスキップします |
| 数値フォーマット | `decimal` / `double` / `float` は `"N2"` 書式（例: `1,234.56`）で表示します |
