using System.IO;
using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using ActiveManager.Services.Models;

namespace ActiveManager.Services;

public class DatabaseSettings
{
    public bool Enabled { get; set; } = true;
    public string Server { get; set; } = "localhost";
    public int Port { get; set; } = 1433;
    public string Database { get; set; } = "ActiveManager";
    public string User { get; set; } = "sa";
    public string Password { get; set; } = "";

    /// <summary>
    /// Unique name identifying this company/tenant (e.g., "company1").
    /// Used to separate backups from different Windows Server instances in the shared DB.
    /// </summary>
    public string CompanyName { get; set; } = string.Empty;

    /// <summary>
    /// The AD domain name for this company (e.g., "company1.lt").
    /// Informational – stored in the companies table.
    /// </summary>
    public string DomainName { get; set; } = string.Empty;

    /// <summary>
    /// Optional raw connection string. When non-empty, overrides all individual fields above.
    /// </summary>
    public string RawConnectionString { get; set; } = string.Empty;

    public string BuildConnectionString()
    {
        if (!string.IsNullOrWhiteSpace(RawConnectionString))
            return RawConnectionString;

        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
        {
            DataSource = Port == 1433 ? Server : $"{Server},{Port}",
            InitialCatalog = Database,
            IntegratedSecurity = string.IsNullOrEmpty(User),
            UserID = string.IsNullOrEmpty(User) ? string.Empty : User,
            Password = string.IsNullOrEmpty(User) ? string.Empty : Password,
            Encrypt = false,
            TrustServerCertificate = true,
            ConnectTimeout = 30
        };
        return builder.ConnectionString;
    }
}

public class AzureADSettings
{
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string EncryptedSecret { get; set; } = string.Empty;

    /// <summary>
    /// Decrypts the stored client secret using Windows DPAPI.
    /// </summary>
    public string DecryptSecret()
    {
        if (string.IsNullOrEmpty(EncryptedSecret)) return string.Empty;
        try
        {
            var encrypted = Convert.FromBase64String(EncryptedSecret);
            var decrypted = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Encrypts a client secret using Windows DPAPI.
    /// </summary>
    public static string EncryptSecret(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }
}

public class DirectorySettings
{
    public DirectoryType DirectoryType { get; set; } = DirectoryType.OnPremisesAD;
    public AzureADSettings? AzureAD { get; set; }
}

public class EmailSettings
{
    public bool Enabled { get; set; } = false;
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 587;
    public bool UseSsl { get; set; } = true;
    public string SenderAddress { get; set; } = string.Empty;
    public string SenderName { get; set; } = "ActiveManager";
    public string Username { get; set; } = string.Empty;
    public string EncryptedPassword { get; set; } = string.Empty;

    /// <summary>
    /// Newline-separated list of recipient email addresses.
    /// </summary>
    public string Recipients { get; set; } = string.Empty;

    public bool NotifyOnTermination { get; set; } = true;
    public bool NotifyOnRollback { get; set; } = true;
    public bool NotifyOnStepFailure { get; set; } = true;

    public List<string> GetRecipientList() =>
        Recipients
            .Split(new[] { '\n', '\r', ';', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim())
            .Where(r => r.Contains('@'))
            .ToList();

    public string DecryptPassword()
    {
        if (string.IsNullOrEmpty(EncryptedPassword)) return string.Empty;
        try
        {
            var encrypted = Convert.FromBase64String(EncryptedPassword);
            var decrypted = System.Security.Cryptography.ProtectedData.Unprotect(
                encrypted, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
            return System.Text.Encoding.UTF8.GetString(decrypted);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string EncryptPassword(string plainText)
    {
        if (string.IsNullOrEmpty(plainText)) return string.Empty;
        var bytes = System.Text.Encoding.UTF8.GetBytes(plainText);
        var encrypted = System.Security.Cryptography.ProtectedData.Protect(
            bytes, null, System.Security.Cryptography.DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }
}

public class AppSettings
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ActiveManager");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "appsettings.json");

    private static AppSettings? _instance;
    private static readonly object _lock = new();

    public string Language { get; set; } = "en";
    public DatabaseSettings Database { get; set; } = new();
    public DirectorySettings Directory { get; set; } = new();
    public EmailSettings Email { get; set; } = new();

    /// <summary>
    /// Gets the singleton instance, loading from disk on first access.
    /// </summary>
    public static AppSettings Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= Load();
                }
            }
            return _instance;
        }
    }

    /// <summary>
    /// Loads settings from the JSON file, or returns defaults if file doesn't exist.
    /// </summary>
    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                // Migration: Ensure Directory settings exist for existing configs
                if (settings != null && settings.Directory == null)
                {
                    settings.Directory = new DirectorySettings
                    {
                        DirectoryType = DirectoryType.OnPremisesAD
                    };
                }

                return settings ?? new AppSettings();
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Settings load", ex);
        }

        return new AppSettings();
    }

    /// <summary>
    /// Saves current settings to the JSON file.
    /// </summary>
    public void Save()
    {
        try
        {
            System.IO.Directory.CreateDirectory(SettingsDir);
            var json = JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            File.WriteAllText(SettingsPath, json);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Settings save", ex);
            throw;
        }
    }

    /// <summary>
    /// Reloads settings from disk, replacing the current instance.
    /// </summary>
    public static void Reload()
    {
        lock (_lock)
        {
            _instance = Load();
        }
    }
}
