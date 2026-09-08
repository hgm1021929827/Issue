# Issue

個人工作追蹤 Web 系統。第一、三～十二階段（分類、正式議題、多層 TODO、首頁清單與行事曆、專案與專案議題、成員與廠商、需要追蹤的 TODO、追蹤提醒與行事曆、專案頁分頁、專案議題左右切分、專案議題不顯示客戶名稱、正式議題獨立列表頁、首頁清單分頁、專案工作項次與工時）已實作。第十三階段「正式議題 Excel 匯入」已實作。

## 技術棧

| 層 | 技術 | 目錄 |
|------|------|------|
| 前端 | React（Vite） | `frontend/` |
| 後端 | ASP.NET Core 9（JSON API） | `backend/` |
| 資料庫 | MySQL 8 | 庫名 `issue_tracker` |

## 本機啟動

1. 確認 MySQL 服務已啟動，並依本機帳密修改 [`backend/appsettings.json`](backend/appsettings.json) 的 `ConnectionStrings:Default`（預設 `root/root`）。
2. 後端：`dotnet run --project backend --launch-profile http` → `http://localhost:5080`
3. 前端：`cd frontend` → `npm start` → `http://localhost:5173`

空庫首次啟動後端會 `EnsureCreated` 建表並寫入兩個預設大分類（預約、議題）。「預約」底下有範例小分類；「議題」底下有「處理中」「加簽」「已結案」。之後可在分類設定自行增刪改名。

單元測試：`dotnet test`

## 目錄

- `AGENTS.md` — AI 架構與協作契約
- `docs/` — 原始需求正本（PDF／DOCX；各階段文件連到此處，不複製）
- `.cursor/docs/開發階段總覽.md` — 階段索引
- `.cursor/docs/專案功能完成總覽.md` — 對照原始需求的完成狀態
- `.cursor/docs/01-第一階段－個人工作追蹤/` — 核心功能規格與設計
- `.cursor/docs/02-第二階段－粉彩視覺風格/` — 畫面視覺規格（後續 UI 開發依據）
- `.cursor/docs/03-第三階段－專案與專案議題/` — 專案、專案議題（已結束，測試完成）
- `.cursor/docs/04-第四階段－成員與廠商/` — 成員與廠商（已結束，測試完成）；Excel 匯入原稿在該階段 `歷史資料/`
- `.cursor/docs/05-第五階段－需要追蹤的 TODO/` — 需要追蹤的 TODO（已結束，測試完成）
- `.cursor/docs/06-第六階段－追蹤 TODO 提醒與行事曆/` — 追蹤 TODO 提醒日期與行事曆（已結束，測試完成）
- `.cursor/docs/07-第七階段－專案頁分頁/` — 專案詳情分頁（已結束，測試完成）
- `.cursor/docs/08-第八階段－專案議題左右切分/` — 專案議題左右切分（已結束，測試完成）
- `.cursor/docs/09-第九階段－專案議題不顯示客戶名稱/` — 專案議題不顯示客戶名稱（已結束，測試完成）
- `.cursor/docs/10-第十階段－正式議題獨立列表頁/` — 正式議題獨立列表頁（已結束，測試完成）
- `.cursor/docs/11-第十一階段－首頁清單分頁/` — 首頁清單分頁（已結束，測試完成）
- `.cursor/docs/12-第十二階段－專案工作項次與工時/` — 專案工作項次、工時與專案 Excel（已結束，測試完成）
- `.cursor/docs/13-第十三階段－正式議題 Excel 匯入/` — 正式議題 Excel 匯入（進行中，已開立）

## 指定角色

在 Cursor 用 `@.cursor/prompts/需求規劃助手.md` 等指定角色。流水線：使用者 → PM → SA →（RD Tech Spike）→ DA → RD → QA。
