# 第四階段－成員與廠商－QA 測試報告

**角色：** QA  
**日期：** 2026-09-04  
**需求類型：** ① Web 應用  
**結案：** 使用者於 2026-09-05 確認測試完成  
**依據：** [系統設計書](../系統設計/第四階段－成員與廠商－系統設計書.md) v1.0、[資料庫設計書](../資料設計/第四階段－成員與廠商－資料庫設計書.md) v1.0、[開發完成報告](../程式開發/第四階段－成員與廠商－開發完成報告.md)

---

## 一、Unit Test

**指令：** `dotnet test --nologo --verbosity normal`  
**結果：** 通過 **42**，失敗 **0**，略過 **0**

| 測試類別 | 測試數 | 結果 |
|----------|--------|------|
| DirectoryServiceTests | 8 | ✅ 全通過 |
| ProjectVendorTests | 3 | ✅ 全通過 |
| IssueServiceTests（含新增 vendor 驗證） | 10 | ✅ 全通過 |
| ProjectServiceTests（含 vendor 必填） | 5 | ✅ 全通過 |
| CategoryServiceTests | 6 | ✅ 全通過（回歸） |
| CategoryProjectUsageTests | 2 | ✅ 全通過（回歸） |
| TodoServiceTests | 4 | ✅ 全通過（回歸） |

### 關鍵測試項對照系統設計書 §11

| 設計書要求 | 測試名稱 | 結果 |
|------------|----------|------|
| 公司名忽略大小寫唯一 | Company_name_unique_ignore_case | ✅ |
| 同公司窗口名唯一、跨公司可同名 | Contact_name_unique_within_company_ok_across_companies | ✅ |
| 成員同中文名可兩筆 | Member_chinese_name_may_duplicate | ✅ |
| 快捷新增回 id | Shortcut_create_company_returns_id | ✅ |
| 專案無廠商 400 | Create_project_rejects_missing_vendor | ✅ |
| 議題無廠商 400 | Create_rejects_missing_vendor | ✅ |
| 舊專案 GET 廠商 null | Get_legacy_project_allows_null_vendor | ✅ |
| 舊議題 GET 廠商 null | Get_legacy_issue_allows_null_vendor | ✅ |
| 刪被引用公司 409 | Delete_company_in_use_is_409 | ✅ |
| 刪被引用成員 409 | Delete_member_in_use_is_409 | ✅ |
| 刪公司後窗口不在 | Delete_company_cascades_contacts | ✅ |
| 專案議題 write 不存自己廠商 | Project_issue_write_does_not_store_own_vendor | ✅ |
| 樹搜尋窗口名命中公司 | Tree_keyword_keeps_company_when_contact_matches | ✅ |

---

## 二、API Integration Test

使用 `Invoke-RestMethod` 對 `http://localhost:5080` 發實際請求。

| 案例 | 端點 | 結果 |
|------|------|------|
| GET 成員列表 | `/company-members` | ✅ 200，data 陣列正確 |
| GET 客戶公司列表 | `/client-companies` | ✅ 200，id + name |
| GET 樹（含窗口＋聯絡） | `/client-companies/tree` | ✅ 200，巢狀 contacts → channels |
| GET 專案（舊列 vendor=null） | `/projects` | ✅ 200，clientCompanyId/Name = null |
| GET 議題（舊列 vendor=null） | `/issues` | ✅ 200，clientCompanyId/Name = null |
| POST 議題無廠商 | `/issues` | ✅ **400** |
| POST 重複公司名 | `/client-companies` | ✅ **409** |

所有回應符合 `{ code, message, data }` 格式。

---

## 三、Database 驗證

透過 `AppDbContext` 映射與 `DbSeeder.EnsureSchema` 確認：

- 四張新表 CREATE IF NOT EXISTS ✅
- project／issue 加欄 ALTER ✅
- FK RESTRICT（project→company, project→member, issue→company）✅
- FK CASCADE（company→contact→channel）✅
- UNIQUE（company_name, (client_company_id, contact_name)）✅
- INDEX（member_name, client_company_id on project/issue, owner_member_id）✅

---

## 四、Business Logic 驗證

| 規則 | 結果 |
|------|------|
| 專案寫入必填 clientCompanyId | ✅ |
| 議題寫入必填 clientCompanyId | ✅ |
| 專案議題讀時帶專案廠商，不存自己的 | ✅ |
| 負責人可空、有值須存在 | ✅ |
| 舊列 GET vendor = null、不擋 | ✅ |
| 刪使用中公司 → 409 含專案/議題筆數 | ✅ |
| 刪使用中成員 → 409 含專案筆數 | ✅ |
| 刪公司 CASCADE 窗口與聯絡 | ✅ |
| 成員排序 name → id | ✅ |
| 公司名/窗口名忽略大小寫唯一 | ✅ |

---

## 五、Exception 驗證

| 情境 | HTTP | 結果 |
|------|------|------|
| 名稱空白 | 400 | ✅ |
| 英文名空白 | 400 | ✅ |
| 聯絡類型無效 | 400 | ✅ |
| 聯絡內容空白 | 400 | ✅ |
| 公司名重複 | 409 | ✅ |
| 窗口名重複 | 409 | ✅ |
| 不存在的 id | 404 | ✅ |
| 刪使用中 | 409 | ✅ |
| 專案/議題缺廠商 | 400 | ✅ |
| 負責人 id 不存在 | 400 | ✅ |

---

## 六、Regression Test

第一、三階段既有測試（分類、TODO、專案代號唯一、項次唯一、行事曆）全數通過，無回歸問題。

---

## 七、發現問題

**無。** 所有測試案例通過，邏輯與設計書一致。

---

## 八、結論

✅ **已結案。** 使用者於 2026-09-05 確認測試完成。

- 42 Unit Test 全通過
- 7 筆 API Integration 驗證通過
- Business Logic 與系統設計書一致
- 例外流程處理正確
- 回歸測試無影響
- 前端手動測試清單已勾選完成

---

## 九、文件清單

| 文件 | 路徑 |
|------|------|
| QA 測試案例 | [QA測試案例.md](第四階段－成員與廠商－QA測試案例.md) |
| 前端手動測試清單 | [前端手動測試清單.md](第四階段－成員與廠商－前端手動測試清單.md) |
| 本報告 | 本文件 |
