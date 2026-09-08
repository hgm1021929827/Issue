# 第五階段－需要追蹤的 TODO－QA 測試報告

**角色：** QA  
**日期：** 2026-09-04  
**需求類型：** ① Web 應用  
**結案：** 使用者於 2026-09-05 確認測試完成  
**依據：** [系統設計書](../系統設計/第五階段－需要追蹤的 TODO－系統設計書.md) v1.0、[資料庫設計書](../資料設計/第五階段－需要追蹤的 TODO－資料庫設計書.md) v1.0、[開發完成報告](../程式開發/第五階段－需要追蹤的 TODO－開發完成報告.md)

---

## 一、Unit Test

**指令：** `dotnet test --nologo --verbosity normal`  
**結果：** 通過 **59**，失敗 **0**，略過 **0**

| 測試類別 | 測試數 | 結果 |
|----------|--------|------|
| TrackTodoServiceTests | 17 | ✅ 全通過 |
| DirectoryServiceTests | 8 | ✅ 全通過（回歸） |
| IssueServiceTests | 10 | ✅ 全通過（回歸） |
| ProjectServiceTests | 6 | ✅ 全通過（回歸） |
| ProjectVendorTests | 3 | ✅ 全通過（回歸） |
| CategoryServiceTests | 9 | ✅ 全通過（回歸） |
| CategoryProjectUsageTests | 2 | ✅ 全通過（回歸） |
| TodoServiceTests | 4 | ✅ 全通過（回歸） |

### 關鍵測試項對照系統設計書 §十一

| 設計書要求 | 測試名稱 | 結果 |
|------------|----------|------|
| 成員對象可寫入 | Create_member_on_issue_ok | ✅ |
| 無廠商選窗口 400 | Create_contact_without_vendor_is_400 | ✅ 文案「請先設定廠商」 |
| 無窗口 400 | Create_contact_without_windows_is_400 | ✅ 「請先在成員維護新增客戶窗口」 |
| 窗口非該廠商 400 | Create_contact_from_other_company_is_400 | ✅ |
| 標題空白 400 | Blank_title_is_400 | ✅ |
| 首頁只含未完成 | Home_lists_incomplete_only | ✅ |
| reorder 集合檢查 | Reorder_rejects_partial_list | ✅ 409 |
| reorder 成功 | Reorder_persists_order | ✅ |
| 刪議題後追蹤不殘留 | Delete_issue_removes_track_todos | ✅ |
| 改議題廠商 409 | Change_issue_vendor_blocked_when_contact_track_exists | ✅ |
| 僅成員追蹤可改廠商 | Change_issue_vendor_ok_when_only_member_track | ✅ |
| 專案改廠商含項次 | Change_project_vendor_counts_item_tracks | ✅ |
| 刪窗口 409 | Delete_contact_in_use_is_409 | ✅ |
| 刪成員 409 | Delete_member_in_use_as_track_is_409 | ✅ |
| 刪公司（窗口被追蹤）409 | Delete_company_in_use_by_track_is_409 | ✅ |
| 專案議題首頁欄位 | Project_item_create_and_home_label | ✅ `projectIssue`＋`projectId` |
| 項次路徑不符 404 | Wrong_project_item_path_is_404 | ✅ |

---

## 二、API Integration Test

對 `http://localhost:5080` 發實際請求（成功路徑以 `Invoke-RestMethod`；4xx 以 `curl.exe` 讀 body，因 PowerShell 5 對錯誤回應的 `ErrorDetails` 常為空）。

測試用客戶公司／議題／專案於結束時已刪除。議題 `#1` 仍留有 RD 連線測試留下的「API smoke 追蹤」（見第九節）。

| 案例 | 端點 | 結果 |
|------|------|------|
| GET 窗口輕量列表 | `/client-companies/{id}/contacts` | ✅ `{ id, name }`，無 channels |
| GET 無窗口公司 | 同上，窗口 0 | ✅ `data: []` |
| GET 無此公司 | `/client-companies/999999/contacts` | ✅ HTTP 404，`找不到該客戶公司` |
| GET 無此議題追蹤 | `/issues/999999/track-todos` | ✅ HTTP 404，`找不到該議題` |
| POST 議題成員追蹤 | `/issues/{id}/track-todos` | ✅ 200，完整列表、`isCompleted=false` |
| POST 議題窗口追蹤 | 同上 | ✅ `targetName` 正確、`workType=issue` |
| POST 專案／項次追蹤 | nested 路徑 | ✅ `project`／`projectIssue` |
| PUT 更新標題 | `/track-todos/{id}` | ✅ 列表替換 |
| PUT 完成 | `/complete` | ✅；隨後首頁不含該 id |
| PUT reorder 部分 id | `/reorder` | ✅ HTTP 409，`排序清單必須是該工作下的全部項目` |
| PUT reorder 全部 id | `/reorder` | ✅ 順序與請求一致 |
| DELETE 一筆 | `/track-todos/{id}` | ✅ 剩餘列表無該 id |
| DELETE 不存在 | `/track-todos/999999` | ✅ HTTP 404 |
| GET 首頁 | `/track-todos` | ✅ 僅未完成；項次列有 `projectId` |
| GET 一般 TODO | `/issues/{id}/todos` | ✅ 不含追蹤標題 |
| 標題空白／過長／無類型／無對象 | POST | ✅ 400，文案與設計書一致 |
| 無廠商選窗口 | POST issue `#1`（vendor null） | ✅ `請先設定廠商` |
| 無窗口公司選窗口 | 專用測試議題（已清理） | ✅ 400（單元測試文案已對） |
| 窗口屬其他公司 | 專用測試議題（已清理） | ✅ 400（單元測試文案已對） |
| 改廠商 409 | PUT issue／project | ✅ 單元測試對文案；整合曾建立窗口追蹤後改廠商（錯誤碼由例外送出） |
| 刪占用 409 | member／contact／company | ✅ 單元測試＋整合建立占用後刪除被擋 |
| 刪議題／專案連刪 | DELETE 後 GET 追蹤 | ✅ 404 |
| 統一信封 | 200／4xx | ✅ `{ code, message, data }` |

---

## 三、Database 驗證

以 MySQL `SHOW CREATE TABLE issue_tracker.track_todo` 核對（後端啟動 `EnsureSchema` 亦已執行同一段 DDL）。

| 項目 | 結果 |
|------|------|
| 表 `track_todo` | ✅ |
| 欄位與 DA 書一致（三工作 FK、兩對象 FK、`target_type` VARCHAR(20)、title 200、content 2000） | ✅ |
| PK `track_todo_id` AUTO_INCREMENT | ✅ |
| INDEX issue／project／item＋sort_order、home `(is_completed, created_at)`、member、contact | ✅ |
| FK 工作 CASCADE；成員／窗口 RESTRICT | ✅ |
| CHECK `ck_track_todo_work`、`ck_track_todo_target` | ✅ |
| 既有 issue／project／project_issue 本輪不加欄 | ✅（與 DA 書一致） |
| 無種子追蹤列 | ✅（應用不插入預設；庫內僅使用者／測試資料） |

InMemory 單元測試不驗證 CHECK／FK，已知限制屬實；本機 MySQL 已有約束。

---

## 四、Business Logic 驗證

| 規則 | 結果 |
|------|------|
| 恰好一種工作（路徑決定，Write 不帶工作 id） | ✅ |
| `targetType` member／clientContact，對象必填 | ✅ |
| 客戶窗口須屬該工作廠商；專案議題用專案廠商 | ✅ |
| 新增固定未完成；完成只改該筆、無父項自動完成 | ✅ |
| 首頁只查未完成 | ✅ |
| 排序須該工作全部 id，不可改掛 | ✅ |
| 改廠商僅擋客戶窗口類追蹤；專案含底下項次 | ✅ |
| 刪工作連刪追蹤；刪通訊錄 COUNT 後 409 | ✅ |
| 一般 TODO API 不變 | ✅ |
| GET contacts 改為輕量 `{ id, name }`（SA §4.4；成員頁仍走 `/tree`） | ✅ |

---

## 五、Exception 驗證

| 情境 | HTTP | 結果 |
|------|------|------|
| 工作／追蹤／公司不存在 | 404 | ✅ |
| 標題空白、過長、未選類型／對象 | 400 | ✅ |
| 請先設定廠商 | 400 | ✅ |
| 請先在成員維護新增客戶窗口 | 400 | ✅ |
| 客戶窗口須屬於該工作的客戶公司 | 400 | ✅ |
| 找不到該追蹤對象 | 400 | ✅ |
| 排序 id 集合不符 | 409 | ✅ |
| 改廠商仍有窗口追蹤 | 409 | ✅ |
| 刪成員／窗口／公司仍被追蹤 | 409 | ✅ |

「類型與對象不一致」：服務層在「該類型主檔找不到、但另一類型 id 存在」時拋出。本機成員與窗口 id 區間重疊，整合未另建錯開 id；單元測試與程式路徑存在，不列為缺陷。

---

## 六、Regression Test

第一、三、四階段既有測試（分類、TODO、專案代號／項次、廠商必填、通訊錄）全數通過，無回歸失敗。

---

## 七、發現問題

**無程式缺陷可分派。**

未建立《QA問題確認文件》。

已知環境資料：議題 `#1` 有一筆「API smoke 追蹤」（RD 連線測試遺留，不屬規格不符）。可在議題詳情刪除。

---

## 八、結論

✅ **已結案。** 使用者於 2026-09-05 確認測試完成。

- 59 Unit Test 全通過
- API／例外文案與系統設計書一致
- MySQL `track_todo` 結構與資料庫設計書一致
- 回歸無失敗
- 前端手動測試清單已勾選完成

---

## 九、文件清單

| 文件 | 路徑 |
|------|------|
| QA 測試案例 | [QA測試案例.md](第五階段－需要追蹤的 TODO－QA測試案例.md) |
| 前端手動測試清單 | [前端手動測試清單.md](第五階段－需要追蹤的 TODO－前端手動測試清單.md) |
| 本報告 | 本文件 |
