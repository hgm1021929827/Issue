# 第五階段－需要追蹤的 TODO－開發完成報告

**角色：** RD  
**日期：** 2026-09-04  
**狀態：** 開發完成，交 QA  
**依據：** [系統設計書](../系統設計/第五階段－需要追蹤的 TODO－系統設計書.md) v1.0、[資料庫設計書](../資料設計/第五階段－需要追蹤的 TODO－資料庫設計書.md) v1.0

---

## 本次完成功能

- 正式議題、專案、專案議題各自可掛單層「需要追蹤的 TODO」（與一般 TODO 分開）
- 欄位：完成核取、標題（必填 200）、內容（可空 2000）、對象類型（內部成員／客戶窗口）＋對象
- 兩層下拉；無廠商提示「請先設定廠商」；有廠商無窗口提示「請先在成員維護新增客戶窗口」
- 同工作拖曳排序（`orderedIds` 須為該工作全部 id）
- 首頁「需要追蹤項目」只列未完成；跳轉議題／專案／專案議題並定位高亮
- 改廠商時若仍有客戶窗口類追蹤 → 409；刪成員／公司／窗口被追蹤引用 → 409
- 刪議題／專案／專案議題連刪其追蹤（確認 dialog 有載入筆數時會提示）

## 新增資料表

- `track_todo`（三個工作 FK 恰好一個非空；對象 FK 恰好一個非空）

既有表不加業務欄。舊庫於啟動時 `DbSeeder.EnsureSchema` 建表。不預設追蹤資料。

## API

| 方法 | 路徑 |
|------|------|
| GET／POST | `/issues/{id}/track-todos`、`/projects/{id}/track-todos`、`/projects/{id}/items/{itemId}/track-todos` |
| GET | `/track-todos`（首頁，固定未完成） |
| PUT／DELETE | `/track-todos/{id}` |
| PUT | `/track-todos/{id}/complete`、`/track-todos/{id}/reorder` |
| GET | `/client-companies/{companyId}/contacts` 改回傳 `{ id, name }[]`（下拉用） |
| PUT | `/issues/{id}`、`/projects/{id}` 改廠商增量 409 |
| DELETE | 成員／公司／窗口增量追蹤占用 409 |

寫入 DTO：`{ title, content, targetType, targetId }`。寫入成功 `data` 為該工作完整列表。`targetType`：`member`｜`clientContact`。首頁 `workType`：`issue`｜`project`｜`projectIssue`。

## 修改檔案（摘要）

後端：實體 `TrackTodo`，`AppDbContext`、`DbSeeder`、`TrackTodoService`、`TrackTodosController`，`IssuesController`／`ProjectsController` 嵌套路徑，`IssueService`／`ProjectService`／`DirectoryService` 增量，DTO。  
測試：`TrackTodoServiceTests.cs`。  
前端：`api.js`、`TrackTodoList.jsx`、`IssuePage.jsx`、`ProjectPage.jsx`、`HomePage.jsx`、`styles.css`。

未使用獨立 Migration 專案。未做工作項次／Excel、加簽、預約、登入、專案上的一般 TODO。

## 單元測試

`dotnet test`：失敗 0，通過 **59**，略過 0。

覆蓋：成員／窗口對象與無廠商／無窗口 400；改廠商 409；刪窗口／成員占用；首頁只含未完成；reorder 須全部 id；刪議題／專案後追蹤不殘留。

## 已完成／未完成

已完成系統設計本輪範圍。未做：工作項次／工時／Excel（第十二階段）、加簽、預約連線、登入。

## 已知限制

- 單人、無登入。
- 新增議題／專案尚未儲存前不顯示追蹤區塊；專案議題追蹤僅編輯既有項次時出現。
- 首頁不可勾完成。
- InMemory 測試不驗證 MySQL CHECK／FK 實際行為。
- `GET /client-companies/{id}/contacts` 改為輕量 `{ id, name }`；成員頁仍走 `/tree`，不受影響。

## 建議 QA 測試重點

1. 議題詳情：追蹤區塊在一般 TODO 之上；CRUD、完成、拖曳；無廠商選窗口提示；刪議題文案含追蹤筆數。
2. 專案詳情：專案自己的追蹤；編輯專案議題 dialog 內緊湊清單；Enter 儲存追蹤不會誤存專案議題。
3. 首頁「需要追蹤項目」在清單／行事曆格線外；只列未完成；空狀態文案；三種跳轉與 `?trackTodo=` 定位／找不到橫幅。
4. 專案議題跳轉：`/projects/{id}?item={workId}&trackTodo={id}` 開項次並高亮追蹤。
5. 改廠商被客戶窗口追蹤擋下；改完完成該筆後可改廠商。
6. 刪被追蹤的成員／窗口／公司 409 文案含筆數。
7. 新畫面奶油白／霧藍；刪除用 `dialog`。
