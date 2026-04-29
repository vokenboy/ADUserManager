using System.Text.Json;
using System.Xml.Linq;
using ActiveManager.Services;
using ActiveManager.Services.Models;

namespace ActiveManager.Tests.UnitTests;

public class ExportTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ADUserModel _testUser;
    private readonly List<GroupMembershipRecord> _testGroups;

    public ExportTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"ActiveManager_Tests_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);

        _testUser = new ADUserModel
        {
            SamAccountName = "jjonaitis",
            DisplayName = "Jonas Jonaitis",
            FirstName = "Jonas",
            LastName = "Jonaitis",
            Email = "jonas@company.lt",
            Department = "IT",
            Title = "Administratorius",
            Description = "IT darbuotojas",
            DistinguishedName = "CN=Jonas Jonaitis,OU=IT,DC=company,DC=lt",
            OrganizationalUnit = "OU=IT,DC=company,DC=lt",
            IsEnabled = true,
            IsLockedOut = false,
            PasswordLastSet = new DateTime(2025, 6, 15, 10, 30, 0),
            LastLogon = new DateTime(2026, 2, 1, 8, 0, 0)
        };

        _testGroups = new List<GroupMembershipRecord>
        {
            new() { GroupName = "IT-Admins", GroupDN = "CN=IT-Admins,OU=Groups,DC=company,DC=lt" },
            new() { GroupName = "VPN-Users", GroupDN = "CN=VPN-Users,OU=Groups,DC=company,DC=lt" }
        };
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private string GetTempFile(string extension) => Path.Combine(_tempDir, $"test.{extension}");

    // === JSON Export Tests ===

    [Fact]
    public void ExportJson_ContainsUserInfoFields()
    {
        var filePath = GetTempFile("json");
        var data = BuildExportData(includeGroups: false, includePermissions: false);

        TerminationService.ExportAsJson(data, filePath);

        var content = File.ReadAllText(filePath);
        Assert.Contains("jjonaitis", content);
        Assert.Contains("Jonas Jonaitis", content);
        Assert.Contains("jonas@company.lt", content);
        Assert.Contains("IT", content);
    }

    [Fact]
    public void ExportJson_IncludesGroups_WhenFlagged()
    {
        var filePath = GetTempFile("json");
        var data = BuildExportData(includeGroups: true, includePermissions: false);

        TerminationService.ExportAsJson(data, filePath);

        var content = File.ReadAllText(filePath);
        Assert.Contains("IT-Admins", content);
        Assert.Contains("VPN-Users", content);
        Assert.Contains("GrupiuNarystes", content);
    }

    [Fact]
    public void ExportJson_ExcludesGroups_WhenNotFlagged()
    {
        var filePath = GetTempFile("json");
        var data = BuildExportData(includeGroups: false, includePermissions: false);

        TerminationService.ExportAsJson(data, filePath);

        var content = File.ReadAllText(filePath);
        Assert.DoesNotContain("GrupiuNarystes", content);
    }

    [Fact]
    public void ExportJson_IsValidJson()
    {
        var filePath = GetTempFile("json");
        var data = BuildExportData(includeGroups: true, includePermissions: true);

        TerminationService.ExportAsJson(data, filePath);

        var content = File.ReadAllText(filePath);
        var ex = Record.Exception(() => JsonDocument.Parse(content));
        Assert.Null(ex);
    }

    // === CSV Export Tests ===

    [Fact]
    public void ExportCsv_HasSemicolonDelimiterAndHeader()
    {
        var filePath = GetTempFile("csv");

        TerminationService.ExportAsCsv(_testUser, _testGroups, filePath, includeGroups: false);

        var content = File.ReadAllText(filePath);
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Laukas;Reikšmė", lines[0].TrimEnd());
        Assert.Contains("SamAccountName;jjonaitis", content);
        Assert.Contains("DisplayName;Jonas Jonaitis", content);
    }

    [Fact]
    public void ExportCsv_IncludesGroupsSection_WhenFlagged()
    {
        var filePath = GetTempFile("csv");

        TerminationService.ExportAsCsv(_testUser, _testGroups, filePath, includeGroups: true);

        var content = File.ReadAllText(filePath);
        Assert.Contains("Grupė;Distinguished Name", content);
        Assert.Contains("IT-Admins", content);
        Assert.Contains("VPN-Users", content);
    }

    [Fact]
    public void ExportCsv_ExcludesGroupsSection_WhenNotFlagged()
    {
        var filePath = GetTempFile("csv");

        TerminationService.ExportAsCsv(_testUser, _testGroups, filePath, includeGroups: false);

        var content = File.ReadAllText(filePath);
        Assert.DoesNotContain("Grupė;Distinguished Name", content);
        Assert.DoesNotContain("IT-Admins", content);
    }

    // === XML Export Tests ===

    [Fact]
    public void ExportXml_HasCorrectRootElement()
    {
        var filePath = GetTempFile("xml");
        var data = BuildExportData(includeGroups: false, includePermissions: false);

        TerminationService.ExportAsXml(data, filePath);

        var doc = XDocument.Load(filePath);
        Assert.Equal("VartotojoEksportas", doc.Root!.Name.LocalName);
        Assert.NotNull(doc.Root.Element("VartotojoInfo"));
        Assert.NotNull(doc.Root.Element("EksportoData"));
    }

    [Fact]
    public void ExportXml_IncludesGroupElements_WhenFlagged()
    {
        var filePath = GetTempFile("xml");
        var data = BuildExportData(includeGroups: true, includePermissions: false);

        TerminationService.ExportAsXml(data, filePath);

        var doc = XDocument.Load(filePath);
        var groupsElement = doc.Root!.Element("GrupiuNarystes");
        Assert.NotNull(groupsElement);
        var groups = groupsElement!.Elements("Grupe").ToList();
        Assert.Equal(2, groups.Count);
        Assert.Equal("IT-Admins", groups[0].Element("Pavadinimas")!.Value);
    }

    // === UTF-8 / Lithuanian character tests ===

    [Fact]
    public void ExportCsv_PreservesLithuanianCharacters()
    {
        var user = new ADUserModel
        {
            SamAccountName = "oonaite",
            DisplayName = "Ona Onaitė",
            FirstName = "Ona",
            LastName = "Onaitė",
            Email = "ona@company.lt",
            Department = "Žmogiškieji ištekliai",
            Title = "Vadovė",
            Description = "Šiaulių skyrius",
            DistinguishedName = "CN=Ona Onaitė,OU=HR,DC=company,DC=lt",
            OrganizationalUnit = "OU=HR,DC=company,DC=lt"
        };
        var filePath = GetTempFile("csv");

        TerminationService.ExportAsCsv(user, new List<GroupMembershipRecord>(), filePath, false);

        var content = File.ReadAllText(filePath);
        Assert.Contains("Onaitė", content);
        Assert.Contains("Žmogiškieji ištekliai", content);
        Assert.Contains("Vadovė", content);
        Assert.Contains("Šiaulių skyrius", content);
    }

    // === Helper ===

    private Dictionary<string, object> BuildExportData(bool includeGroups, bool includePermissions)
    {
        var data = new Dictionary<string, object>
        {
            ["VartotojoInfo"] = new
            {
                _testUser.SamAccountName,
                _testUser.DisplayName,
                _testUser.FirstName,
                _testUser.LastName,
                _testUser.Email,
                _testUser.Department,
                _testUser.Title,
                _testUser.Description,
                _testUser.DistinguishedName,
                _testUser.OrganizationalUnit,
                _testUser.IsEnabled,
                _testUser.IsLockedOut,
                PasswordLastSet = _testUser.PasswordLastSet?.ToString("yyyy-MM-dd HH:mm:ss"),
                LastLogon = _testUser.LastLogon?.ToString("yyyy-MM-dd HH:mm:ss")
            },
            ["EksportoData"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            ["EksportavoVartotojas"] = Environment.UserName
        };

        if (includeGroups)
        {
            data["GrupiuNarystes"] = _testGroups.Select(g => new
            {
                g.GroupName,
                g.GroupDN
            }).ToList();
        }

        if (includePermissions)
        {
            data["Teises"] = _testGroups.Select(g => g.GroupName).ToList();
        }

        return data;
    }
}
