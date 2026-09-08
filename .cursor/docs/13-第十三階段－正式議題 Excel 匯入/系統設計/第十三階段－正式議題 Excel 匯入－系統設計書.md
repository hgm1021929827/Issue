# 第十三階段－正式議題 Excel 匯入－系統設計書

**角色：** SA  
**版本：** v1.0  
**日期：** 2026-09-05  
**狀態：** 設計完成，交 DA  
**依據：** [功能規格書](../需求規劃/第十三階段－正式議題 Excel 匯入－功能規格書.md) v1.0  
**Tech Spike：** 必做（需求類型 ①，含 UI／API）。結論見 [技術可行性評估](第十三階段－正式議題 Excel 匯入－技術可行性評估.md)，已併入本文。

本階段為增量：不改第一～十二階段正式文件。沿用既有前後端切分、統一回應、正式議題／客戶公司／分類契約。本輪新增議題 UOF 匯入；正式議題增量兩欄；修正專案 `import-decisions` 覆蓋檢查。

---

# 一、系統架構

沿用第一階段：React（Vite）`5173`、ASP.NET Core 9 `5080`、MySQL 8 `issue_tracker`。無登入。統一回應 `{ "code", "message", "data" }`；成功 `code === 200`。JSON camelCase。路徑無 `/api` 前綴。

UOF 檔只在後端解析（**HtmlAgilityPack**，當 HTML）。前端 `multipart/form-data` 上傳一個 `.xls` 或 `.html`。不執行檔內 script。建議 `[RequestSizeLimit]` 5 MiB。**禁止**用 ClosedXML 開此檔。

專案 Excel 匯入路徑與套件維持第十二階段，不與本輪共用解析器。

新畫面重用 `frontend/src/styles.css` token。確認用 HTML `dialog`；錯誤用頁內橫幅。

上次議題匯入成員：前端 `localStorage`（鍵例如 `issue.issueImportMemberId`），**不要**與專案匯入鍵共用。處理中／加簽／已結案小分類同樣本機記住（`issue.issueImportInProgressSubId`、`issue.issueImportCountersignSubId`、`issue.issueImportDoneSubId`）。不進資料庫。

# 二、模組拆分

| 模組 | 職責 |
|------|------|
| 議題 UOF 匯入 | 讀表、篩選、編號 upsert、建客戶公司、結果、未找到決策 |
| 正式議題（增量） | `importedFromUof`、`missingKept`；PUT 鎖五欄 |
| 客戶公司（既有） | 名稱對不上則新增只有名稱的公司 |
| 專案匯入決策（增量） | Q-12-01：恰好覆蓋本次 notFound |
| 分類（既有＋seed） | 正式議題綁大分類「議題」；seed 確保小分類「處理中」「加簽」「已結案」；匯入對話選這三個對應 |

前端：`IssueListPage` 匯入鈕；`ImportIssueDialog`；`/issues/import-result`；`IssuePage` 唯讀；列表保留標籤。

# 三、功能流程

### 3.1 匯入

1. `/issues` 開 dialog：一個檔＋`currentUserMemberId`＋`inProgressSubCategoryId`＋`countersignSubCategoryId`＋`doneSubCategoryId`。三小分類必填、須屬大分類「議題」（若無則「議題分類」）、不可相同。無成員不可送。上次選擇的三小分類記在獨立 localStorage。
2. `POST /issue-imports`（multipart：`file`、四個 id）。單一交易：
   - 讀 HTML 第一張含「表單編號」與「議題標題」表頭的 table；缺 → 400 不寫入。
   - 載入目前使用者成員；不存在 → 400。
   - 三個小分類必須存在、屬「議題」、且 id 兩兩不同 → 否則 400。
   - 逐列：表頭對照見 §3.2。篩選：SA／工程師-1／工程師-2 任一 Contains 中文名或英文名（忽略大小寫、trim）。未過篩：計入 `skipped`，**不**進入有效列、**不**當未找到來源。
   - 有效列：通過篩選且表單編號、議題標題 trim 非空。編號空或標題空：該列 `rowErrors`，不寫。
   - 同檔有效列撞同一編號（忽略大小寫）：後列異常。
   - 編號對既有 `issue.issueNo`（忽略大小寫）→ 更新標題、內容、預計完成日、廠商；大分類一律寫「議題」；`importedFromUof=true`；`missingKept=false`；不改備註、TODO、追蹤、預計項目。
   - 小分類：狀態含「結案」一律已結案。其餘（處理中）且工程師-1／2 命中——無該編號 → 處理中；已有且「目前簽核者」不含使用者 → **不改**小分類，列入 `pendingCategory` 供結果頁逐筆選加簽／結案；已有且簽核者是使用者 → 處理中。僅 SA 命中且未結案：新建寫處理中；既有未結案且已有屬「議題」的小分類則不改。
   - 無該編號 → 新增；小分類依上款；`importedFromUof=true`；`missingKept=false`。
   - 廠商：§3.3。
   - 未找到：所有 `importedFromUof=true` 且編號不在本次有效列編號集合中的正式議題（含已 `missingKept` 者，若仍沒出現仍列一次，讓使用者再選）。
   - 記住本次 `notFound` 鍵集合（記憶體，鍵＝議題匯入全域）供決策核對。
3. 前端 `navigate('/issues/import-result', { state: result })`。
4. 有未找到或待判斷加簽／結案：選齊後 `POST /issue-imports/decisions`（`decisions` 加 `categoryChoices`：`countersign`／`done`）。keep → `missingKept=true`；delete → 既有刪議題。`countersign`／`done` 寫入匯入時選的加簽／已結案小分類。未送決策就離開：upsert 已生效，分類維持匯入前。
5. 同一編號再出現在後來有效列：`missingKept=false`，並更新 §3.1 那些欄。

### 3.2 欄名與列對照

表頭 Decode＋trim 後對：

| 表頭 | 用途 |
|------|------|
| 表單編號 | 議題編號 |
| 議題標題 | 標題 |
| 議題內容 | 內容 |
| 預計完成日 | 預計完成日 |
| 廠商資訊 | 廠商名稱來源 |
| 狀態 | 結案判定（含「結案」） |
| 目前簽核者 | 是否為目前使用者（分類） |
| SA、工程師-1、工程師-2 | 篩選；工程師另用於分類 |
| 其餘 | 忽略 |

### 3.3 廠商

廠商資訊去掉開頭「客戶名稱 :」或「客戶名稱：」（全半形冒號）再 trim。空 → 該列異常。其餘與客戶公司名稱忽略大小寫、去空白比對；對上則用該 id；對不上則新增公司（只名稱、無窗口）再綁定。名稱已存在（唯一約束）則綁既有，不報錯。結果 `createdCompanies[]`。

### 3.4 一般編輯鎖

`PUT /issues/{id}`：若 `importedFromUof`：request 的 `issueNo`、`title`、`content`、`dueDate`、`clientCompanyId` 必須與庫存相同（編號忽略大小寫；內容 null 當空字串；日期比日期），否則 400「此議題由匯入產生，請重新匯入以更新編號、標題、內容、預計完成日或廠商」。`remark`、`subCategoryId` 仍可改。新增／詳情畫面大分類鎖定「議題」。小分類若有，須屬該大分類（既有規則）。

不可用 API 把 `importedFromUof` 改回 false。

手建 `POST /issues` 不設 `importedFromUof`（false）。

# 四、API 規劃

既有分類、議題 CRUD、TODO、追蹤、專案匯入路徑維持。下列新增或增量。

### 4.1 正式議題增量

`Issue`／`IssueDto` 加：

- `importedFromUof`（布林）
- `missingKept`（布林）

列表、詳情、新建回傳都帶。前端用這兩個欄位決定唯讀與標籤。

`PUT /issues/{id}`：見 §3.4。400 文案如上。

### 4.2 議題匯入

| 名稱 | 方法 | 路徑 | Request | Response data | Error |
|------|------|------|---------|---------------|-------|
| 匯入寫入 | POST | `/issue-imports` | multipart `file`＋`currentUserMemberId`＋`inProgressSubCategoryId`＋`countersignSubCategoryId`＋`doneSubCategoryId` | `IssueImportResult` | 400／404 |
| 未找到決策 | POST | `/issue-imports/decisions` | `IssueImportDecisionRequest` | `{ applied: true }` | 400 |

`IssueImportResult`：`summary`（`created`, `updated`, `skipped`）、`rowErrors[]`、`createdCompanies[]`（`id`, `name`）、`notFound[]`（`id`, `issueNo`, `title`）、`pendingCategory[]`（`id`, `issueNo`, `title`, `status`）、`countersignSubCategoryId`、`doneSubCategoryId`

`rowErrors`：`rowIndex`（表體 1-based 或 HTML 列序，RD 定一種並在測試固定）、`kind`（`missingIssueNo`／`missingTitle`／`missingVendor`／`duplicateIssueNo`／`badDate`）、`message`

`IssueImportDecisionRequest`：`decisions: { id, action: keep｜delete }[]`、`categoryChoices: { id, action: countersign｜done }[]`  
`decisions` 必須**恰好覆蓋**本次 `notFound`；`categoryChoices` 必須恰好覆蓋本次 `pendingCategory`。缺漏、多餘 → 400。未找到文案 `請為每筆未找到資料選擇保留或刪除`；加簽／結案文案 `請為每筆選擇加簽或結案`。

400 文案（繁中）：`請選擇一個檔案`、`僅支援 xls 或 html`、`檔案不是議題匯入表格`、`找不到表頭「表單編號」`、`請先選擇目前使用者`、`請選擇處理中、加簽與已結案小分類`、`處理中、加簽與已結案不可相同`、`找不到大分類「議題」`、`小分類不屬於大分類「議題」`、`請為每筆未找到資料選擇保留或刪除`、`請為每筆選擇加簽或結案`、`請重新匯入後再選擇加簽或結案`。

非 xlsx 當議題來源：副檔名不是 `xls`／`html`／`htm` → `僅支援 xls 或 html`。內容不是 HTML table → `檔案不是議題匯入表格`。

### 4.3 專案匯入決策（Q-12-01）

`POST /projects/{projectId}/import-decisions` 增量：必須恰好覆蓋**該專案最近一次** `POST .../imports` 回傳的 `notFound` 每一筆（`ownerKind`＋`ownerId`）。空陣列且該次有未找到 → 400 同一句。最近一次集合在程序重啟後遺失：視為無待決策，空陣列 200（使用者應重匯）。不改 request JSON 形狀。

# 五、互動→API 對照表

| UI 操作 | API | 成功 | 失敗 |
|---------|-----|------|------|
| 開匯入 dialog | GET `/company-members`、GET `/majorCategories`、GET `/subCategories?majorCategoryId=` | 下拉；成員與三小分類預設 localStorage | 無成員／無「議題」提示 |
| 送出匯入 | POST `/issue-imports` | 結果頁 | 400 橫幅／dialog |
| 結果頁確認 | POST `/issue-imports/decisions` | 回 `/issues` | 未選完 400 |
| 結果頁查看 | GET `/issues/{id}` | 唯讀 dialog | 橫幅 |
| 開詳情 | GET 既有 | 五欄 disabled 若 importedFromUof；保留標籤 | 如舊 |
| 存詳情（匯入列） | PUT 既有 | 備註／分類寫入 | 改五欄 400 |
| 列表 | GET `/issues` | 保留列可辨識 | 如舊 |
| 專案結果頁確認 | 既有 import-decisions | 空且有未找到 → 400 | 補核對 |

# 六、前端元件對應表

| 畫面區塊 | 元件類型 | 主要狀態 |
|----------|----------|----------|
| `/issues` 匯入鈕 | btn，與新增並列 | — |
| 匯入 dialog | AppDialog：file＋兩個 AppSelect 小分類＋成員 | 未選檔／人／分類相同；大分類固定「議題」 |
| 結果頁 | 新頁 `/issues/import-result`：摘要、異常、新建廠商、未找到 radio、確認 | 無 state／未選完 |
| 列表列 | 既有 issue-row＋「檔中沒有」標籤 | missingKept |
| 詳情五欄 | input/textarea/select disabled | importedFromUof |

不引入新 UI 套件。不引入前端 Excel 函式庫。可抽 `ImportIssueDialog`，不要把專案 `ImportExcelDialog` 硬塞 UOF 欄位。

# 七、資料模型與資料流程

## 7.1 邏輯 Entity Model

| 實體名稱 | 中文名稱 | 用途 | 主要屬性 | 關聯 | 持久化方式 | 驗證規則 |
|----------|----------|------|----------|------|------------|----------|
| issue | 正式議題 | 既有增量：匯入來源與保留 | 既有＋7.1.1 增量 | N:1 clientCompany；N:1 majorCategory | Database | importedFromUof 時五欄不可經一般編輯改 |
| clientCompany | 客戶公司 | 既有；匯入可新增名稱 | 既有 | 被議題指向 | Database | 名稱全系統唯一（忽略大小寫） |
| importMemberPref | 上次議題匯入成員 | 下拉預設 | memberId | 指向公司成員 | 檔案（本機） | 不進 DB |
| importSubPref | 上次處理中／加簽／已結案小分類 | 下拉預設 | 三個 subCategoryId | 屬「議題」 | 檔案（本機） | 不進 DB |
| importNotFoundCache | 本次未找到集合 | 核對決策恰好覆蓋 | kind、主體 id 集合 | 專案匯入按 projectId；議題匯入全域 | Session（行程記憶體） | 重啟即空 |

`category` 僅為格式範例，非本功能實體。不為工時、不為匯入檔建永久實體。

## 7.1.1 邏輯 Entity 屬性明細

### issue（正式議題）增量

既有屬性不重列（編號、標題、內容、大分類、小分類、預計完成日、備註、廠商、時間戳）。本輪只加：

| 屬性名稱 | 中文名稱 | 邏輯型態 | 驗證限制 | 是否允許空值 |
|----------|----------|----------|----------|--------------|
| importedFromUof | 來自議題匯入 | 布林 | 預設否；匯入對上或新增後為是；不可經一般 API 改回否 | 否 |
| missingKept | 本次檔案沒有而保留 | 布林 | 預設否；決策 keep＝是；有效列再出現＝否 | 否 |

### clientCompany（客戶公司）

無新屬性。匯入新增時只寫名稱，規則同第四階段快捷新增。

`importNotFoundCache` 持久化為 Session，**無需** §7.1.1 表。

## 7.2 實體關聯概覽

- issue N:1 clientCompany（廠商必填，匯入寫入時不可空）
- issue N:1 majorCategory
- 無新的 1:N 實體

## 7.3 資料流向

檔 → 匯入服務 → 篩選列 → upsert issue／必要時 insert clientCompany → 組 ImportResult → 前端 state。決策 → 更新 missingKept 或刪 issue（既有 cascade TODO／追蹤／預計項目）。一般 PUT 讀 importedFromUof 決定是否拒絕五欄。

## 7.4 DA 接手指引

有 Database 持久化：DA 依 §7.1、**§7.1.1**、§7.2 產出完整《資料庫設計書》。本輪物理變更＝既有正式議題表加兩欄布林（預設否），EnsureSchema 增量。**不要**為 importNotFoundCache 建表。客戶公司表不改。每個有改動的 Table 須有七欄「欄位設計」表（見 `DA.md`）；可只詳列增量欄，並註明其餘欄不變。

# 八、權限流程

單人、無登入。開得了網站即可匯入與決策。成員下拉是通訊錄。

# 九、例外流程

| 情況 | 行為 |
|------|------|
| 空檔／非 html table | 400，不寫入 |
| 兩大分類相同或不存在 | 400 |
| 兩小分類相同、不存在或不屬「議題」 | 400 |
| 成員不存在 | 400 |
| 有效列 0（全被篩掉） | **仍 200**：summary created/updated＝0，skipped＞0，notFound＝所有 importedFromUof 且不在空集合（即全部匯入列）。讓使用者處理「這次檔裡沒有我的單」。不視為格式失敗。 |
| 廠商空 | 該列異常，其他列繼續 |
| 決策未覆蓋 | 400 |
| PUT 改匯入五欄 | 400 |
| 刪匯入列（決策 delete 或手刪） | 既有刪議題 |

# 十、系統限制

- 一次一檔。切換「目前使用者」再匯時，未出現在該人有效列的匯入議題會進未找到（規格 II-F09 字面）；單人記住上次可減輕。
- 手建編號若等於表單編號，會被當成匯入列並開始唯讀。
- 不解析網域帳號、不合併公司、不讀附件。
- 記憶體 notFound 重啟即失；須重匯才能再決策核對。

# 十一、開發注意事項

- EnsureSchema 加兩欄，方式同第四、五階段。
- 套件：`HtmlAgilityPack`。前端 FormData 不要硬設 JSON Content-Type。
- 單元測試：HTML 表頭實體、篩選三欄、編號 upsert、廠商去前綴與新建、結案字、PUT 鎖五欄、決策空陣列、決策缺漏、不碰 TODO。
- 用 Template `議題匯入範本.xls` 做解析抽樣（不必把數百列全當單元測試夾具，可截一小段 HTML）。
- Spike 已確認 §8.1–8.2 可行。
- Q-12-01：改 `ProjectImportService.ApplyDecisionsAsync`，記得該專案上次 imports 的 notFound 集合。

# 十二、已確認決策

| 決策 | 來源 |
|------|------|
| 下拉成員並記住 | PM Q1＝A |
| SA 或工程師任一命中 | Q2＝A |
| 表單編號＝議題編號；不蓋備註 | Q3＝A |
| 三個小分類；工程師新建寫處理中；既有且簽核者不是使用者則加簽或結案 | 使用者 2026-09-05 |
| 廠商對不上就新建公司 | Q5＝A |
| 未找到只含曾匯入 | Q6＝A |
| 不碰 TODO | Q7＝A |
| 只在 `/issues` 匯入；一次一檔 | Q8＝A |
| 不做工時 | Q9＝A |
| 匯入五欄唯讀 | Q10＝B |
| 修 Q-12-01 | Q11＝B |
| HtmlAgilityPack；結果 state；記憶體核對決策 | Spike |
| 路徑 `/issue-imports` | SA |
