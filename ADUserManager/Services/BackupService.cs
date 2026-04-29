using ActiveManager.Services.Models;
using Microsoft.Data.SqlClient;

namespace ActiveManager.Services;

public class BackupService
{
    private readonly DatabaseService _db;

    private int? _cachedCompanyId;

    public BackupService(DatabaseService db)
    {
        _db = db;
    }

    public async Task<int?> GetOrRegisterCompanyAsync()
    {
        if (_cachedCompanyId.HasValue) return _cachedCompanyId;

        await _db.EnsureInitializedAsync();
        if (!_db.IsAvailable) return null;

        var companyName = AppSettings.Instance.Database.CompanyName;

        if (string.IsNullOrWhiteSpace(companyName))
            companyName = Environment.UserDomainName;

        if (string.IsNullOrWhiteSpace(companyName)) return null;

        var domainName = string.IsNullOrWhiteSpace(AppSettings.Instance.Database.DomainName)
            ? Environment.UserDomainName
            : AppSettings.Instance.Database.DomainName;

        using var connection = new SqlConnection(AppSettings.Instance.Database.BuildConnectionString());
        await connection.OpenAsync();

        using (var selectCmd = connection.CreateCommand())
        {
            selectCmd.CommandText = "SELECT [id] FROM [companies] WHERE [company_name] = @name";
            selectCmd.Parameters.AddWithValue("@name", companyName);
            var existing = await selectCmd.ExecuteScalarAsync();

            if (existing != null && existing != DBNull.Value)
            {
                _cachedCompanyId = Convert.ToInt32(existing);
                return _cachedCompanyId;
            }
        }

        using (var insertCmd = connection.CreateCommand())
        {
            insertCmd.CommandText = @"
                INSERT INTO [companies] ([company_name], [domain_name], [domain_controller])
                OUTPUT INSERTED.id
                VALUES (@name, @domain, NULL)";
            insertCmd.Parameters.AddWithValue("@name", companyName);
            insertCmd.Parameters.AddWithValue("@domain", string.IsNullOrWhiteSpace(domainName) ? (object)DBNull.Value : domainName);
            _cachedCompanyId = Convert.ToInt32(await insertCmd.ExecuteScalarAsync());
            return _cachedCompanyId;
        }
    }

    public void ResetCache() => _cachedCompanyId = null;

    public async Task<long> CreateBackupAsync(
        ADUserModel user,
        List<GroupMembershipRecord> groups,
        DateTime? accountExpiration,
        BackupType backupType,
        OperationType operationType,
        int? terminationRecordId = null,
        string? notes = null)
    {
        await _db.EnsureInitializedAsync();
        if (!_db.IsAvailable) return -1;

        var companyId = await GetOrRegisterCompanyAsync();
        if (companyId == null) return -1;

        using var connection = new SqlConnection(AppSettings.Instance.Database.BuildConnectionString());
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();

        try
        {
            int nextVersion;
            using (var versionCmd = connection.CreateCommand())
            {
                versionCmd.Transaction = transaction;
                versionCmd.CommandText = @"
                    SELECT ISNULL(MAX([version_number]), 0) + 1
                    FROM [ad_user_backups]
                    WHERE [company_id] = @cid AND [sam_account_name] = @sam";
                versionCmd.Parameters.AddWithValue("@cid", companyId.Value);
                versionCmd.Parameters.AddWithValue("@sam", user.SamAccountName);
                nextVersion = Convert.ToInt32(await versionCmd.ExecuteScalarAsync());
            }

            using (var markCmd = connection.CreateCommand())
            {
                markCmd.Transaction = transaction;
                markCmd.CommandText = @"
                    UPDATE [ad_user_backups]
                    SET [is_latest] = 0
                    WHERE [company_id] = @cid AND [sam_account_name] = @sam AND [is_latest] = 1";
                markCmd.Parameters.AddWithValue("@cid", companyId.Value);
                markCmd.Parameters.AddWithValue("@sam", user.SamAccountName);
                await markCmd.ExecuteNonQueryAsync();
            }

            long backupId;
            using (var insertCmd = connection.CreateCommand())
            {
                insertCmd.Transaction = transaction;
                insertCmd.CommandText = @"
                    INSERT INTO [ad_user_backups] (
                        [company_id],
                        [sam_account_name], [display_name], [first_name], [last_name], [email],
                        [department], [title], [description], [distinguished_name], [organizational_unit],
                        [is_enabled], [is_locked_out], [password_last_set], [last_logon], [account_expiration],
                        [backup_type], [operation_type],
                        [created_at], [created_by],
                        [termination_record_id],
                        [version_number], [is_latest],
                        [notes]
                    )
                    OUTPUT INSERTED.id
                    VALUES (
                        @cid,
                        @sam, @display, @first, @last, @email,
                        @dept, @title, @desc, @dn, @ou,
                        @enabled, @locked, @pwdset, @logon, @expiry,
                        @btype, @otype,
                        @createdat, @createdby,
                        @termid,
                        @version, 1,
                        @notes
                    )";

                insertCmd.Parameters.AddWithValue("@cid", companyId.Value);
                insertCmd.Parameters.AddWithValue("@sam", user.SamAccountName);
                insertCmd.Parameters.AddWithValue("@display", user.DisplayName);
                insertCmd.Parameters.AddWithValue("@first", user.FirstName);
                insertCmd.Parameters.AddWithValue("@last", user.LastName);
                insertCmd.Parameters.AddWithValue("@email", user.Email);
                insertCmd.Parameters.AddWithValue("@dept", user.Department);
                insertCmd.Parameters.AddWithValue("@title", user.Title);
                insertCmd.Parameters.AddWithValue("@desc", user.Description);
                insertCmd.Parameters.AddWithValue("@dn", user.DistinguishedName);
                insertCmd.Parameters.AddWithValue("@ou", user.OrganizationalUnit);
                insertCmd.Parameters.AddWithValue("@enabled", user.IsEnabled);
                insertCmd.Parameters.AddWithValue("@locked", user.IsLockedOut);
                insertCmd.Parameters.AddWithValue("@pwdset", user.PasswordLastSet ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@logon", user.LastLogon ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@expiry", accountExpiration ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@btype", backupType.ToString());
                insertCmd.Parameters.AddWithValue("@otype", operationType.ToString());
                insertCmd.Parameters.AddWithValue("@createdat", DateTime.Now);
                insertCmd.Parameters.AddWithValue("@createdby", Environment.UserName);
                insertCmd.Parameters.AddWithValue("@termid", terminationRecordId ?? (object)DBNull.Value);
                insertCmd.Parameters.AddWithValue("@version", nextVersion);
                insertCmd.Parameters.AddWithValue("@notes", notes ?? (object)DBNull.Value);

                backupId = Convert.ToInt64(await insertCmd.ExecuteScalarAsync());
            }

            if (groups.Count > 0)
            {
                using var groupCmd = connection.CreateCommand();
                groupCmd.Transaction = transaction;
                groupCmd.CommandText = @"
                    INSERT INTO [ad_user_backup_groups] ([backup_id], [group_name], [group_dn])
                    VALUES (@bid, @gname, @gdn)";

                foreach (var group in groups)
                {
                    groupCmd.Parameters.Clear();
                    groupCmd.Parameters.AddWithValue("@bid", backupId);
                    groupCmd.Parameters.AddWithValue("@gname", group.GroupName);
                    groupCmd.Parameters.AddWithValue("@gdn", group.GroupDN);
                    await groupCmd.ExecuteNonQueryAsync();
                }
            }

            await transaction.CommitAsync();
            return backupId;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<ADUserBackup?> GetBackupByIdAsync(long backupId)
    {
        await _db.EnsureInitializedAsync();
        if (!_db.IsAvailable) return null;

        using var connection = new SqlConnection(AppSettings.Instance.Database.BuildConnectionString());
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM [ad_user_backups] WHERE [id] = @id";
        cmd.Parameters.AddWithValue("@id", backupId);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var backup = MapBackup(reader);
        await reader.CloseAsync();

        backup.Groups = await LoadGroupsAsync(connection, backupId);
        return backup;
    }

    public async Task<ADUserBackup?> GetLatestBackupAsync(string samAccountName)
    {
        await _db.EnsureInitializedAsync();
        if (!_db.IsAvailable) return null;

        var companyId = await GetOrRegisterCompanyAsync();
        if (companyId == null) return null;

        using var connection = new SqlConnection(AppSettings.Instance.Database.BuildConnectionString());
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT TOP 1 * FROM [ad_user_backups]
            WHERE [company_id] = @cid AND [sam_account_name] = @sam AND [is_latest] = 1";
        cmd.Parameters.AddWithValue("@cid", companyId.Value);
        cmd.Parameters.AddWithValue("@sam", samAccountName);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var backup = MapBackup(reader);
        await reader.CloseAsync();

        backup.Groups = await LoadGroupsAsync(connection, backup.Id);
        return backup;
    }

    public async Task<List<ADUserBackup>> GetBackupHistoryAsync(string samAccountName)
    {
        await _db.EnsureInitializedAsync();
        if (!_db.IsAvailable) return new();

        var companyId = await GetOrRegisterCompanyAsync();
        if (companyId == null) return new();

        using var connection = new SqlConnection(AppSettings.Instance.Database.BuildConnectionString());
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = @"
            SELECT * FROM [ad_user_backups]
            WHERE [company_id] = @cid AND [sam_account_name] = @sam
            ORDER BY [version_number] DESC";
        cmd.Parameters.AddWithValue("@cid", companyId.Value);
        cmd.Parameters.AddWithValue("@sam", samAccountName);

        var results = new List<ADUserBackup>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
            results.Add(MapBackup(reader));
        await reader.CloseAsync();

        foreach (var b in results)
            b.Groups = await LoadGroupsAsync(connection, b.Id);

        return results;
    }

    public async Task<List<ADUserBackup>> GetCompanyBackupsAsync(
        DateTime? since = null,
        string? searchTerm = null,
        int limit = 200)
    {
        await _db.EnsureInitializedAsync();
        if (!_db.IsAvailable) return new();

        var companyId = await GetOrRegisterCompanyAsync();
        if (companyId == null) return new();

        using var connection = new SqlConnection(AppSettings.Instance.Database.BuildConnectionString());
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();

        var where = "WHERE b.[company_id] = @cid";
        cmd.Parameters.AddWithValue("@cid", companyId.Value);

        if (since.HasValue)
        {
            where += " AND b.[created_at] >= @since";
            cmd.Parameters.AddWithValue("@since", since.Value);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            where += " AND (b.[sam_account_name] LIKE @s OR b.[display_name] LIKE @s)";
            cmd.Parameters.AddWithValue("@s", $"%{searchTerm}%");
        }

        cmd.CommandText = $@"
            SELECT b.*,
                   (SELECT COUNT(*) FROM [ad_user_backup_groups] g WHERE g.[backup_id] = b.[id]) AS [group_count]
            FROM [ad_user_backups] b
            {where}
            ORDER BY b.[created_at] DESC
            OFFSET 0 ROWS FETCH NEXT {limit} ROWS ONLY";

        var results = new List<ADUserBackup>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var backup = MapBackup(reader);
            int colGroupCount = reader.GetOrdinal("group_count");
            backup.GroupCountSnapshot = reader.GetInt32(colGroupCount);
            results.Add(backup);
        }

        return results;
    }

    public async Task LinkToTerminationRecordAsync(long backupId, int terminationRecordId)
    {
        await _db.EnsureInitializedAsync();
        if (!_db.IsAvailable) return;

        using var connection = new SqlConnection(AppSettings.Instance.Database.BuildConnectionString());
        await connection.OpenAsync();

        using var cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE [ad_user_backups] SET [termination_record_id] = @tid WHERE [id] = @id";
        cmd.Parameters.AddWithValue("@tid", terminationRecordId);
        cmd.Parameters.AddWithValue("@id", backupId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static ADUserBackup MapBackup(SqlDataReader r)
    {
        int colId         = r.GetOrdinal("id");
        int colCid        = r.GetOrdinal("company_id");
        int colSam        = r.GetOrdinal("sam_account_name");
        int colDisplay    = r.GetOrdinal("display_name");
        int colFirst      = r.GetOrdinal("first_name");
        int colLast       = r.GetOrdinal("last_name");
        int colEmail      = r.GetOrdinal("email");
        int colDept       = r.GetOrdinal("department");
        int colTitle      = r.GetOrdinal("title");
        int colDesc       = r.GetOrdinal("description");
        int colDn         = r.GetOrdinal("distinguished_name");
        int colOu         = r.GetOrdinal("organizational_unit");
        int colEnabled    = r.GetOrdinal("is_enabled");
        int colLocked     = r.GetOrdinal("is_locked_out");
        int colPwdSet     = r.GetOrdinal("password_last_set");
        int colLogon      = r.GetOrdinal("last_logon");
        int colExpiry     = r.GetOrdinal("account_expiration");
        int colBType      = r.GetOrdinal("backup_type");
        int colOType      = r.GetOrdinal("operation_type");
        int colCreatedAt  = r.GetOrdinal("created_at");
        int colCreatedBy  = r.GetOrdinal("created_by");
        int colTermId     = r.GetOrdinal("termination_record_id");
        int colVersion    = r.GetOrdinal("version_number");
        int colLatest     = r.GetOrdinal("is_latest");
        int colNotes      = r.GetOrdinal("notes");

        return new ADUserBackup
        {
            Id                  = r.GetInt64(colId),
            CompanyId           = r.GetInt32(colCid),
            SamAccountName      = r.GetString(colSam),
            DisplayName         = r.GetString(colDisplay),
            FirstName           = r.IsDBNull(colFirst)   ? "" : r.GetString(colFirst),
            LastName            = r.IsDBNull(colLast)    ? "" : r.GetString(colLast),
            Email               = r.IsDBNull(colEmail)   ? "" : r.GetString(colEmail),
            Department          = r.IsDBNull(colDept)    ? "" : r.GetString(colDept),
            Title               = r.IsDBNull(colTitle)   ? "" : r.GetString(colTitle),
            Description         = r.IsDBNull(colDesc)    ? "" : r.GetString(colDesc),
            DistinguishedName   = r.GetString(colDn),
            OrganizationalUnit  = r.IsDBNull(colOu)      ? "" : r.GetString(colOu),
            IsEnabled           = r.GetBoolean(colEnabled),
            IsLockedOut         = r.GetBoolean(colLocked),
            PasswordLastSet     = r.IsDBNull(colPwdSet)  ? null : r.GetDateTime(colPwdSet),
            LastLogon           = r.IsDBNull(colLogon)   ? null : r.GetDateTime(colLogon),
            AccountExpiration   = r.IsDBNull(colExpiry)  ? null : r.GetDateTime(colExpiry),
            BackupType          = Enum.TryParse<BackupType>(r.GetString(colBType), out var bt) ? bt : BackupType.Termination,
            OperationType       = Enum.TryParse<OperationType>(r.GetString(colOType), out var ot) ? ot : OperationType.PreTermination,
            CreatedAt           = r.GetDateTime(colCreatedAt),
            CreatedBy           = r.GetString(colCreatedBy),
            TerminationRecordId = r.IsDBNull(colTermId)  ? null : r.GetInt32(colTermId),
            VersionNumber       = r.GetInt32(colVersion),
            IsLatest            = r.GetBoolean(colLatest),
            Notes               = r.IsDBNull(colNotes)   ? null : r.GetString(colNotes),
        };
    }

    private static async Task<List<ADUserBackupGroup>> LoadGroupsAsync(
        SqlConnection connection, long backupId)
    {
        using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT * FROM [ad_user_backup_groups] WHERE [backup_id] = @bid ORDER BY [group_name]";
        cmd.Parameters.AddWithValue("@bid", backupId);

        var groups = new List<ADUserBackupGroup>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int cId      = reader.GetOrdinal("id");
            int cBid     = reader.GetOrdinal("backup_id");
            int cName    = reader.GetOrdinal("group_name");
            int cDn      = reader.GetOrdinal("group_dn");

            groups.Add(new ADUserBackupGroup
            {
                Id        = reader.GetInt64(cId),
                BackupId  = reader.GetInt64(cBid),
                GroupName = reader.GetString(cName),
                GroupDN   = reader.GetString(cDn),
            });
        }
        return groups;
    }
}