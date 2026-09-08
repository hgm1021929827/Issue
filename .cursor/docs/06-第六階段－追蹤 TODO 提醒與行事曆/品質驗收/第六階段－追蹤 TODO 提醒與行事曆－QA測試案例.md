# 第六階段－追蹤 TODO 提醒與行事曆－QA 測試案例

**角色：** QA  
**日期：** 2026-09-04  
**依據：** [系統設計書](../系統設計/第六階段－追蹤 TODO 提醒與行事曆－系統設計書.md) v1.0  
**需求類型：** ① Web 應用

---

## TC-01 寫入可空提醒日

| 案例 | 步驟 | 預期 |
|------|------|------|
| 01-01 議題新增含日期 | POST `/issues/{id}/track-todos`，`reminderDate` ISO 日 | 200，該列 `reminderDate` 有值；回傳該工作完整列表 |
| 01-02 省略日期 | POST 不帶 `reminderDate` | 200，`reminderDate` 為 null |
| 01-03 專案新增含日期 | POST `/projects/{id}/track-todos` | 200，`workType=project` |
| 01-04 專案議題新增含日期 | POST `/projects/{pid}/items/{itemId}/track-todos` | 200，`workType=projectIssue` |
| 01-05 更新改日期 | PUT `/track-todos/{id}` 改日 | 200，列表該列已改 |
| 01-06 更新清空 | PUT `reminderDate: null` | 200，該列 null |

## TC-02 完成不清空日期

| 案例 | 步驟 | 預期 |
|------|------|------|
| 02-01 勾完成 | PUT `/complete` `{ isCompleted: true }` | 工作清單該列仍有原 `reminderDate` |
| 02-02 取消完成 | PUT `{ isCompleted: false }` | 日期仍在 |

## TC-03 行事曆增量

| 案例 | 步驟 | 預期 |
|------|------|------|
| 03-01 未完成有日當月 | GET `/issues/calendar?year=&month=` | 出現 `source=trackTodo`、`kind=track`、`id`＝追蹤 id、`date`／`dueDate`＝提醒日、`title`＝追蹤標題 |
| 03-02 跳轉欄位（議題） | 同上 | `workType=issue`，`workId`＝議題 id |
| 03-03 跳轉欄位（專案） | 專案追蹤 | `workType=project`，`workId`＝專案 id；`projectId` 空 |
| 03-04 跳轉欄位（專案議題） | 項次追蹤 | `workType=projectIssue`，`workId`＝項次 id，`projectId` 有值 |
| 03-05 無日期不上曆 | 未完成、reminderDate null | calendar 不含該 id |
| 03-06 已完成不上曆 | 有日期但已完成 | 不含該 id |
| 03-07 跨月不上該月 | 提醒日在下月 | 本月不含、下月含（若仍未完成） |
| 03-08 完成後再取消 | 完成→calendar 無；取消完成→同日再出現 | 日期未丟 |
| 03-09 清空日期 | PUT null 後 GET 當月 | 不含該 id |
| 03-10 既有列仍在 | 同一次 GET | 仍有 `kind=due`／`plan`；追蹤不併入 due |
| 03-11 年月無效 | month=13 | 400，`年月無效` |

## TC-04 首頁列表

| 案例 | 步驟 | 預期 |
|------|------|------|
| 04-01 有日期 | GET `/track-todos` | 未完成列含 `reminderDate` |
| 04-02 已完成不列 | 勾完成後 GET | 不含該 id |

## TC-05 例外

| 案例 | 步驟 | 預期 |
|------|------|------|
| 05-01 追蹤不存在 | PUT／DELETE 不存在 id | 既有 404 |
| 05-02 非法日期字串 | `reminderDate` 非日期 | HTTP 400（平台繫結；見測試報告已知限制） |
