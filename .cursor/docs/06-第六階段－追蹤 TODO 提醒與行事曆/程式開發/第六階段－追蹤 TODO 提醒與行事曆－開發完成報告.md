# 第六階段－追蹤 TODO 提醒與行事曆－開發完成報告

**角色：** RD  
**日期：** 2026-09-04  
**狀態：** 開發完成，交 QA  
**依據：** [系統設計書](../系統設計/第六階段－追蹤 TODO 提醒與行事曆－系統設計書.md) v1.0、[資料庫設計書](../資料設計/第六階段－追蹤 TODO 提醒與行事曆－資料庫設計書.md) v1.0

---

## 本次完成功能

- 追蹤 TODO 可填可空「提醒日」（僅日期、無時刻）；三種工作皆可
- 寫入／列表／首頁 DTO 皆回傳 `reminderDate`；完成**不清空**日期
- 首頁行事曆增量：未完成且有提醒日、落在查詢年月者出現；完成或清空日期後不再出現
- 第三視角「追蹤提醒」（預設開）；點標記沿用第五階段跳轉；標記**沒有**移除鈕
- 首頁「需要追蹤項目」加「提醒日」欄，空顯示「—」
- 追蹤表單用既有 `AppDateField`；列上有日期才顯示「提醒日 yyyy-MM-dd」

## 資料庫

- 不加新表。`track_todo` 加可空 `reminder_date` DATE
- 索引 `ix_track_todo_reminder` (`is_completed`, `reminder_date`)
- 舊庫於啟動 `DbSeeder.EnsureSchema`：`AddColumnIfMissing`／`AddIndexIfMissing`

## API

既有路徑不變。Write／List／Home 增量可空 `reminderDate`。

`GET /issues/calendar?year=&month=` 同一陣列新增元素：

| 欄位 | 值 |
|------|-----|
| `source` | `trackTodo` |
| `kind` | `track` |
| `id` | 追蹤 TODO id |
| `workType` | `issue`／`project`／`projectIssue` |
| `workId` | 所屬工作 id |
| `projectId` | 僅專案議題有值 |
| `date`／`dueDate` | 提醒日 |
| `title` | 追蹤標題 |
| `label` | 工作摘要（編號／代號／項次） |
| 顏色 | `#B7D0E0` |

既有到期與預計項目列維持。年月無效仍 400。

## 修改檔案（摘要）

後端：`TrackTodo`、`AppDbContext`、`DbSeeder`、`Dtos`、`TrackTodoService`、`IssueService.CalendarAsync`。  
測試：`TrackTodoServiceTests.Calendar_includes_incomplete_reminder_only`。  
前端：`TrackTodoList.jsx`、`HomePage.jsx`、`MonthCalendar.jsx`、`styles.css`。

未使用獨立 Migration 專案。未做外部日曆、推播、從月曆寫入／刪除提醒。

## 單元測試

`dotnet test`：失敗 0，通過 **60**，略過 0。

覆蓋：未完成有日期出現在該月 calendar；無日期／已完成／跨月不出現；完成後日期仍在清單；首頁含 `reminderDate`。

## 已完成／未完成

已完成系統設計本輪範圍。未做：工作項次／工時／Excel（第十二階段）、加簽、預約連線、登入、ICS／Google、推播。

## 已知限制

- 單人、無登入。
- 無效日期由 ASP.NET `DateOnly` 繫結拒絕（400）；前端用日期欄，一般不會送出非法字串。
- 不從月曆拖曳設定或點 X 刪提醒。
- InMemory 測試不驗證 MySQL INDEX／ALTER 實際行為。
- 重啟後端後 `EnsureSchema` 才會對既有 MySQL 加欄。

## 建議 QA 測試重點

1. 議題／專案／專案議題追蹤：提醒日可空、可改、可清；列上有日才顯示。
2. 首頁列表「提醒日」欄；空為「—」；已完成不列。
3. 月曆第三視角「追蹤提醒」預設開；只顯示未完成＋有日＋當月；完成後消失、取消完成後回來（日期仍在）。
4. 三種跳轉與第五階段相同（含專案議題 `?item=`＋`trackTodo=`）。
5. 追蹤標記無移除鈕；全關三視角顯示「請至少選擇一種視角。」
6. 既有到期日／預計項目行為不變。
7. 視覺：奶油白／霧藍；追蹤標記可與到期、預計項目區分。
