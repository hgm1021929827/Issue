# 第五階段－需要追蹤的 TODO－QA 測試案例

**角色：** QA  
**日期：** 2026-09-04  
**依據：** [系統設計書](../系統設計/第五階段－需要追蹤的 TODO－系統設計書.md) v1.0  
**需求類型：** ① Web 應用

---

## TC-01 依附工作的追蹤清單

| 案例 | 步驟 | 預期 |
|------|------|------|
| 01-01 議題列表空 | GET `/issues/{id}/track-todos`（無追蹤） | 200，`data: []` |
| 01-02 議題新增成員對象 | POST `{ title, targetType: member, targetId }` | 200，回傳該議題完整列表；`isCompleted=false`；`workType=issue` |
| 01-03 議題新增窗口對象 | 工作已設廠商；POST `clientContact` | 200，`targetName`＝窗口名，`targetSecondaryName`＝公司名 |
| 01-04 專案新增 | POST `/projects/{id}/track-todos` | 200，`workType=project` |
| 01-05 專案議題新增 | POST `/projects/{pid}/items/{itemId}/track-todos` | 200，`workType=projectIssue` |
| 01-06 無此議題 | GET `/issues/999999/track-todos` | 404 |
| 01-07 項次與專案不符 | GET `/projects/{pid}/items/999999/track-todos` | 404 |
| 01-08 寫入回完整列表 | 連續新增兩筆 | 第二次 `data` 含兩筆，順序 `sortOrder` 再 `id` |
| 01-09 內容可空 | POST `content: ""` | 200，內容為空字串 |

## TC-02 驗證 400

| 案例 | 步驟 | 預期 |
|------|------|------|
| 02-01 標題空白 | title=`"  "` | 400，`標題不可空白` |
| 02-02 標題過長 | title 201 字 | 400，`標題過長` |
| 02-03 未選類型 | targetType=`""` | 400，`請選擇追蹤對象類型` |
| 02-04 未選對象 | 不傳 targetId | 400，`請選擇追蹤對象` |
| 02-05 無廠商選窗口 | 議題 `clientCompanyId` null，POST `clientContact` | 400，`請先設定廠商` |
| 02-06 有廠商無窗口 | 公司無窗口，POST `clientContact` | 400，`請先在成員維護新增客戶窗口` |
| 02-07 窗口非該廠商 | 窗口屬其他公司 | 400，`客戶窗口須屬於該工作的客戶公司` |
| 02-08 對象不存在 | targetId 不存在 | 400，`找不到該追蹤對象` |

## TC-03 單筆更新／完成／排序／刪除

| 案例 | 步驟 | 預期 |
|------|------|------|
| 03-01 更新欄位 | PUT `/track-todos/{id}` | 200，該工作完整列表，標題已改 |
| 03-02 標示完成 | PUT `/complete` `{ isCompleted: true }` | 200；該列仍在工作清單 |
| 03-03 排序須全部 id | `orderedIds` 缺漏 | 409，`排序清單必須是該工作下的全部項目` |
| 03-04 排序成功 | 帶該工作全部 id 的新順序 | 200，列表順序與請求一致 |
| 03-05 刪除一筆 | DELETE `/track-todos/{id}` | 200，回傳剩餘列表、該 id 不在 |
| 03-06 刪不存在 | DELETE `/track-todos/999999` | 404，`找不到該筆需要追蹤的 TODO` |

## TC-04 首頁未完成

| 案例 | 步驟 | 預期 |
|------|------|------|
| 04-01 只含未完成 | 一筆完成、一筆未完成 | GET `/track-todos` 只有未完成 |
| 04-02 議題 workLabel | 有編號 | `編號 標題` |
| 04-03 專案議題欄位 | 項次追蹤 | `workType=projectIssue`，`projectId` 有值 |
| 04-04 無 query | GET `/track-todos` 不帶子參數 | 固定未完成 |
| 04-05 統一信封 | 成功 | `{ code: 200, message, data }` |

## TC-05 窗口輕量列表

| 案例 | 步驟 | 預期 |
|------|------|------|
| 05-01 有窗口 | GET `/client-companies/{id}/contacts` | `{ id, name }[]`，無 channels |
| 05-02 無窗口 | 公司存在、窗口 0 | 200，`[]` |
| 05-03 無此公司 | companyId 不存在 | 404 |

## TC-06 改廠商 409

| 案例 | 步驟 | 預期 |
|------|------|------|
| 06-01 議題有窗口追蹤 | PUT 議題改 `clientCompanyId` | 409，message 含「無法更改廠商」「客戶窗口」 |
| 06-02 僅成員追蹤 | 議題只掛 member 追蹤後改廠商 | 200（單元測試） |
| 06-03 專案議題窗口追蹤 | PUT 專案改廠商 | 409，message 含「專案議題」 |

## TC-07 刪除連刪與占用

| 案例 | 步驟 | 預期 |
|------|------|------|
| 07-01 刪議題連刪追蹤 | DELETE 議題後 GET 其追蹤 | 404；庫內無殘列（單元測試） |
| 07-02 刪專案連刪 | DELETE 專案（含項次追蹤） | 專案 404 |
| 07-03 刪窗口被引用 | DELETE contact | 409，含「需要追蹤的 TODO」 |
| 07-04 刪成員被引用 | DELETE member | 409，含「需要追蹤的 TODO」 |
| 07-05 刪公司（窗口被追蹤） | DELETE company | 409 |

## TC-08 回歸

| 案例 | 步驟 | 預期 |
|------|------|------|
| 08-01 一般 TODO 分離 | GET `/issues/{id}/todos` | 不含追蹤標題 |
| 08-02 分類／專案／目錄既有測試 | `dotnet test` | 全通過 |
