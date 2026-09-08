# 第四階段－專案 Excel 匯入－系統設計書

**角色：** SA  
**版本：** v1.0  
**日期：** 2026-09-02  
**狀態：** 設計完成，交 DA  
**依據：** [功能規格書](../需求規劃/第四階段－專案 Excel 匯入－功能規格書.md) v1.0  
**Tech Spike：** 必做（需求類型 ①，含 UI／API）。結論見 [技術可行性評估](第四階段－專案 Excel 匯入－技術可行性評估.md)，已併入本文。

本階段為增量：不改第一、三階段正式文件；沿用既有前後端切分、統一回應、專案／專案議題／分類／行事曆契約。本輪新增工作項目、日期工時、標記完成與實際起迄、Excel 匯入與結果頁。

規格授權 SA 定案、不另開產品路徑者：工作項目跳轉 query、表頭別名、專案本體預計日不從工作項目列回寫、匯入結果不持久化歷程。

---

# 一、系統架構

沿用第一階段：

- **frontend：** React（Vite）SPA，埠 `5173`
- **backend：** ASP.NET Core 9 API Controller，埠 `5080`
- **database：** MySQL 8，庫名 `issue_tracker`

瀏覽器只打後端 HTTP API。EF Core（Pomelo）存取 MySQL。無登入、無 JWT。

統一回應：

```json
{ "code": 200, "message": "success", "data": {} }
```

成功 HTTP 200 且 `code === 200`。業務錯誤 HTTP 400／404／409，body 同上（`data` 可 null）。前端以 `code === 200` 判斷成功。JSON 屬性 **camelCase**。路徑無 `/api` 前綴。

Excel 只在後端解析（ClosedXML，見 Spike）。前端以 `multipart/form-data` 上傳 `.xlsx`，不在瀏覽器讀工作表。不執行巨集。

新畫面重用 `frontend/src/styles.css` 的 `:root` token。確認刪除、匯入對話用 HTML `dialog`。錯誤用頁內橫幅。

# 二、模組拆分

| 模組 | 職責 |
|------|------|
| 專案（既有增量） | 詳情含工作項目區、本體日期工時、標記完成、實際起迄、匯入入口、實際日缺口橫幅；刪專案時連同工作項目、專案議題與其工時一併刪 |
| 工作項目 | 專案底下獨立工作包 CRUD；日期工時；標記完成；行事曆到期日 |
| 專案議題（既有增量） | 加處理人字串、日期工時、標記完成、實際起迄；項次 upsert 語意不變 |
| Excel 匯入 | 驗表、可選文字過濾、upsert、備註解析、比對、整批交易 |
| 日期工時 | 歸屬專案或工作項目或專案議題恰好一個；同一歸屬同一日唯一 |
| 分類（既有增量） | 刪大／小分類時加計工作項目使用筆數 |
| 行事曆（既有增量） | `GET /issues/calendar` 到期日視角再併入有預計完成日的工作項目 |

前端頁面增量：專案詳情工作項目區與工時區；匯入 `dialog`；匯入結果頁。路由：`/projects/:id/import-result`。

不實作：臨時工作、成員主檔、廠商、一份檔多專案、工作項目 TODO、正式議題綁專案、匯入歷程檔。

# 三、功能流程

對齊規格第六章與 EX-F01～EX-F10。

### 3.1 匯入（整批）

1. 使用者在**已存在**的專案詳情開啟匯入 `dialog`（未選專案、新增專案頁不可匯入）。
2. 可選填 `ownerName`（負責人員）、`handlerName`（處理人）；選 `.xlsx`；送出。
3. 後端開檔失敗、非 xlsx、超過大小上限 → 400，資料庫不變。
4. 工作表名稱比對「工作項目」「議題單」：去前後空白、忽略大小寫。缺任一張 → 400 整批不寫入，`message` 說明缺哪一張。
5. 讀表頭列（第一個非完全空白列），依 §11.1 別名對欄。
6. 若檔內專案代號欄（有該欄且儲存格非空白）與目前專案 `code` 忽略大小寫不符 → 400 整批不寫入。無該欄或空白 → 只信路徑上的專案。
7. 「工作項目」列：略過完全空白列。若 `ownerName` 有填，負責人員儲存格須全等（trim、忽略大小寫）才留下。留下後須至少有項次／工作代號或標題，才算**有效列**。有效列為零 → 400 整批不寫入。
8. 「議題單」列：同樣略過空白列；若 `handlerName` 有填則過濾處理人。零列允許。項次空白的列列為異常、不寫入該列。
9. 通過 4～7 後，**單一資料庫交易**寫入：工作項目 upsert、專案議題 upsert、對應備註工時、重算實際開始。列級／行級異常不回滾整批（該列或該行略過）。提交後回 `ImportResult`。
10. 前端進結果頁展示摘要與異常；可跳轉。

Excel「工作項目」列**不**寫入專案本體日期工時，也**不**回寫專案本體預計開始／完成（列級預計日只寫該工作項目）。避免多列衝突。

### 3.2 工作項目唯一鍵（同一專案內）

優先：`workItemCode`（Excel「項次」或「工作代號」有值；trim、忽略大小寫）。此命名空間**獨立於**專案議題 `seqNo`，兩邊可以都是 `1`。

若該列兩欄皆空：改用 `title`＋`ownerName`（trim、忽略大小寫；`ownerName` 空則只比標題）。仍無非空標題 → 該列異常、不寫入。

同一檔內第二列撞同一鍵 → 該列異常、不寫入。網頁有、Excel 無的工作項目不刪，結果列 `webOnlyWorkItem`（資訊、非失敗）。

手建：`workItemCode` 可空；若空，同一專案內「標題＋負責人員」不得與另一筆**同樣無代號**者重複（忽略大小寫）。有代號時以代號唯一為準。

### 3.3 專案議題 upsert

項次 `seqNo` 必填，同一專案忽略大小寫唯一。有則更新標題、內容、處理人（若 Excel 有該欄）、分類（名稱對得到才改）、預計日（§3.5）、該筆備註工時。無則新增。網頁多出項次不刪，結果列 `webOnlyProjectIssue`。

分類：Excel 大／小分類以**名稱**對現有分類（trim、忽略大小寫）。對不到大分類 → 該欄不改（新增列則用與第三階段專案相同的預設大分類：名稱「議題分類」→ 否則「預約」→ 否則列表第一筆）；對不到小分類 → 小分類維持原值或新增時為 null。不因對不到而整列失敗。

### 3.4 日期工時與備註解析

一列＝一個日期＋小時（正數、最多兩位小數、系統上限 999.99）。同一主體同一日只能一列：手改衝突 409；匯入同一日則**更新小時**。

備註可多行。每行獨立：

```
可選西元年/月/日 - 數字H
```

正規語意：`^\s*(\d{4}/)?(\d{1,2})/(\d{1,2})\s*-\s*(\d+(?:\.\d+)?)\s*[Hh]\s*$`  
省略年時用**伺服器當地日曆年**（單人本機即匯入當下西元年）。`H`／`h` 皆可。無法解析的行：`unparseableRemark` 異常，不寫該行；其餘行照寫。

比對在寫入後、針對**本列備註解析出的日期集合**與**該主體匯入前既有工時**（工作項目對工作項目、專案議題對專案議題）：

| kind | 條件 |
|------|------|
| `hoursWebOnly` | 網頁該日有工時、Excel 本列備註沒有該日 |
| `hoursExcelOnly` | Excel 有該日、匯入前網頁無 |
| `hoursDiffer` | 同日小時不同（匯入仍寫成 Excel 值） |
| `unparseableRemark` | 該行無法解析 |

專案本體不做備註比對。

### 3.5 預計日與實際起迄、標記完成

**預計開始／完成：** Excel 該欄有值 → 更新該工作項目或專案議題；空白不覆蓋。兩邊都有但不同 → 寫 Excel，結果 `plannedDateUpdated`。起迄都有值且開始晚於完成 → 該列異常、**不寫該列預計日**（其他欄仍可寫）。日期儲存格以 ClosedXML 日期值為準；純文字則解析常見 `yyyy/M/d`、`yyyy-MM-dd`。

**實際開始（每次工時變更後重算，使用者不手填）：**

- 工作項目／專案議題：該主體日期工時最早日；無工時則 null。
- 專案：專案本體工時 **聯集** 其下**所有工作項目**工時的最早日（不含專案議題工時）。

**標記完成：** 專案、工作項目、專案議題各有獨立操作，不改小分類、不寫死分類名稱。無工時（專案＝本體＋其工作項目皆無）→ 400，不可標記。成功則 `actualEndDate`＝當時該計算範圍最後一筆工時日。標記後再加工時**不**改 `actualEndDate`。取消完成：`actualEndDate`＝null，不刪工時，`isCompleted`＝false。

**Excel 實際開始／結束：** 本輪**永不覆蓋**網頁計算值。空白且網頁已有實際開始或結束 → 列異常 `excelActualBlank`，並將該主體 `actualDateWarning`＝true。Excel 有值且與網頁不同 → 列異常 `excelActualDiffer`，仍不覆蓋。下次匯入該兩欄至少一欄有值 → 將 `actualDateWarning` 設回 false（即使仍不採用 Excel 日期）。使用者可在橫幅略過（`actualDateWarning`＝false）。

### 3.6 跳轉

| 對象 | 路徑 |
|------|------|
| 專案 | `/projects/{projectId}` |
| 工作項目 | `/projects/{projectId}?workItem={workItemId}` |
| 專案議題 | `/projects/{projectId}?item={projectIssueId}`（第三階段既有） |

詳情載入後：`workItem` 捲動並高亮該工作項目列，可打開編輯；找不到則橫幅「找不到該工作項目」。`item` 行為維持第三階段。兩者同時出現時優先 `item`（匯入結果連結一次只帶一個）。

# 四、API 規劃

既有 `/majorCategories`、`/subCategories`、`/issues`、`/todos`、第三階段專案／專案議題路徑維持。寫入專案／專案議題的分類與起迄驗證不變。下列為新增或標明增量。

色彩規則不變。清單／月曆用小分類色；無小分類時 `#8FA8C8`。

### 4.1 專案增量

`GET /projects/{id}` 的 `ProjectDetail` 擴充：

| 欄位 | 說明 |
|------|------|
| `workItemCount` | 工作項目筆數 |
| `workItems` | `WorkItem[]` |
| `hours` | 專案本體 `DailyHour[]` |
| `isCompleted` | 標記完成 |
| `actualStartDate` | 計算值，可 null |
| `actualEndDate` | 僅完成後有值 |
| `actualDateWarning` | 是否顯示 Excel 實際日缺口橫幅 |
| `items` | `ProjectIssue[]` 增量欄見 4.3 |

`ProjectListItem` 加 `workItemCount`（可選顯示）。`itemCount` 仍為專案議題筆數。

`DELETE /projects/{id}` 成功 `data`：`{ deleted, itemCount, workItemCount }`（確認文案用；確認前用詳情數字）。

| 名稱 | 方法 | 路徑 | Request | Response data | Error |
|------|------|------|---------|---------------|-------|
| 標記完成 | PUT | `/projects/{id}/completion` | `{ isCompleted }` | `ProjectDetail` | 404；標記 true 但計算範圍無工時 400 |
| 略過實際日橫幅 | PUT | `/projects/{id}/actual-date-warning` | `{ actualDateWarning: false }` | `ProjectDetail` | 404 |
| 本體加工時 | POST | `/projects/{id}/hours` | `DailyHourWrite` | 該專案 `DailyHour[]` | 404；400；409 同日 |
| 改工時 | PUT | `/projects/{id}/hours/{hourId}` | `DailyHourWrite` | 該專案 `DailyHour[]` | 404；400；409 |
| 刪工時 | DELETE | `/projects/{id}/hours/{hourId}` | 無 | 該專案 `DailyHour[]` | 404 |

`DailyHourWrite`：`date`（必填 `yyyy-MM-dd`）、`hours`（必填、大於 0、≤ 999.99、最多兩位小數）。

`DailyHour`：`id`, `date`, `hours`。

工時寫入成功後後端重算該主體（及專案，若變更的是工作項目工時）的 `actualStartDate`。

`ProjectWrite` **不**含 `isCompleted`、實際日、工時，避免與標記完成流程混用。

### 4.2 工作項目

路徑 `{projectId}` 必須與所屬專案一致，否則 404。

| 名稱 | 方法 | 路徑 | Request | Response data | Error |
|------|------|------|---------|---------------|-------|
| 列表 | GET | `/projects/{projectId}/work-items` | 無 | `WorkItem[]` | 404 專案 |
| 新增 | POST | `/projects/{projectId}/work-items` | `WorkItemWrite` | 該專案完整 `WorkItem[]` | 404；400；409 代號或無代號撞名 |
| 更新 | PUT | `/projects/{projectId}/work-items/{id}` | `WorkItemWrite` | 該專案完整 `WorkItem[]` | 404；400；409 |
| 刪除 | DELETE | `/projects/{projectId}/work-items/{id}` | 無 | 該專案完整 `WorkItem[]` | 404 |
| 標記完成 | PUT | `/projects/{projectId}/work-items/{id}/completion` | `{ isCompleted }` | 該專案完整 `WorkItem[]` | 404；無工時 400 |
| 略過橫幅 | PUT | `/projects/{projectId}/work-items/{id}/actual-date-warning` | `{ actualDateWarning: false }` | 該專案完整 `WorkItem[]` | 404 |
| 加工時 | POST | `/projects/{projectId}/work-items/{id}/hours` | `DailyHourWrite` | 該工作項目 `DailyHour[]` | 404；400；409 |
| 改工時 | PUT | `/projects/{projectId}/work-items/{id}/hours/{hourId}` | `DailyHourWrite` | 該工作項目 `DailyHour[]` | 404；400；409 |
| 刪工時 | DELETE | `/projects/{projectId}/work-items/{id}/hours/{hourId}` | 無 | 該工作項目 `DailyHour[]` | 404 |

`WorkItemWrite`：`workItemCode`（可 null／空＝無代號、最長 50）、`title`（必填、最長 200）、`description`（可空字串、最長 4000）、`ownerName`（可空字串、最長 50）、`majorCategoryId`、`subCategoryId`（可 null）、`startDate`、`dueDate`。分類與起迄驗證同 `ProjectWrite`。不可傳 `projectId` 改掛。

`WorkItem`：`id`, `projectId`, `projectCode`, `workItemCode`, `title`, `description`, `ownerName`, 分類欄（同專案議題）、`startDate`, `dueDate`, `isCompleted`, `actualStartDate`, `actualEndDate`, `actualDateWarning`, `hours`（詳情與列表皆帶；列表可帶以減少來回），`createdAt`, `updatedAt`。

列表排序：有 `workItemCode` 者依代號字串序，其次標題，再 `id`。

### 4.3 專案議題增量

`ProjectIssueWrite` 加 `handlerName`（可空字串、最長 50）。其餘欄不變。

`ProjectIssue` 加：`handlerName`, `isCompleted`, `actualStartDate`, `actualEndDate`, `actualDateWarning`, `hours`。

| 名稱 | 方法 | 路徑 | Request | Response data | Error |
|------|------|------|---------|---------------|-------|
| 標記完成 | PUT | `/projects/{projectId}/items/{id}/completion` | `{ isCompleted }` | 該專案完整 `ProjectIssue[]` | 404；無工時 400 |
| 略過橫幅 | PUT | `/projects/{projectId}/items/{id}/actual-date-warning` | `{ actualDateWarning: false }` | 該專案完整 `ProjectIssue[]` | 404 |
| 加工時 | POST | `/projects/{projectId}/items/{id}/hours` | `DailyHourWrite` | 該議題 `DailyHour[]` | 404；400；409 |
| 改工時 | PUT | `/projects/{projectId}/items/{id}/hours/{hourId}` | `DailyHourWrite` | 該議題 `DailyHour[]` | 404；400；409 |
| 刪工時 | DELETE | `/projects/{projectId}/items/{id}/hours/{hourId}` | 無 | 該議題 `DailyHour[]` | 404 |

新增／更新／刪除專案議題成功仍回該專案完整 `ProjectIssue[]`（含新欄）。

### 4.4 匯入

| 名稱 | 方法 | 路徑 | Request | Response data | Error |
|------|------|------|---------|---------------|-------|
| 匯入 Excel | POST | `/projects/{projectId}/import` | `multipart/form-data`：`file`（必填）、`ownerName`、`handlerName`（可省略） | `ImportResult` | 404 專案；400 見下 |

400 且整批不寫入：`file` 缺、副檔名或內容不是 xlsx、超過 5 MiB、缺工作表、過濾後工作項目有效列為零、專案代號不符。`message` 繁中一句話；`data` 可為 null 或僅含 `aborted: true` 與 `reason`。

列級異常不使用 400：HTTP 200，`data` 為完整 `ImportResult`（含已寫入筆數與 `anomalies`）。

`ImportResult`：

```
projectId
aborted                 布林，成功寫入時 false
workItemsCreated
workItemsUpdated
projectIssuesCreated
projectIssuesUpdated
anomalies: ImportAnomaly[]
```

`ImportAnomaly`：

| 欄位 | 說明 |
|------|------|
| `kind` | 見下表 |
| `sheet` | `workItem`／`projectIssue`／`project`／空 |
| `row` | Excel 列號（1-based，含表頭）；無法對列時 null |
| `line` | 備註行號（1-based）；非備註時 null |
| `workItemId` | 可跳轉時填 |
| `projectIssueId` | 可跳轉時填 |
| `message` | 繁中說明 |
| `path` | 前端跳轉路徑（§3.6）；專案級用專案路徑 |

`kind` 列舉：`missingSheet`（僅整批 400 使用）、`noWorkItemRows`、`projectCodeMismatch`、`blankSeqNo`、`blankTitle`、`duplicateKey`、`plannedDateInvalid`、`plannedDateUpdated`、`unparseableRemark`、`hoursWebOnly`、`hoursExcelOnly`、`hoursDiffer`、`excelActualBlank`、`excelActualDiffer`、`webOnlyWorkItem`、`webOnlyProjectIssue`。

`plannedDateUpdated`、`webOnly*` 為資訊列，不算失敗。

### 4.5 行事曆增量

既有 `GET /issues/calendar?year=&month=` 繼續使用。`source` 新增 `workItem`。

| 欄位 | `workItem` 列 |
|------|----------------|
| `source` | `workItem` |
| `kind` | 僅 `due` |
| `id` | 工作項目 id |
| `projectId` | 所屬專案 id |
| `label` | `{projectCode} {workItemCode}`；無代號則 `{projectCode}`＋標題截斷 |
| `title` | 工作項目標題 |
| `content` | 標題（hover） |
| `date`／`dueDate` | 工作項目 `dueDate` |

查詢：該月內、`dueDate` 有值的工作項目。不回傳工作項目 `startDate`。不為工作項目建立 `plan` 列。

前端必須依 `source === "workItem"` 連到 `/projects/{projectId}?workItem={id}`，不可連 `/issues/{id}`。

### 4.6 分類刪除增量

`DELETE /majorCategories/{id}`、`DELETE /subCategories/{id}` 路徑不變。409 條件改為：正式議題 **或** 專案 **或** 專案議題 **或** 工作項目仍引用則不可刪。

`message` 只列出大於 0 的段：

`使用中，共 {n} 筆正式議題、{n} 筆專案、{n} 筆專案議題、{n} 筆工作項目`

# 五、互動→API 對照表

| UI 操作 | API | 成功 | 失敗 |
|---------|-----|------|------|
| 開專案詳情 | GET `/projects/{id}` | 表單＋工作項目區＋專案議題區＋本體工時＋橫幅 | 404 回清單 |
| 儲存專案欄位 | PUT `/projects/{id}` | 詳情替換（不含標記完成） | 400／409 橫幅 |
| 刪專案 | DELETE `/projects/{id}` | 回清單；確認文案含議題與工作項目筆數 | 404 |
| 匯入 dialog 送出 | POST `/projects/{id}/import` | `navigate` 結果頁並帶 `ImportResult` | 400／404 留在詳情橫幅 |
| 結果頁跳轉 | 不新增 API | `anomaly.path` | — |
| 略過實際日橫幅 | PUT `.../actual-date-warning` | 橫幅消失 | 橫幅 |
| 工作項目新增／編輯／刪 | POST／PUT／DELETE `.../work-items` | `workItems` 替換 | 409 代號／撞名、400 |
| 專案議題新增／編輯／刪 | 既有 items API（Write 加 handlerName） | `items` 替換 | 同第三階段 |
| 標記／取消完成 | PUT `.../completion` | 該主體列表或詳情替換；實際結束更新 | 無工時 400 |
| 工時增改刪 | POST／PUT／DELETE `.../hours` | 該主體 `hours` 替換；實際開始重算 | 同日 409 |
| 開首頁／切月曆 | GET `/issues/calendar` | 含 `workItem` 到期標記 | 橫幅 |
| 點月曆工作項目 | 不新增 API | `/projects/{projectId}?workItem={id}` | 無該列則橫幅 |
| 刪大／小分類 | 既有 DELETE | 列表替換 | 409 message 含工作項目 |

匯入結果頁重新整理：不打匯入 API；若無 `location.state` 則顯示「請回專案詳情重新匯入」並連回 `/projects/{id}`。

# 六、前端元件對應表

| 畫面區塊 | 元件類型 | 主要狀態 |
|----------|----------|----------|
| 匯入入口 | 專案詳情按鈕「匯入 Excel」 | 新增專案頁隱藏 |
| 匯入對話 | HTML `dialog`：檔案、負責人員、處理人、確定／取消 | 上傳中禁用送出；副檔名前端先擋 `.xlsx` |
| 匯入結果頁 | 摘要數字＋異常表（kind 文案、列號、跳轉鈕）＋回專案 | 無 state 的空狀態 |
| 工作項目區 | 與專案議題分區列表；新增／編輯用 `dialog`（可 `wide`）；query `workItem` 高亮 | empty／highlight／editing |
| 日期工時區 | 各主體小表：日期、小時、刪；新增一列 | 同日錯誤、saving |
| 標記完成 | 核取或按鈕＋取消完成 | 無工時時禁用並提示 |
| 實際起迄 | 唯讀日期 | 隨工時／完成更新 |
| 實際日橫幅 | 頁內提示＋略過 | `actualDateWarning` |
| 月曆標記 | `MonthCalendar` 加 `source === "workItem"` 的路徑、圖示、`aria-label` | 格內既有捲動；四種來源可並存 |

UI 函式庫：React 原生＋既有 token；圖示 `lucide-react`。工作項目圖示須與專案、專案議題、正式議題可區分。

不新開 Excel 預覽套件。結果頁不強制虛擬捲動（Spike：數百列異常用一般表格即可）。

# 七、資料模型與資料流程

## 7.1 邏輯 Entity Model

邏輯 Entity 來自《功能規格書》第七章。既有 `issue`、`todo` 不改屬性。

| 實體名稱 | 中文名稱 | 用途 | 主要屬性 | 關聯 | 持久化方式 | 驗證規則 |
|----------|----------|------|----------|------|------------|----------|
| project（增量） | 專案 | 匯入容器；本體可有工時與完成 | 既有＋ isCompleted, actualStartDate, actualEndDate, actualDateWarning | 另 1:N workItem、1:N dailyHour | Database | 完成語意見 §3.5；實際日不手填 |
| workItem | 工作項目 | Excel「工作項目」列對應的工作包 | id, projectId, workItemCode, title, description, ownerName, majorCategoryId, subCategoryId, startDate, dueDate, isCompleted, actualStartDate, actualEndDate, actualDateWarning, createdAt, updatedAt | N:1 project；N:1 majorCategory；N:1 subCategory（可空）；1:N dailyHour | Database | 有代號則同專案代號唯一（忽略大小寫）；無代號則同專案標題＋負責人員唯一；title 必填；不可改掛專案 |
| projectIssue（增量） | 專案議題 | Excel「議題單」 | 既有＋ handlerName, isCompleted, actualStartDate, actualEndDate, actualDateWarning | 另 1:N dailyHour | Database | seqNo 規則不變 |
| dailyHour | 日期工時 | 某日小時數 | id, ownerKind, ownerId, date, hours | 必屬 project 或 workItem 或 projectIssue 恰好一個 | Database | 同一歸屬同一日唯一；hours＞0 |
| majorCategory（既有） | 大分類 | 分組 | （既有） | 另 1:N workItem | Database | 刪除須無 issue／project／projectIssue／workItem 引用 |
| subCategory（既有） | 小分類 | 狀態與顏色 | （既有） | 另被 workItem 選用 | Database | 同上 |

匯入結果不持久化。`ownerKind` 邏輯列舉：`project`／`workItem`／`projectIssue`。

屬性名 camelCase；C# PascalCase 由 DA 對應。

## 7.1.1 邏輯 Entity 屬性明細

### project（專案）增量欄

既有欄見第三階段系統設計書。本輪加：

| 屬性名稱 | 中文名稱 | 邏輯型態 | 驗證限制 | 是否允許空值 |
|----------|----------|----------|----------|--------------|
| isCompleted | 標記完成 | 布林 | 預設否；無工時計算範圍不可改為是 | 否 |
| actualStartDate | 實際開始日 | 日期 | 系統計算，不手填 | 是 |
| actualEndDate | 實際結束日 | 日期 | 僅 isCompleted 為是時可有值 | 是 |
| actualDateWarning | 實際日匯入提醒 | 布林 | 預設否 | 否 |

### workItem（工作項目）

| 屬性名稱 | 中文名稱 | 邏輯型態 | 驗證限制 | 是否允許空值 |
|----------|----------|----------|----------|--------------|
| id | 識別 | 長整數 | 系統給定、唯一 | 否 |
| projectId | 所屬專案 | 長整數 | 必填；建立後不可改掛 | 否 |
| workItemCode | 項次或工作代號 | 字串 | 有值時同一專案唯一（忽略大小寫）；最長 50 | 是 |
| title | 標題 | 字串 | 必填；最長 200 | 否 |
| description | 說明 | 字串 | 最長 4000；無則空字串 | 否 |
| ownerName | 負責人員 | 字串 | 最長 50；無則空字串 | 否 |
| majorCategoryId | 大分類 | 長整數 | 必填；須為現有大分類 | 否 |
| subCategoryId | 小分類 | 長整數 | 若有值須屬該大分類 | 是 |
| startDate | 預計開始日 | 日期 | 與 dueDate 都有則不可晚於 dueDate | 是 |
| dueDate | 預計完成日 | 日期 | 行事曆到期日用此欄 | 是 |
| isCompleted | 標記完成 | 布林 | 預設否 | 否 |
| actualStartDate | 實際開始日 | 日期 | 系統計算 | 是 |
| actualEndDate | 實際結束日 | 日期 | 僅完成時寫入 | 是 |
| actualDateWarning | 實際日匯入提醒 | 布林 | 預設否 | 否 |
| createdAt | 建立時間 | 日期時間 | 系統給定 | 否 |
| updatedAt | 更新時間 | 日期時間 | 系統給定 | 否 |

### projectIssue（專案議題）增量欄

既有欄見第三階段。本輪加：

| 屬性名稱 | 中文名稱 | 邏輯型態 | 驗證限制 | 是否允許空值 |
|----------|----------|----------|----------|--------------|
| handlerName | 處理人 | 字串 | 最長 50；無則空字串 | 否 |
| isCompleted | 標記完成 | 布林 | 預設否 | 否 |
| actualStartDate | 實際開始日 | 日期 | 系統計算 | 是 |
| actualEndDate | 實際結束日 | 日期 | 僅完成時寫入 | 是 |
| actualDateWarning | 實際日匯入提醒 | 布林 | 預設否 | 否 |

### dailyHour（日期工時）

| 屬性名稱 | 中文名稱 | 邏輯型態 | 驗證限制 | 是否允許空值 |
|----------|----------|----------|----------|--------------|
| id | 識別 | 長整數 | 系統給定、唯一 | 否 |
| ownerKind | 歸屬種類 | 列舉 | 必填；project／workItem／projectIssue | 否 |
| ownerId | 歸屬識別 | 長整數 | 必填；須指向對應實體 | 否 |
| date | 日期 | 日期 | 必填；同一歸屬唯一 | 否 |
| hours | 小時 | 小數 | 必填；大於 0；最多兩位小數；≤ 999.99 | 否 |

## 7.2 實體關聯概覽

- project 1:N workItem
- project 1:N projectIssue（既有）
- project 1:N dailyHour（ownerKind＝project）
- workItem 1:N dailyHour
- projectIssue 1:N dailyHour
- majorCategory 1:N workItem；subCategory 1:N workItem（可空）
- 刪 project → 其 workItem、projectIssue 與上述 dailyHour 皆消失
- 刪 workItem／projectIssue → 其 dailyHour 消失
- project 與 issue **無**關聯
- workItem 與 projectIssue **無**關聯（項次命名空間分開）

## 7.3 資料流向

選檔 → POST import → 後端解析 → 交易 upsert 工作項目／專案議題／工時 → ImportResult（不存檔）。  
手改工時 → 重算實際開始 → 詳情／列表。  
標記完成 → 寫實際結束。  
行事曆讀 workItem.dueDate。  
刪分類 COUNT 加 workItem。

## 7.4 DA 接手指引

有 Database 持久化。請依 §7.1、§7.1.1、§7.2 產出完整《資料庫設計書》**增量**（第四階段目錄新開；不覆寫第一、三階段資料庫設計書本文）。每個 Table 須有七欄「欄位設計」表。

- 主鍵 BIGINT AUTO_INCREMENT、欄位 snake_case，C# 實體 PascalCase。
- 既有 `project`、`project_issue` **加欄**（完成、實際起迄、提醒；議題加處理人），不要為工時去改第三階段文件本文。
- 新表：工作項目、日期工時。
- `workItemCode` 有值時同專案唯一（忽略大小寫）；空值多筆允許，撞名規則在應用層。
- 刪 project 必須使其 workItem、projectIssue、所有相關 dailyHour 一併不存在（CASCADE 或等價）。
- 刪 major／sub 仍 RESTRICT；應用層 COUNT 含 workItem。
- `dailyHour` 必須保證恰好一個歸屬；同一歸屬同一日唯一。
- 舊庫啟動須能加欄、加表（與第三階段相同，勿只靠空庫 EnsureCreated）。
- 不建成員表、廠商表、匯入歷程表。
- 種子：不新增範例工作項目／工時。

# 八、權限流程

本輪無認證。所有 API 開放。不區分角色。

# 九、例外流程

| 情境 | 行為 |
|------|------|
| 驗證失敗（空白、過長、起迄顛倒、小時 ≤0、小分類不屬大分類） | 400，message 繁中 |
| 找不到專案／工作項目／專案議題／工時／路徑專案不符 | 404 |
| 專案代號重複、項次重複、工作項目標號重複、無代號撞名、同日工時 | 409 |
| 無工時卻標記完成 | 400 |
| 匯入整批停止（缺表、工作項目零有效列、代號不符、檔案非法） | 400，不寫入 |
| 匯入列級／行級異常 | 200，列入 anomalies |
| 分類使用中 | 409，message 含工作項目筆數 |
| 未處理例外 | 500，message 不暴露堆疊 |

# 十、系統限制

- 單人、無登入
- 單次檔案上限 **5 MiB**；建議列數數百列，硬上限 2 000 列資料列（含兩張表合計），超出 400
- 只接受 `.xlsx`（Office Open XML），不接受 `.xls`、csv
- 工作項目／專案議題不可改掛專案
- 行事曆預計項目不含工作項目
- 預計開始日不上行事曆
- 無工作項目 TODO、無正式議題所屬專案
- 標記完成與小分類並存；完成不寫死小分類名稱
- 刪除不可復原
- 匯入結果不入庫；重新整理結果頁即失去當次摘要

# 十一、開發注意事項

## 11.1 Excel 表頭別名（集中一處常數）

大小寫與空白忽略後比對。同一列命中多個別名時先列出者優先。

| 邏輯欄 | 別名（任一） |
|--------|----------------|
| 專案代號 | 專案代號、專案編號、專案代碼、專案 |
| 項次／工作代號 | 項次、工作代號、工作編號 |
| 標題 | 標題、工作項目、名稱 |
| 負責人員 | 負責人員、負責人 |
| 處理人 | 處理人 |
| 內容 | 內容、說明 |
| 大分類 | 大分類 |
| 小分類 | 小分類 |
| 預計開始 | 預計開始、預計開始日、開始日 |
| 預計完成 | 預計完成、預計完成日、完成日、到期日 |
| 實際開始 | 實際開始、實際開始日 |
| 實際結束 | 實際結束、實際結束日、實際完成 |
| 備註 | 備註、工時備註 |

工作表名稱：`工作項目`、`議題單`。

## 11.2 其他

- 月曆 React `key` 必須含 `source`（加 `kind`、`id`、`date`）。
- `MonthCalendar` 增加 `workItem` 分支，不可落到 `/issues/{id}`。
- 匯入與工時寫入用交易；整批失敗不得留下半筆工作項目。
- JSON camelCase；EF 對 DB snake_case。
- 小時 JSON 用 number；比較 `hoursDiffer` 時以兩位小數四捨五入後比較。
- 單元測試至少覆蓋：缺表／工作項目零列不寫入；代號不符不寫入；議題單空表可匯工作項目；項次 upsert 不刪多出列；工作項目代號命名空間與議題項次互不干擾；無代號撞名；備註 `6/12 - 3H` 與 `2.5h`；壞行不擋其餘行；空白預計日不覆蓋；標記完成才有實際結束；取消完成清空結束；Excel 實際空白不覆蓋；同日工時 409；分類刪除 COUNT 含工作項目；行事曆 `source === "workItem"` 且無 plan。
- 前端文案區隔「正式議題／專案／專案議題／工作項目」。

# 十二、已確認決策

- 架構沿用 React SPA + ASP.NET Core JSON API + MySQL。不換框架。Excel 套件採 **ClosedXML**（Spike）。
- 匯入 `POST /projects/{id}/import` multipart；結果不入庫，前端 `location.state`。
- 工作項目路徑 `/work-items`，避免與 `/items`（專案議題）混淆。跳轉 `?workItem=`。
- 專案本體預計日與本體工時不由「工作項目」列寫入。
- 實際開始：專案＝本體工時∪工作項目工時；專案議題獨立。
- 四種月曆來源：格內既有捲動即可，不上 FullCalendar。規格 8.1／8.4 待評估項改為**已確認可行**。
- 不調整產品範圍（無成員主檔、無廠商、無匯入歷程、無 `.xls`）。
