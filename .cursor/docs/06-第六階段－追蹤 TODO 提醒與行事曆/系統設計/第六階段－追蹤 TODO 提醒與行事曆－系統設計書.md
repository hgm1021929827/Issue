# 第六階段－追蹤 TODO 提醒與行事曆－系統設計書

**角色：** SA  
**版本：** v1.0  
**日期：** 2026-09-04  
**狀態：** 設計完成，交 DA  
**依據：** [功能規格書](../需求規劃/第六階段－追蹤 TODO 提醒與行事曆－功能規格書.md) v1.0  
**Tech Spike：** [技術可行性評估](第六階段－追蹤 TODO 提醒與行事曆－技術可行性評估.md)，已併入本文。

本階段為增量：不改第一、三、四、五階段正式文件。沿用統一回應 `{ code, message, data }`、`code === 200`、無 `/api` 前綴、JSON camelCase。

---

# 一、系統架構

React `5173`、ASP.NET Core 9 `5080`、MySQL `issue_tracker`。無登入。新 UI 用既有 token 與 `AppDateField`。

# 二、模組拆分

| 模組 | 職責 |
|------|------|
| 追蹤 TODO（增量） | CRUD 多可空 `reminderDate` |
| 行事曆（增量） | 同月未完成有提醒日的追蹤列 |
| 首頁月曆／列表 | 第三視角；列表加提醒日；標記跳轉 |

# 三、功能流程

- 寫入追蹤時 `reminderDate` 可 null；有值須為有效日。
- 月曆：`isCompleted === false` 且 `reminderDate` 落在查詢年月。
- 完成或清空日期後，下次 GET calendar 不再含該列。
- 不提供月曆寫入提醒、不提供點標記刪除。

# 四、API 規劃

既有 nested 追蹤路徑與 `PUT /track-todos/{id}` 的 Write 增加可空 `reminderDate`（ISO 日期或 null／省略）。列表與首頁 DTO 同樣回傳該欄（無則 null）。

### 4.1 行事曆增量

既有 `GET /issues/calendar?year=&month=` 繼續一次回傳陣列。

本輪新增元素約定：

| 欄位 | 追蹤提醒列 |
|------|------------|
| `source` | `trackTodo` |
| `kind` | `track`（不是 `due`、不是 `plan`） |
| `id` | 追蹤 TODO id |
| `workType` | `issue`／`project`／`projectIssue` |
| `workId` | 所屬工作 id（專案議題＝專案議題 id） |
| `projectId` | 僅 `projectIssue` 有值 |
| `date`／`dueDate` | 皆為提醒日 |
| `title` | 追蹤標題 |
| `label` | 可用工作摘要（編號／代號／項次）供格內短標 |
| `content` | 可為對象顯示名，供 title 提示 |
| 顏色 | 無分類色時用霧藍 token 可接受的既有預設色 |

既有議題／專案到期與 plan 列維持；可無 `workType`。

錯誤：年月無效仍 400。

前端跳轉：

- issue → `/issues/{workId}?trackTodo={id}`
- project → `/projects/{workId}?trackTodo={id}`
- projectIssue → `/projects/{projectId}?item={workId}&trackTodo={id}`

# 五、互動→API

| UI | API |
|----|-----|
| 存追蹤（含日期） | 既有 POST／PUT，body 多 `reminderDate` |
| 開首頁／切月 | GET calendar（含 track 列）＋ GET `/track-todos`（多 `reminderDate`） |
| 點月曆追蹤標記 | 前端組路徑，不再打新 API |
| 勾完成 | 既有 complete；其後 calendar 不含該列 |

# 六、前端元件

| 區塊 | 行為 |
|------|------|
| `TrackTodoList` 表單 | `AppDateField` 提醒日，可空 |
| 列 | 顯示提醒日（有則） |
| 首頁表 | 欄「提醒日」，空為「—」 |
| `MonthCalendar` | `views.track`；過濾 `kind==="track"`；`source==="trackTodo"` 的 Link；無 remove |
| HomePage | `views` 預設 `{ due, plan, track }` 皆 true；全關時既有提示改為含第三種 |

# 七、邏輯模型

不新增 Entity。`trackTodo` 加 `reminderDate`（日期，可空）。生命週期：隨追蹤列刪除；完成不刪列、只影響查詢。

既有 issue／project 行事曆規則不變。

## 7.4 DA 接手

有 Database：`track_todo` **加一欄**可空日期；建議索引利於「未完成＋該月」。舊庫 EnsureSchema ALTER。無新表。

# 八、權限

單人無登入。

# 九、例外

| 情境 | 行為 |
|------|------|
| 無效日期 | 400 繁中 |
| 追蹤不存在 | 既有 404 |
| 月曆點已刪追蹤 | 200 開工作；前端找不到列橫幅（第五階段已有） |

# 十、限制

- 無時刻、無多日、無外部日曆、無推播。
- 不從月曆寫入或刪除追蹤。

# 十一、開發注意

- 單元測試：有日期未完成出現在 calendar；已完成不出現；無日期不出現；跨月不出現。
- Spike 結論：第三 `kind` 可行。

# 十二、已確認決策

Q1–Q8 皆 A。路徑沿用第五階段跳轉。
