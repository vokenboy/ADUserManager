-- =============================================================
-- Unified MySQL Database Dump
-- Apps: ADUserManager + PC Inventory
-- Database: MySQL 8.0+
-- Generated: 2026-04-10
-- =============================================================

SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;

-- =============================================================
-- TABLE: companies
-- Shared root table for both apps. Multi-tenant.
-- Base from PC Inventory; extended with ADUserManager columns.
-- =============================================================
DROP TABLE IF EXISTS `companies`;
CREATE TABLE `companies` (
  `id`                INT           NOT NULL AUTO_INCREMENT,
  `company_name`      VARCHAR(255)  COLLATE utf8mb4_unicode_ci NOT NULL,
  `domain_name`       VARCHAR(256)  DEFAULT NULL,
  `domain_controller` VARCHAR(512)  DEFAULT NULL,
  `created_at`        DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `updated_at`        DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  `active`            TINYINT(1)    NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_company_name` (`company_name`),
  KEY `idx_companies_active` (`active`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `companies` (`id`, `company_name`) VALUES
  (1,'Aromama'),
  (2,'Arpolis'),
  (3,'Cargo24'),
  (4,'Ciongo LT'),
  (5,'Domus Decora Group'),
  (6,'Helso'),
  (7,'Lex Blind'),
  (8,'Nostra'),
  (9,'Paurega'),
  (10,'Ramrenta'),
  (11,'Trukmė'),
  (12,'Urmas'),
  (13,'12eat'),
  (14,'Audimas'),
  (15,'GRPrekyba'),
  (16,'IgluTech'),
  (17,'LHM Mirosta Medenis'),
  (18,'Mantinga'),
  (19,'Mantinga FFP PC'),
  (20,'Medvita'),
  (21,'Softera'),
  (22,'TG Group'),
  (23,'Civinity'),
  (24,'Grainmore (Tasty Foods)'),
  (25,'Gravera'),
  (26,'Kijora'),
  (27,'Kika'),
  (28,'Projektana'),
  (29,'SODO UAB'),
  (30,'Valandinis');


-- =============================================================
-- TABLE: users
-- PC Inventory application login accounts.
-- =============================================================
DROP TABLE IF EXISTS `users`;
CREATE TABLE `users` (
  `id`        INT           NOT NULL AUTO_INCREMENT,
  `username`  VARCHAR(255)  COLLATE utf8mb4_unicode_ci NOT NULL,
  `password`  VARCHAR(255)  COLLATE utf8mb4_unicode_ci NOT NULL,
  `full_name` VARCHAR(255)  COLLATE utf8mb4_unicode_ci DEFAULT '',
  `role`      VARCHAR(50)   COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'user',
  PRIMARY KEY (`id`),
  UNIQUE KEY `username` (`username`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT INTO `users` (`id`, `username`, `password`, `full_name`, `role`) VALUES
  (1, 'admin', 'admin', 'Administratorius', 'admin'),
  (2, 'user',  'user',  'Useris Useriauskas', 'user');


-- =============================================================
-- TABLE: computers
-- PC Inventory asset tracking. References companies.
-- =============================================================
DROP TABLE IF EXISTS `computers`;
CREATE TABLE `computers` (
  `id`               INT            NOT NULL AUTO_INCREMENT,
  `sn`               VARCHAR(100)   DEFAULT NULL COMMENT 'Serijos numeris',
  `purchase_date`    VARCHAR(20)    DEFAULT NULL COMMENT 'Pirkimo data (YYYY-MM-DD)',
  `handover_date`    VARCHAR(20)    DEFAULT NULL COMMENT 'Atidavimo data',
  `setup_date`       VARCHAR(20)    DEFAULT NULL COMMENT 'Ruošimo data',
  `assigned_to`      VARCHAR(150)   DEFAULT NULL COMMENT 'Asmens vardas pavardė',
  `company_id`       INT            DEFAULT NULL,
  `pc_name`          VARCHAR(160)   DEFAULT NULL COMMENT 'Kompiuterio tinklo vardas',
  `model`            VARCHAR(130)   DEFAULT NULL COMMENT 'Modelis',
  `os`               VARCHAR(100)   DEFAULT NULL COMMENT 'Operacinė sistema',
  `notes`            TEXT           COMMENT 'Pastabos',
  `physical_status`  VARCHAR(50)    NOT NULL DEFAULT 'Paruoštas',
  `financial_status` VARCHAR(50)    DEFAULT NULL,
  `rent_number`      VARCHAR(70)    DEFAULT NULL,
  `created_at`       VARCHAR(30)    DEFAULT NULL,
  `updated_at`       VARCHAR(30)    DEFAULT NULL,
  `distributor_code` VARCHAR(100)   DEFAULT NULL,
  `quantity`         INT            DEFAULT 1,
  `cost_price`       DECIMAL(10,2)  DEFAULT NULL,
  `sale_price`       DECIMAL(10,2)  DEFAULT NULL,
  `invoice_number`   VARCHAR(100)   DEFAULT NULL,
  `item_type`        VARCHAR(100)   DEFAULT NULL,
  PRIMARY KEY (`id`),
  KEY `fk_company` (`company_id`),
  CONSTRAINT `fk_computers_company` FOREIGN KEY (`company_id`) REFERENCES `companies` (`id`) ON DELETE SET NULL ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


-- =============================================================
-- TABLE: column_permissions
-- PC Inventory per-user column visibility settings.
-- =============================================================
DROP TABLE IF EXISTS `column_permissions`;
CREATE TABLE `column_permissions` (
  `id`          INT           NOT NULL AUTO_INCREMENT,
  `user_id`     INT           NOT NULL,
  `table_name`  VARCHAR(100)  COLLATE utf8mb4_unicode_ci NOT NULL,
  `column_name` VARCHAR(100)  COLLATE utf8mb4_unicode_ci NOT NULL,
  `is_visible`  TINYINT(1)    NOT NULL DEFAULT 1,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uq_user_table_column` (`user_id`, `table_name`, `column_name`),
  KEY `idx_user_id` (`user_id`),
  KEY `idx_table_name` (`table_name`),
  CONSTRAINT `fk_column_permissions_user` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE ON UPDATE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- =============================================================
-- TABLE: meta
-- PC Inventory key-value store.
-- =============================================================
DROP TABLE IF EXISTS `meta`;
CREATE TABLE `meta` (
  `key`   VARCHAR(100)  NOT NULL,
  `value` TEXT,
  PRIMARY KEY (`key`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;


-- =============================================================
-- TABLE: trash
-- PC Inventory soft-deleted records.
-- =============================================================
DROP TABLE IF EXISTS `trash`;
CREATE TABLE `trash` (
  `id`               INT            NOT NULL AUTO_INCREMENT,
  `original_id`      INT            DEFAULT NULL,
  `sn`               TEXT           COLLATE utf8mb4_unicode_ci,
  `purchase_date`    TEXT           COLLATE utf8mb4_unicode_ci,
  `handover_date`    TEXT           COLLATE utf8mb4_unicode_ci,
  `setup_date`       TEXT           COLLATE utf8mb4_unicode_ci,
  `assigned_to`      TEXT           COLLATE utf8mb4_unicode_ci,
  `company`          TEXT           COLLATE utf8mb4_unicode_ci,
  `pc_name`          TEXT           COLLATE utf8mb4_unicode_ci,
  `model`            TEXT           COLLATE utf8mb4_unicode_ci,
  `os`               TEXT           COLLATE utf8mb4_unicode_ci,
  `notes`            TEXT           COLLATE utf8mb4_unicode_ci,
  `status`           TEXT           COLLATE utf8mb4_unicode_ci,
  `created_at`       TEXT           COLLATE utf8mb4_unicode_ci,
  `updated_at`       TEXT           COLLATE utf8mb4_unicode_ci,
  `deleted_at`       TEXT           COLLATE utf8mb4_unicode_ci,
  `deleted_by_user`  TEXT           COLLATE utf8mb4_unicode_ci,
  `deleted_by_name`  TEXT           COLLATE utf8mb4_unicode_ci,
  `item_type`        TEXT           COLLATE utf8mb4_unicode_ci,
  `distributor_code` TEXT           COLLATE utf8mb4_unicode_ci,
  `quantity`         INT            DEFAULT NULL,
  `cost_price`       DECIMAL(10,2)  DEFAULT NULL,
  `sale_price`       DECIMAL(10,2)  DEFAULT NULL,
  `invoice_number`   TEXT           COLLATE utf8mb4_unicode_ci,
  `physical_status`  TEXT           COLLATE utf8mb4_unicode_ci,
  `financial_status` TEXT           COLLATE utf8mb4_unicode_ci,
  `rent_number`      TEXT           COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- =============================================================
-- TABLE: audit_log
-- PC Inventory change history.
-- =============================================================
DROP TABLE IF EXISTS `audit_log`;
CREATE TABLE `audit_log` (
  `id`             INT   NOT NULL AUTO_INCREMENT,
  `timestamp`      TEXT  COLLATE utf8mb4_unicode_ci NOT NULL,
  `action`         TEXT  COLLATE utf8mb4_unicode_ci NOT NULL,
  `user_id`        TEXT  COLLATE utf8mb4_unicode_ci,
  `user_name`      TEXT  COLLATE utf8mb4_unicode_ci,
  `record_sn`      TEXT  COLLATE utf8mb4_unicode_ci,
  `record_pc_name` TEXT  COLLATE utf8mb4_unicode_ci,
  `details`        TEXT  COLLATE utf8mb4_unicode_ci,
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- =============================================================
-- TABLE: termination_log
-- ADUserManager: audit trail for every user-termination operation.
-- =============================================================
DROP TABLE IF EXISTS `termination_log`;
CREATE TABLE `termination_log` (
  `id`                         INT           NOT NULL AUTO_INCREMENT,
  `company_id`                 INT           DEFAULT NULL,
  `sam_account_name`           VARCHAR(256)  NOT NULL,
  `display_name`               VARCHAR(512)  NOT NULL,
  `distinguished_name`         VARCHAR(1024) NOT NULL,
  `terminated_at`              DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `performed_by`               VARCHAR(256)  NOT NULL,

  -- Actions performed during termination
  `account_disabled`           TINYINT(1)    NOT NULL DEFAULT 0,
  `moved_to_disabled_ou`       TINYINT(1)    NOT NULL DEFAULT 0,
  `target_ou`                  VARCHAR(1024) DEFAULT NULL,
  `original_ou`                VARCHAR(1024) DEFAULT NULL,
  `password_changed`           TINYINT(1)    NOT NULL DEFAULT 0,
  `removed_from_groups`        TINYINT(1)    NOT NULL DEFAULT 0,
  `account_expiration_set`     TINYINT(1)    NOT NULL DEFAULT 0,
  `expiration_date`            DATETIME      DEFAULT NULL,
  `data_exported`              TINYINT(1)    NOT NULL DEFAULT 0,
  `export_path`                VARCHAR(1024) DEFAULT NULL,

  -- Rollback tracking
  `rolled_back`                TINYINT(1)    NOT NULL DEFAULT 0,
  `rolled_back_at`             DATETIME      DEFAULT NULL,
  `rolled_back_by`             VARCHAR(256)  DEFAULT NULL,

  -- JSON payloads
  `group_memberships`          JSON          DEFAULT NULL,
  `step_results`               JSON          DEFAULT NULL,

  -- Links to point-in-time snapshots (soft FKs)
  `pre_termination_backup_id`  BIGINT        DEFAULT NULL,
  `post_termination_backup_id` BIGINT        DEFAULT NULL,

  PRIMARY KEY (`id`),
  KEY `idx_termlog_sam`          (`sam_account_name`),
  KEY `idx_termlog_terminated_at` (`terminated_at`),
  KEY `idx_termlog_company`      (`company_id`),
  CONSTRAINT `fk_termlog_company` FOREIGN KEY (`company_id`) REFERENCES `companies` (`id`) ON DELETE SET NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- =============================================================
-- TABLE: ad_user_backups
-- ADUserManager: point-in-time snapshots of an AD user's full state.
-- =============================================================
DROP TABLE IF EXISTS `ad_user_backups`;
CREATE TABLE `ad_user_backups` (
  `id`                    BIGINT        NOT NULL AUTO_INCREMENT,
  `company_id`            INT           NOT NULL,

  -- Core identity
  `sam_account_name`      VARCHAR(256)  NOT NULL,
  `display_name`          VARCHAR(512)  NOT NULL,
  `first_name`            VARCHAR(256)  DEFAULT NULL,
  `last_name`             VARCHAR(256)  DEFAULT NULL,
  `email`                 VARCHAR(512)  DEFAULT NULL,

  -- Organisational
  `department`            VARCHAR(256)  DEFAULT NULL,
  `title`                 VARCHAR(256)  DEFAULT NULL,
  `description`           TEXT          DEFAULT NULL,
  `distinguished_name`    VARCHAR(1024) NOT NULL,
  `organizational_unit`   VARCHAR(1024) DEFAULT NULL,

  -- Account status at time of backup
  `is_enabled`            TINYINT(1)    NOT NULL DEFAULT 1,
  `is_locked_out`         TINYINT(1)    NOT NULL DEFAULT 0,
  `password_last_set`     DATETIME      DEFAULT NULL,
  `last_logon`            DATETIME      DEFAULT NULL,
  `account_expiration`    DATETIME      DEFAULT NULL,

  -- Backup classification
  `backup_type`           VARCHAR(64)   NOT NULL DEFAULT 'Termination'
                          CHECK (`backup_type` IN ('Termination','Manual','Scheduled','Auto')),
  `operation_type`        VARCHAR(64)   NOT NULL DEFAULT 'PreTermination'
                          CHECK (`operation_type` IN ('PreTermination','PostTermination','Snapshot','Rollback')),

  -- Audit metadata
  `created_at`            DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `created_by`            VARCHAR(256)  NOT NULL,

  -- Link back to originating termination record (soft FK)
  `termination_record_id` INT           DEFAULT NULL,

  -- Versioning
  `version_number`        INT           NOT NULL DEFAULT 1,
  `is_latest`             TINYINT(1)    NOT NULL DEFAULT 1,

  `notes`                 TEXT          DEFAULT NULL,

  PRIMARY KEY (`id`),
  KEY `idx_backups_company_sam`    (`company_id`, `sam_account_name`),
  KEY `idx_backups_created_at`     (`created_at`),
  KEY `idx_backups_type`           (`backup_type`),
  KEY `idx_backups_latest`         (`is_latest`),
  KEY `idx_backups_company_latest` (`company_id`, `sam_account_name`, `is_latest`),
  CONSTRAINT `fk_backups_company` FOREIGN KEY (`company_id`) REFERENCES `companies` (`id`) ON DELETE RESTRICT
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- =============================================================
-- TABLE: ad_user_backup_groups
-- ADUserManager: group memberships per backup snapshot.
-- =============================================================
DROP TABLE IF EXISTS `ad_user_backup_groups`;
CREATE TABLE `ad_user_backup_groups` (
  `id`         BIGINT        NOT NULL AUTO_INCREMENT,
  `backup_id`  BIGINT        NOT NULL,
  `group_name` VARCHAR(512)  NOT NULL,
  `group_dn`   VARCHAR(1024) NOT NULL,
  PRIMARY KEY (`id`),
  KEY `idx_bkpgroups_backup` (`backup_id`),
  CONSTRAINT `fk_bkpgroups_backup` FOREIGN KEY (`backup_id`) REFERENCES `ad_user_backups` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- =============================================================
-- TABLE: ad_user_backups_archive
-- ADUserManager: long-term storage for superseded backups.
-- No FK constraints — intentionally decoupled.
-- =============================================================
DROP TABLE IF EXISTS `ad_user_backups_archive`;
CREATE TABLE `ad_user_backups_archive` (
  `id`                    BIGINT        NOT NULL,
  `company_id`            INT           NOT NULL,
  `sam_account_name`      VARCHAR(256)  NOT NULL,
  `display_name`          VARCHAR(512)  NOT NULL,
  `first_name`            VARCHAR(256)  DEFAULT NULL,
  `last_name`             VARCHAR(256)  DEFAULT NULL,
  `email`                 VARCHAR(512)  DEFAULT NULL,
  `department`            VARCHAR(256)  DEFAULT NULL,
  `title`                 VARCHAR(256)  DEFAULT NULL,
  `description`           TEXT          DEFAULT NULL,
  `distinguished_name`    VARCHAR(1024) NOT NULL,
  `organizational_unit`   VARCHAR(1024) DEFAULT NULL,
  `is_enabled`            TINYINT(1)    NOT NULL,
  `is_locked_out`         TINYINT(1)    NOT NULL,
  `password_last_set`     DATETIME      DEFAULT NULL,
  `last_logon`            DATETIME      DEFAULT NULL,
  `account_expiration`    DATETIME      DEFAULT NULL,
  `backup_type`           VARCHAR(64)   NOT NULL,
  `operation_type`        VARCHAR(64)   NOT NULL,
  `created_at`            DATETIME      NOT NULL,
  `created_by`            VARCHAR(256)  NOT NULL,
  `termination_record_id` INT           DEFAULT NULL,
  `version_number`        INT           NOT NULL DEFAULT 1,
  `is_latest`             TINYINT(1)    NOT NULL DEFAULT 1,
  `notes`                 TEXT          DEFAULT NULL,
  `archived_at`           DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  `archived_by`           VARCHAR(256)  NOT NULL,
  KEY `idx_archive_sam`         (`sam_account_name`),
  KEY `idx_archive_company_sam` (`company_id`, `sam_account_name`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


-- =============================================================
-- TABLE: ad_user_backup_groups_archive
-- ADUserManager: archived group memberships.
-- =============================================================
DROP TABLE IF EXISTS `ad_user_backup_groups_archive`;
CREATE TABLE `ad_user_backup_groups_archive` (
  `id`          BIGINT        NOT NULL,
  `backup_id`   BIGINT        NOT NULL,
  `group_name`  VARCHAR(512)  NOT NULL,
  `group_dn`    VARCHAR(1024) NOT NULL,
  `archived_at` DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  KEY `idx_archive_groups_backup` (`backup_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;


SET FOREIGN_KEY_CHECKS = 1;
