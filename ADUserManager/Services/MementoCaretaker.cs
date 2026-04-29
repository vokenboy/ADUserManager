using System.Text.Json;
using System.Text.Json.Serialization;
using ActiveManager.Services.Models;

namespace ActiveManager.Services;

/// <summary>
/// Memento pattern - Caretaker.
/// Manages the storage and retrieval of user state snapshots (mementos).
/// Supports multiple snapshots per user (history) for audit purposes.
/// Persists mementos to a JSON file alongside the application executable.
/// </summary>
public class MementoCaretaker
{
    private readonly Dictionary<string, List<UserMemento>> _mementos = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _filePath;

    public MementoCaretaker()
    {
        _filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "memento_states.json");
    }

    /// <summary>
    /// Save a memento for a user. Multiple mementos per user are supported (history).
    /// Automatically persists to file.
    /// </summary>
    public void Save(UserMemento memento)
    {
        if (!_mementos.ContainsKey(memento.SamAccountName))
        {
            _mementos[memento.SamAccountName] = new List<UserMemento>();
        }

        _mementos[memento.SamAccountName].Add(memento);
        SaveToFile();
    }

    /// <summary>
    /// Get the most recent memento for a user, or null if none exists.
    /// </summary>
    public UserMemento? GetLatest(string samAccountName)
    {
        if (_mementos.TryGetValue(samAccountName, out var history) && history.Count > 0)
        {
            return history[^1];
        }

        return null;
    }

    /// <summary>
    /// Get the full history of mementos for a user.
    /// Returns an empty list if no mementos exist.
    /// </summary>
    public IReadOnlyList<UserMemento> GetHistory(string samAccountName)
    {
        if (_mementos.TryGetValue(samAccountName, out var history))
        {
            return history.AsReadOnly();
        }

        return Array.Empty<UserMemento>();
    }

    /// <summary>
    /// Get all stored mementos across all users, ordered by capture time descending.
    /// </summary>
    public IReadOnlyList<UserMemento> GetAll()
    {
        return _mementos.Values
            .SelectMany(list => list)
            .OrderByDescending(m => m.CapturedAt)
            .ToList()
            .AsReadOnly();
    }

    /// <summary>
    /// Remove the most recent memento for a user (e.g., after successful rollback).
    /// Returns true if a memento was removed. Automatically persists to file.
    /// </summary>
    public bool RemoveLatest(string samAccountName)
    {
        if (_mementos.TryGetValue(samAccountName, out var history) && history.Count > 0)
        {
            history.RemoveAt(history.Count - 1);
            if (history.Count == 0)
            {
                _mementos.Remove(samAccountName);
            }
            SaveToFile();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Remove all mementos for a user. Automatically persists to file.
    /// </summary>
    public bool RemoveAll(string samAccountName)
    {
        if (!_mementos.Remove(samAccountName))
            return false;

        SaveToFile();
        return true;
    }

    /// <summary>
    /// Remove all mementos. Automatically persists to file.
    /// </summary>
    public void ClearAll()
    {
        _mementos.Clear();
        SaveToFile();
    }

    /// <summary>
    /// Check if a memento exists for a given user.
    /// </summary>
    public bool HasMemento(string samAccountName)
    {
        return _mementos.TryGetValue(samAccountName, out var history) && history.Count > 0;
    }

    /// <summary>
    /// Get the total count of stored mementos across all users.
    /// </summary>
    public int Count => _mementos.Values.Sum(list => list.Count);

    // =============================================
    // File Persistence
    // =============================================

    /// <summary>
    /// Save all mementos to a JSON file next to the application executable.
    /// </summary>
    private void SaveToFile()
    {
        try
        {
            var allMementos = _mementos.Values
                .SelectMany(list => list)
                .Select(m => new UserMementoDto
                {
                    SamAccountName = m.SamAccountName,
                    DisplayName = m.DisplayName,
                    DistinguishedName = m.DistinguishedName,
                    OrganizationalUnit = m.OrganizationalUnit ?? string.Empty,
                    IsEnabled = m.IsEnabled,
                    IsLockedOut = m.IsLockedOut,
                    AccountExpiration = m.AccountExpiration,
                    GroupMemberships = m.GroupMemberships.Select(g => new GroupMembershipDto
                    {
                        GroupName = g.GroupName,
                        GroupDN = g.GroupDN
                    }).ToList(),
                    CapturedAt = m.CapturedAt,
                    CapturedBy = m.CapturedBy
                })
                .ToList();

            var json = JsonSerializer.Serialize(allMementos, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            File.WriteAllText(_filePath, json, System.Text.Encoding.UTF8);
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Memento išsaugojimas į failą", ex);
        }
    }

    /// <summary>
    /// Load mementos from the JSON file. Call this once on startup.
    /// Handles missing or corrupt files gracefully.
    /// </summary>
    public void LoadFromFile()
    {
        if (!File.Exists(_filePath))
            return;

        try
        {
            var json = File.ReadAllText(_filePath, System.Text.Encoding.UTF8);
            var dtos = JsonSerializer.Deserialize<List<UserMementoDto>>(json);

            if (dtos == null) return;

            _mementos.Clear();

            foreach (var dto in dtos)
            {
                var groups = dto.GroupMemberships
                    .Select(g => new GroupMembershipRecord
                    {
                        GroupName = g.GroupName,
                        GroupDN = g.GroupDN
                    })
                    .ToList();

                var memento = new UserMemento(
                    samAccountName: dto.SamAccountName,
                    displayName: dto.DisplayName,
                    distinguishedName: dto.DistinguishedName,
                    organizationalUnit: dto.OrganizationalUnit,
                    isEnabled: dto.IsEnabled,
                    isLockedOut: dto.IsLockedOut,
                    accountExpiration: dto.AccountExpiration,
                    groupMemberships: groups,
                    capturedAt: dto.CapturedAt,
                    capturedBy: dto.CapturedBy
                );

                if (!_mementos.ContainsKey(memento.SamAccountName))
                {
                    _mementos[memento.SamAccountName] = new List<UserMemento>();
                }
                _mementos[memento.SamAccountName].Add(memento);
            }
        }
        catch (Exception ex)
        {
            ErrorLogger.Log("Memento įkėlimas iš failo", ex);
            // If the file is corrupt, start fresh
        }
    }

    // =============================================
    // DTOs for JSON serialization
    // =============================================

    private class UserMementoDto
    {
        [JsonPropertyName("samAccountName")]
        public string SamAccountName { get; set; } = string.Empty;

        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = string.Empty;

        [JsonPropertyName("distinguishedName")]
        public string DistinguishedName { get; set; } = string.Empty;

        [JsonPropertyName("organizationalUnit")]
        public string OrganizationalUnit { get; set; } = string.Empty;

        [JsonPropertyName("isEnabled")]
        public bool IsEnabled { get; set; }

        [JsonPropertyName("isLockedOut")]
        public bool IsLockedOut { get; set; }

        [JsonPropertyName("accountExpiration")]
        public DateTime? AccountExpiration { get; set; }

        [JsonPropertyName("groupMemberships")]
        public List<GroupMembershipDto> GroupMemberships { get; set; } = new();

        [JsonPropertyName("capturedAt")]
        public DateTime CapturedAt { get; set; }

        [JsonPropertyName("capturedBy")]
        public string CapturedBy { get; set; } = string.Empty;
    }

    private class GroupMembershipDto
    {
        [JsonPropertyName("groupName")]
        public string GroupName { get; set; } = string.Empty;

        [JsonPropertyName("groupDN")]
        public string GroupDN { get; set; } = string.Empty;
    }
}
