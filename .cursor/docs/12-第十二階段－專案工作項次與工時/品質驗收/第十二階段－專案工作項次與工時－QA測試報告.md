# 第十二階段－專案工作項次與工時－QA 測試報告

**角色：** QA  
**日期：** 2026-09-05  
**需求類型：** ① Web 應用  
**結案：** 使用者於 2026-09-05 確認測試完成  
**依據：** [系統設計書](../系統設計/第十二階段－專案工作項次與工時－系統設計書.md) v1.0、[資料庫設計書](../資料設計/第十二階段－專案工作項次與工時－資料庫設計書.md) v1.0、[開發完成報告](../程式開發/第十二階段－專案工作項次與工時－開發完成報告.md)

---

## 一、Unit Test

**指令：** `dotnet test backend.Tests --nologo`  
**結果：** 通過 **75**，失敗 **0**，略過 **0**

含本輪 `WorkItemHourImportTests`：檔名解析、All／含名篩選、備註解析、同日 409、無工時可完成、項次鎖定、追蹤 workItem、刪專案連刪、匯入 upsert 寫入缺日工時、未找到 keep／delete、空白工作代號列異常。既有專案／追蹤／分類／議題回歸全過。

未覆蓋（殘餘風險，非缺陷）：完成後再加工時不改 `actualEndDate`（已用 API 補驗通過）；改廠商含工作項次窗口追蹤（程式有 COUNT，無專測）；月曆 `source=workItem`（庫無項次，未打出該標記）。

---

## 二、API Integration Test

對 `http://localhost:5080` 以 `curl` 驗證。測試工時已刪回；議題 `handlerName` 已還原空白。

| 案例 | 結果 |
|------|------|
| GET `/projects/999999/work-items` | ✅ 404「找不到該專案」 |
| GET `/projects/1/work-items` | ✅ `[]`；列表帶 `workItemCount: 0` |
| POST `/projects/1/work-items` | ✅ HTTP 405（無手新增） |
| GET `/work-items/999999` | ✅ 404「找不到該工作項次」 |
| PUT 已存議題改 `seqNo` | ✅ 400「項次建立後不可修改」 |
| PUT `handlerName` | ✅ 200，讀取有值（已還原） |
| POST hours `0`／`1000`／`1.234` | ✅ 400，文案與設計一致 |
| POST hours `2` @ 2026-09-12 | ✅ 200；`actualStartDate`＝該日 |
| 同日再 POST | ✅ 409「同一日已有工時」 |
| PUT complete true | ✅ `actualEndDate`＝09-12 |
| 完成後加 09-18 | ✅ `actualEndDate` 仍 09-12（§3.2） |
| PUT complete false | ✅ `actualEndDate` null；開始日仍在 |
| DELETE hours | ✅ 列表空；開始日清空 |
| POST resolve 無檔 | ✅ 400「請選擇一個 Excel 檔」 |
| 副檔名 `.xls` | ✅ 400「僅支援 xlsx」 |
| 檔名 `.xlsx` | ✅ 400「檔名解析不出專案代號」 |
| Template `P-20260108-1497 …xlsx` | ✅ `needProject`，代號／建議名稱正確 |
| imports 有檔無成員 | ✅ 400「請先選擇目前使用者」 |
| GET work-item tracks 999 | ✅ 404 |
| 月曆 2026-09 | ✅ 既有 project／projectIssue／trackTodo；無 workItem（庫無項次） |
| 統一信封 | ✅ `{ code, message, data }` |
| POST import-decisions `[]` | ❌ 200 `applied:true`（見 Q-12-01） |

---

## 三、Database 驗證

本機 `mysql` CLI 不在 PATH。以後端啟動 `EnsureSchema` 日誌對照 DA 書：

| 項目 | 結果 |
|------|------|
| `project_work_item` 欄位／UNIQUE `(project_id, work_item_code)`／`due_date` 索引／CASCADE | ✅ |
| `work_hour` 兩 FK、UNIQUE 同日、CHECK 恰好一歸屬、`hour_value`＞0 ≤999.99 | ✅ |
| `project_issue` 加 handler／完成／實際起迄／missing_kept | ✅ |
| `track_todo.project_work_item_id`、索引、FK CASCADE、CHECK 四選一 | ✅ |
| InMemory 不驗證 CHECK／FK | 已知限制 |

---

## 四、Business Logic

| 規則 | 結果 |
|------|------|
| 無 POST 工作項次 | ✅ |
| 管理欄只匯入寫 | ✅ API |
| 同日工時唯一 | ✅ 單元＋API |
| 實際開始＝最早工時 | ✅ |
| 無工時可完成、結束可空 | ✅ 單元 |
| 完成後加工時不改結束 | ✅ API |
| 取消完成清結束 | ✅ API |
| 項次鎖定 | ✅ |
| 備註缺日寫入、已有不覆蓋 | ✅ 單元 |
| 檔名空白前＝代號 | ✅ 單元＋Template resolve |
| 篩選 All／含中文名或英文名 | ✅ 單元 |
| 追蹤 `workType=workItem` | ✅ 單元 |
| 刪專案連刪項次／工時／追蹤 | ✅ 單元 |

---

## 五、前端手動測試

已建立 [前端手動測試清單](第十二階段－專案工作項次與工時－前端手動測試清單.md)。使用者於 2026-09-05 確認階段完成。

---

## 六、發現問題與分派

見 [QA問題確認文件](第十二階段－專案工作項次與工時－QA問題確認文件.md)。

| 編號 | 類型 | 分派 | 狀態 |
|------|------|------|------|
| Q-12-01 | 實作類 | RD（CC SA） | 階段結案延後 |
| Q-12-02 | 規格類 | SA | 階段結案延後 |

---

## 七、結論

核心工作項次／工時／匯入解析與寫入規則與系統設計書一致；單元測試全過。使用者於 2026-09-05 確認階段完成。Q-12-01／Q-12-02 不阻擋結案，延後另開階段處理。

✅ **已結案。**
