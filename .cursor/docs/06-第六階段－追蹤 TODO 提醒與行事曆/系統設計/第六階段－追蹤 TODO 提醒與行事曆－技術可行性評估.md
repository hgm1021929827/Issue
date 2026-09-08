# 第六階段－追蹤 TODO 提醒與行事曆－技術可行性評估

**角色：** RD Tech Spike  
**日期：** 2026-09-04  
**依據：** [功能規格書](../需求規劃/第六階段－追蹤 TODO 提醒與行事曆－功能規格書.md) v1.0  

| 項目 | 結論 | 說明 |
|------|------|------|
| 提醒日欄位 | 可行 | 追蹤 Write／列表 DTO 加可空 `reminderDate`（DateOnly）；表單用既有 `AppDateField`。 |
| 月曆資料 | 可行 | 繼續 `GET /issues/calendar`；該月未完成且 `reminderDate` 落在該月的追蹤列併入陣列。`kind=track`，`source=trackTodo`；`id`＝追蹤 id；另回 `workType`／`workId`／`projectId` 供跳轉。 |
| 第三視角 | 可行 | `MonthCalendar` 現把非 `plan` 當 due。改為 `plan`→`views.plan`、`track`→`views.track`、其餘→`views.due`。HomePage `views` 加 `track: true`。 |
| 標記可辨 | 可行 | 既有 `is-due`／`is-plan` 外加 `is-track` 與 Bookmark 圖示；不需 FullCalendar。 |
| 點擊定位 | 可行 | 重用第五階段 query；`markPath` 為 `source===trackTodo` 分支。 |
| 月曆刪除 | 可行（不做） | 規格拒絕點 X；追蹤標記不渲染 remove。 |

規格 8.2 Spike 由「待評估」改為**已確認可行**。不擋 DA／RD。
