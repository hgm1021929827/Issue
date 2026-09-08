# 第十二階段－專案工作項次與工時－QA 測試案例

**角色：** QA  
**日期：** 2026-09-05  
**依據：** [系統設計書](../系統設計/第十二階段－專案工作項次與工時－系統設計書.md) v1.0  
**需求類型：** ① Web 應用

---

## TC-01 工作項次 API

| 案例 | 步驟 | 預期 |
|------|------|------|
| 01-01 無此專案列表 | GET `/projects/999999/work-items` | 404 |
| 01-02 空列表 | 專案無項次 | 200，`[]` |
| 01-03 無手新增 | 搜尋 POST `/projects/{id}/work-items`（非 hours／track） | 無此路由 |
| 01-04 單筆 404 | GET `/work-items/999999` | 404 |
| 01-05 完成／取消 | PUT `/complete` `{ isCompleted }` | 200；無工時時 `actualEndDate` null |
| 01-06 刪除 | DELETE 項次 | 200，`{ deleted: true }`；再 GET 404 |
| 01-07 排序 | 多筆列表 | `workItemCode` 字串再 `id`，不解析 `3.1.1` 階層 |

## TC-02 工時

| 案例 | 步驟 | 預期 |
|------|------|------|
| 02-01 項次新增 | POST hours `{ date, hours, remark }` | 200，完整列表；`actualStartDate`＝最早日 |
| 02-02 同日第二筆 | 同日再 POST | 409，`同一日已有工時` |
| 02-03 hours≤0 | `hours: 0` | 400 |
| 02-04 超過 999.99 | `hours: 1000` | 400 |
| 02-05 三位小數 | `hours: 1.234` | 400 |
| 02-06 議題工時 | `/items/{id}/hours` 同上 | 同左 |
| 02-07 刪工時重算開始 | 刪最早日 | `actualStartDate` 改為剩餘最早；無則 null |
| 02-08 完成後再加工時 | 已完成且已有 `actualEndDate`，再加較晚日 | `actualEndDate` **不變** |

## TC-03 專案議題增量

| 案例 | 步驟 | 預期 |
|------|------|------|
| 03-01 改項次 | PUT 已存議題 `seqNo` 不同 | 400，`項次建立後不可修改` |
| 03-02 處理人 | PUT `handlerName` | 200，讀取有值 |
| 03-03 完成 | PUT `.../items/{id}/complete` | 規則同項次 |

## TC-04 追蹤 workItem

| 案例 | 步驟 | 預期 |
|------|------|------|
| 04-01 新增 | POST `/work-items/{id}/track-todos` | 200，`workType=workItem` |
| 04-02 首頁 | GET `/track-todos` | 含該筆；`projectId` 有值；`workLabel` 含工作代號 |
| 04-03 路徑不符 | 他專案 id | 404 |

## TC-05 匯入

| 案例 | 步驟 | 預期 |
|------|------|------|
| 05-01 無檔 | POST resolve 不帶 file | 400，`請選擇一個 Excel 檔` |
| 05-02 非 xlsx | `.xls` 或錯副檔名 | 400，`僅支援 xlsx` |
| 05-03 檔名無代號 | 副檔名前空白 | 400，`檔名解析不出專案代號` |
| 05-04 已有代號 | 檔名空白前＝現有 `projectCode` | `status=ready`，有 `projectId` |
| 05-05 無此代號 | 新代號 | `status=needProject`，有 `suggestedName` |
| 05-06 未選使用者 | imports 不帶 member | 400，`請先選擇目前使用者` |
| 05-07 缺表 | 無「議題單」 | 400，`找不到工作表「議題單」`；DB 不變 |
| 05-08 零有效列 | 篩選後無工作項次 | 400，`沒有符合目前使用者的工作項次` |
| 05-09 All／含名 | 負責人員 `All` 或含中文名／英文名 | 匯入該列；中英文名都不含且非 All 不匯 |
| 05-10 再匯更新 | 同工作代號改說明 | `workItemUpdated`；不新增第二筆 |
| 05-11 備註寫入缺日 | 備註 `6/12 - 3H` 或 `3/16-2h` 且網頁無該日 | 寫入該日工時；已有不同小時不覆蓋並列 `hoursDiffer` |
| 05-12 空白代號 | 有說明無代號 | `rowErrors.kind=missingWorkItemCode` |
| 05-13 決策 keep／delete | import-decisions | keep → `missingKept=true`；delete → 實體消失 |
| 05-14 決策缺漏 | 未覆蓋本次 notFound | 400，`請為每筆未找到資料選擇保留或刪除` |

## TC-06 刪除與月曆

| 案例 | 步驟 | 預期 |
|------|------|------|
| 06-01 刪專案 | DELETE `/projects/{id}` | `workItemCount`；項次／工時／追蹤不殘留 |
| 06-02 月曆 | 項次有 `dueDate` | `source=workItem`，`projectId`＋`id` |
| 06-03 改廠商 | 項次上有窗口追蹤 | 409，message 含工作項次 |
| 06-04 刪分類 | 有工作項次 | message 含「工作項次 n 筆」（只列＞0） |

## TC-07 回歸

| 案例 | 步驟 | 預期 |
|------|------|------|
| 07-01 既有專案／議題／追蹤 | 單元測試套件 | 全通過 |
| 07-02 統一信封 | 200／4xx | `{ code, message, data }` |
