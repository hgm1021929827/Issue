# 第十三階段－正式議題 Excel 匯入－資料庫設計書

**角色：** DA  
**版本：** v1.0  
**日期：** 2026-09-05  
**狀態：** 定稿，交 RD  
**依據：** [系統設計書](../系統設計/第十三階段－正式議題 Excel 匯入－系統設計書.md) 第七章  
**關係：** 增量設計。不覆寫第一～十二階段資料庫設計書本文。本輪無新表；`issue` 加兩欄。

---

## 一、資料庫設計理念

個人單人系統。正式議題已有 `issue` 表。本輪只需記住「是不是 UOF 匯進來的」以及「這次檔裡沒有但使用者選擇保留」，以便唯讀與未找到標示。

不建匯入歷程表、不建上傳檔表。`importNotFoundCache` 依 SA 為行程記憶體，不落庫。`importMemberPref` 在本機，不建表。客戶公司沿用第四階段，匯入新建公司只寫既有 `company_name`。

主鍵 `BIGINT AUTO_INCREMENT`。表名與欄位 `snake_case`。庫名 `issue_tracker`，`utf8mb4`。

---

## 二、Table 清單

本輪**新增表：** 無。

本輪**加欄：**

| 表名 | 加欄 |
|------|------|
| issue | imported_from_uof、missing_kept |

`client_company` 本輪不改（SA §7.4）。無種子。

---

## 三、Table 詳細設計

### issue（加欄）

中文名稱：正式議題

用途：第一階段工作主體。本輪標示是否來自議題匯入、以及未找到而保留。既有欄不重出（見第一階段、第四階段 `client_company_id`）。

#### 欄位設計

| 欄位名稱 | 中文名稱 | 資料型態 | 驗證限制 | 是否允許 NULL | 預設值 | 說明 |
| -------- | -------- | -------- | -------- | ------------- | ------ | ---- |
| imported_from_uof | 來自議題匯入 | TINYINT(1) | NOT NULL | 否 | `0` | 對應 `importedFromUof`；匯入新增或編號對上後為 1；應用層不可改回 0 |
| missing_kept | 本次檔案沒有而保留 | TINYINT(1) | NOT NULL | 否 | `0` | 對應 `missingKept`；決策 keep＝1；有效列再出現＝0 |

C# `IssueItem` 加：`ImportedFromUof`、`MissingKept`。JSON：`importedFromUof`、`missingKept`。

舊列 EnsureSchema 加欄後兩欄皆為 `0`（手建、尚未匯入）。

### client_company

中文名稱：客戶公司

用途：第四階段廠商主檔。本輪匯入可 **INSERT 一列只填名稱**（無窗口），表結構不改。欄位設計見 [第四階段資料庫設計書](../../04-第四階段－成員與廠商/資料設計/第四階段－成員與廠商－資料庫設計書.md) `client_company`，本章不重出。

---

## 四、Table 關聯

本輪無新 FK。既有不變：

- `issue.client_company_id` → `client_company.client_company_id` ON DELETE RESTRICT
- `issue.major_category_id` → 大分類 ON DELETE RESTRICT
- `issue.sub_category_id` → 小分類 ON DELETE RESTRICT（可 NULL）

刪議題：既有 CASCADE TODO／追蹤／預計項目，本輪不改。

---

## 五、Index 建議

- 既有 UNIQUE (`issue_no`) 不變，供表單編號對上。
- 未找到查詢為 `imported_from_uof = 1` 的全表掃描；個人資料量不必加索引。
- `client_company.company_name` 既有 UNIQUE 不變，供名稱對上與新建。

---

## 六、Constraint

- 兩新欄 TINYINT(1) 僅 0／1；預設 0。
- 匯入列五欄唯讀、決策恰好覆蓋：僅應用層（含記憶體 notFound 集合）。
- 客戶公司名稱唯一：既有 UNIQUE；應用層忽略大小寫比對後再 INSERT。
- 不在 DB 禁止 `imported_from_uof` 改回 0（無 CHECK）；應用層拒絕。

---

## 七、資料生命週期

- 手建議題：兩欄維持 0，直到編號被匯入對上。
- 匯入新增／對上：`imported_from_uof=1`，`missing_kept=0`。
- 未找到 keep：`missing_kept=1`；再出現有效列：`missing_kept=0` 並更新規格允許的欄。
- 未找到 delete 或手刪：刪 `issue` 列，既有子列 CASCADE。
- 無本輪種子。

---

## 八、命名規範

對齊 AGENTS.md：`snake_case` 表欄、BIGINT 主鍵、C# PascalCase。JSON camelCase。`imported_from_uof` 對 `importedFromUof`；`missing_kept` 對 `missingKept`（與第十二階段 `project_issue.missing_kept` 同名同義）。

---

## 九、設計決策

| 決策 | 原因 |
|------|------|
| 只加兩布林、不建匯入批次表 | SA §7.4／Spike：結果走前端 state，決策集合走記憶體 |
| 不加 imported_from_uof 索引 | 單人、列數以百計 |
| client_company 不改結構 | 快捷新增已能只寫名稱 |
| 舊列預設 0 | 手建不得被當成匯入列，直到編號對上 |
