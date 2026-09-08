# 第十三階段－正式議題 Excel 匯入－QA 測試報告

**角色：** QA  
**日期：** 2026-09-05  
**需求類型：** ① Web 應用  
**依據：** [系統設計書](../系統設計/第十三階段－正式議題 Excel 匯入－系統設計書.md) v1.0、[資料庫設計書](../資料設計/第十三階段－正式議題 Excel 匯入－資料庫設計書.md) v1.0、[開發完成報告](../程式開發/第十三階段－正式議題 Excel 匯入－開發完成報告.md)

---

## 一、Unit Test

**指令：** `dotnet test backend.Tests --nologo`  
**結果：** 通過 **92**，失敗 **0**，略過 **0**

含本輪 `IssueImportTests`：HTML 表頭數字實體、廠商前綴、SA／工程師篩選、編號 upsert、不蓋備註／TODO、結案改大分類並清小分類、列異常 kind、零有效列仍 200 且未找到只含曾匯入、xlsx 拒絕、決策空陣列 400、keep → `missingKept`、PUT 鎖五欄。`WorkItemHourImportTests` 含 Q-12-01：有未找到送空陣列 400；快取遺失送空陣列 200。既有議題／專案／工時回歸全過。

未以 Template 全檔當單元夾具（開發完成報告已知限制；靜態抽樣見下）。

---

## 二、API Integration Test

對 `http://localhost:5080` 驗證。寫入使用編號 `QA13-*`，未對 `notFound` 送 keep／delete，以免誤標或刪除其他匯入列。

| 案例 | 結果 |
|------|------|
| POST `/issue-imports` 無檔 | ✅ 400「請選擇一個檔案」 |
| 空檔 `.xls` | ✅ 400「請選擇一個檔案」 |
| 副檔名 `.xlsx` | ✅ 400「僅支援 xls 或 html」 |
| HTML 無 table | ✅ 400「檔案不是議題匯入表格」 |
| 有表無「表單編號」 | ✅ 400「找不到表頭「表單編號」」 |
| 未帶使用者／id=999999 | ✅ 400「請先選擇目前使用者」 |
| 未帶兩個小分類 | 取代原「兩個大分類」：400「請選擇處理中與已結案小分類」 |
| 兩小分類相同 | ✅ 400「處理中與已結案不可相同」 |
| 已結案小分類 id 不存在 | 400「請選擇處理中與已結案小分類」 |
| 有效 HTML：李翊華／Eva 兩列＋一列未命中 | ✅ 200；created=2，skipped=1；新建「QA13公司甲」；結案列 major＝已結案 id |
| 再匯只含 B | ✅ updated=1；A 備註仍在；A 與其他曾匯入列進 notFound |
| POST `/issue-imports/decisions` `[]`（本次有未找到） | ✅ 400「請為每筆未找到資料選擇保留或刪除」 |
| 決策 id 不在集合 | ✅ 400 同上 |
| PUT 匯入列改標題 | ✅ 400「此議題由匯入產生，請重新匯入以更新編號、標題、內容、預計完成日或廠商」 |
| PUT 五欄不變改 remark | ✅ 200；`importedFromUof` 仍 true |
| GET `/issues/1` 手建 | ✅ `importedFromUof=false`，`missingKept=false` |
| GET `/issues` 信封 | ✅ `{ code, message, data }`；列含 `importedFromUof` |
| POST `/projects/1/import-decisions` `[]`（本行程未做專案匯入） | ✅ 200 `applied:true`（§4.3 快取空視為無待決策） |

Q-12-01「有未找到仍送空陣列 → 400」由單元測試覆蓋（避免對本機專案實匯 Excel）。

---

## 三、Database 驗證

`mysql`：`issue_tracker.issue`

| 項目 | 結果 |
|------|------|
| `imported_from_uof` TINYINT(1) NOT NULL DEFAULT 0 | ✅ |
| `missing_kept` TINYINT(1) NOT NULL DEFAULT 0 | ✅ |
| 無匯入歷程表／快取表 | ✅ 與 DA 一致 |
| EnsureSchema `AddColumnIfMissing` | ✅ 對照 `DbSeeder.cs` |

---

## 四、Business Logic

| 規則 | 結果 |
|------|------|
| HtmlAgilityPack；不用 ClosedXML 開 UOF | ✅ 套件＋服務 |
| 篩選 SA 或工程師-1／2 Contains 中／英 | ✅ 單元＋API skipped |
| 表單編號 upsert；不蓋備註／TODO | ✅ 單元＋API |
| 結案字進已結案大分類；小分類不屬則 null | ✅ 單元 |
| 廠商去「客戶名稱 :／：」；對不上新建 | ✅ 單元＋API |
| 未找到只含 `importedFromUof` | ✅ 單元＋API（手建 #1 不列） |
| 零有效列仍 200 | ✅ 單元 |
| 決策恰好覆蓋 | ✅ 單元 keep；API 空／多餘 400 |
| PUT 鎖五欄；備註可改 | ✅ 單元＋API |
| 手建兩欄為否 | ✅ API |
| 議題／專案 localStorage 鍵分離 | ✅ 靜態：`issue.issueImportMemberId` vs `issue.importMemberId` |
| 專案 ImportExcelDialog 未塞 UOF | ✅ 靜態；議題用 `ImportIssueDialog` |
| Q-12-01 專案決策核對上次 notFound | ✅ 單元 |

Template `議題匯入範本.xls`：HTML `table.Grid`、明文「表單編號」、實體表頭（預計完成日／工程師／議題標題／內容／廠商）。未把全檔 POST 進本機庫。

---

## 五、前端手動測試

已建立 [前端手動測試清單](第十三階段－正式議題 Excel 匯入－前端手動測試清單.md)。QA 依角色規範不自行點擊；請使用者勾選。

靜態對照：`/issues/import-result` 在 `/issues/:id` 之前；結果無 state 提示重匯；詳情五欄 `disabled`；列表「檔中沒有」。

---

## 六、發現問題與分派

無。未建立《QA問題確認文件》。

第十二階段 Q-12-01 本輪已依設計修好（有未找到＋空陣列 → 400；重啟無快取 → 空陣列 200）。

---

## 七、已知限制與測試殘留

- 結果頁 state、記憶體 notFound：與系統設計書第十章／開發完成報告相同。
- 庫中大分類名稱若無「處理中／已結案」小分類，對話須手選（啟動 seed 會補上）。
- 本機寫入殘留：`QA13-A`、`QA13-B`、客戶「QA13公司甲」；另有開發驗證列 `RD13-UI-001`／「匯入驗證客戶」。可手動刪。

**增量（同日，分類綁定）：** 匯入改選兩個小分類（處理中／已結案），大分類固定「議題」。上表「兩個大分類／結案列 major」已由程式取代，須重跑單元測試與匯入對話。其餘項目仍有效。本輪未再開 QA 結案。

---

## 八、結論

可執行測試與系統設計書、資料庫設計書一致；單元 92 全過；無 Bug 分派。

✅ **本輪 QA 測試結案。** 階段是否關閉請使用者勾完前端清單後決定。
