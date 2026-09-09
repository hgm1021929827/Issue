# 第十五階段－議題列表廠商與搜尋－QA 測試報告

**角色：** QA  
**日期：** 2026-09-09  
**依據：** 系統設計書 v1.0、開發完成報告  
**結案：** 靜態／單元／API 對照通過；前端手動清單待使用者勾選  

| 案例 | 結果 |
|------|------|
| 01 廠商下拉 | `IssueListPage` 用 `AppSelect`＋`clientCompanies`，無新增廠商 |
| 02 選一廠商 | `List_filters_by_vendor_and_search`；本機 `clientCompanyId` mismatch=0 |
| 03 搜尋 | 單元測試編號／標題；無命中空列表 |
| 04 與 Chip 疊加 | query AND |
| 05 無符合 | 「沒有符合的議題。」 |
| 06 首頁／專案 | 未加此列 |

本環境無瀏覽器自動化。`/issues` HTTP 200。無缺陷。前端手動清單請使用者勾選。
