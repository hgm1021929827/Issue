# 第六階段－追蹤 TODO 提醒與行事曆－資料庫設計書

**角色：** DA  
**版本：** v1.0  
**日期：** 2026-09-04  
**狀態：** 定稿，交 RD  
**依據：** [系統設計書](../系統設計/第六階段－追蹤 TODO 提醒與行事曆－系統設計書.md) 第七章  
**關係：** 增量。不覆寫第五階段資料庫設計書本文。本輪**不加表**，只加欄。

---

## 一、理念

提醒日是追蹤 TODO 的可空屬性，不是排程子表（PM 拒絕多日）。用 `DATE` 只到日。

## 二、Table 清單

本輪不加表。變更：`track_todo` 加欄。

## 三、欄位設計（增量）

### track_todo（既有表加欄）

| 欄位名稱 | 中文名稱 | 資料型態 | 驗證限制 | 是否允許 NULL | 預設值 | 說明 |
| -------- | -------- | -------- | -------- | ------------- | ------ | ---- |
| reminder_date | 提醒日 | DATE | 可空 | 是 | NULL | 對應 `reminderDate`；無則不上月曆 |

C#：`TrackTodo.ReminderDate` 型態 `DateOnly?`。JSON `reminderDate`。

其餘欄位仍依第五階段設計書。

## 四、關聯

無新 FK。提醒日不指向其他表。

## 五、Index

- INDEX (`is_completed`, `reminder_date`) on `track_todo`（月曆：未完成且該月有日）

名稱建議 `ix_track_todo_reminder`。舊庫 EnsureSchema 若索引不存在再 ADD。

## 六、Constraint

- 應用層：有值須為有效日。不在 DB 強制「完成則日期必空」（完成後列仍可留日期，只是查詢不採用）。
- 不加 CHECK 與到期日比較。

## 七、生命週期

- 新增可空；更新可改可清。
- 刪追蹤或 CASCADE 刪工作則欄隨列消失。
- 勾完成不 UPDATE 掉日期，以便取消完成後月曆再出現。

## 八、命名

`reminder_date`，避免與議題 `due_date`、預計項目 `plan_date` 混淆。

## 九、決策

- 單欄 DATE 而非 `track_todo_reminder` 子表：對齊 Q4＝A。
- 完成不清空日期：取消完成仍記得哪天。
