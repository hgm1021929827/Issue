using Microsoft.EntityFrameworkCore;

namespace Issue.Api.Data;

public static partial class DbSeeder
{
    static void EnsureSchemaMySql(AppDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
                        SET @exists := (
                          SELECT COUNT(*) FROM information_schema.COLUMNS
                          WHERE TABLE_SCHEMA = DATABASE()
                            AND TABLE_NAME = 'sub_category'
                            AND COLUMN_NAME = 'color_hex'
                        );
                        SET @sql := IF(@exists = 0,
                          'ALTER TABLE `sub_category` ADD COLUMN `color_hex` VARCHAR(7) NOT NULL DEFAULT ''#91BFD5''',
                          'SELECT 1');
                        PREPARE stmt FROM @sql;
                        EXECUTE stmt;
                        DEALLOCATE PREPARE stmt;
                        """);
                    db.Database.ExecuteSqlRaw("""
                        SET @exists := (
                          SELECT COUNT(*) FROM information_schema.COLUMNS
                          WHERE TABLE_SCHEMA = DATABASE()
                            AND TABLE_NAME = 'issue'
                            AND COLUMN_NAME = 'issue_no'
                        );
                        SET @sql := IF(@exists = 0,
                          'ALTER TABLE `issue` ADD COLUMN `issue_no` VARCHAR(50) NULL',
                          'SELECT 1');
                        PREPARE stmt FROM @sql;
                        EXECUTE stmt;
                        DEALLOCATE PREPARE stmt;
                        """);
                    db.Database.ExecuteSqlRaw("""
                        UPDATE `issue`
                        SET `issue_no` = CAST(`issue_id` AS CHAR)
                        WHERE `issue_no` IS NULL OR `issue_no` = '';
                        """);
                    db.Database.ExecuteSqlRaw("""
                        SET @idx := (
                          SELECT COUNT(*) FROM information_schema.STATISTICS
                          WHERE TABLE_SCHEMA = DATABASE()
                            AND TABLE_NAME = 'issue'
                            AND INDEX_NAME = 'uk_issue_no'
                        );
                        SET @sql := IF(@idx = 0,
                          'ALTER TABLE `issue` MODIFY COLUMN `issue_no` VARCHAR(50) NOT NULL, ADD UNIQUE KEY `uk_issue_no` (`issue_no`)',
                          'SELECT 1');
                        PREPARE stmt FROM @sql;
                        EXECUTE stmt;
                        DEALLOCATE PREPARE stmt;
                        """);
                    db.Database.ExecuteSqlRaw("""
                        ALTER TABLE `major_category`
                        MODIFY COLUMN `category_name` VARCHAR(50) NOT NULL;
                        """);
                    db.Database.ExecuteSqlRaw("""
                        CREATE TABLE IF NOT EXISTS `issue_plan` (
                          `plan_id` BIGINT NOT NULL AUTO_INCREMENT,
                          `issue_id` BIGINT NOT NULL,
                          `plan_date` DATE NOT NULL,
                          PRIMARY KEY (`plan_id`),
                          UNIQUE KEY `uk_issue_plan_issue_date` (`issue_id`, `plan_date`),
                          CONSTRAINT `fk_issue_plan_issue` FOREIGN KEY (`issue_id`) REFERENCES `issue` (`issue_id`) ON DELETE CASCADE
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                        """);
                    db.Database.ExecuteSqlRaw("""
                        CREATE TABLE IF NOT EXISTS `project` (
                          `project_id` BIGINT NOT NULL AUTO_INCREMENT,
                          `project_code` VARCHAR(50) NOT NULL,
                          `project_name` VARCHAR(200) NOT NULL,
                          `description` VARCHAR(4000) NOT NULL DEFAULT '',
                          `major_category_id` BIGINT NOT NULL,
                          `sub_category_id` BIGINT NULL,
                          `start_date` DATE NULL,
                          `due_date` DATE NULL,
                          `created_at` DATETIME NOT NULL,
                          `updated_at` DATETIME NOT NULL,
                          PRIMARY KEY (`project_id`),
                          UNIQUE KEY `uk_project_code` (`project_code`),
                          KEY `ix_project_major` (`major_category_id`),
                          KEY `ix_project_due` (`due_date`),
                          CONSTRAINT `fk_project_major` FOREIGN KEY (`major_category_id`) REFERENCES `major_category` (`major_category_id`) ON DELETE RESTRICT,
                          CONSTRAINT `fk_project_sub` FOREIGN KEY (`sub_category_id`) REFERENCES `sub_category` (`sub_category_id`) ON DELETE RESTRICT
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                        """);
                    db.Database.ExecuteSqlRaw("""
                        CREATE TABLE IF NOT EXISTS `project_issue` (
                          `project_issue_id` BIGINT NOT NULL AUTO_INCREMENT,
                          `project_id` BIGINT NOT NULL,
                          `seq_no` VARCHAR(50) NOT NULL,
                          `title` VARCHAR(200) NOT NULL,
                          `content` VARCHAR(4000) NOT NULL DEFAULT '',
                          `major_category_id` BIGINT NOT NULL,
                          `sub_category_id` BIGINT NULL,
                          `start_date` DATE NULL,
                          `due_date` DATE NULL,
                          `created_at` DATETIME NOT NULL,
                          `updated_at` DATETIME NOT NULL,
                          PRIMARY KEY (`project_issue_id`),
                          UNIQUE KEY `uk_project_issue_seq` (`project_id`, `seq_no`),
                          KEY `ix_project_issue_major` (`major_category_id`),
                          KEY `ix_project_issue_due` (`due_date`),
                          CONSTRAINT `fk_project_issue_project` FOREIGN KEY (`project_id`) REFERENCES `project` (`project_id`) ON DELETE CASCADE,
                          CONSTRAINT `fk_project_issue_major` FOREIGN KEY (`major_category_id`) REFERENCES `major_category` (`major_category_id`) ON DELETE RESTRICT,
                          CONSTRAINT `fk_project_issue_sub` FOREIGN KEY (`sub_category_id`) REFERENCES `sub_category` (`sub_category_id`) ON DELETE RESTRICT
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                        """);
                    db.Database.ExecuteSqlRaw("""
                        CREATE TABLE IF NOT EXISTS `company_member` (
                          `company_member_id` BIGINT NOT NULL AUTO_INCREMENT,
                          `member_name` VARCHAR(50) NOT NULL,
                          `english_name` VARCHAR(50) NOT NULL,
                          `remark` VARCHAR(400) NOT NULL DEFAULT '',
                          `created_at` DATETIME NOT NULL,
                          `updated_at` DATETIME NOT NULL,
                          PRIMARY KEY (`company_member_id`),
                          KEY `ix_company_member_name` (`member_name`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                        """);
                    db.Database.ExecuteSqlRaw("""
                        CREATE TABLE IF NOT EXISTS `client_company` (
                          `client_company_id` BIGINT NOT NULL AUTO_INCREMENT,
                          `company_name` VARCHAR(100) NOT NULL,
                          `created_at` DATETIME NOT NULL,
                          `updated_at` DATETIME NOT NULL,
                          PRIMARY KEY (`client_company_id`),
                          UNIQUE KEY `uk_client_company_name` (`company_name`)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                        """);
                    db.Database.ExecuteSqlRaw("""
                        CREATE TABLE IF NOT EXISTS `client_contact` (
                          `client_contact_id` BIGINT NOT NULL AUTO_INCREMENT,
                          `client_company_id` BIGINT NOT NULL,
                          `contact_name` VARCHAR(50) NOT NULL,
                          `job_title` VARCHAR(50) NOT NULL DEFAULT '',
                          `created_at` DATETIME NOT NULL,
                          `updated_at` DATETIME NOT NULL,
                          PRIMARY KEY (`client_contact_id`),
                          UNIQUE KEY `uk_client_contact_name` (`client_company_id`, `contact_name`),
                          CONSTRAINT `fk_client_contact_company` FOREIGN KEY (`client_company_id`) REFERENCES `client_company` (`client_company_id`) ON DELETE CASCADE
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                        """);
                    db.Database.ExecuteSqlRaw("""
                        CREATE TABLE IF NOT EXISTS `contact_channel` (
                          `contact_channel_id` BIGINT NOT NULL AUTO_INCREMENT,
                          `client_contact_id` BIGINT NOT NULL,
                          `channel_type` VARCHAR(20) NOT NULL,
                          `channel_value` VARCHAR(200) NOT NULL,
                          PRIMARY KEY (`contact_channel_id`),
                          CONSTRAINT `fk_contact_channel_contact` FOREIGN KEY (`client_contact_id`) REFERENCES `client_contact` (`client_contact_id`) ON DELETE CASCADE
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                        """);
                    MySqlAddColumnIfMissing(db, "project", "client_company_id",
                        "ALTER TABLE `project` ADD COLUMN `client_company_id` BIGINT NULL, ADD KEY `ix_project_company` (`client_company_id`)");
                    MySqlAddColumnIfMissing(db, "project", "owner_member_id",
                        "ALTER TABLE `project` ADD COLUMN `owner_member_id` BIGINT NULL, ADD KEY `ix_project_owner` (`owner_member_id`)");
                    MySqlAddColumnIfMissing(db, "issue", "imported_from_uof",
                        "ALTER TABLE `issue` ADD COLUMN `imported_from_uof` TINYINT(1) NOT NULL DEFAULT 0");
                    MySqlAddColumnIfMissing(db, "issue", "missing_kept",
                        "ALTER TABLE `issue` ADD COLUMN `missing_kept` TINYINT(1) NOT NULL DEFAULT 0");
                    MySqlAddColumnIfMissing(db, "issue", "client_company_id",
                        "ALTER TABLE `issue` ADD COLUMN `client_company_id` BIGINT NULL, ADD KEY `ix_issue_company` (`client_company_id`)");
                    MySqlAddFkIfMissing(db, "project", "fk_project_company",
                        "ALTER TABLE `project` ADD CONSTRAINT `fk_project_company` FOREIGN KEY (`client_company_id`) REFERENCES `client_company` (`client_company_id`) ON DELETE RESTRICT");
                    MySqlAddFkIfMissing(db, "project", "fk_project_owner",
                        "ALTER TABLE `project` ADD CONSTRAINT `fk_project_owner` FOREIGN KEY (`owner_member_id`) REFERENCES `company_member` (`company_member_id`) ON DELETE RESTRICT");
                    MySqlAddFkIfMissing(db, "issue", "fk_issue_company",
                        "ALTER TABLE `issue` ADD CONSTRAINT `fk_issue_company` FOREIGN KEY (`client_company_id`) REFERENCES `client_company` (`client_company_id`) ON DELETE RESTRICT");
                    db.Database.ExecuteSqlRaw("""
                        CREATE TABLE IF NOT EXISTS `track_todo` (
                          `track_todo_id` BIGINT NOT NULL AUTO_INCREMENT,
                          `issue_id` BIGINT NULL,
                          `project_id` BIGINT NULL,
                          `project_issue_id` BIGINT NULL,
                          `is_completed` TINYINT(1) NOT NULL DEFAULT 0,
                          `title` VARCHAR(200) NOT NULL,
                          `content` VARCHAR(2000) NOT NULL DEFAULT '',
                          `target_type` VARCHAR(20) NOT NULL,
                          `company_member_id` BIGINT NULL,
                          `client_contact_id` BIGINT NULL,
                          `reminder_date` DATE NULL,
                          `sort_order` INT NOT NULL,
                          `created_at` DATETIME NOT NULL,
                          `updated_at` DATETIME NOT NULL,
                          PRIMARY KEY (`track_todo_id`),
                          KEY `ix_track_todo_issue` (`issue_id`, `sort_order`),
                          KEY `ix_track_todo_project` (`project_id`, `sort_order`),
                          KEY `ix_track_todo_item` (`project_issue_id`, `sort_order`),
                          KEY `ix_track_todo_home` (`is_completed`, `created_at`),
                          KEY `ix_track_todo_member` (`company_member_id`),
                          KEY `ix_track_todo_contact` (`client_contact_id`),
                          KEY `ix_track_todo_reminder` (`is_completed`, `reminder_date`),
                          CONSTRAINT `fk_track_todo_issue` FOREIGN KEY (`issue_id`) REFERENCES `issue` (`issue_id`) ON DELETE CASCADE,
                          CONSTRAINT `fk_track_todo_project` FOREIGN KEY (`project_id`) REFERENCES `project` (`project_id`) ON DELETE CASCADE,
                          CONSTRAINT `fk_track_todo_item` FOREIGN KEY (`project_issue_id`) REFERENCES `project_issue` (`project_issue_id`) ON DELETE CASCADE,
                          CONSTRAINT `fk_track_todo_member` FOREIGN KEY (`company_member_id`) REFERENCES `company_member` (`company_member_id`) ON DELETE RESTRICT,
                          CONSTRAINT `fk_track_todo_contact` FOREIGN KEY (`client_contact_id`) REFERENCES `client_contact` (`client_contact_id`) ON DELETE RESTRICT,
                          CONSTRAINT `ck_track_todo_work` CHECK (
                            (issue_id IS NOT NULL AND project_id IS NULL AND project_issue_id IS NULL)
                            OR (issue_id IS NULL AND project_id IS NOT NULL AND project_issue_id IS NULL)
                            OR (issue_id IS NULL AND project_id IS NULL AND project_issue_id IS NOT NULL)
                          ),
                          CONSTRAINT `ck_track_todo_target` CHECK (
                            (target_type = 'member' AND company_member_id IS NOT NULL AND client_contact_id IS NULL)
                            OR (target_type = 'clientContact' AND client_contact_id IS NOT NULL AND company_member_id IS NULL)
                          )
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                        """);
                    MySqlAddColumnIfMissing(db, "track_todo", "reminder_date",
                        "ALTER TABLE `track_todo` ADD COLUMN `reminder_date` DATE NULL");
                    MySqlAddIndexIfMissing(db, "track_todo", "ix_track_todo_reminder",
                        "ALTER TABLE `track_todo` ADD KEY `ix_track_todo_reminder` (`is_completed`, `reminder_date`)");
                    MySqlAddColumnIfMissing(db, "project_issue", "handler_name",
                        "ALTER TABLE `project_issue` ADD COLUMN `handler_name` VARCHAR(50) NOT NULL DEFAULT ''");
                    MySqlAddColumnIfMissing(db, "project_issue", "is_completed",
                        "ALTER TABLE `project_issue` ADD COLUMN `is_completed` TINYINT(1) NOT NULL DEFAULT 0");
                    MySqlAddColumnIfMissing(db, "project_issue", "actual_start_date",
                        "ALTER TABLE `project_issue` ADD COLUMN `actual_start_date` DATE NULL");
                    MySqlAddColumnIfMissing(db, "project_issue", "actual_end_date",
                        "ALTER TABLE `project_issue` ADD COLUMN `actual_end_date` DATE NULL");
                    MySqlAddColumnIfMissing(db, "project_issue", "missing_kept",
                        "ALTER TABLE `project_issue` ADD COLUMN `missing_kept` TINYINT(1) NOT NULL DEFAULT 0");
                    MySqlAddColumnIfMissing(db, "project_issue", "remark",
                        "ALTER TABLE `project_issue` ADD COLUMN `remark` VARCHAR(2000) NOT NULL DEFAULT ''");
                    MySqlAddColumnIfMissing(db, "project_work_item", "remark",
                        "ALTER TABLE `project_work_item` ADD COLUMN `remark` VARCHAR(2000) NOT NULL DEFAULT ''");
                    db.Database.ExecuteSqlRaw("""
                        CREATE TABLE IF NOT EXISTS `project_work_item` (
                          `project_work_item_id` BIGINT NOT NULL AUTO_INCREMENT,
                          `project_id` BIGINT NOT NULL,
                          `work_item_code` VARCHAR(50) NOT NULL,
                          `title` VARCHAR(4000) NOT NULL DEFAULT '',
                          `planned_days` DECIMAL(8,2) NULL,
                          `owner_name` VARCHAR(200) NOT NULL DEFAULT '',
                          `start_date` DATE NULL,
                          `due_date` DATE NULL,
                          `is_completed` TINYINT(1) NOT NULL DEFAULT 0,
                          `actual_start_date` DATE NULL,
                          `actual_end_date` DATE NULL,
                          `missing_kept` TINYINT(1) NOT NULL DEFAULT 0,
                          `remark` VARCHAR(2000) NOT NULL DEFAULT '',
                          `created_at` DATETIME NOT NULL,
                          `updated_at` DATETIME NOT NULL,
                          PRIMARY KEY (`project_work_item_id`),
                          UNIQUE KEY `uk_project_work_item_code` (`project_id`, `work_item_code`),
                          KEY `ix_project_work_item_due` (`project_id`, `due_date`),
                          CONSTRAINT `fk_project_work_item_project` FOREIGN KEY (`project_id`) REFERENCES `project` (`project_id`) ON DELETE CASCADE
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                        """);
                    db.Database.ExecuteSqlRaw("""
                        CREATE TABLE IF NOT EXISTS `work_hour` (
                          `work_hour_id` BIGINT NOT NULL AUTO_INCREMENT,
                          `project_work_item_id` BIGINT NULL,
                          `project_issue_id` BIGINT NULL,
                          `work_date` DATE NOT NULL,
                          `hour_value` DECIMAL(6,2) NOT NULL,
                          `remark` VARCHAR(2000) NOT NULL DEFAULT '',
                          PRIMARY KEY (`work_hour_id`),
                          UNIQUE KEY `uk_work_hour_item` (`project_work_item_id`, `work_date`),
                          UNIQUE KEY `uk_work_hour_issue` (`project_issue_id`, `work_date`),
                          CONSTRAINT `fk_work_hour_item` FOREIGN KEY (`project_work_item_id`) REFERENCES `project_work_item` (`project_work_item_id`) ON DELETE CASCADE,
                          CONSTRAINT `fk_work_hour_issue` FOREIGN KEY (`project_issue_id`) REFERENCES `project_issue` (`project_issue_id`) ON DELETE CASCADE,
                          CONSTRAINT `ck_work_hour_target` CHECK (
                            (project_work_item_id IS NOT NULL AND project_issue_id IS NULL)
                            OR (project_work_item_id IS NULL AND project_issue_id IS NOT NULL)
                          ),
                          CONSTRAINT `ck_work_hour_value` CHECK (`hour_value` > 0 AND `hour_value` <= 999.99)
                        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
                        """);
                    MySqlAddColumnIfMissing(db, "track_todo", "project_work_item_id",
                        "ALTER TABLE `track_todo` ADD COLUMN `project_work_item_id` BIGINT NULL");
                    MySqlAddIndexIfMissing(db, "track_todo", "ix_track_todo_work_item",
                        "ALTER TABLE `track_todo` ADD KEY `ix_track_todo_work_item` (`project_work_item_id`, `sort_order`)");
                    MySqlAddFkIfMissing(db, "track_todo", "fk_track_todo_work_item",
                        "ALTER TABLE `track_todo` ADD CONSTRAINT `fk_track_todo_work_item` FOREIGN KEY (`project_work_item_id`) REFERENCES `project_work_item` (`project_work_item_id`) ON DELETE CASCADE");
                    MySqlDropCheckIfExists(db, "track_todo", "ck_track_todo_work");
                    MySqlAddCheckIfMissing(db, "track_todo", "ck_track_todo_work", """
                        ALTER TABLE `track_todo` ADD CONSTRAINT `ck_track_todo_work` CHECK (
                          (issue_id IS NOT NULL AND project_id IS NULL AND project_issue_id IS NULL AND project_work_item_id IS NULL)
                          OR (issue_id IS NULL AND project_id IS NOT NULL AND project_issue_id IS NULL AND project_work_item_id IS NULL)
                          OR (issue_id IS NULL AND project_id IS NULL AND project_issue_id IS NOT NULL AND project_work_item_id IS NULL)
                          OR (issue_id IS NULL AND project_id IS NULL AND project_issue_id IS NULL AND project_work_item_id IS NOT NULL)
                        )
                        """);
    }

    private static void MySqlAddColumnIfMissing(AppDbContext db, string table, string column, string alterSql)
    {
        db.Database.ExecuteSqlRaw($"""
            SET @exists := (
              SELECT COUNT(*) FROM information_schema.COLUMNS
              WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = '{table}'
                AND COLUMN_NAME = '{column}'
            );
            SET @sql := IF(@exists = 0, '{alterSql.Replace("'", "''")}', 'SELECT 1');
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);
    }

    private static void MySqlAddFkIfMissing(AppDbContext db, string table, string constraint, string alterSql)
    {
        db.Database.ExecuteSqlRaw($"""
            SET @exists := (
              SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
              WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = '{table}'
                AND CONSTRAINT_NAME = '{constraint}'
            );
            SET @sql := IF(@exists = 0, '{alterSql.Replace("'", "''")}', 'SELECT 1');
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);
    }

    private static void MySqlAddIndexIfMissing(AppDbContext db, string table, string index, string alterSql)
    {
        db.Database.ExecuteSqlRaw($"""
            SET @exists := (
              SELECT COUNT(*) FROM information_schema.STATISTICS
              WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = '{table}'
                AND INDEX_NAME = '{index}'
            );
            SET @sql := IF(@exists = 0, '{alterSql.Replace("'", "''")}', 'SELECT 1');
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);
    }

    private static void MySqlDropCheckIfExists(AppDbContext db, string table, string constraint)
    {
        db.Database.ExecuteSqlRaw($"""
            SET @exists := (
              SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
              WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = '{table}'
                AND CONSTRAINT_NAME = '{constraint}'
                AND CONSTRAINT_TYPE = 'CHECK'
            );
            SET @sql := IF(@exists > 0, 'ALTER TABLE `{table}` DROP CHECK `{constraint}`', 'SELECT 1');
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);
    }

    private static void MySqlAddCheckIfMissing(AppDbContext db, string table, string constraint, string alterSql)
    {
        db.Database.ExecuteSqlRaw($"""
            SET @exists := (
              SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
              WHERE TABLE_SCHEMA = DATABASE()
                AND TABLE_NAME = '{table}'
                AND CONSTRAINT_NAME = '{constraint}'
                AND CONSTRAINT_TYPE = 'CHECK'
            );
            SET @sql := IF(@exists = 0, '{alterSql.Replace("'", "''")}', 'SELECT 1');
            PREPARE stmt FROM @sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            """);
    }

    }
