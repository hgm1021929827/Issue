# 第四階段－成員與廠商－QA 測試案例

**角色：** QA  
**日期：** 2026-09-04  
**依據：** [系統設計書](../系統設計/第四階段－成員與廠商－系統設計書.md) v1.0  
**需求類型：** ① Web 應用

---

## TC-01 公司成員 CRUD

| 案例 | 步驟 | 預期 |
|------|------|------|
| 01-01 列表空白 | GET `/company-members` 無資料 | 200，`data: []` |
| 01-02 新增成員 | POST `{ name, englishName }` | 200，回傳含 id、createdAt |
| 01-03 中文名可重複 | 連續兩次 POST 同 `name` 不同 `englishName` | 兩筆皆成功 |
| 01-04 缺中文名 | POST `{ name: "", englishName: "X" }` | 400 |
| 01-05 缺英文名 | POST `{ name: "X", englishName: "" }` | 400 |
| 01-06 更新成員 | PUT `/company-members/{id}` | 200，`updatedAt` 變化 |
| 01-07 刪除未使用成員 | DELETE | 200 `{ deleted: true }` |
| 01-08 刪除使用中成員 | DELETE（owner_member_id 被專案引用） | 409，message 含「專案」筆數 |
| 01-09 關鍵字搜尋 name | GET `?q=中文片段` | 過濾正確 |
| 01-10 關鍵字搜尋 englishName | GET `?q=eng` | 過濾正確 |
| 01-11 排序 | 結果按 name → id | 正確 |

## TC-02 客戶公司 CRUD

| 案例 | 步驟 | 預期 |
|------|------|------|
| 02-01 新增公司 | POST `{ name }` | 200，回傳 id |
| 02-02 名稱唯一忽略大小寫 | POST 相同名稱不同大小寫 | 409 |
| 02-03 名稱空白 | POST `{ name: "" }` | 400 |
| 02-04 更新公司名 | PUT | 200 |
| 02-05 更新後名稱衝突 | PUT 改為已存在名稱 | 409 |
| 02-06 刪除未使用公司 | DELETE | 200 |
| 02-07 刪使用中公司 | DELETE（有專案引用） | 409，含「專案」筆數 |
| 02-08 刪使用中公司 | DELETE（有議題引用） | 409，含「正式議題」筆數 |
| 02-09 列表（下拉用） | GET `/client-companies` | id + name 輕量回傳 |

## TC-03 客戶窗口 CRUD

| 案例 | 步驟 | 預期 |
|------|------|------|
| 03-01 新增窗口 | POST `/client-companies/{id}/contacts` | 回傳該公司完整窗口列表 |
| 03-02 窗口名同公司唯一 | 同公司新增重複名 | 409 |
| 03-03 跨公司可同名 | 不同公司新增相同窗口名 | 200 |
| 03-04 更新窗口 | PUT | 回傳完整列表 |
| 03-05 刪窗口 | DELETE | 回傳剩餘列表 |
| 03-06 不存在 companyId | POST 路徑 companyId 錯誤 | 404 |

## TC-04 聯絡資料 CRUD

| 案例 | 步驟 | 預期 |
|------|------|------|
| 04-01 新增聯絡 | POST channels | 回傳窗口完整 channels |
| 04-02 type 列舉 | type=`phone/mobile/email/teams/other` | 200 |
| 04-03 type 無效 | type=`fax` | 400 |
| 04-04 value 空白 | value=`""` | 400 |
| 04-05 更新聯絡 | PUT | 200 |
| 04-06 刪聯絡 | DELETE | 回傳剩餘 channels |

## TC-05 樹 API

| 案例 | 步驟 | 預期 |
|------|------|------|
| 05-01 完整樹 | GET `/client-companies/tree` | 含 contacts + channels |
| 05-02 關鍵字過濾公司名 | `?q=公司片段` | 命中公司 |
| 05-03 關鍵字過濾窗口名 | `?q=窗口名` | 保留該公司（含全部窗口） |

## TC-06 專案增量

| 案例 | 步驟 | 預期 |
|------|------|------|
| 06-01 新增專案無廠商 | POST 不帶 clientCompanyId | 400 |
| 06-02 新增專案有廠商 | POST 帶 clientCompanyId | 200 |
| 06-03 負責人不存在 | ownerMemberId = 999 | 400 |
| 06-04 負責人可空 | ownerMemberId = null | 200 |
| 06-05 舊專案 GET | GET（client_company_id=NULL） | 200，vendor 欄為 null |
| 06-06 列表含 vendor | GET `/projects` | 回傳含 clientCompanyName、ownerMemberName |

## TC-07 正式議題增量

| 案例 | 步驟 | 預期 |
|------|------|------|
| 07-01 新增議題無廠商 | POST 不帶 clientCompanyId | 400 |
| 07-02 新增議題有廠商 | POST 帶 clientCompanyId | 200 |
| 07-03 舊議題 GET | GET（client_company_id=NULL） | 200，vendor 為 null |
| 07-04 DTO 含 vendor | GET `/issues` | 含 clientCompanyId, clientCompanyName |

## TC-08 專案議題增量

| 案例 | 步驟 | 預期 |
|------|------|------|
| 08-01 專案議題帶出廠商 | GET 項目列表 | clientCompanyId/Name 來自專案 |
| 08-02 Write DTO 無廠商欄 | ProjectIssueWriteDto 無 ClientCompanyId 屬性 | 確認 |

## TC-09 CASCADE 與 RESTRICT

| 案例 | 步驟 | 預期 |
|------|------|------|
| 09-01 刪公司 cascade 窗口 | 刪除無引用公司 | 窗口與聯絡同步消失 |
| 09-02 刪公司 RESTRICT | 刪公司（有專案引用） | 409 |

## TC-10 統一回應格式

| 案例 | 步驟 | 預期 |
|------|------|------|
| 10-01 成功回應 | 任意 200 | `{ code: 200, message, data }` |
| 10-02 錯誤回應 | 任意 4xx | `{ code: 4xx, message }` |
