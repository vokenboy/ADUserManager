using System.Text;
using ADUserManager.Services;
using ADUserManager.Services.Models;

namespace ADUserManager.Forms;

public class MainForm : Form
{
    private readonly UserService? _adService;
    private readonly DataGridView _grid;
    private readonly TextBox _searchBox;
    private readonly ToolStrip _toolbar;
    private readonly StatusStrip _statusBar;
    private readonly ToolStripStatusLabel _statusLabel;
    private readonly ToolStripStatusLabel _countLabel;
    private List<ADUserModel> _currentUsers = new();

    public MainForm()
    {
        Text = "AD Vartotojų Valdytojas";
        Size = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = SystemIcons.Shield;

        try
        {
            _adService = new UserService();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Nepavyko prisijungti prie Active Directory:\n{ex.Message}\n\nPrograma atsidarys, bet AD funkcijos bus neprieinamos.",
                "AD Prisijungimo Klaida", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        // Toolbar
        _toolbar = new ToolStrip { Dock = DockStyle.Top };

        var refreshBtn = new ToolStripButton("Atnaujinti") { Image = null, DisplayStyle = ToolStripItemDisplayStyle.Text };
        refreshBtn.Click += (_, _) => LoadUsers();

        var exportBtn = new ToolStripButton("Eksportuoti CSV") { DisplayStyle = ToolStripItemDisplayStyle.Text };
        exportBtn.Click += OnExportCsv;

        _toolbar.Items.AddRange(new ToolStripItem[] { refreshBtn, new ToolStripSeparator(), exportBtn });
        Controls.Add(_toolbar);

        // Search panel
        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 40, Padding = new Padding(5) };
        var searchLabel = new Label { Text = "Ieškoti:", Dock = DockStyle.Left, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(0, 5, 5, 0) };
        _searchBox = new TextBox { Dock = DockStyle.Fill };
        _searchBox.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter) { LoadUsers(); e.SuppressKeyPress = true; } };
        var searchBtn = new Button { Text = "Ieškoti", Dock = DockStyle.Right, Width = 80 };
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
            new DataGridViewTextBoxColumn { Name = "DisplayName", HeaderText = "Vardas", FillWeight = 20 },
            new DataGridViewTextBoxColumn { Name = "SamAccountName", HeaderText = "Prisijungimo vardas", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "Email", HeaderText = "El. paštas", FillWeight = 20 },
            new DataGridViewTextBoxColumn { Name = "Department", HeaderText = "Skyrius", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Būsena", FillWeight = 10 },
            new DataGridViewTextBoxColumn { Name = "LastLogon", HeaderText = "Paskutinis prisijungimas", FillWeight = 15 }
        });

        Controls.Add(_grid);

        // Status bar
        _statusBar = new StatusStrip();
        _statusLabel = new ToolStripStatusLabel { Text = _adService != null ? $"Prisijungta prie: {_adService.DomainName}" : "Neprisijungta prie AD" };
        _countLabel = new ToolStripStatusLabel { Text = "Vartotojų: 0", Alignment = ToolStripItemAlignment.Right };
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
            _countLabel.Text = $"Vartotojų: {_currentUsers.Count}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Klaida įkeliant vartotojus:\n{ex.Message}", "Klaida", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            var status = !user.IsEnabled ? "Išjungtas" : user.IsLockedOut ? "Užrakintas" : "Aktyvus";
            _grid.Rows.Add(
                user.DisplayName,
                user.SamAccountName,
                user.Email,
                user.Department,
                status,
                user.LastLogon?.ToString("yyyy-MM-dd HH:mm") ?? "Niekada"
            );
        }
    }

    private void OnExportCsv(object? sender, EventArgs e)
    {
        if (_currentUsers.Count == 0)
        {
            MessageBox.Show("Nėra vartotojų eksportavimui.", "Eksportas", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SaveFileDialog
        {
            Filter = "CSV failai (*.csv)|*.csv",
            DefaultExt = "csv",
            FileName = $"AD_Vartotojai_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("Prisijungimo vardas,Veiklos vardas,Vardas,Pavardė,El. paštas,Skyrius,Pareigos,Būsena,Paskutinis prisijungimas,Slaptažodžio keitimas");
            foreach (var u in _currentUsers)
            {
                var status = !u.IsEnabled ? "Išjungtas" : u.IsLockedOut ? "Užrakintas" : "Aktyvus";
                sb.AppendLine($"\"{u.SamAccountName}\",\"{u.DisplayName}\",\"{u.FirstName}\",\"{u.LastName}\",\"{u.Email}\",\"{u.Department}\",\"{u.Title}\",\"{status}\",\"{u.LastLogon?.ToString("yyyy-MM-dd HH:mm") ?? ""}\",\"{u.PasswordLastSet?.ToString("yyyy-MM-dd HH:mm") ?? ""}\"");
            }
            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show($"Eksportuota {_currentUsers.Count} vartotojų į:\n{dialog.FileName}", "Eksportas Baigtas", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Eksportavimo klaida:\n{ex.Message}", "Klaida", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _adService?.Dispose();
        base.Dispose(disposing);
    }
}
