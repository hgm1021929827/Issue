# 第三階段－專案與專案議題－QA測試案例

**角色：** QA  
**類型：** 需求類型 ① 功能驗收  
**依據：** [系統設計書](../系統設計/第三階段－專案與專案議題－系統設計書.md) v1.0、[資料庫設計書](../資料設計/第三階段－專案與專案議題－資料庫設計書.md) v1.0、[開發完成報告](../程式開發/第三階段－專案與專案議題－開發完成報告.md)  
**狀態：** 2026-09-02 已執行（結果見測試報告）

| 編號 | 案例 | 預期 |
|------|------|------|
| TC-01 | `dotnet test` | 失敗 0；至少覆蓋設計書第十一章所列：代號忽略大小寫、項次同專案唯一／跨專案可同、起迄、刪專案連刪列、路徑不符 404、行事曆三種 source、分類使用中 409 |
| TC-02 | GET `/projects` | 200；`code === 200`；元素含 `id`／`code`／`name`／`itemCount`／分類與日期；排序 `updatedAt` 新到舊 |
| TC-03 | GET `/projects?majorCategoryId=` 非所屬大分類 | 200；`data` 為空陣列 |
| TC-04 | POST `/projects` 代號與名稱必填、去空白後空 | 400 |
| TC-05 | POST 重複代號（大小寫不同） | 409，訊息含代號 |
| TC-06 | POST `startDate` 晚於 `dueDate` | 400，預計開始日不可晚於預計完成日 |
| TC-07 | GET `/projects/{id}` | 200；含 `items`；欄位 camelCase |
| TC-08 | GET 不存在專案 | 404，找不到該專案 |
| TC-09 | POST `/projects/{id}/items` 同專案重複項次（忽略大小寫） | 409 |
| TC-10 | POST 項次成功 | 200；`data` 為該專案完整 `ProjectIssue[]` |
| TC-11 | PUT／DELETE 路徑 `projectId` 與所屬專案不符 | 404 |
| TC-12 | GET `/issues/calendar?year=&month=` | 專案／專案議題僅 `kind: "due"`；有 `source`／`label`／`projectId`；不含其 `startDate`；不含專案 `plan` |
| TC-13 | 行事曆 `id` 撞號 | 前端 React key 含 `source`；點專案進 `/projects/{id}`；點專案議題進 `/projects/{projectId}?item={id}` |
| TC-14 | DELETE `/majorCategories/{id}` 被專案或專案議題引用 | 409；`使用中，共 …` 只列出大於 0 的段，且含「專案」或「專案議題」 |
| TC-15 | DELETE `/subCategories/{id}` 被專案或專案議題引用 | 同 TC-14 |
| TC-16 | DELETE `/projects/{id}` | 200；`{ deleted, itemCount }`；其專案議題不再存在；月曆對應列消失 |
| TC-17 | 首頁 GET 正式議題清單 | 不含專案列 |
| TC-18 | 新表 `project`／`project_issue` | 既有庫啟動可建表；FK 刪專案 CASCADE；分類 RESTRICT |
