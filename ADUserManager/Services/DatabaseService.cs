using System.Text.Json;
using ActiveManager.Services.Models;
using Microsoft.Data.SqlClient;

namespace ActiveManager.Services;

public class DatabaseService : IDisposable
{
    private bool _initialized;
    private bool _initAttempted;

    /// <summary>
    /// Indicates whether the database connection is available.
    /// False if DB is disabled in settings, or if the connection failed during initialization.
    /// </summary>
    public bool IsAvailable { get; private set; }

    private static string ConnectionString => AppSettings.Instance.Database.BuildConnectionString();
    private static bool IsEnabled => AppSettings.Instance.Database.Enabled;

    /// <summary>
    /// Connection string pointing to the master database on the same server.
    /// Used to auto-create the target database if it does not exist.
    /// </summary>
    private static string MasterConnectionString
    {
        get
        {
            var builder = new SqlConnectionStringBuilder(ConnectionString)
            {
                InitialCatalog = "master"
            };
            return builder.ConnectionString;
        }
    }

    /// <summary>
    /// Resets initialization state so the next call to EnsureInitializedAsync will re-attempt connection.
    /// Call this when database settings change.
    /// </summary>
    public void Reinitialize()
    {
        _initialized = false;
        _initAttempted = false;
        IsAvailable = false;
    }

    public async Task EnsureInitializedAsync()
    {
        if (_initialized) return;
        if (_initAttempted) return;

        _initAttempted = true;

        if (!IsEnabled)
        {
            IsAvailable = false;
            return;
        }

        try
        {
            // ── 1. Auto-create the database if it does not exist ──────────────
            var dbName = AppSettings.Instance.Database.Database;
            if (!string.IsNullOrWhiteSpace(dbName))
            {
                try
                {
                    var safeDbName = dbName.Replace("]", "]]");
                    using var masterConn = new SqlConnection(MasterConnectionString);
                    await masterConn.OpenAsync();
                    using var createDbCmd = masterConn.CreateCommand();
                    createDbCmd.CommandText = $@"
                        IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = @dbName)
                            CREATE DATABASE [{safeDbName}]";
                    createDbCmd.Parameters.AddWithValue("@dbName", dbName);
                    await createDbCmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    // May not have sufficient permission on master; log and continue.
                    // The target database may already exist.
                    ErrorLogger.Log("DB auto-create", ex);
                }
            }

            // ── 2. Connect to the target database and build the schema ─────────
            using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();

            // ── companies ─────────────────────────────────────────────────────
            await ExecAsync(connection, @"
                IF OBJECT_ID('dbo.companies', 'U') IS NULL
                CREATE TABLE dbo.companies (
                    id                INT            NOT NULL IDENTITY(1,1),
                    company_name      NVARCHAR(255)  NOT NULL,
                    domain_name       NVARCHAR(256)  NULL,
                    domain_controller NVARCHAR(512)  NULL,
                    created_at        DATETIME       NOT NULL DEFAULT GETDATE(),
                    updated_at        DATETIME       NOT NULL DEFAULT GETDATE(),
                    active            BIT            NOT NULL DEFAULT 1,
                    CONSTRAINT PK_companies PRIMARY KEY (id),
                    CONSTRAINT UQ_companies_name UNIQUE (company_name)
                )");

            await CreateIndexIfNotExistsAsync(connection, "companies", "IX_companies_active",
                "CREATE INDEX IX_companies_active ON dbo.companies (active)");

            // ── seed companies (enum-style reference data) ─────────────────────
            await ExecAsync(connection, @"
                SET IDENTITY_INSERT dbo.companies ON;
                MERGE dbo.companies AS t
                USING (VALUES
                    (1,  N'Aromama',                NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (2,  N'Arpolis',                NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (3,  N'Cargo24',                NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (4,  N'Ciongo LT',              NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (5,  N'Domus Decora Group',     NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (6,  N'Helso',                  NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (7,  N'Lex Blind',              NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (8,  N'Nostra',                 NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (9,  N'Paurega',                NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (10, N'Ramrenta',               NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (11, N'Trukmė',                 NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (12, N'Urmas',                  NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (14, N'Audimas',                NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (15, N'GRPrekyba',              NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (16, N'IgluTech',               NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (17, N'LHM Mirosta Medenis',    NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (18, N'Mantinga',               NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (19, N'Mantinga FFP PC',        NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (20, N'Medvita',                NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (21, N'Softera',                NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (22, N'TG Group',               NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (23, N'Civinity',               NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (24, N'Grainmore (Tasty Foods)',NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (25, N'Gravera',                NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (26, N'Kijora',                 NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (27, N'Kika',                   NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (28, N'Projektana',             NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (29, N'SODO UAB',               NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (30, N'Valandinis',             NULL, NULL, '2026-04-10 11:32:00', '2026-04-10 11:32:00', 1),
                    (31, N'Databank',               NULL, NULL, '2026-04-10 14:41:28', '2026-04-10 14:41:28', 1),
                    (32, N'Testas',                 NULL, NULL, '2026-04-22 11:05:00', '2026-04-22 11:05:00', 1)
                ) AS s (id, company_name, domain_name, domain_controller, created_at, updated_at, active)
                ON t.id = s.id
                WHEN NOT MATCHED THEN
                    INSERT (id, company_name, domain_name, domain_controller, created_at, updated_at, active)
                    VALUES (s.id, s.company_name, s.domain_name, s.domain_controller, s.created_at, s.updated_at, s.active);
                SET IDENTITY_INSERT dbo.companies OFF;");

            // ── termination_log ────────────────────────────────────────────────
            await ExecAsync(connection, @"
                IF OBJECT_ID('dbo.termination_log', 'U') IS NULL
                CREATE TABLE dbo.termination_log (
                    id                         INT            NOT NULL IDENTITY(1,1),
                    company_id                 INT            NULL,
                    sam_account_name           NVARCHAR(256)  NOT NULL,
                    display_name               NVARCHAR(512)  NOT NULL,
                    distinguished_name         NVARCHAR(1024) NOT NULL,
                    terminated_at              DATETIME       NOT NULL DEFAULT GETDATE(),
                    performed_by               NVARCHAR(256)  NOT NULL,
                    account_disabled           BIT            NOT NULL DEFAULT 0,
                    moved_to_disabled_ou       BIT            NOT NULL DEFAULT 0,
                    target_ou                  NVARCHAR(1024) NULL,
                    original_ou                NVARCHAR(1024) NULL,
                    password_changed           BIT            NOT NULL DEFAULT 0,
                    removed_from_groups        BIT            NOT NULL DEFAULT 0,
                    account_expiration_set     BIT            NOT NULL DEFAULT 0,
                    expiration_date            DATETIME       NULL,
                    data_exported              BIT            NOT NULL DEFAULT 0,
                    export_path                NVARCHAR(1024) NULL,
                    rolled_back                BIT            NOT NULL DEFAULT 0,
                    rolled_back_at             DATETIME       NULL,
                    rolled_back_by             NVARCHAR(256)  NULL,
                    deleted_from_directory     BIT            NOT NULL DEFAULT 0,
                    deleted_at                 DATETIME       NULL,
                    deleted_by                 NVARCHAR(256)  NULL,
                    group_memberships          NVARCHAR(MAX)  NULL,
                    step_results               NVARCHAR(MAX)  NULL,
                    pre_termination_backup_id  BIGINT         NULL,
                    post_termination_backup_id BIGINT         NULL,
                    termination_reason         NVARCHAR(128)  NULL,
                    CONSTRAINT PK_termination_log PRIMARY KEY (id),
                    CONSTRAINT FK_termlog_company FOREIGN KEY (company_id)
                        REFERENCES dbo.companies (id) ON DELETE SET NULL
                )");

            await CreateIndexIfNotExistsAsync(connection, "termination_log", "IX_termlog_sam",
                "CREATE INDEX IX_termlog_sam ON dbo.termination_log (sam_account_name)");
            await CreateIndexIfNotExistsAsync(connection, "termination_log", "IX_termlog_terminated_at",
                "CREATE INDEX IX_termlog_terminated_at ON dbo.termination_log (terminated_at)");
            await CreateIndexIfNotExistsAsync(connection, "termination_log", "IX_termlog_company",
                "CREATE INDEX IX_termlog_company ON dbo.termination_log (company_id)");

            // ── password_reset_log ─────────────────────────────────────────────
            await ExecAsync(connection, @"
                IF OBJECT_ID('dbo.password_reset_log', 'U') IS NULL
                CREATE TABLE dbo.password_reset_log (
                    id                           INT            NOT NULL IDENTITY(1,1),
                    sam_account_name             NVARCHAR(256)  NOT NULL,
                    display_name                 NVARCHAR(512)  NOT NULL,
                    distinguished_name           NVARCHAR(1024) NOT NULL,
                    reset_at                     DATETIME       NOT NULL DEFAULT GETDATE(),
                    performed_by                 NVARCHAR(256)  NOT NULL,
                    force_change_at_next_sign_in BIT            NOT NULL DEFAULT 1,
                    CONSTRAINT PK_password_reset_log PRIMARY KEY (id)
                )");

            await CreateIndexIfNotExistsAsync(connection, "password_reset_log", "IX_pwdreset_sam",
                "CREATE INDEX IX_pwdreset_sam ON dbo.password_reset_log (sam_account_name)");
            await CreateIndexIfNotExistsAsync(connection, "password_reset_log", "IX_pwdreset_reset_at",
                "CREATE INDEX IX_pwdreset_reset_at ON dbo.password_reset_log (reset_at)");

            // ── admin_action_log ───────────────────────────────────────────────
            await ExecAsync(connection, @"
                IF OBJECT_ID('dbo.admin_action_log', 'U') IS NULL
                CREATE TABLE dbo.admin_action_log (
                    id               INT            NOT NULL IDENTITY(1,1),
                    action_type      NVARCHAR(128)  NOT NULL,
                    sam_account_name NVARCHAR(256)  NULL,
                    display_name     NVARCHAR(512)  NULL,
                    performed_by     NVARCHAR(256)  NOT NULL,
                    action_at        DATETIME       NOT NULL DEFAULT GETDATE(),
                    details          NVARCHAR(MAX)  NULL,
                    CONSTRAINT PK_admin_action_log PRIMARY KEY (id)
                )");

            await CreateIndexIfNotExistsAsync(connection, "admin_action_log", "IX_adminaction_at",
                "CREATE INDEX IX_adminaction_at ON dbo.admin_action_log (action_at)");
            await CreateIndexIfNotExistsAsync(connection, "admin_action_log", "IX_adminaction_sam",
                "CREATE INDEX IX_adminaction_sam ON dbo.admin_action_log (sam_account_name)");

            // ── ad_user_backups ────────────────────────────────────────────────
            await ExecAsync(connection, @"
                IF OBJECT_ID('dbo.ad_user_backups', 'U') IS NULL
                CREATE TABLE dbo.ad_user_backups (
                    id                    BIGINT         NOT NULL IDENTITY(1,1),
                    company_id            INT            NOT NULL,
                    sam_account_name      NVARCHAR(256)  NOT NULL,
                    display_name          NVARCHAR(512)  NOT NULL,
                    first_name            NVARCHAR(256)  NULL,
                    last_name             NVARCHAR(256)  NULL,
                    email                 NVARCHAR(512)  NULL,
                    department            NVARCHAR(256)  NULL,
                    title                 NVARCHAR(256)  NULL,
                    description           NVARCHAR(MAX)  NULL,
                    distinguished_name    NVARCHAR(1024) NOT NULL,
                    organizational_unit   NVARCHAR(1024) NULL,
                    is_enabled            BIT            NOT NULL DEFAULT 1,
                    is_locked_out         BIT            NOT NULL DEFAULT 0,
                    password_last_set     DATETIME       NULL,
                    last_logon            DATETIME       NULL,
                    account_expiration    DATETIME       NULL,
                    backup_type           NVARCHAR(64)   NOT NULL DEFAULT 'Termination'
                        CONSTRAINT CHK_backups_backup_type
                            CHECK (backup_type IN ('Termination','Manual','Scheduled','Auto')),
                    operation_type        NVARCHAR(64)   NOT NULL DEFAULT 'PreTermination'
                        CONSTRAINT CHK_backups_operation_type
                            CHECK (operation_type IN ('PreTermination','PostTermination','Snapshot','Rollback')),
                    created_at            DATETIME       NOT NULL DEFAULT GETDATE(),
                    created_by            NVARCHAR(256)  NOT NULL,
                    termination_record_id INT            NULL,
                    version_number        INT            NOT NULL DEFAULT 1,
                    is_latest             BIT            NOT NULL DEFAULT 1,
                    notes                 NVARCHAR(MAX)  NULL,
                    CONSTRAINT PK_ad_user_backups PRIMARY KEY (id),
                    CONSTRAINT FK_backups_company FOREIGN KEY (company_id)
                        REFERENCES dbo.companies (id) ON DELETE NO ACTION
                )");

            await CreateIndexIfNotExistsAsync(connection, "ad_user_backups", "IX_backups_company_sam",
                "CREATE INDEX IX_backups_company_sam ON dbo.ad_user_backups (company_id, sam_account_name)");
            await CreateIndexIfNotExistsAsync(connection, "ad_user_backups", "IX_backups_created_at",
                "CREATE INDEX IX_backups_created_at ON dbo.ad_user_backups (created_at)");
            await CreateIndexIfNotExistsAsync(connection, "ad_user_backups", "IX_backups_type",
                "CREATE INDEX IX_backups_type ON dbo.ad_user_backups (backup_type)");
            await CreateIndexIfNotExistsAsync(connection, "ad_user_backups", "IX_backups_latest",
                "CREATE INDEX IX_backups_latest ON dbo.ad_user_backups (is_latest)");
            await CreateIndexIfNotExistsAsync(connection, "ad_user_backups", "IX_backups_company_latest",
                "CREATE INDEX IX_backups_company_latest ON dbo.ad_user_backups (company_id, sam_account_name, is_latest)");

            // ── ad_user_backup_groups ──────────────────────────────────────────
            await ExecAsync(connection, @"
                IF OBJECT_ID('dbo.ad_user_backup_groups', 'U') IS NULL
                CREATE TABLE dbo.ad_user_backup_groups (
                    id         BIGINT         NOT NULL IDENTITY(1,1),
                    backup_id  BIGINT         NOT NULL,
                    group_name NVARCHAR(512)  NOT NULL,
                    group_dn   NVARCHAR(1024) NOT NULL,
                    CONSTRAINT PK_ad_user_backup_groups PRIMARY KEY (id),
                    CONSTRAINT FK_bkpgroups_backup FOREIGN KEY (backup_id)
                        REFERENCES dbo.ad_user_backups (id) ON DELETE CASCADE
                )");

            await CreateIndexIfNotExistsAsync(connection, "ad_user_backup_groups", "IX_bkpgroups_backup",
                "CREATE INDEX IX_bkpgroups_backup ON dbo.ad_user_backup_groups (backup_id)");

            // ── ad_user_backups_archive ────────────────────────────────────────
            await ExecAsync(connection, @"
                IF OBJECT_ID('dbo.ad_user_backups_archive', 'U') IS NULL
                CREATE TABLE dbo.ad_user_backups_archive (
                    id                    BIGINT         NOT NULL,
                    company_id            INT            NOT NULL,
                    sam_account_name      NVARCHAR(256)  NOT NULL,
                    display_name          NVARCHAR(512)  NOT NULL,
                    first_name            NVARCHAR(256)  NULL,
                    last_name             NVARCHAR(256)  NULL,
                    email                 NVARCHAR(512)  NULL,
                    department            NVARCHAR(256)  NULL,
                    title                 NVARCHAR(256)  NULL,
                    description           NVARCHAR(MAX)  NULL,
                    distinguished_name    NVARCHAR(1024) NOT NULL,
                    organizational_unit   NVARCHAR(1024) NULL,
                    is_enabled            BIT            NOT NULL,
                    is_locked_out         BIT            NOT NULL,
                    password_last_set     DATETIME       NULL,
                    last_logon            DATETIME       NULL,
                    account_expiration    DATETIME       NULL,
                    backup_type           NVARCHAR(64)   NOT NULL,
                    operation_type        NVARCHAR(64)   NOT NULL,
                    created_at            DATETIME       NOT NULL,
                    created_by            NVARCHAR(256)  NOT NULL,
                    termination_record_id INT            NULL,
                    version_number        INT            NOT NULL DEFAULT 1,
                    is_latest             BIT            NOT NULL DEFAULT 1,
                    notes                 NVARCHAR(MAX)  NULL,
                    archived_at           DATETIME       NOT NULL DEFAULT GETDATE(),
                    archived_by           NVARCHAR(256)  NOT NULL
                )");

            await CreateIndexIfNotExistsAsync(connection, "ad_user_backups_archive", "IX_archive_sam",
                "CREATE INDEX IX_archive_sam ON dbo.ad_user_backups_archive (sam_account_name)");
            await CreateIndexIfNotExistsAsync(connection, "ad_user_backups_archive", "IX_archive_company_sam",
                "CREATE INDEX IX_archive_company_sam ON dbo.ad_user_backups_archive (company_id, sam_account_name)");

            // ── ad_user_backup_groups_archive ──────────────────────────────────
            await ExecAsync(connection, @"
                IF OBJECT_ID('dbo.ad_user_backup_groups_archive', 'U') IS NULL
                CREATE TABLE dbo.ad_user_backup_groups_archive (
                    id          BIGINT         NOT NULL,
                    backup_id   BIGINT         NOT NULL,
                    group_name  NVARCHAR(512)  NOT NULL,
                    group_dn    NVARCHAR(1024) NOT NULL,
                    archived_at DATETIME       NOT NULL DEFAULT GETDATE()
                )");

            await CreateIndexIfNotExistsAsync(connection, "ad_user_backup_groups_archive", "IX_archive_groups_backup",
                "CREATE INDEX IX_archive_groups_backup ON dbo.ad_user_backup_groups_archive (backup_id)");

            // ── safe column additions for existing databases ───────────────────
            // Uses sys.columns check so each ALTER is idempotent.
            var safeAlters = new[]
            {
                ("termination_log", "original_ou",                "NVARCHAR(1024) NULL"),
                ("termination_log", "account_expiration_set",     "BIT NOT NULL DEFAULT 0"),
                ("termination_log", "expiration_date",            "DATETIME NULL"),
                ("termination_log", "rolled_back",                "BIT NOT NULL DEFAULT 0"),
                ("termination_log", "rolled_back_at",             "DATETIME NULL"),
                ("termination_log", "rolled_back_by",             "NVARCHAR(256) NULL"),
                ("termination_log", "deleted_from_directory",     "BIT NOT NULL DEFAULT 0"),
                ("termination_log", "deleted_at",                 "DATETIME NULL"),
                ("termination_log", "deleted_by",                 "NVARCHAR(256) NULL"),
                ("termination_log", "company_id",                 "INT NULL"),
                ("termination_log", "pre_termination_backup_id",  "BIGINT NULL"),
                ("termination_log", "post_termination_backup_id", "BIGINT NULL"),
                ("termination_log", "termination_reason",         "NVARCHAR(128) NULL"),
            };

            foreach (var (table, column, typeDef) in safeAlters)
            {
                await ExecAsync(connection, $@"
                    IF NOT EXISTS (
                        SELECT 1 FROM sys.columns
                        WHERE object_id = OBJECT_ID('dbo.{table}') AND name = '{column}'
                    )
                    ALTER TABLE dbo.{table} ADD [{column}] {typeDef}");
            }

            _initialized = true;
            IsAvailable = true;
        }
        catch (Exception ex)
        {
            IsAvailable = false;
            ErrorLogger.Log("DB initialization", ex);
        }
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private static async Task ExecAsync(SqlConnection conn, string sql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task CreateIndexIfNotExistsAsync(
        SqlConnection conn, string table, string indexName, string createSql)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = $@"
            IF NOT EXISTS (
                SELECT 1 FROM sys.indexes
                WHERE object_id = OBJECT_ID('dbo.{table}') AND name = '{indexName}'
            )
            {createSql}";
        await cmd.ExecuteNonQueryAsync();
    }

    // =========================================================================
    // Password reset log
    // =========================================================================

    public async Task<int> SavePasswordResetRecordAsync(PasswordResetRecord record)
    {
        await EnsureInitializedAsync();
        if (!IsAvailable) return -1;

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO dbo.password_reset_log (
                sam_account_name, display_name, distinguished_name,
                reset_at, performed_by, force_change_at_next_sign_in
            )
            OUTPUT INSERTED.id
            VALUES (
                @sam, @display, @dn,
                @resetAt, @performedBy, @forceChange
            )";

        cmd.Parameters.AddWithValue("@sam",         record.SamAccountName);
        cmd.Parameters.AddWithValue("@display",     record.DisplayName);
        cmd.Parameters.AddWithValue("@dn",          record.DistinguishedName);
        cmd.Parameters.AddWithValue("@resetAt",     record.ResetAt);
        cmd.Parameters.AddWithValue("@performedBy", record.PerformedBy);
        cmd.Parameters.AddWithValue("@forceChange", record.ForceChangeAtNextSignIn);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<PasswordResetRecord>> GetPasswordResetRecordsAsync(string? searchTerm = null)
    {
        await EnsureInitializedAsync();
        if (!IsAvailable) return new List<PasswordResetRecord>();

        var records = new List<PasswordResetRecord>();

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            cmd.CommandText = @"
                SELECT TOP 200 * FROM dbo.password_reset_log
                ORDER BY reset_at DESC";
        }
        else
        {
            cmd.CommandText = @"
                SELECT TOP 200 * FROM dbo.password_reset_log
                WHERE sam_account_name LIKE @search OR display_name LIKE @search
                ORDER BY reset_at DESC";
            cmd.Parameters.AddWithValue("@search", $"%{searchTerm}%");
        }

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            records.Add(MapPasswordResetRecord(reader));

        return records;
    }

    public async Task<List<PasswordResetRecord>> GetRecentPasswordResetRecordsAsync(DateTime since, int limit = 20)
    {
        await EnsureInitializedAsync();
        if (!IsAvailable) return new List<PasswordResetRecord>();

        var records = new List<PasswordResetRecord>();

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT TOP (@limit) * FROM dbo.password_reset_log
            WHERE reset_at >= @since
            ORDER BY reset_at DESC";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@since", since);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            records.Add(MapPasswordResetRecord(reader));

        return records;
    }

    // =========================================================================
    // Admin action log
    // =========================================================================

    public async Task<int> SaveAdminActionAsync(AdminActionRecord record)
    {
        await EnsureInitializedAsync();
        if (!IsAvailable) return -1;

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO dbo.admin_action_log (
                action_type, sam_account_name, display_name,
                performed_by, action_at, details
            )
            OUTPUT INSERTED.id
            VALUES (
                @actionType, @sam, @display,
                @performedBy, @actionAt, @details
            )";

        cmd.Parameters.AddWithValue("@actionType",  record.ActionType);
        cmd.Parameters.AddWithValue("@sam",          string.IsNullOrWhiteSpace(record.SamAccountName) ? (object)DBNull.Value : record.SamAccountName);
        cmd.Parameters.AddWithValue("@display",      string.IsNullOrWhiteSpace(record.DisplayName)    ? (object)DBNull.Value : record.DisplayName);
        cmd.Parameters.AddWithValue("@performedBy",  record.PerformedBy);
        cmd.Parameters.AddWithValue("@actionAt",     record.ActionAt);
        cmd.Parameters.AddWithValue("@details",      string.IsNullOrWhiteSpace(record.Details) ? (object)DBNull.Value : record.Details);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<AdminActionRecord>> GetRecentAdminActionsAsync(DateTime since, int limit = 20)
    {
        await EnsureInitializedAsync();
        if (!IsAvailable) return new List<AdminActionRecord>();

        var records = new List<AdminActionRecord>();

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT TOP (@limit) * FROM dbo.admin_action_log
            WHERE action_at >= @since
            ORDER BY action_at DESC";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@since", since);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            records.Add(new AdminActionRecord
            {
                Id             = reader.GetInt32(reader.GetOrdinal("id")),
                ActionType     = reader.GetString(reader.GetOrdinal("action_type")),
                SamAccountName = reader.IsDBNull(reader.GetOrdinal("sam_account_name")) ? string.Empty : reader.GetString(reader.GetOrdinal("sam_account_name")),
                DisplayName    = reader.IsDBNull(reader.GetOrdinal("display_name"))     ? string.Empty : reader.GetString(reader.GetOrdinal("display_name")),
                PerformedBy    = reader.GetString(reader.GetOrdinal("performed_by")),
                ActionAt       = reader.GetDateTime(reader.GetOrdinal("action_at")),
                Details        = reader.IsDBNull(reader.GetOrdinal("details"))          ? string.Empty : reader.GetString(reader.GetOrdinal("details")),
            });
        }

        return records;
    }

    // =========================================================================
    // Termination log
    // =========================================================================

    public async Task<int> SaveTerminationRecordAsync(
        TerminationRecord record,
        long preBackupId = -1,
        long postBackupId = -1)
    {
        await EnsureInitializedAsync();
        if (!IsAvailable) return -1;

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO dbo.termination_log (
                sam_account_name, display_name, distinguished_name,
                terminated_at, performed_by,
                account_disabled, moved_to_disabled_ou, target_ou, original_ou,
                password_changed, removed_from_groups,
                account_expiration_set, expiration_date,
                data_exported, export_path,
                group_memberships, step_results,
                pre_termination_backup_id, post_termination_backup_id,
                termination_reason
            )
            OUTPUT INSERTED.id
            VALUES (
                @sam, @display, @dn,
                @terminated, @performed,
                @disabled, @moved, @targetou, @originalou,
                @pwdchanged, @removed,
                @expirationset, @expirationdate,
                @exported, @exportpath,
                @groups, @steps,
                @prebackup, @postbackup,
                @reason
            )";

        cmd.Parameters.AddWithValue("@sam",            record.SamAccountName);
        cmd.Parameters.AddWithValue("@display",        record.DisplayName);
        cmd.Parameters.AddWithValue("@dn",             record.DistinguishedName);
        cmd.Parameters.AddWithValue("@terminated",     record.TerminatedAt);
        cmd.Parameters.AddWithValue("@performed",      record.PerformedBy);
        cmd.Parameters.AddWithValue("@disabled",       record.AccountDisabled);
        cmd.Parameters.AddWithValue("@moved",          record.MovedToDisabledOU);
        cmd.Parameters.AddWithValue("@targetou",       record.TargetOU     ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@originalou",     record.OriginalOU   ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@pwdchanged",     record.PasswordChanged);
        cmd.Parameters.AddWithValue("@removed",        record.RemovedFromGroups);
        cmd.Parameters.AddWithValue("@expirationset",  record.AccountExpirationSet);
        cmd.Parameters.AddWithValue("@expirationdate", record.ExpirationDate ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@exported",       record.DataExported);
        cmd.Parameters.AddWithValue("@exportpath",     record.ExportPath   ?? (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@groups",         JsonSerializer.Serialize(record.GroupMemberships));
        cmd.Parameters.AddWithValue("@steps",          JsonSerializer.Serialize(record.StepResults));
        cmd.Parameters.AddWithValue("@prebackup",      preBackupId  > 0 ? preBackupId  : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@postbackup",     postBackupId > 0 ? postBackupId : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@reason",         string.IsNullOrEmpty(record.TerminationReason) ? (object)DBNull.Value : record.TerminationReason);

        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    public async Task<List<TerminationRecord>> GetTerminationRecordsAsync(string? searchTerm = null)
    {
        await EnsureInitializedAsync();
        if (!IsAvailable) return new List<TerminationRecord>();

        var records = new List<TerminationRecord>();

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        if (string.IsNullOrWhiteSpace(searchTerm))
        {
            cmd.CommandText = @"
                SELECT TOP 200 * FROM dbo.termination_log
                ORDER BY terminated_at DESC";
        }
        else
        {
            cmd.CommandText = @"
                SELECT TOP 200 * FROM dbo.termination_log
                WHERE sam_account_name LIKE @search OR display_name LIKE @search
                ORDER BY terminated_at DESC";
            cmd.Parameters.AddWithValue("@search", $"%{searchTerm}%");
        }

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            records.Add(MapTerminationRecord(reader));

        return records;
    }

    public async Task<List<TerminationRecord>> GetRecentTerminationRecordsAsync(DateTime since, int limit = 20)
    {
        await EnsureInitializedAsync();
        if (!IsAvailable) return new List<TerminationRecord>();

        var records = new List<TerminationRecord>();

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT TOP (@limit) * FROM dbo.termination_log
            WHERE terminated_at >= @since
               OR (rolled_back = 1 AND rolled_back_at >= @since)
            ORDER BY terminated_at DESC";
        cmd.Parameters.AddWithValue("@limit", limit);
        cmd.Parameters.AddWithValue("@since", since);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            records.Add(MapTerminationRecord(reader));

        return records;
    }

    public async Task<TerminationRecord?> GetTerminationRecordByIdAsync(int id)
    {
        await EnsureInitializedAsync();
        if (!IsAvailable) return null;

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM dbo.termination_log WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return MapTerminationRecord(reader);

        return null;
    }

    public async Task MarkAsRolledBackAsync(int id)
    {
        await EnsureInitializedAsync();
        if (!IsAvailable) return;

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE dbo.termination_log
            SET rolled_back = 1, rolled_back_at = @at, rolled_back_by = @by
            WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", id);
        cmd.Parameters.AddWithValue("@at", DateTime.Now);
        cmd.Parameters.AddWithValue("@by", Environment.UserName);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task MarkTerminationRecordsAsDeletedAsync(string samAccountName)
    {
        await EnsureInitializedAsync();
        if (!IsAvailable) return;

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE dbo.termination_log
            SET deleted_from_directory = 1, deleted_at = @at, deleted_by = @by
            WHERE sam_account_name = @sam";
        cmd.Parameters.AddWithValue("@sam", samAccountName);
        cmd.Parameters.AddWithValue("@at",  DateTime.Now);
        cmd.Parameters.AddWithValue("@by",  Environment.UserName);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Moves all backup rows (and their group rows) for a given user into the archive tables,
    /// then deletes them from the live tables. Call this after a successful restore.
    /// No-op if the DB is unavailable.
    /// </summary>
    public async Task ArchiveUserBackupsAsync(string samAccountName, int companyId)
    {
        await EnsureInitializedAsync();
        if (!IsAvailable) return;

        using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Copy group rows to the archive table
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO dbo.ad_user_backup_groups_archive
                        (id, backup_id, group_name, group_dn, archived_at)
                    SELECT g.id, g.backup_id, g.group_name, g.group_dn, GETDATE()
                    FROM dbo.ad_user_backup_groups g
                    JOIN dbo.ad_user_backups b ON g.backup_id = b.id
                    WHERE b.sam_account_name = @sam AND b.company_id = @cid";
                cmd.Parameters.AddWithValue("@sam", samAccountName);
                cmd.Parameters.AddWithValue("@cid", companyId);
                await cmd.ExecuteNonQueryAsync();
            }

            // 2. Copy backup rows to the archive table
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    INSERT INTO dbo.ad_user_backups_archive (
                        id, company_id, sam_account_name, display_name,
                        first_name, last_name, email, department, title, description,
                        distinguished_name, organizational_unit,
                        is_enabled, is_locked_out, password_last_set, last_logon, account_expiration,
                        backup_type, operation_type, created_at, created_by,
                        termination_record_id, version_number, is_latest, notes,
                        archived_at, archived_by
                    )
                    SELECT
                        id, company_id, sam_account_name, display_name,
                        first_name, last_name, email, department, title, description,
                        distinguished_name, organizational_unit,
                        is_enabled, is_locked_out, password_last_set, last_logon, account_expiration,
                        backup_type, operation_type, created_at, created_by,
                        termination_record_id, version_number, is_latest, notes,
                        GETDATE(), @archivedBy
                    FROM dbo.ad_user_backups
                    WHERE sam_account_name = @sam AND company_id = @cid";
                cmd.Parameters.AddWithValue("@sam",        samAccountName);
                cmd.Parameters.AddWithValue("@cid",        companyId);
                cmd.Parameters.AddWithValue("@archivedBy", Environment.UserName);
                await cmd.ExecuteNonQueryAsync();
            }

            // 3. Delete from the live table (group rows cascade via ON DELETE CASCADE)
            using (var cmd = connection.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = @"
                    DELETE FROM dbo.ad_user_backups
                    WHERE sam_account_name = @sam AND company_id = @cid";
                cmd.Parameters.AddWithValue("@sam", samAccountName);
                cmd.Parameters.AddWithValue("@cid", companyId);
                await cmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            ErrorLogger.Log("ArchiveUserBackups", ex);
            // Non-fatal: the restore already succeeded; we log and continue.
        }
    }

    // =========================================================================
    // Mapping helpers
    // =========================================================================

    private static PasswordResetRecord MapPasswordResetRecord(SqlDataReader reader)
    {
        return new PasswordResetRecord
        {
            Id                      = reader.GetInt32(reader.GetOrdinal("id")),
            SamAccountName          = reader.GetString(reader.GetOrdinal("sam_account_name")),
            DisplayName             = reader.GetString(reader.GetOrdinal("display_name")),
            DistinguishedName       = reader.GetString(reader.GetOrdinal("distinguished_name")),
            ResetAt                 = reader.GetDateTime(reader.GetOrdinal("reset_at")),
            PerformedBy             = reader.GetString(reader.GetOrdinal("performed_by")),
            ForceChangeAtNextSignIn = reader.GetBoolean(reader.GetOrdinal("force_change_at_next_sign_in")),
        };
    }

    private static TerminationRecord MapTerminationRecord(SqlDataReader reader)
    {
        int colId             = reader.GetOrdinal("id");
        int colSam            = reader.GetOrdinal("sam_account_name");
        int colDisplay        = reader.GetOrdinal("display_name");
        int colDn             = reader.GetOrdinal("distinguished_name");
        int colTerminatedAt   = reader.GetOrdinal("terminated_at");
        int colPerformedBy    = reader.GetOrdinal("performed_by");
        int colDisabled       = reader.GetOrdinal("account_disabled");
        int colMoved          = reader.GetOrdinal("moved_to_disabled_ou");
        int colTargetOU       = reader.GetOrdinal("target_ou");
        int colOriginalOU     = reader.GetOrdinal("original_ou");
        int colPwdChanged     = reader.GetOrdinal("password_changed");
        int colRemoved        = reader.GetOrdinal("removed_from_groups");
        int colExpSet         = reader.GetOrdinal("account_expiration_set");
        int colExpDate        = reader.GetOrdinal("expiration_date");
        int colExported       = reader.GetOrdinal("data_exported");
        int colExportPath     = reader.GetOrdinal("export_path");
        int colRolledBack     = reader.GetOrdinal("rolled_back");
        int colRolledBackAt   = reader.GetOrdinal("rolled_back_at");
        int colRolledBackBy   = reader.GetOrdinal("rolled_back_by");
        int colDeleted        = reader.GetOrdinal("deleted_from_directory");
        int colDeletedAt      = reader.GetOrdinal("deleted_at");
        int colDeletedBy      = reader.GetOrdinal("deleted_by");
        int colGroups         = reader.GetOrdinal("group_memberships");
        int colSteps          = reader.GetOrdinal("step_results");

        var record = new TerminationRecord
        {
            Id                   = reader.GetInt32(colId),
            SamAccountName       = reader.GetString(colSam),
            DisplayName          = reader.GetString(colDisplay),
            DistinguishedName    = reader.GetString(colDn),
            TerminatedAt         = reader.GetDateTime(colTerminatedAt),
            PerformedBy          = reader.GetString(colPerformedBy),
            AccountDisabled      = reader.GetBoolean(colDisabled),
            MovedToDisabledOU    = reader.GetBoolean(colMoved),
            TargetOU             = reader.IsDBNull(colTargetOU)    ? null : reader.GetString(colTargetOU),
            OriginalOU           = reader.IsDBNull(colOriginalOU)  ? null : reader.GetString(colOriginalOU),
            PasswordChanged      = reader.GetBoolean(colPwdChanged),
            RemovedFromGroups    = reader.GetBoolean(colRemoved),
            AccountExpirationSet = reader.GetBoolean(colExpSet),
            ExpirationDate       = reader.IsDBNull(colExpDate)      ? null : reader.GetDateTime(colExpDate),
            DataExported         = reader.GetBoolean(colExported),
            ExportPath           = reader.IsDBNull(colExportPath)   ? null : reader.GetString(colExportPath),
            RolledBack           = reader.GetBoolean(colRolledBack),
            RolledBackAt         = reader.IsDBNull(colRolledBackAt) ? null : reader.GetDateTime(colRolledBackAt),
            RolledBackBy         = reader.IsDBNull(colRolledBackBy) ? null : reader.GetString(colRolledBackBy),
            DeletedFromDirectory = reader.GetBoolean(colDeleted),
            DeletedAt            = reader.IsDBNull(colDeletedAt)    ? null : reader.GetDateTime(colDeletedAt),
            DeletedBy            = reader.IsDBNull(colDeletedBy)    ? null : reader.GetString(colDeletedBy),
        };

        try
        {
            var preCol = reader.GetOrdinal("pre_termination_backup_id");
            record.PreTerminationBackupId = reader.IsDBNull(preCol) ? null : reader.GetInt64(preCol);
        }
        catch { }

        try
        {
            var postCol = reader.GetOrdinal("post_termination_backup_id");
            record.PostTerminationBackupId = reader.IsDBNull(postCol) ? null : reader.GetInt64(postCol);
        }
        catch { }

        try
        {
            var reasonCol = reader.GetOrdinal("termination_reason");
            record.TerminationReason = reader.IsDBNull(reasonCol) ? null : reader.GetString(reasonCol);
        }
        catch { }

        var groupsJson = reader.IsDBNull(colGroups) ? null : reader.GetString(colGroups);
        if (!string.IsNullOrEmpty(groupsJson))
            record.GroupMemberships = JsonSerializer.Deserialize<List<GroupMembershipRecord>>(groupsJson) ?? new();

        var stepsJson = reader.IsDBNull(colSteps) ? null : reader.GetString(colSteps);
        if (!string.IsNullOrEmpty(stepsJson))
            record.StepResults = JsonSerializer.Deserialize<List<TerminationStepResult>>(stepsJson) ?? new();

        return record;
    }

    // =========================================================================
    // Connection test and lifecycle
    // =========================================================================

    public async Task<(bool Success, string? Error)> TestConnectionAsync()
    {
        try
        {
            using var connection = new SqlConnection(ConnectionString);
            await connection.OpenAsync();
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    public void Dispose()
    {
        // SqlClient connections are pooled; nothing to dispose explicitly.
    }
}
