# 第十三階段－正式議題 Excel 匯入－開發完成報告

**角色：** RD  
**日期：** 2026-09-05  
**依據：** [系統設計書](../系統設計/第十三階段－正式議題 Excel 匯入－系統設計書.md) v1.0；[資料庫設計書](../資料設計/第十三階段－正式議題 Excel 匯入－資料庫設計書.md) v1.0  
**狀態：** 開發完成，交 QA  

---

## 本次完成功能

- `/issues`「匯入議題」對話：選一個 `.xls`／`.html`、目前使用者（獨立 localStorage）、處理中／加簽／已結案三個小分類（獨立 localStorage；屬大分類「議題」）。
- 後端以 HtmlAgilityPack 讀 UOF HTML 表；表頭 Decode（含數字實體）；SA／工程師-1／工程師-2 任一命中中文或英文名才匯入。
- 表單編號 upsert 正式議題：寫標題、內容、預計完成日、廠商；大分類一律「議題」。狀態含結案一律已結案。處理中且工程師命中：新建寫處理中；既有且簽核者不是使用者則結果頁逐筆選加簽或結案。不改備註、TODO、追蹤、預計項目。
- 廠商資訊去掉「客戶名稱 :／：」後對客戶公司；對不上則只建名稱。
- 未找到僅含曾匯入（`importedFromUof`）且不在本次有效列；結果頁選保留／刪除，須恰好覆蓋。
- 匯入產生的議題：編號、標題、內容、預計完成日、廠商一般編輯唯讀；列表「檔中沒有」標籤。新增／詳情不顯示大分類。
- Seed：確保大分類「議題」與小分類「處理中」「加簽」「已結案」。
- Q-12-01：專案 `import-decisions` 必須恰好覆蓋該專案最近一次 imports 的 `notFound`；空陣列且有未找到 → 400；程序重啟後快取空則空陣列 200。

## 修改檔案

後端：`Entities/IssueItem.cs`、`Data/AppDbContext.cs`、`Data/DbSeeder.cs`、`Dtos/Dtos.cs`、`Services/IssueService.cs`、`Services/IssueImportService.cs`、`Services/UofIssueHtmlParser.cs`、`Services/ImportNotFoundCache.cs`、`Services/ProjectImportService.cs`、`Controllers/IssueImportsController.cs`、`Program.cs`、`Issue.Api.csproj`（HtmlAgilityPack）。

測試：`backend.Tests/IssueImportTests.cs`、`IssueServiceTests.cs`、`WorkItemHourImportTests.cs`。

前端：`api.js`、`App.jsx`、`importSession.js`、`pages/IssueListPage.jsx`、`pages/IssuePage.jsx`、`pages/IssueImportResultPage.jsx`、`components/ImportIssueDialog.jsx`、`components/AppSelect.jsx`、`components/AppDateField.jsx`、`components/VendorSelect.jsx`、`styles.css`。

## Migration／Schema

EnsureSchema 增量（無獨立 Migration 專案）：

- `issue.imported_from_uof` TINYINT(1) NOT NULL DEFAULT 0
- `issue.missing_kept` TINYINT(1) NOT NULL DEFAULT 0

啟動 API 時會自動套用。不為匯入快取建表。

## API（新增／增量）

| 方法 | 路徑 |
|------|------|
| POST | `/issue-imports`（multipart：`file`、`currentUserMemberId`、`inProgressSubCategoryId`、`countersignSubCategoryId`、`doneSubCategoryId`） |
| POST | `/issue-imports/decisions`（`{ decisions: [{ id, action: keep\|delete }] }`） |

`IssueDto` 加 `importedFromUof`、`missingKept`。`PUT /issues/{id}` 對匯入列鎖五欄。專案 `POST /projects/{id}/import-decisions` 行為增量：須恰好覆蓋上次 notFound。

## 單元測試

`dotnet test backend.Tests`：97 通過。含 HTML 表頭實體、三欄篩選、編號 upsert、廠商去前綴與新建、工程師新建寫處理中、既有加簽／結案改結果頁手動選、僅 SA 命中維持舊規則、三小分類相同／缺漏／不屬議題 400、PUT 鎖五欄、決策空陣列／保留、零有效列仍 200、xlsx 拒絕、不碰 TODO；專案 Q-12-01 空陣列拒絕與重啟後空陣列 200。

## 已知限制

- 結果頁重整後 state 消失，需重匯（規格已載）。
- 未找到集合在程序記憶體；API 重啟後空決策視為無待決策，須重匯才能再核對。
- 手建編號若等於表單編號，再匯入會被當成匯入列並開始唯讀。
- 未把 Template 全檔當單元測試夾具；請 QA 用 `Template/議題匯入範本.xls` 手動抽樣。
- 預設小分類依名稱「處理中」「加簽」「已結案」；庫中若尚無該名稱，對話需手動選三個不同小分類（啟動 seed 會補上）。

## 建議 QA 測試重點

1. `/issues` 匯入：選成員與三小分類記住（勿與專案匯入共用鍵）；一次一檔；`.xlsx` 拒絕。
2. 用 `議題匯入範本.xls`：表頭實體可讀；SA 或工程師-1／2 命中中文或英文名。
3. 表單編號＝議題編號；再匯入更新標題／內容／日期／廠商；大分類固定「議題」。
4. 狀態含結案 → 已結案；處理中且新建工程師命中 → 處理中；處理中、既有且簽核者不是使用者 → 結果頁逐筆選加簽或結案。
5. 廠商「客戶名稱 : …」對上既有公司或新建只有名稱的公司。
6. 有效列 0 仍 200；未找到只列曾匯入者；保留出現「檔中沒有」，刪除連刪 TODO／追蹤／預計項目。
7. 匯入詳情五欄唯讀；改這五欄 PUT 400；備註／分類仍可存。
8. 結果頁重整提示請重匯。
9. 專案匯入結果：有未找到卻送空陣列 → 400「請為每筆未找到資料選擇保留或刪除」。
