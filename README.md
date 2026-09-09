# Issue

個人工作追蹤 Web 系統。第一、三～十四階段（分類、正式議題、多層 TODO、首頁清單與行事曆、專案與專案議題、成員與廠商、需要追蹤的 TODO、追蹤提醒與行事曆、專案頁分頁、專案議題左右切分、專案議題不顯示客戶名稱、正式議題獨立列表頁、首頁清單分頁、專案工作項次與工時、正式議題 Excel 匯入、議題列表小分類篩選）已實作。第十五階段「議題列表廠商與搜尋」已實作。

## 技術棧

| 層 | 技術 | 目錄 |
|------|------|------|
| 前端 | React（Vite） | `frontend/` |
| 後端 | ASP.NET Core 9（JSON API） | `backend/` |
| 資料庫 | MySQL 8 或 SQL Server | 庫名 `issue_tracker`；由 `Database:Provider` 切換 |

## 本機啟動

1. 編輯 [`backend/appsettings.json`](backend/appsettings.json)：
   - `Database:Provider`：`SqlServer` 或 `MySql`
   - 對應連線字串：`ConnectionStrings:SqlServer` 或 `ConnectionStrings:MySql`
2. 後端：`dotnet run --project backend --launch-profile http` → `http://localhost:5080`
3. 前端：`cd frontend` → `npm start` → `http://localhost:5173`

亦可雙擊 [`網站.bat`](網站.bat) 啟動／停止本機前後端。

空庫首次啟動後端會 `EnsureCreated` 建表，並補齊大分類「議題」與小分類「處理中」「加簽」「已結案」。之後可在分類設定自行增刪改名。不會自動建立「預約」。

單元測試：`dotnet test`

## 發佈（IIS 等）

1. 在 [`backend/appsettings.json`](backend/appsettings.json) 設定：
   - `Publish:Root`：發佈根目錄（相對專案根目錄或絕對路徑）
   - `Publish:ApiBaseUrl`：前端要呼叫的 API 網址（須與 IIS 上的 API 網站一致，例如 `http://localhost:5080`）
   - `Cors:Origins`：畫面網址（`localhost`／`127.0.0.1` 任意埠已自動允許；用電腦名稱或區網 IP 再開畫面時再加）
   - 目標庫的 `Database:Provider` 與連線字串
2. 雙擊或執行 [`發佈.bat`](發佈.bat)。
3. 產出：
   - `%Publish.Root%\api` — 後端 API（`dotnet publish`）
   - `%Publish.Root%\web` — 前端靜態檔（`npm run build`）；內含 `config.js` 的 `apiBase`
4. 發佈結束前會對 `api\appsettings.json` 的連線執行 `--ensure-db`（建表／補 schema／補「議題」初始資料）。

IIS 請分成兩個網站：`web` 靜態檔（例如 `http://localhost:81`）、`api` ASP.NET Core（網址必須等於 `Publish:ApiBaseUrl`）。之後若只改 API 網址，可直接改 `web\config.js` 的 `apiBase`，不必重編前端。

`Publish:Root` 與 `Publish:ApiBaseUrl` 僅供發佈腳本使用，API 執行期會忽略。CORS 則讀 `Cors:Origins`。

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
- `.cursor/docs/13-第十三階段－正式議題 Excel 匯入/` — 正式議題 Excel 匯入（已結束，QA 完成）
- `.cursor/docs/14-第十四階段－議題列表小分類篩選/` — 議題列表小分類篩選（進行中；前端手動清單待勾選）
- `.cursor/docs/15-第十五階段－議題列表廠商與搜尋/` — 議題列表廠商下拉與搜尋（進行中；前端手動清單待勾選）

## 指定角色

在 Cursor 用 `@.cursor/prompts/需求規劃助手.md` 等指定角色。流水線：使用者 → PM → SA →（RD Tech Spike）→ DA → RD → QA。
