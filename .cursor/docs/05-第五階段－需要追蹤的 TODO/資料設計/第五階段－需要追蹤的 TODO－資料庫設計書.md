# 第五階段－需要追蹤的 TODO－資料庫設計書

**角色：** DA  
**版本：** v1.0  
**日期：** 2026-09-04  
**狀態：** 定稿，交 RD  
**依據：** [系統設計書](../系統設計/第五階段－需要追蹤的 TODO－系統設計書.md) 第七章  
**關係：** 增量設計。不覆寫第一、三、四階段資料庫設計書本文。本輪新表一張；既有表不加欄。

---

## 一、資料庫設計理念

個人單人系統。邏輯實體 `trackTodo` 的「恰好一種工作」與「恰好一種對象」用**三個／兩個可空 FK** 表達，以便真正的參照完整性與 CASCADE／RESTRICT，而不是單一 `work_id` 無 FK。

主鍵 `BIGINT AUTO_INCREMENT`。表名與欄位 `snake_case`。庫名 `issue_tracker`，`utf8mb4`。

不做追蹤子項表、不加一般 `issue_todo` 欄位。

---

## 二、Table 清單

本輪**新增**：

| 表名 | 中文 | 用途 |
|------|------|------|
| track_todo | 追蹤 TODO | 掛在議題／專案／專案議題上的追蹤列 |

本輪**不加欄**：`issue`、`project`、`project_issue`、`company_member`、`client_contact`、`client_company` 結構不變，只被新表引用。

無種子追蹤列。

---

## 三、Table 詳細設計

### track_todo

中文名稱：追蹤 TODO

用途：單層追蹤事項。恰好隸屬一種工作；對象為公司成員或客戶窗口恰好一種。

#### 欄位設計

| 欄位名稱 | 中文名稱 | 資料型態 | 驗證限制 | 是否允許 NULL | 預設值 | 說明 |
| -------- | -------- | -------- | -------- | ------------- | ------ | ---- |
| track_todo_id | 編號 | BIGINT | PRIMARY KEY | 否 | AUTO_INCREMENT | 主鍵 |
| issue_id | 所屬正式議題 | BIGINT | FK | 是 | NULL | 與 project_id、project_issue_id 恰好一個有值 |
| project_id | 所屬專案 | BIGINT | FK | 是 | NULL | 專案自己的追蹤，不是專案議題 |
| project_issue_id | 所屬專案議題 | BIGINT | FK | 是 | NULL | — |
| is_completed | 是否完成 | TINYINT(1) | NOT NULL | 否 | `0` | 對應 `isCompleted` |
| title | 標題 | VARCHAR(200) | NOT NULL | 否 | — | 去空白後不可空由應用層 |
| content | 內容 | VARCHAR(2000) | NOT NULL | 否 | `''` | 無則空字串 |
| target_type | 追蹤對象類型 | VARCHAR(20) | NOT NULL | 否 | — | `member`／`clientContact` |
| company_member_id | 內部對象 | BIGINT | FK | 是 | NULL | `target_type=member` 時必填 |
| client_contact_id | 窗口對象 | BIGINT | FK | 是 | NULL | `target_type=clientContact` 時必填 |
| sort_order | 排列順序 | INT | NOT NULL | 否 | — | 同一所屬工作內 |
| created_at | 建立時間 | DATETIME | NOT NULL | 否 | CURRENT_TIMESTAMP | — |
| updated_at | 更新時間 | DATETIME | NOT NULL | 否 | CURRENT_TIMESTAMP | — |

C#：`TrackTodo`；`TrackTodoId`、`IssueId`、`ProjectId`、`ProjectIssueId`、`IsCompleted`、`Title`、`Content`、`TargetType`、`CompanyMemberId`、`ClientContactId`、`SortOrder`、`CreatedAt`、`UpdatedAt`。

JSON 不直接暴露三個工作 FK：API 用路徑＋`workType`／`workId`。對象 JSON 用 `targetType`＋`targetId`，服務層寫入對應 FK。

不做 DB ENUM。`target_type` 僅兩種字串，應用層限制。

MySQL CHECK（EnsureSchema 一併建立；若舊版不支援則應用層保證、註解保留）：

- 工作：`(issue_id IS NOT NULL) + (project_id IS NOT NULL) + (project_issue_id IS NOT NULL) = 1`
- 對象：`(target_type = 'member' AND company_member_id IS NOT NULL AND client_contact_id IS NULL) OR (target_type = 'clientContact' AND client_contact_id IS NOT NULL AND company_member_id IS NULL)`

窗口是否屬於該工作廠商：**不**用 CHECK（跨表），由應用層驗證（SA 400）。

---

## 四、Table 關聯

- `track_todo.issue_id` → `issue.issue_id` ON DELETE CASCADE
- `track_todo.project_id` → `project.project_id` ON DELETE CASCADE
- `track_todo.project_issue_id` → `project_issue.project_issue_id` ON DELETE CASCADE
- `track_todo.company_member_id` → `company_member.company_member_id` ON DELETE RESTRICT
- `track_todo.client_contact_id` → `client_contact.client_contact_id` ON DELETE RESTRICT

刪專案時第三階段已 CASCADE 刪 `project_issue`，其追蹤隨 `project_issue_id` CASCADE。專案自己的列靠 `project_id` CASCADE。

`client_company` 無直接 FK。刪公司前應用層 COUNT 窗口被引用；窗口 RESTRICT 也會擋住仍被追蹤的窗口，進而擋 CASCADE 刪公司。應用層須先 COUNT 給出繁中筆數，不要只丟 FK 英文錯誤。

---

## 五、Index 建議

- INDEX (`issue_id`, `sort_order`) on `track_todo`
- INDEX (`project_id`, `sort_order`) on `track_todo`
- INDEX (`project_issue_id`, `sort_order`) on `track_todo`
- INDEX (`is_completed`, `created_at`) on `track_todo`（首頁未完成）
- INDEX (`company_member_id`) on `track_todo`
- INDEX (`client_contact_id`) on `track_todo`

---

## 六、Constraint

- 標題／內容長度：VARCHAR＋應用層去空白。
- `target_type` 僅 `member`、`clientContact`。
- 恰好一個工作 FK、恰好一個對象 FK：CHECK＋應用層。
- 客戶窗口須屬工作廠商：僅應用層。
- 成員／窗口刪除：RESTRICT＋應用層 409 筆數。
- 改廠商：無 DB 觸發器，僅應用層 409。

---

## 七、資料生命週期

- 使用者在已存在的工作上新增；刪單筆則列消失。
- 刪議題／專案／專案議題：CASCADE 刪其追蹤。
- 勾完成只改 `is_completed`，列仍在，首頁用條件過濾。
- 無種子資料。

---

## 八、命名規範

表名 `track_todo`，避免與 `issue_todo` 混淆。

`target_type` 存 API 同一組字串（`clientContact` camelCase），與第四階段 `channel_type` 小寫策略不同處：本輪對齊 SA JSON 列舉，以免前後端再映射。`member` 全小寫；`clientContact` 維持 camelCase 一字串。

---

## 九、設計決策

- 三個工作 FK 而非 `work_type`+`work_id`：才能 CASCADE／FK。
- 兩個對象 FK 而非只存 `target_id`：才能 RESTRICT 刪成員／窗口。
- 廠商一致性不進 CHECK：跨 `issue`／`project`／`project_issue` 路徑不同，放應用層。
- 舊庫必須 `EnsureSchema` CREATE，勿只靠空庫 EnsureCreated。
