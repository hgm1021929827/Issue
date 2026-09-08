# 第五階段－需要追蹤的 TODO－系統設計書

**角色：** SA  
**版本：** v1.0  
**日期：** 2026-09-04  
**狀態：** 設計完成，交 DA  
**依據：** [功能規格書](../需求規劃/第五階段－需要追蹤的 TODO－功能規格書.md) v1.0  
**Tech Spike：** 必做（需求類型 ①，含 UI／API）。結論見 [技術可行性評估](第五階段－需要追蹤的 TODO－技術可行性評估.md)，已併入本文。

本階段為增量：不改第一、三、四階段正式文件；沿用既有前後端切分與統一回應。本輪新增追蹤 TODO，並擴大通訊錄刪除與改廠商檢查。

---

# 一、系統架構

沿用第一階段：React（Vite）`5173`、ASP.NET Core 9 `5080`、MySQL 8 `issue_tracker`。無登入。統一回應 `{ "code", "message", "data" }`；成功 `code === 200`。JSON camelCase。路徑無 `/api` 前綴。

新畫面重用 `frontend/src/styles.css` token。刪除確認用 HTML `dialog`；錯誤用頁內橫幅。

# 二、模組拆分

| 模組 | 職責 |
|------|------|
| 追蹤 TODO | 隸屬一種工作的平鋪清單：CRUD、完成、同工作排序 |
| 首頁追蹤 | 未完成列表（含工作摘要與跳轉所需欄位） |
| 正式議題／專案（既有增量） | 改廠商時檢查客戶窗口類追蹤；刪除時連刪追蹤 |
| 專案議題（既有增量） | 可掛追蹤；刪除連刪；廠商仍唯讀跟隨專案 |
| 通訊錄（既有增量） | 刪成員／公司／窗口時多計追蹤占用 |

前端：共用「追蹤清單」區塊；首頁加 card。不新增獨立追蹤詳情路由。

# 三、功能流程

- **所屬工作：** 每一筆追蹤 TODO 恰好屬於正式議題、專案、或專案議題之一。API 用 nested 路徑表達歸屬，不讓用戶端自填三種 id。
- **對象：** `targetType` = `member`｜`clientContact`。`targetId` 必填。`member` 時須為現有公司成員。`clientContact` 時須為現有窗口，且窗口的客戶公司＝該工作廠商（專案議題用所屬專案的 `clientCompanyId`）。
- **無廠商：** 工作（或專案）`clientCompanyId` 為 null 時，寫入 `clientContact` → 400，message 提示先設定廠商。前端選客戶窗口時先擋並顯示說明。
- **無窗口：** 公司存在但窗口數為 0 → 400，提示先到成員頁新增窗口。
- **完成：** 只改該筆 `isCompleted`；無父項自動完成。首頁只查 `isCompleted === false`。
- **排序：** 與一般 TODO 同層 reorder 相同語意：請求帶該工作下**全部**追蹤 id 的新順序；不可改掛工作。
- **改廠商：** `PUT` 議題／專案且 `clientCompanyId` 與原值不同時，COUNT 客戶窗口類追蹤（專案還含其專案議題上的）＞0 → 409。
- **刪工作：** 應用層或資料庫 CASCADE 刪該工作追蹤；刪專案時專案議題連刪，其追蹤一併沒。
- **刪通訊錄：** COUNT 後 409，不刪。

# 四、API 規劃

既有分類、議題、一般 TODO、專案路徑維持。下列為新增或增量。

`targetType` 列舉字串：`member`、`clientContact`。  
`workType` 僅出現在首頁 DTO：`issue`、`project`、`projectIssue`。

### 4.1 依附工作的追蹤清單

寫入成功 `data` 回傳**該工作更新後完整列表**（方便前端替換），順序為 `sortOrder` 再 `id`。

| 名稱 | 方法 | 路徑 | Request | Response data | Error |
|------|------|------|---------|---------------|-------|
| 議題下列表 | GET | `/issues/{issueId}/track-todos` | 無 | `TrackTodo[]` | 404 無此議題 |
| 議題下新增 | POST | `/issues/{issueId}/track-todos` | `TrackTodoWrite` | 該議題完整列表 | 404；400 |
| 專案下列表 | GET | `/projects/{projectId}/track-todos` | 無 | `TrackTodo[]` | 404 |
| 專案下新增 | POST | `/projects/{projectId}/track-todos` | `TrackTodoWrite` | 該專案完整列表 | 404；400 |
| 專案議題下列表 | GET | `/projects/{projectId}/items/{itemId}/track-todos` | 無 | `TrackTodo[]` | 404（專案或項次不符） |
| 專案議題下新增 | POST | `/projects/{projectId}/items/{itemId}/track-todos` | `TrackTodoWrite` | 該項次完整列表 | 404；400 |

`TrackTodoWrite`：`title`（必填、最長 200）、`content`（可空字串、最長 2000）、`targetType`（必填）、`targetId`（必填）。新增時 `isCompleted` 固定 false。不可在 Write 傳所屬工作。

`TrackTodo`：`id`, `workType`, `workId`, `isCompleted`, `title`, `content`, `targetType`, `targetId`, `targetName`, `targetSecondaryName`, `sortOrder`, `createdAt`, `updatedAt`

- `workType`／`workId`：讀取用，與路徑一致。`workId` 對專案議題為專案議題識別。
- `targetName`：成員＝中文名；窗口＝窗口名稱。
- `targetSecondaryName`：成員＝英文名；窗口＝所屬公司名稱（供顯示「名（次要）」或附公司）。
- 內部顯示規則：`targetName（targetSecondaryName）`。窗口：主要顯示 `targetName`，需要時可附公司名。

新增時 `sortOrder`＝該工作目前最大＋1（無則 0 或 1，與一般 TODO 一致即可）。

### 4.2 單筆追蹤

| 名稱 | 方法 | 路徑 | Request | Response data | Error |
|------|------|------|---------|---------------|-------|
| 更新欄位 | PUT | `/track-todos/{id}` | `TrackTodoWrite` | 該工作完整列表 | 404；400 |
| 完成 | PUT | `/track-todos/{id}/complete` | `{ isCompleted }` | 該工作完整列表 | 404 |
| 排序 | PUT | `/track-todos/{id}/reorder` | `{ orderedIds: long[] }` | 該工作完整列表 | 404；409 不是該工作全部 id 或含外來 id |
| 刪除 | DELETE | `/track-todos/{id}` | 無 | 該工作完整列表 | 404 |

`orderedIds` 必須是**同一工作**下現有全部追蹤 TODO 的排列，不可改所屬工作。

400 文案（繁中，擇一對應）：

- 標題空白／過長、內容過長
- 未選對象類型或對象
- 類型與對象不一致（例如 `member` 但 id 是窗口）
- 對象不存在
- 選客戶窗口但工作尚未設定廠商：`請先設定廠商`
- 該公司沒有窗口：`請先在成員維護新增客戶窗口`
- 窗口不屬於該工作廠商：`客戶窗口須屬於該工作的客戶公司`

### 4.3 首頁未完成列表

| 名稱 | 方法 | 路徑 | Request | Response data | Error |
|------|------|------|---------|---------------|-------|
| 未完成 | GET | `/track-todos` | 無 query；固定只回未完成 | `TrackTodoHomeItem[]` | — |

`TrackTodoHomeItem`：`id`, `workType`, `workId`, `projectId`, `title`, `targetType`, `targetId`, `targetName`, `targetSecondaryName`, `workLabel`, `sortOrder`, `createdAt`

- `projectId`：僅 `projectIssue` 有值（所屬專案 id，供跳轉）；其餘 null。
- `workLabel`：議題＝有編號則 `編號 標題`，否則標題；專案＝`代號 名稱`；專案議題＝`項次 標題`（前面可加專案代號）。
- 排序：`createdAt` 新到舊，同一工作內改以該工作 `sortOrder`（先工作最近活動／`createdAt`，同工作內 `sortOrder`）。實作採：未完成全體依 `createdAt DESC`，但同一 `workType+workId` 的列彼此依 `sortOrder ASC` 群在一起亦可。**最低要求：** 同一工作內順序與詳情一致；跨工作穩定即可。建議：`createdAt DESC` 作工作群組鍵，組內 `sortOrder ASC`。

跳轉由前端組路徑，不必後端回 URL：

- `issue` → `/issues/{workId}?trackTodo={id}`
- `project` → `/projects/{workId}?trackTodo={id}`
- `projectIssue` → `/projects/{projectId}?item={workId}&trackTodo={id}`

### 4.4 窗口輕量列表（下拉）

既有 `GET /company-members` 供內部成員。窗口避免為一層下拉拉整棵樹：

| 名稱 | 方法 | 路徑 | Request | Response data | Error |
|------|------|------|---------|---------------|-------|
| 某公司窗口 | GET | `/client-companies/{companyId}/contacts` | 無 | `{ id, name }[]`（不含聯絡渠道） | 404 無此公司 |

排序：窗口名再 id。空陣列＝有公司無窗口（前端提示 TT-F03）。

### 4.5 既有 API 增量

`PUT /issues/{id}`、`PUT /projects/{id}`：若 `clientCompanyId` 變更，檢查客戶窗口類追蹤。

- 議題：該議題 `targetType=clientContact` 筆數＞0 → 409：`無法更改廠商，尚有 {n} 筆需要追蹤的 TODO 使用客戶窗口`
- 專案：該專案自己的此類筆數為 n1，底下專案議題合計 n2；n1+n2＞0 → 409，只列＞0 的段，例如 `無法更改廠商，尚有 1 筆專案、2 筆專案議題的需要追蹤的 TODO 使用客戶窗口`

`DELETE /issues/{id}`、`DELETE /projects/{id}`、`DELETE /projects/{projectId}/items/{id}`：連刪追蹤。專案刪除確認仍由前端既有 dialog；可加追蹤筆數（選配，前端另 GET 或刪前不算亦可；規格允許大於 0 才顯示。建議刪專案／議題前若前端已載入追蹤列表，用長度提示；後端不另開 COUNT API）。

`DELETE /company-members/{id}`：既有專案負責人員 COUNT 外，加內部追蹤筆數。message 只列＞0：`使用中，共 {n} 筆專案、{n} 筆需要追蹤的 TODO`

`DELETE /client-companies/{companyId}/contacts/{id}`：若窗口被追蹤引用 → 409：`使用中，共 {n} 筆需要追蹤的 TODO`（本輪起刪窗口不再無檢查）。

`DELETE /client-companies/{id}`：既有專案／正式議題廠商外，加「其窗口被追蹤」筆數（不重複算公司當廠商）。只列＞0：`使用中，共 {n} 筆專案、{n} 筆正式議題、{n} 筆需要追蹤的 TODO`

# 五、互動→API 對照表

| UI 操作 | API | 成功 | 失敗 |
|---------|-----|------|------|
| 開啟議題詳情 | GET 議題；GET 一般 TODO；GET `/issues/{id}/track-todos` | 兩區塊替換 | 橫幅 |
| 開啟專案詳情 | GET 專案；GET items；GET `/projects/{id}/track-todos` | 追蹤區塊 | 橫幅 |
| 開啟／編輯專案議題 | GET 該項次追蹤列表 | 表單內列表 | 橫幅 |
| 選內部成員 | GET `/company-members` | 填第二層 | 空則提示先新增成員 |
| 選客戶窗口 | 工作有廠商則 GET `.../contacts` | 填第二層 | 無廠商不打 API、提示設廠商；空陣列提示新增窗口 |
| 新增／改追蹤 | POST 或 PUT | 用回傳列表替換 | 400 橫幅 |
| 勾完成 | PUT complete | 列表替換；回首頁時該列應消失 | 橫幅 |
| 拖曳排序 | PUT reorder | 列表替換 | 409 橫幅、還原順序 |
| 刪追蹤 | DELETE（先 dialog） | 列表替換 | 橫幅 |
| 首頁載入 | GET `/track-todos` | 填區塊 | 橫幅；空陣列空狀態 |
| 跳轉 | 前端導頁＋query | 詳情載入後 scroll／高亮 | 無該列：橫幅「找不到該筆需要追蹤的 TODO」 |
| 儲存議題／專案改廠商 | PUT 既有 | 如舊 | 409 不改資料 |
| 刪成員／公司／窗口 | DELETE 既有 | 如舊 | 409 含追蹤筆數 |

# 六、前端元件對應表

| 畫面區塊 | 元件類型 | 主要狀態 |
|----------|----------|----------|
| 追蹤清單 | 共用 list（核取、標題、對象、拖曳、刪） | loading／empty／data |
| 追蹤編輯 | 表單或列內編輯；兩 `AppSelect` | 驗證錯誤 |
| 首頁需要追蹤項目 | card＋列（類型、工作、標題、對象、跳轉） | loading／empty／data |
| 定位高亮 | 列 class＋`scrollIntoView` | 有 query／找不到 |
| 占用錯誤 | 既有頁內橫幅 | 409 message |

不引入新 UI 套件。專案議題追蹤做在既有 item 編輯區內。

# 七、資料模型與資料流程

## 7.1 邏輯 Entity Model

| 實體名稱 | 中文名稱 | 用途 | 主要屬性 | 關聯 | 持久化方式 | 驗證規則 |
|----------|----------|------|----------|------|------------|----------|
| trackTodo | 追蹤 TODO | 工作上要追的事項 | 見 7.1.1 | 恰好一個所屬工作；一個對象（成員或窗口） | Database | 標題必填；類型與對象成對；客戶窗口須屬該工作廠商 |
| issue | 正式議題 | 既有；可掛多筆追蹤 | 既有＋廠商 | 1:N trackTodo | Database | 改廠商見第三章 |
| project | 專案 | 既有；可掛多筆追蹤 | 既有＋廠商 | 1:N trackTodo | Database | 改廠商含底下專案議題追蹤 |
| projectIssue | 專案議題 | 既有；可掛多筆追蹤 | 既有 | 1:N trackTodo；廠商跟隨專案 | Database | — |
| companyMember | 公司成員 | 內部對象 | 既有 | 被 N 筆 trackTodo 引用 | Database | 刪前 COUNT |
| clientContact | 客戶窗口 | 窗口對象 | 既有 | 被 N 筆 trackTodo 引用 | Database | 刪前 COUNT |
| clientCompany | 客戶公司 | 過濾窗口 | 既有 | 不直接被 trackTodo 指向 | Database | 刪前含窗口被引用 |

`category` 僅為專案 CRUD 範例，非本功能實體。

## 7.1.1 邏輯 Entity 屬性明細

### trackTodo（追蹤 TODO）

| 屬性名稱 | 中文名稱 | 邏輯型態 | 驗證限制 | 是否允許空值 |
|----------|----------|----------|----------|--------------|
| id | 識別 | 長整數 | 系統給定、唯一 | 否 |
| workType | 所屬工作類型 | 列舉 | 必填；issue／project／projectIssue 恰好一種 | 否 |
| workId | 所屬工作 | 長整數 | 必填；須為該類型現有列 | 否 |
| isCompleted | 是否完成 | 布林 | 必填；預設否 | 否 |
| title | 標題 | 字串 | 必填；去空白後不可空；最長 200 | 否 |
| content | 內容 | 字串 | 最長 2000；無則空字串 | 否 |
| targetType | 追蹤對象類型 | 列舉 | 必填；member／clientContact | 否 |
| targetId | 追蹤對象 | 長整數 | 必填；與 targetType 對應之現有成員或窗口；clientContact 時窗口公司＝工作廠商 | 否 |
| sortOrder | 排列順序 | 整數 | 必填；同一所屬工作內 | 否 |
| createdAt | 建立時間 | 日期時間 | 系統給定 | 否 |
| updatedAt | 更新時間 | 日期時間 | 系統給定 | 否 |

物理上「恰好一種工作」如何拆欄由 DA 決定（例如三個可空參照＋檢查約束）；邏輯上仍是 workType＋workId。

既有 issue／project／projectIssue／companyMember／clientContact／clientCompany **不新增業務屬性**（本輪只多關聯與刪除／改廠商規則）。

## 7.2 實體關聯概覽

- 一個 issue 有 0..N 個 trackTodo；刪 issue 則其 trackTodo 全刪。
- 一個 project 有 0..N 個 trackTodo（專案自己的）；另有 0..N 個 projectIssue，每個 projectIssue 有 0..N 個 trackTodo。刪 project 則專案自己的與底下項次的 trackTodo 全刪。
- trackTodo 對 companyMember 或 clientContact 為 N:1；刪成員／窗口前若仍有引用則禁止。
- trackTodo 不直接關聯 clientCompany；公司關係經工作廠商或窗口所屬公司。

## 7.3 資料流向

瀏覽器 → JSON API → 應用服務驗證對象與廠商 → 持久化 trackTodo。首頁一次 GET 組摘要（join 工作標題／代號與對象名稱）。改廠商與刪通訊錄在既有服務內加 COUNT。

## 7.4 DA 接手指引

有 Database 持久化。DA 依 §7.1、§7.1.1、§7.2 產出《資料庫設計書》。須覆蓋 trackTodo 實體，以及既有表**不必加欄**但須能以 FK／約束表達所屬工作與對象。刪工作須能連刪追蹤（CASCADE 或服務層，於設計書註明）。

# 八、權限流程

單人無登入。任何人可呼叫本機 API。成員／窗口不是操作者。

# 九、例外流程

| 情境 | 行為 |
|------|------|
| 工作不存在 | 404 |
| 驗證失敗 | 400＋繁中 message |
| 排序 id 集合不符 | 409 |
| 改廠商仍有客戶窗口追蹤 | 409，資料不變 |
| 刪通訊錄仍被追蹤 | 409，不刪 |
| 首頁跳轉的 id 已刪 | 200 開詳情；前端找不到列則橫幅 |
| 一般 TODO API | 不變；追蹤不進 `/todos` |

# 十、系統限制

- 不提供追蹤子項、自動完成、改掛工作、首頁勾完成、已完成首頁列表。
- 不把追蹤列入預約處理事項（預約本輪不做）。
- 不新增獨立追蹤維護頁。
- 專案／專案議題仍無一般 TODO。

# 十一、開發注意事項

- EnsureSchema 增量建表；與第四階段加欄方式相同。
- 單元測試：對象／廠商 400、改廠商 409、刪窗口 409、首頁只含未完成、reorder 集合檢查、刪議題後追蹤不殘留。
- 前端共用 `TrackTodoList`（名稱由 RD 定），三處引用。
- Spike 已確認 §8.1／8.3／8.4 可行；規格書 Spike 狀態交 PM 於下輪修訂回寫，本設計以評估結論為準，不擋 DA／RD。

# 十二、已確認決策

| 決策 | 來源 |
|------|------|
| 三種工作都可掛；獨立於一般 TODO | PM Q1＝A、Q2＝A |
| 單層拖曳排序；可空內容；對象必填 | Q3＝B、Q4＝B、Q5＝A |
| 無廠商／無窗口提示；改廠商擋客戶窗口追蹤 | Q6＝A、Q7＝A |
| 刪成員／公司／窗口檢查追蹤 | Q8＝A |
| 首頁只未完成；跳轉定位 | Q9＝A、Q10＝A |
| 路徑 nested 三條＋`/track-todos/{id}` 單筆 | SA |
| 首頁 GET `/track-todos` 無 query | SA |
| 窗口下拉用輕量 GET contacts | SA |
