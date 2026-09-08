# 第六階段－追蹤 TODO 提醒與行事曆－QA 測試報告

**角色：** QA  
**日期：** 2026-09-04  
**需求類型：** ① Web 應用  
**結案：** 使用者於 2026-09-05 確認測試完成  
**依據：** [系統設計書](../系統設計/第六階段－追蹤 TODO 提醒與行事曆－系統設計書.md) v1.0、[資料庫設計書](../資料設計/第六階段－追蹤 TODO 提醒與行事曆－資料庫設計書.md) v1.0、[開發完成報告](../程式開發/第六階段－追蹤 TODO 提醒與行事曆－開發完成報告.md)

---

## 一、Unit Test

**指令：** `dotnet test --nologo --verbosity normal`  
**結果：** 通過 **60**，失敗 **0**，略過 **0**

| 測試類別 | 測試數 | 結果 |
|----------|--------|------|
| TrackTodoServiceTests | 18 | ✅ 全通過（本輪＋1：`Calendar_includes_incomplete_reminder_only`） |
| DirectoryServiceTests | 8 | ✅ 全通過（回歸） |
| IssueServiceTests | 10 | ✅ 全通過（回歸） |
| ProjectServiceTests | 6 | ✅ 全通過（回歸） |
| ProjectVendorTests | 3 | ✅ 全通過（回歸） |
| CategoryServiceTests | 9 | ✅ 全通過（回歸） |
| CategoryProjectUsageTests | 2 | ✅ 全通過（回歸） |
| TodoServiceTests | 4 | ✅ 全通過（回歸） |

### 關鍵測試項對照系統設計書 §十一

| 設計書要求 | 測試名稱／驗證 | 結果 |
|------------|----------------|------|
| 有日期未完成出現在 calendar | `Calendar_includes_incomplete_reminder_only` | ✅ `source=trackTodo`、`kind=track`、日期正確 |
| 已完成不出現 | 同上 | ✅ |
| 無日期不出現 | 同上 | ✅ |
| 跨月不出現 | 同上（10 月提醒不在 9 月） | ✅ |
| 完成不清空日期 | 同上（完成後清單仍有日） | ✅ |
| 首頁含 reminderDate | 同上 | ✅ |

---

## 二、API Integration Test

對 `http://localhost:5080` 發實際請求（重啟後端後 `EnsureSchema` 已跑過加欄）。測試列於結束時已刪除。

| 案例 | 端點 | 結果 |
|------|------|------|
| POST 議題追蹤含提醒日 | `/issues/1/track-todos` | ✅ 200，`reminderDate=2026-09-10` |
| GET 首頁 | `/track-todos` | ✅ 該列含相同提醒日 |
| GET 行事曆 2026-09 | `/issues/calendar?year=2026&month=9` | ✅ `source=trackTodo`、`kind=track`、`workType=issue`、`workId=1` |
| GET 行事曆 2026-10 | 同上 month=10 | ✅ 不含該列 |
| PUT 完成 | `/complete` | ✅ 清單日期仍在；calendar 不含；首頁不含 |
| PUT 取消完成 | `/complete` | ✅ calendar 同日再出現 |
| PUT 清空日期 | PUT `reminderDate: null` | ✅ 列為 null；calendar 不含 |
| POST 專案含日 | `/projects/1/track-todos` | ✅ calendar `workType=project` |
| POST 專案議題含日 | `/projects/1/items/1/track-todos` | ✅ `workType=projectIssue`、`projectId=1` |
| POST 省略日期 | 議題 | ✅ `reminderDate` 空；calendar 不含 |
| GET 無效月 | `month=13` | ✅ HTTP 400，`年月無效` |
| 回歸既有標記 | GET 2026-09 calendar（清完測試列後） | ✅ 仍有 `kind=plan`／`due`；`source` 含 issue／project／projectIssue |

---

## 三、Database 驗證

本機無 `mysql` CLI。改以後端啟動日誌與 EF 實際 SQL 核對（`EnsureSchema` 對 `track_todo` 執行 `CREATE TABLE IF NOT EXISTS` 含 `reminder_date`，並 `AddColumnIfMissing`／`AddIndexIfMissing`；其後 INSERT／SELECT／UPDATE 皆帶 `reminder_date`）。

| 項目 | 結果 |
|------|------|
| 不加新表 | ✅ |
| `reminder_date` DATE NULL | ✅ 日誌 DDL 與後續 DML 一致；寫入 ISO 日成功 |
| 索引 `ix_track_todo_reminder` (`is_completed`, `reminder_date`) | ✅ `AddIndexIfMissing` 已執行 |
| 完成不清空欄位 | ✅ UPDATE complete 後 GET 清單日期仍在 |

InMemory 單元測試不驗證 MySQL INDEX／ALTER，已知限制屬實。

---

## 四、Business Logic 驗證

| 規則 | 結果 |
|------|------|
| 提醒日可空；有值為單日 | ✅ |
| 月曆：未完成且提醒日落在查詢年月 | ✅ |
| 完成或清空後下次 GET calendar 不含該列 | ✅ |
| 完成不刪日期，取消完成可再出現 | ✅ |
| 不提供月曆寫入提醒、不提供點標記刪除（後端無此 API） | ✅ |
| 既有到期／plan 契約不變 | ✅ |

---

## 五、Exception 驗證

| 情境 | HTTP | 結果 |
|------|------|------|
| 年月無效 | 400 | ✅ 繁中 `年月無效`，統一信封 |
| 追蹤不存在 | 404 | ✅ 既有路徑 |
| `reminderDate` 非法字串 | 400 | ⚠️ ASP.NET 預設 ProblemDetails（英文），**不是** `{ code, message, data }` 繁中 |

非法字串日期與既有 `DateOnly` 欄位（到期日等）同一套繫結。前端使用 `AppDateField`，一般不會送出。RD 開發完成報告已列為已知限制。**不列本輪缺陷**（若改為統一信封繁中，屬全域繫結／契約，需交 SA，非本輪實作修補範圍）。

---

## 六、Regression Test

第一、三、四、五階段既有測試全數通過（60 含本輪 1 筆新案例），無回歸失敗。行事曆整合抽樣仍見 due／plan。

---

## 七、發現問題

**無程式缺陷可分派。**

未建立《QA問題確認文件》。

---

## 八、結論

✅ **已結案。** 使用者於 2026-09-05 確認測試完成。

- 60 Unit Test 全通過
- API 提醒日與行事曆增量與系統設計書第三、四、十一章一致
- Schema 增量與資料庫設計書一致（日誌＋寫入驗證）
- 回歸無失敗
- 前端手動測試清單已勾選完成

---

## 九、文件清單

| 文件 | 路徑 |
|------|------|
| QA 測試案例 | [QA測試案例.md](第六階段－追蹤 TODO 提醒與行事曆－QA測試案例.md) |
| 前端手動測試清單 | [前端手動測試清單.md](第六階段－追蹤 TODO 提醒與行事曆－前端手動測試清單.md) |
| 本報告 | 本文件 |
