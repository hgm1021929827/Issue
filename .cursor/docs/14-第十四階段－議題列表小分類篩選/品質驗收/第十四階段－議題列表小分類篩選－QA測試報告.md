# 第十四階段－議題列表小分類篩選－QA 測試報告

**角色：** QA  
**日期：** 2026-09-09  
**依據：** 系統設計書 v1.0、開發完成報告  
**結案：** 靜態／單元／API 對照通過；前端手動清單待使用者勾選  

| 案例 | 結果 |
|------|------|
| 01 `/issues` Chip | 程式改為小分類；靜態對照 `IssueListPage` |
| 02 選一小分類 | `List_filters_by_subCategoryId` 通過；本機 `GET /issues?subCategoryId=` 各類 mismatch=0 |
| 03 全部 | 不帶 query 回全部 |
| 04 無效 `subCategoryId` | 空列表、code 200 |
| 05 首頁正式議題 | `HomePage` Chip 仍 `majors`；僅 `api.issues({ majorCategoryId })` |
| 06 專案列表 | 未改 |

本環境無瀏覽器自動化；`/issues`、`/`、`/projects` HTTP 200。無缺陷。前端手動清單請使用者勾選。
