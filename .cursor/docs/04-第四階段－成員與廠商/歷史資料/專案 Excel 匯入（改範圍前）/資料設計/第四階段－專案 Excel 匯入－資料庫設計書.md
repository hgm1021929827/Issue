# 第四階段－專案 Excel 匯入－資料庫設計書

**角色：** DA  
**版本：** v1.0  
**日期：** 2026-09-02  
**狀態：** 定稿，交 RD  
**依據：** [系統設計書](../系統設計/第四階段－專案 Excel 匯入－系統設計書.md) 第七章  
**關係：** 增量設計。不覆寫 [第一階段資料庫設計書](../../../../01-第一階段－個人工作追蹤/資料設計/第一階段－個人工作追蹤－資料庫設計書.md)、[第三階段資料庫設計書](../../../../03-第三階段－專案與專案議題/資料設計/第三階段－專案與專案議題－資料庫設計書.md)。本輪：既有 `project`／`project_issue` **加欄**；新增 `project_work_item`、`daily_hour`。

---

## 一、資料庫設計理念

個人單人系統，結構求清楚、好改。對應 SA：`workItem`、`dailyHour`，以及專案／專案議題的完成與實際起迄。

不用分片、不用讀寫分離。主鍵一律 `BIGINT AUTO_INCREMENT`。表名與欄位 `snake_case`。C# 實體 PascalCase 對應 underscore。庫名仍為 `issue_tracker`，charset `utf8mb4`。

不建成員表、廠商表、匯入歷程表。`itemCount`／`workItemCount` 為查詢計算，不存欄。`actual_start_date` 由應用層依工時重算後寫入，方便詳情與橫幅讀取。

`daily_hour` 用三個可空 FK 表達恰好一個歸屬（不用無 FK 的 kind+id），以便 CASCADE。邏輯 `ownerKind`／`ownerId` 由應用層從非空 FK 推導，不另存 kind 欄，避免與 FK 不一致。

---

## 二、Table 清單

本輪**新增**：

| 表名 | 中文 | 用途 |
|------|------|------|
| project_work_item | 工作項目 | Excel「工作項目」列對應的工作包 |
| daily_hour | 日期工時 | 專案本體／工作項目／專案議題的逐日小時 |

本輪**加欄**（不改既有欄定義）：

| 表名 | 加欄 |
|------|------|
| project | is_completed, actual_start_date, actual_end_date, actual_date_warning |
| project_issue | handler_name, is_completed, actual_start_date, actual_end_date, actual_date_warning |

既有表（本輪僅被新表／新欄引用，欄位本文仍以第三階段為準）：`major_category`、`sub_category`。`issue` 及其子表無關聯。

無種子工作項目／工時列。

---

## 三、Table 詳細設計

### project（加欄）

中文名稱：專案

用途：第三階段專案主資料。本輪加完成標記與實際起迄、匯入提醒。既有欄不重出；下列僅新增欄。

#### 欄位設計（本輪新增）

| 欄位名稱 | 中文名稱 | 資料型態 | 驗證限制 | 是否允許 NULL | 預設值 | 說明 |
| -------- | -------- | -------- | -------- | ------------- | ------ | ---- |
| is_completed | 標記完成 | TINYINT(1) | NOT NULL | 否 | `0` | 對應邏輯 `isCompleted`；不是小分類 |
| actual_start_date | 實際開始日 | DATE | — | 是 | NULL | 應用層依本體＋工作項目工時最早日寫入 |
| actual_end_date | 實際結束日 | DATE | — | 是 | NULL | 僅標記完成時寫入 |
| actual_date_warning | 實際日匯入提醒 | TINYINT(1) | NOT NULL | 否 | `0` | 對應 `actualDateWarning` |

C# 實體 `Project` 加：`IsCompleted`、`ActualStartDate`、`ActualEndDate`、`ActualDateWarning`。既有 `ProjectCode` 等不變。

舊列加欄後視同未完成、無實際日、無提醒。

### project_issue（加欄）

中文名稱：專案議題

用途：第三階段專案議題。本輪加處理人字串、完成與實際起迄、匯入提醒。既有欄不重出。

#### 欄位設計（本輪新增）

| 欄位名稱 | 中文名稱 | 資料型態 | 驗證限制 | 是否允許 NULL | 預設值 | 說明 |
| -------- | -------- | -------- | -------- | ------------- | ------ | ---- |
| handler_name | 處理人 | VARCHAR(50) | NOT NULL | 否 | `''` | 對應 `handlerName`；非正式成員 FK |
| is_completed | 標記完成 | TINYINT(1) | NOT NULL | 否 | `0` | — |
| actual_start_date | 實際開始日 | DATE | — | 是 | NULL | 依該議題工時最早日 |
| actual_end_date | 實際結束日 | DATE | — | 是 | NULL | 僅標記完成時寫入 |
| actual_date_warning | 實際日匯入提醒 | TINYINT(1) | NOT NULL | 否 | `0` | — |

C# 實體 `ProjectIssue` 加：`HandlerName`、`IsCompleted`、`ActualStartDate`、`ActualEndDate`、`ActualDateWarning`。

### project_work_item

中文名稱：工作項目

用途：隸屬一筆專案的工作包。有代號時同一專案內代號唯一；無代號時多筆允許，撞名由應用層檢查。不可改掛專案。行事曆到期日用 `due_date`。

#### 欄位設計

| 欄位名稱 | 中文名稱 | 資料型態 | 驗證限制 | 是否允許 NULL | 預設值 | 說明 |
| -------- | -------- | -------- | -------- | ------------- | ------ | ---- |
| project_work_item_id | 編號 | BIGINT | PRIMARY KEY | 否 | AUTO_INCREMENT | 主鍵 |
| project_id | 所屬專案 | BIGINT | NOT NULL；FK | 否 | — | 指向 project；刪專案 CASCADE |
| work_item_code | 項次或工作代號 | VARCHAR(50) | — | 是 | NULL | 對應 `workItemCode`；無代號存 NULL（勿存空字串，以免 UNIQUE 互撞） |
| title | 標題 | VARCHAR(200) | NOT NULL | 否 | — | — |
| description | 說明 | VARCHAR(4000) | NOT NULL | 否 | `''` | 無則空字串 |
| owner_name | 負責人員 | VARCHAR(50) | NOT NULL | 否 | `''` | 對應 `ownerName` |
| major_category_id | 大分類 | BIGINT | NOT NULL；FK | 否 | — | 指向 major_category |
| sub_category_id | 小分類 | BIGINT | FK | 是 | NULL | 若有值須屬該大分類（應用層） |
| start_date | 預計開始日 | DATE | — | 是 | NULL | 不上行事曆 |
| due_date | 預計完成日 | DATE | — | 是 | NULL | 行事曆到期日 |
| is_completed | 標記完成 | TINYINT(1) | NOT NULL | 否 | `0` | — |
| actual_start_date | 實際開始日 | DATE | — | 是 | NULL | 系統計算 |
| actual_end_date | 實際結束日 | DATE | — | 是 | NULL | 僅完成時寫入 |
| actual_date_warning | 實際日匯入提醒 | TINYINT(1) | NOT NULL | 否 | `0` | — |
| created_at | 建立時間 | DATETIME | NOT NULL | 否 | CURRENT_TIMESTAMP | — |
| updated_at | 更新時間 | DATETIME | NOT NULL | 否 | CURRENT_TIMESTAMP | 更新時刷新 |

C# 實體建議：`ProjectWorkItem`；`ProjectWorkItemId`、`ProjectId`、`WorkItemCode`、`Title`、`Description`、`OwnerName`、`MajorCategoryId`、`SubCategoryId`、`StartDate`、`DueDate`、`IsCompleted`、`ActualStartDate`、`ActualEndDate`、`ActualDateWarning`、`CreatedAt`、`UpdatedAt`。

JSON `id` 對應 `project_work_item_id`。

### daily_hour

中文名稱：日期工時

用途：某一日的小時數。恰好歸屬專案、工作項目、專案議題之一。

#### 欄位設計

| 欄位名稱 | 中文名稱 | 資料型態 | 驗證限制 | 是否允許 NULL | 預設值 | 說明 |
| -------- | -------- | -------- | -------- | ------------- | ------ | ---- |
| daily_hour_id | 編號 | BIGINT | PRIMARY KEY | 否 | AUTO_INCREMENT | 主鍵 |
| project_id | 專案歸屬 | BIGINT | FK | 是 | NULL | 非空＝專案本體工時 |
| project_work_item_id | 工作項目歸屬 | BIGINT | FK | 是 | NULL | 非空＝該工作項目工時 |
| project_issue_id | 專案議題歸屬 | BIGINT | FK | 是 | NULL | 非空＝該專案議題工時 |
| work_date | 日期 | DATE | NOT NULL | 否 | — | 對應邏輯 `date` |
| hour_value | 小時 | DECIMAL(6,2) | NOT NULL | 否 | — | 對應 JSON `hours`；應用層 >0 且 ≤999.99 |

C# 實體建議：`DailyHour`；`DailyHourId`、`ProjectId`、`ProjectWorkItemId`、`ProjectIssueId`、`WorkDate`、`HourValue`。

JSON：`id`、`date`（`work_date`）、`hours`（`hour_value`）。不輸出三個 FK；由所屬 API 路徑決定歸屬。

CHECK（MySQL 8）：三個 FK 恰好一個非 NULL：

```
(project_id IS NOT NULL) + (project_work_item_id IS NOT NULL) + (project_issue_id IS NOT NULL) = 1
```

---

## 四、Table 關聯

新增：

- `project_work_item.project_id` → `project.project_id`（ON DELETE CASCADE）
- `project_work_item.major_category_id` → `major_category.major_category_id`（ON DELETE RESTRICT）
- `project_work_item.sub_category_id` → `sub_category.sub_category_id`（ON DELETE RESTRICT）
- `daily_hour.project_id` → `project.project_id`（ON DELETE CASCADE）
- `daily_hour.project_work_item_id` → `project_work_item.project_work_item_id`（ON DELETE CASCADE）
- `daily_hour.project_issue_id` → `project_issue.project_issue_id`（ON DELETE CASCADE）

既有：`project_issue.project_id` CASCADE 維持。刪專案時專案議題、工作項目、各工時隨 CASCADE 消失。

刪大／小分類：應用層 COUNT 正式議題＋專案＋專案議題＋工作項目，使用中則 409；資料庫 RESTRICT 為第二道防線。

`project`／`project_work_item`／`project_issue` 與 `issue` 無 FK。工作項目與專案議題無 FK。

---

## 五、Index 建議

- UNIQUE (`project_id`, `work_item_code`) on `project_work_item`  
  MySQL 允許多個 NULL `work_item_code`，符合「無代號可多筆」。
- INDEX (`major_category_id`) on `project_work_item`
- INDEX (`due_date`) on `project_work_item`
- UNIQUE (`project_id`, `work_date`) on `daily_hour`  
  僅約束專案本體列；`project_id` 為 NULL 的列不互撞。
- UNIQUE (`project_work_item_id`, `work_date`) on `daily_hour`
- UNIQUE (`project_issue_id`, `work_date`) on `daily_hour`

第三階段 `uk_project_code`、`uk_project_issue_seq` 維持。

---

## 六、Constraint

- 工作項目標號：UNIQUE (`project_id`, `work_item_code`)；應用層再以忽略大小寫檢查有值代號。寫入時空白代號必須存 **NULL** 而非 `''`。
- 無代號撞名（標題＋負責人員）：僅應用層（忽略大小寫）。
- 專案議題項次唯一維持第三階段；與 `work_item_code` 分開，不建跨表唯一。
- `daily_hour` CHECK 恰好一個 FK；三組 UNIQUE 防同日重複。
- 應用層：`hour_value` > 0；`sub_category` 須屬該列大分類；起迄都有值時開始≤完成；更新工作項目不得改 `project_id`。
- 應用層：`is_completed = 0` 時應將 `actual_end_date` 置 NULL（取消完成）。

---

## 七、資料生命週期

- 專案：刪除即永久刪除，連同專案議題、工作項目、本體與子列工時。
- 工作項目：只在所屬專案底下增改刪；刪一筆不刪專案、不刪專案議題；其 `daily_hour` CASCADE。
- 專案議題：同第三階段；另 CASCADE 其工時。
- 日期工時：隨主檔刪或使用者單筆刪。
- 加欄後舊專案／舊議題：`is_completed=0`、實際日 NULL、提醒 0、處理人空字串。
- 行事曆工作項目標記無獨立表；讀 `project_work_item.due_date`。
- 匯入結果不入庫。

---

## 八、命名規範

表與欄位 snake_case。C#：`ProjectWorkItemId` 對 `project_work_item_id`，`WorkItemCode` 對 `work_item_code`，`OwnerName` 對 `owner_name`，`HandlerName` 對 `handler_name`，`WorkDate` 對 `work_date`，`HourValue` 對 `hour_value`。

JSON／邏輯屬性仍為 SA camelCase（`workItemCode`、`ownerName`、`hours`、`date`）；資料表用上表實體名，不把 JSON 名直接當欄名。

表名用 `project_work_item` 而非 `work_item`，避免日後與臨時工作混淆。

---

## 九、設計決策

- 三個可空 FK 優於 `owner_kind`+`owner_id` 無 FK：刪主檔工時一定清掉。
- `hour_value` 不用欄名 `hours`，減少與 SQL 關鍵字摩擦。
- `work_item_code` 可空＋UNIQUE，對齊「有代號才資料庫唯一」。
- 實際日起存欄而非每次 JOIN 聚合，因標記完成後實際結束不再隨工時變動，且詳情要穩定讀取。
- 舊庫已有 `project`／`project_issue`：RD **必須**在 `EnsureSchema`（或等價啟動 DDL）對新欄 `INFORMATION_SCHEMA` 判斷後 `ALTER`，並 `CREATE TABLE IF NOT EXISTS` 兩張新表。不得假定 EnsureCreated 會幫舊庫加欄。
- 不預埋成員／廠商／匯入批次表。
