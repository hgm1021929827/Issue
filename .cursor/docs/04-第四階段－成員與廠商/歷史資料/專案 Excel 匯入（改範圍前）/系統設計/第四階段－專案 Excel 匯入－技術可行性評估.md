# 第四階段－專案 Excel 匯入－技術可行性評估

**角色：** RD（Tech Spike，非正式開發）  
**日期：** 2026-09-02  
**依據：** 規格書 §8.1／§8.4 待評估項、系統設計草擬 API／UI

## 結論

**可行。** 沿用既有 React SPA、ASP.NET Core 9 API、MySQL、粉彩 token。Excel 在後端用 ClosedXML 讀 `.xlsx`，前端只做 multipart 上傳。行事曆第四種標記沿用格內捲動。不需新 UI 套件、不需改第一階段議題／TODO 契約。

本輪無須於 Spike 交付正式功能程式；下列供 SA 定稿。

## 評估項目

| 項目 | 結果 | 說明 |
|------|------|------|
| 讀取指定工作表名 | 可行 | ClosedXML `XLWorkbook` → `TryGetWorksheet`；名稱 trim、忽略大小寫後與「工作項目」「議題單」比對。不執行 VBA／巨集。 |
| 表頭別名 | 可行 | 第一個非空列當表頭；正規化（去空白、ToLower）對別名表。缺欄則該邏輯欄為空，交給列規則。 |
| 儲存格日期／數字 | 可行 | `cell.TryGetValue<DateTime>`／`GetDateTime()` 處理 Excel 序列日；否則當字串解析。小時用 decimal。 |
| 上傳契約 | 可行 | `POST` `multipart/form-data`，`IFormFile file`。Kestrel／`[RequestSizeLimit]` 5 MiB。前端 `FormData`，既有 `request()` 須允許不設 JSON Content-Type。 |
| 整批交易 | 可行 | EF `Database.BeginTransactionAsync`；缺表／零列在交易外先失敗。列異常 `continue` 仍可提交。 |
| 工作項目 CRUD 與 `/items` 並存 | 可行 | 新路徑 `/projects/{id}/work-items`，不改專案議題路徑。 |
| 日期工時三歸屬 | 可行 | 一張表三個可空 FK＋應用層恰好一個；或等價結構由 DA 定。同日 UNIQUE 在單一 FK 上。 |
| 備註正則 | 可行 | .NET 正規式即可；年省略用 `DateTime.Now.Year`（本機單人）。 |
| 匯入結果頁 | 可行 | `navigate(..., { state })`；無歷程表。重新整理顯示請重匯，符合規格「不強制歷程」。數百列異常用一般 `<table>`。 |
| 跳轉 `?workItem=` | 可行 | 與既有 `?item=` 相同：`useSearchParams` 捲動高亮。 |
| 行事曆第四來源 | 可行 | `IssueCalendarItemDto.source = "workItem"`；`MonthCalendar` 加分支。格內已 `overflow-y: auto`，四種來源同格仍達標。 |
| 分類刪除 COUNT | 可行 | 再加 workItem 一段文案。 |
| 視覺 | 可行 | 匯入 dialog、結果頁、工時小表沿用 token 與 `AppDialog`。 |

## 套件選擇

| 方案 | 結論 |
|------|------|
| ClosedXML | **採用**。MIT；讀 sheet 名與儲存格值直接；本專案尚無 Excel 套件。 |
| ExcelDataReader | 不採用（第一次讀完須自建表頭列索引，對別名需求沒有比較優勢）。 |
| EPPlus | 不採用（授權對個人專案不必要）。 |
| COM／Excel Interop | 不採用（伺服器無 Excel）。 |

前端不引入 SheetJS：檔案可含公式與日期序列，統一後端解析較一致。

## 框架成本

| 工作 | 成本 |
|------|------|
| ClosedXML 解析＋別名＋備註＋交易 upsert | 高 |
| 工作項目 API＋工時＋完成／實際日 | 中高 |
| 專案／專案議題加欄與工時 API | 中 |
| 匯入 dialog、結果頁、詳情分區 | 中 |
| 行事曆 `workItem` 與分類 COUNT | 低 |

## 規格回饋

- 8.1 匯入對話／檔案與表頭容錯：由「待評估」改為**已確認可行**（ClosedXML＋5 MiB＋別名表）。
- 8.4 行事曆工作項目標記擁擠：由「待評估」改為**已確認可行**（維持格內捲動＋截斷＋圖示）。
- 不調整產品範圍：不上匯入歷程、不上前端 Excel 預覽、不上 FullCalendar。
