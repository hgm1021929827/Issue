# 第十三階段－正式議題 Excel 匯入－技術可行性評估

**角色：** RD（Tech Spike，非正式開發）  
**日期：** 2026-09-05  
**依據：** 規格書 §8.1／8.2 待評估項、系統設計草擬 API／UI；Template [`議題匯入範本.xls`](../../../../Template/議題匯入範本.xls)

## 結論

**可行。** 沿用 React SPA、ASP.NET Core 9、MySQL、粉彩 token、`AppDialog`／`AppSelect`。檔案在後端用 **HtmlAgilityPack** 當 HTML 表格讀，**不用 ClosedXML**（此檔不是 OOXML `.xlsx`）。結果頁與專案匯入相同：**前端 state 持有 ImportResult**；決策覆蓋以伺服器暫存本次 `notFound` 核對。不建匯入歷程表、不存上傳檔。

本輪無須於 Spike 交付正式功能程式。

## 評估項目

| 項目 | 結果 | 說明 |
|------|------|------|
| UOF `.xls` 實為 HTML | 可行 | 檔頭 `<header><meta … charset='utf-8'>`，一張 `table.Grid`。ClosedXML 開此檔會失敗。後端以 UTF-8 讀文字，HtmlAgilityPack 取第一張含「表單編號」表頭的 table。 |
| 表頭 HTML 實體 | 可行 | 樣本同時有明文（申請者）與數字實體（`&#35696;&#38988;&#27161;&#38988;`＝議題標題）。`HttpUtility.HtmlDecode`（或 WebUtility）後 trim 即可對照規格欄名。 |
| 儲存格 `<br>`／`&nbsp;` | 可行 | inner HTML 換成換行與空白再 Decode；連續空白可正規化，換行保留。 |
| 預計完成日 | 可行 | 樣本 `2026/09/04`。`DateOnly.TryParse`／`yyyy/M/d`。空則 null。 |
| 廠商「客戶名稱 :」 | 可行 | 去掉開頭「客戶名稱 :」或「客戶名稱：」再 trim。 |
| 篩選 Contains 姓名 | 可行 | 與第十二階段相同：忽略大小寫 Contains 中文名或英文名；三欄任一命中。 |
| 匯入對話 | 可行 | 重用 `AppDialog`；檔案＋成員＋兩個小分類 `AppSelect`（屬「議題」）。成員與兩小分類各自獨立 localStorage 鍵（勿與專案匯入共用）。 |
| 結果頁重整 | 可行 | `navigate('/issues/import-result', { state })`。重整無 state → 提示重匯。決策 POST 一次送出。 |
| 唯讀五欄 | 可行 | `IssuePage` 依 `importedFromUof` 把編號／標題／內容／預計完成日／廠商改 disabled；PUT 後端若這五欄與庫存不同 → 400。 |
| 保留標籤 | 可行 | 列表／詳情加 chip，不鎖顏色。 |
| Q-12-01 恰好覆蓋 | 可行 | 匯入成功後伺服器記住本次 `notFound` 鍵集合（專案：每 `projectId`；議題：全域一次）。決策必須恰好等於該集合；空陣列且集合非空 → 400「請為每筆未找到資料選擇保留或刪除」。程序重啟後集合空了：空陣列當「無待決策」200，與現況「重整請重匯」一致，不建歷程表。 |
| 上傳大小 | 可行 | 建議 5 MiB（範本數百列 HTML 遠小於此）。不執行 script。 |

## 套件選擇

| 方案 | 結論 |
|------|------|
| HtmlAgilityPack | **採用**。讀 table／th／td。 |
| ClosedXML | **不採用**於本輪議題檔。專案 `.xlsx` 仍用 ClosedXML。 |
| 前端 DOMParser | 不採用。篩選、upsert、建公司、交易統一後端。 |
| 伺服器暫存上傳檔 | 不採用。 |

## 框架成本

| 工作 | 成本 |
|------|------|
| HTML 解析＋篩選＋upsert＋建公司＋交易 | 中高 |
| 議題兩欄增量、PUT 唯讀、列表標籤 | 中 |
| 匯入對話、結果頁、Q-12-01 核對 | 中 |
| 與專案匯入分離路徑 | 低 |

## 規格回饋

- 8.1 匯入對話：由「待評估」改為**已確認可行**（HtmlAgilityPack＋5 MiB）。
- 8.2 結果頁：由「待評估」改為**已確認可行**（state；重整請重匯；伺服器暫存本次 notFound 以核對決策）。
- 無需調整產品範圍。
