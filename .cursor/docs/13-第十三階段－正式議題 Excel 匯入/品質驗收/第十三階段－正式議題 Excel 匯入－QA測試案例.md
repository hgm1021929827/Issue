# 第十三階段－正式議題 Excel 匯入－QA 測試案例

**角色：** QA  
**日期：** 2026-09-05  
**依據：** [系統設計書](../系統設計/第十三階段－正式議題 Excel 匯入－系統設計書.md) v1.0  
**需求類型：** ① Web 應用

---

## TC-01 匯入例外

| 案例 | 步驟 | 預期 |
|------|------|------|
| 01-01 無檔 | POST `/issue-imports` 不帶 file | 400，`請選擇一個檔案` |
| 01-02 空檔 | 長度 0 的 `.xls` | 400，`請選擇一個檔案` |
| 01-03 非允許副檔名 | `.xlsx` | 400，`僅支援 xls 或 html` |
| 01-04 非表格 | `.html` 無 table | 400，`檔案不是議題匯入表格` |
| 01-05 缺表單編號表頭 | 有 table 但無「表單編號」 | 400，`找不到表頭「表單編號」` |
| 01-06 未選使用者 | 不帶或 `currentUserMemberId=0` | 400，`請先選擇目前使用者` |
| 01-07 成員不存在 | `currentUserMemberId=999999` | 400，`請先選擇目前使用者` |
| 01-08 缺小分類 | 不帶三個小分類 id | 400，`請選擇處理中、加簽與已結案小分類` |
| 01-09 小分類相同 | 任兩 id 相同 | 400，`處理中、加簽與已結案不可相同` |
| 01-10 小分類不存在 | 其中一個 id 不存在 | 400，`請選擇處理中、加簽與已結案小分類` |
| 01-11 小分類不屬議題 | 小分類屬其他大分類 | 400，`小分類不屬於大分類「議題」` |

## TC-02 解析與篩選寫入

| 案例 | 步驟 | 預期 |
|------|------|------|
| 02-01 表頭實體 | 表頭為數字 HTML 實體 | 可對上預計完成日／工程師／議題標題／內容／廠商 |
| 02-02 三欄篩選 | SA 或工程師-1／2 Contains 中文或英文名 | 命中列寫入；未命中 `skipped`，不進有效列、不進 notFound 來源 |
| 02-03 編號 upsert | 既有 `issueNo`（忽略大小寫）再匯 | `updated`；標題／內容／日期／廠商更新；大分類＝「議題」；處理中且工程師命中、簽核者不是使用者時**不改**小分類並進 `pendingCategory`；備註、TODO 不變 |
| 02-04 新建 | 無該編號 | `created`；工程師命中則小分類＝處理中；`importedFromUof=true`；`missingKept=false` |
| 02-05 廠商前綴 | `客戶名稱 :` 或 `：` | 去掉前綴後對公司；對上不新建；對不上新建只名稱 |
| 02-06 待判斷 | 既有、處理中、工程師命中、簽核者不是使用者 | 進 `pendingCategory`；小分類暫不改 |
| 02-12 選加簽 | 對 pending 送 `countersign` | 小分類＝加簽 id |
| 02-13 新建工程師處理中 | 無該編號、處理中、工程師命中 | 小分類＝處理中 id；不進 pending |
| 02-14 結案直接結案 | 狀態含「結案」（新建或既有、工程師或 SA） | 小分類＝已結案 id；不進 pending |
| 02-11 無小分類 | 既有列 `subCategoryId` 空、狀態處理中 | 寫入處理中小分類 |
| 02-07 列異常 | 編號空／標題空／廠商空／同檔重複編號／日期壞 | `rowErrors.kind` 對應；其他列繼續 |
| 02-08 零有效列 | 全部未過篩 | **200**；created/updated＝0；skipped＞0；notFound＝所有 `importedFromUof` |
| 02-09 手建不列未找到 | `importedFromUof=false` | 不進 `notFound` |
| 02-10 Template 抽樣 | 靜態讀 `議題匯入範本.xls` | HTML table、含「表單編號」與實體表頭；本輪不以全檔打入本機庫 |

## TC-03 決策與鎖

| 案例 | 步驟 | 預期 |
|------|------|------|
| 03-01 空陣列有未找到 | 匯入後 `notFound` 非空，POST `/issue-imports/decisions` `{ decisions: [] }` | 400，`請為每筆未找到資料選擇保留或刪除` |
| 03-02 恰好覆蓋 keep | 對每一筆 notFound `keep` | 200，`applied: true`；`missingKept=true` |
| 03-03 缺漏／多餘 | 少一筆或多一筆 id | 400，同上文案 |
| 03-04 PUT 鎖五欄 | `importedFromUof` 改 title／issueNo／content／dueDate／clientCompanyId | 400，`此議題由匯入產生，請重新匯入以更新編號、標題、內容、預計完成日或廠商` |
| 03-05 PUT 備註 | 五欄不變，改 remark | 200；`importedFromUof` 仍 true |
| 03-06 手建 POST | POST `/issues` | `importedFromUof=false`；請求不能把該欄改回 false（DTO 無此欄） |

## TC-04 Q-12-01 專案決策

| 案例 | 步驟 | 預期 |
|------|------|------|
| 04-01 有未找到送空陣列 | 該專案最近一次 imports 有 notFound，POST `import-decisions` `[]` | 400，`請為每筆未找到資料選擇保留或刪除` |
| 04-02 快取遺失 | 新快取（模擬重啟）對存在專案送 `[]` | 200（視為無待決策） |

## TC-05 契約與回歸

| 案例 | 步驟 | 預期 |
|------|------|------|
| 05-01 信封 | 成功／失敗 | `{ code, message, data }`；成功 `code===200` |
| 05-02 IssueDto | GET 列表／詳情 | 含 `importedFromUof`、`missingKept` |
| 05-03 路徑 | 議題匯入不走 `/projects/.../imports` | `POST /issue-imports` |
| 05-04 既有議題 GET | 手建列 | 仍可讀；兩新欄為 false／0 |

## TC-06 前端（清單另檔；本表不點畫面）

見《前端手動測試清單》。對照：獨立 dialog、獨立 localStorage 鍵、結果頁 state、五欄 disabled、列表「檔中沒有」。
