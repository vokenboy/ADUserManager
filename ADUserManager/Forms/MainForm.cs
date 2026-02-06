using System.Text;
using ADUserManager.Services;

namespace ADUserManager.Forms;

public class MainForm : Form
{
    private readonly ActiveDirectoryService? _adService;
    private readonly DataGridView _grid;
    private readonly TextBox _searchBox;
    private readonly ToolStrip _toolbar;
    private readonly StatusStrip _statusBar;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripStatusLabel _countLabel;
    private List<ADUserModel> _currentUsers = new();

    public MainForm()
    {
        Text = "AD User Manager";
        Size = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Shield;

        try
        {
            _adService = new ActiveDirectoryService();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not connect to Active Directory:\n{ex.Message}\n\nThe application will open but AD features will be unavailable.",
                "AD Connection Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Toolbar
        _toolbar = new ToolStrip { Dock = DockStyle.Top };

        var refreshBtn = new ToolStripButton("Refresh") { Image = null, DisplayStyle = ToolStripItemDisplayStyle.Text };
        refreshBtn.Click += (_, _) => LoadUsers();

        var exportBtn = new ToolStripButton("Export CSV") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        exportBtn.Click += OnExportCsv;

        _toolbar.Items.AddRange(new ToolStripItem[] { refreshBtn, new ToolStripSeparator(), exportBtn });
        Controls.Add(_toolbar);

        // Search panel
        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5) };
        var searchLabel = new Label { Text = "Search:", Dock = DockStyle.Left, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 5, 5, 0) };
        _searchBox = new TextBox { Dock = DockStyle.Fill };
        _searchBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { LoadUsers(); e.SuppressKeyPress = true; } };
        var searchBtn = new Button { Text = "Search", Dock = DockStyle.Right, Width = 80 };
        searchBtn.Click += (_, _) => LoadUsers();

        searchPanel.Controls.Add(_searchBox);
        searchPanel.Controls.Add(searchBtn);
        searchPanel.Controls.Add(searchLabel);
        Controls.Add(searchPanel);

        // Grid
        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            BackgroundColor = SystemColors.Window
        };

        _grid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "DisplayName", HeaderText = "Name", FillWeight = 20 },
            new DataGridViewTextBoxColumn { Name = "SamAccountName", HeaderText = "Username", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "Email", HeaderText = "Email", FillWeight = 20 },
            new DataGridViewTextBoxColumn { Name = "Department", HeaderText = "Department", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 10 },
            new DataGridViewTextBoxColumn { Name = "LastLogon", HeaderText = "Last Logon", FillWeight = 15 }
        });

        Controls.Add(_grid);

        // Status bar
        _statusBar = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel { Text = _adService != null ? $"Connected to: {_adService.DomainName}" : "Not connected to AD" };
        _countLabel = new ToolStripStatusLabel { Text = "Users: 0", Alignment = ToolStripItemAlignment.Right };
        _statusBar.Items.AddRange(new ToolStripItem[] { _statusLabel, new ToolStripStatusLabel { Spring = true }, _countLabel });
        Controls.Add(_statusBar);

        Load += (_, _) => LoadUsers();
    }

    private void LoadUsers()
    {
        if (_adService == null) return;
        try
        {
            Cursor = Cursors.WaitCursor;
            _currentUsers = _adService.SearchUsers(_searchBox.Text.Trim());
            PopulateGrid();
            _countLabel.Text = $"Users: {_currentUsers.Count}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading users:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Cursor = Cursors.Default;
        }
    }

    private void PopulateGrid()
    {
        _grid.Rows.Clear();
        foreach (var user in _currentUsers)
        {
            var status = !user.IsEnabled ? "Disabled" : user.IsLockedOut ? "Locked" : "Active";
            _grid.Rows.Add(
                user.DisplayName,
                user.SamAccountName,
                user.Email,
                user.Department,
                status,
                user.LastLogon?.ToString("yyyy-MM-dd HH:mm") ?? "Never"
            );
        }
    }

    private void OnExportCsv(object? sender, EventArgs e)
    {
        if (_currentUsers.Count == 0)
        {
            MessageBox.Show("No users to export.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv",
            DefaultExt = "csv",
            FileName = $"AD_Users_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Username,Display Name,First Name,Last Name,Email,Department,Title,Status,Last Logon,Password Last Set");
            foreach (var u in _currentUsers)
            {
                var status = !u.IsEnabled ? "Disabled" : u.IsLockedOut ? "Locked" : "Active";
                sb.AppendLine($"\"{u.SamAccountName}\",\"{u.DisplayName}\",\"{u.FirstName}\",\"{u.LastName}\",\"{u.Email}\",\"{u.Department}\",\"{u.Title}\",\"{status}\",\"{u.LastLogon?.ToString("yyyy-MM-dd HH:mm") ?? ""}\",\"{u.PasswordLastSet?.ToString("yyyy-MM-dd HH:mm") ?? ""}\"");
            }
            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show($"Exported {_currentUsers.Count} users to:\n{dialog.FileName}", "Export Complete", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _adService?.Dispose();
        base.Dispose(disposing);
    }
}
