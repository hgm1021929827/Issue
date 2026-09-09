# AI 使用說明 — Issue 個人工作追蹤

本文件供 AI 助理（Cursor Agent、Copilot 等）在本專案協作時參考。人類開發者亦可閱讀；日常入口見根目錄 `README.md`。

---

## 1. 專案定位

這是一個**個人工作追蹤 Web 系統**。第一、三～十五階段核心功能已有應用程式碼；第十五階段「議題列表廠商與搜尋」已開發完成，前端手動清單待使用者勾選。舊 Excel 設計原稿在第四階段歷史資料，不得直接實作。新畫面對齊第二階段視覺規格。

**已選定技術棧：**

| 目錄 | 技術 | 說明 |
|------|------|------|
| `frontend/` | React（Vite），埠 5173 | SPA；使用者可見文案為繁體中文。畫面視覺依第二階段《視覺設計規格書》，token 在 `frontend/src/styles.css` 的 `:root`。API 位址開發預設 `http://localhost:5080`；發佈後讀 `web/config.js` 的 `apiBase` |
| `backend/` | ASP.NET Core 9 API Controller（C#），埠 5080 | JSON API，非 Razor 產品頁。CORS 允許 `localhost`／`127.0.0.1` 任意埠，另可由 `Cors:Origins` 加網址（IIS 畫面如 `http://localhost:81`） |
| 資料庫 | MySQL 8 或 SQL Server，庫名 `issue_tracker` | 由 `Database:Provider`（`MySql`／`SqlServer`）切換；表名與欄位 `snake_case`；主鍵 MySQL 為 `BIGINT AUTO_INCREMENT`、SQL Server 為 `BIGINT IDENTITY`。`Publish:Root`、`Publish:ApiBaseUrl` 僅供 [`發佈.bat`](發佈.bat) 使用 |

啟動：先改 `backend/appsettings.json` 的 `Database:Provider` 與對應連線字串，再 `dotnet run --project backend --launch-profile http`，另開終端 `cd frontend && npm start`。API 契約見第一階段《系統設計書》第四章。統一回應 `{ "code", "message", "data" }`，前端判斷 `code === 200`。

角色提示（`.cursor/prompts/`）裡出現的 `category` 與 Java Entity 範例，僅為**文件格式示範**。C# 實體使用 **PascalCase**，並做 snake_case ↔ PascalCase 對應。

**不要**自行改成 Angular、Spring Boot 或其他框架。

---

## 2. 資料慣例

- 表名、欄位：**snake_case**（如 `issue_id`）
- 主鍵：MySQL 為 `BIGINT AUTO_INCREMENT`；SQL Server 為 `BIGINT IDENTITY`（由 `Database:Provider` 決定）
- C# 實體屬性：**PascalCase**（如 `IssueId`），對應資料庫 underscore
- 若日後改棧，須修訂本節與相關角色提示中的對齊說明

---

## 3. 文件目錄慣例

| 文件 | 對象 |
|------|------|
| `README.md` | 人類快速入口 |
| **本文件 `AGENTS.md`** | AI 擴充專案時的架構與契約 |
| `.cursor/prompts/*.md` | 多角色敏捷協作（需求規劃、系統設計、資料設計、程式開發、品質驗收、流程管理） |
| `.cursor/docs/流程管理助手使用手冊.html` | WFM 操作對照 |
| `.cursor/docs/開發階段總覽.md` | 階段時間軸、狀態與識別碼索引 |
| `.cursor/docs/{NN}-第{階段}階段－{功能}/` | 各階段功能產出；資料夾以兩位數開頭方便排序（例如 `01-第一階段－個人工作追蹤/`、`06-第六階段－追蹤 TODO 提醒與行事曆/`） |
| `.cursor/docs/{NN}-第{階段}階段－{功能}/歷史資料/` | 整理前累加原稿、原始計畫與可追溯歷史資料；原始需求正本在根目錄 `docs/`，此處以連結指向，不複製 PDF／DOCX |

階段目錄建議分層：`需求規劃/`、`系統設計/`、`資料設計/`、`程式開發/`、`品質驗收/`、`流程管理/`、`共用資料/`。

第一個功能從「第一階段」開始編號，資料夾加兩位數前綴（`01-`、`02-`…）；新一輪改善建立下一階段，不覆寫已結束階段的正式文件。第十二階段目錄為 `12-第十二階段－專案工作項次與工時/`。第十三階段目錄為 `13-第十三階段－正式議題 Excel 匯入/`。第十四階段目錄為 `14-第十四階段－議題列表小分類篩選/`。第十五階段目錄為 `15-第十五階段－議題列表廠商與搜尋/`。

修改架構級慣例時，請同步更新本文件與根目錄 `README.md`。

---

## 4. AI 多角色敏捷協作

本專案為**個人 Web 系統**，採敏捷 AI 協作。指定角色時引用 `.cursor/prompts/` 的中文角色提示（例如 `@.cursor/prompts/程式開發助手.md`）。

### 流水線

```
使用者 → PM → SA →（RD Tech Spike）→ DA → RD → QA
```

各角色**直接交接**，日常交付不經 WFM 關卡。

### Bug 分派

| 類型 | 處理 |
|------|------|
| 規格不符／邏輯矛盾 | QA → SA |
| 純實作錯誤／程式崩潰 | QA → RD（看板 CC SA） |

實作 Bug 修正**不得**變更 API 契約、Database Schema、商業邏輯；若必須變更 → **熔斷**，升級為規格 Bug 交 SA。

### 產品優化軌（與 MVP 區分）

| 情境 | 路徑 |
|------|------|
| MVP 已規劃增量 | 對應階段的 `品質驗收/測試報告/` |
| v1.0 後產品體驗／功能調整提案 | 建立下一個「第{階段}階段－{功能}」目錄，不覆寫已結束階段 |

流水線：使用者構想登錄 → 需求規劃 → 系統設計 →（技術可行性評估）→ 資料設計 → 程式開發 → 品質驗收。實作問題仍記錄於對應階段的「共用資料／工作進度表」。

### WFM

僅在流程爭議、里程碑檢視或使用者要求修訂角色制度時喚醒。詳見 `.cursor/prompts/流程管理助手.md`。

---

## 5. 通用約束

- 使用者可見文案使用**繁體中文**
- 新畫面與元件必須對齊第二階段《視覺設計規格書》（奶油白底、霧藍主色、棕灰文字、大圓角淺陰影）；不要改回純白／純黑或高飽和企業風
- 變更範圍宜小：只改與任務相關的檔案
- **不要**自動 `git commit` / `git push`，除非使用者明確要求
- **不要**提交 `.env`、密鑰或本機憑證
