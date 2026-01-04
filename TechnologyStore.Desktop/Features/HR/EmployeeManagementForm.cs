using TechnologyStore.Desktop.Features.Auth;

namespace TechnologyStore.Desktop.Features.HR;

public sealed class EmployeeManagementForm : Form
{
    private readonly EmployeeManagementRepository _repo;
    private readonly IAuthenticationService _authService;

    private DataGridView _grid = null!;
    private Label _lblStatus = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnRefresh = null!;

    public EmployeeManagementForm(string connectionString, IAuthenticationService authService)
    {
        _repo = new EmployeeManagementRepository(connectionString);
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));

        InitializeComponent();
        _ = LoadEmployeesAsync();
    }

    private void InitializeComponent()
    {
        Text = "Çalışan Yönetimi";
        StartPosition = FormStartPosition.CenterParent;
        Width = 1100;
        Height = 650;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var header = new Label
        {
            Text = "Çalışan Yönetimi",
            Dock = DockStyle.Top,
            Font = new Font(Font.FontFamily, 14, FontStyle.Bold),
            Padding = new Padding(10),
            AutoSize = true
        };
        root.Controls.Add(header, 0, 0);

        _grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AutoGenerateColumns = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ad Soyad", DataPropertyName = nameof(EmployeeManagementRow.FullName), FillWeight = 140 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kullanıcı Adı", DataPropertyName = nameof(EmployeeManagementRow.Username), FillWeight = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Rol", DataPropertyName = nameof(EmployeeManagementRow.RoleText), FillWeight = 60 });
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { HeaderText = "Aktif", DataPropertyName = nameof(EmployeeManagementRow.IsActive), FillWeight = 40 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Çalışan Kodu", DataPropertyName = nameof(EmployeeManagementRow.EmployeeCode), FillWeight = 70 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Departman", DataPropertyName = nameof(EmployeeManagementRow.Department), FillWeight = 80 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "İşe Giriş", DataPropertyName = nameof(EmployeeManagementRow.HireDate), FillWeight = 70, DefaultCellStyle = new DataGridViewCellStyle { Format = "d" } });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Kalan İzin", DataPropertyName = nameof(EmployeeManagementRow.RemainingLeaveDays), FillWeight = 55 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Saatlik Ücret", DataPropertyName = nameof(EmployeeManagementRow.HourlyRate), FillWeight = 60, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });

        _grid.SelectionChanged += (_, _) => UpdateButtons();
        _grid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex >= 0) await EditSelectedAsync();
        };

        root.Controls.Add(_grid, 0, 1);

        var footer = new Panel { Dock = DockStyle.Fill, Height = 55, Padding = new Padding(10) };
        _btnAdd = new Button { Text = "Yeni Çalışan", Width = 140, Left = 10, Top = 10 };
        _btnEdit = new Button { Text = "Düzenle", Width = 110, Left = 160, Top = 10 };
        _btnDelete = new Button { Text = "Sil", Width = 110, Left = 280, Top = 10 };
        _btnRefresh = new Button { Text = "Yenile", Width = 110, Left = 400, Top = 10 };
        _lblStatus = new Label { Text = "Hazır", AutoSize = true, Left = 530, Top = 15 };

        _btnAdd.Click += async (_, _) => await AddAsync();
        _btnEdit.Click += async (_, _) => await EditSelectedAsync();
        _btnDelete.Click += async (_, _) => await DeleteSelectedAsync();
        _btnRefresh.Click += async (_, _) => await LoadEmployeesAsync();

        footer.Controls.AddRange(new Control[] { _btnAdd, _btnEdit, _btnDelete, _btnRefresh, _lblStatus });
        root.Controls.Add(footer, 0, 2);

        Controls.Add(root);

        // Security: this is an admin operation screen
        if (!_authService.IsAdmin)
        {
            _btnAdd.Enabled = false;
            _btnEdit.Enabled = false;
            _btnDelete.Enabled = false;
            _lblStatus.Text = "Bu ekrana yalnızca yöneticiler erişebilir.";
        }
        else
        {
            UpdateButtons();
        }
    }

    private void UpdateButtons()
    {
        var hasSelection = _grid.SelectedRows.Count > 0;
        _btnEdit.Enabled = _authService.IsAdmin && hasSelection;
        _btnDelete.Enabled = _authService.IsAdmin && hasSelection;
        _btnAdd.Enabled = _authService.IsAdmin;
    }

    private EmployeeManagementRow? GetSelected()
        => _grid.SelectedRows.Count > 0 ? _grid.SelectedRows[0].DataBoundItem as EmployeeManagementRow : null;

    private async Task LoadEmployeesAsync()
    {
        try
        {
            _lblStatus.Text = "Yükleniyor...";
            var rows = await _repo.GetAllAsync();
            _grid.DataSource = rows;
            _lblStatus.Text = $"Toplam: {rows.Count}";
            UpdateButtons();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "Hata";
            MessageBox.Show($"Çalışanlar yüklenirken hata oluştu: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task AddAsync()
    {
        if (!_authService.IsAdmin)
        {
            MessageBox.Show("Bu işlem için yönetici yetkisi gerekir.", "Erişim Reddedildi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        using var dialog = new EmployeeEditorDialog();
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.CreateRequest == null) return;

        try
        {
            _lblStatus.Text = "Oluşturuluyor...";
            await _repo.CreateAsync(dialog.CreateRequest);
            _lblStatus.Text = "Oluşturuldu";
            await LoadEmployeesAsync();
            MessageBox.Show("Çalışan başarıyla oluşturuldu.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "Hata";
            MessageBox.Show($"Çalışan oluşturulamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task EditSelectedAsync()
    {
        if (!_authService.IsAdmin)
        {
            MessageBox.Show("Bu işlem için yönetici yetkisi gerekir.", "Erişim Reddedildi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selected = GetSelected();
        if (selected == null)
        {
            MessageBox.Show("Lütfen bir çalışan seçin.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new EmployeeEditorDialog(selected);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.UpdateRequest == null) return;

        try
        {
            _lblStatus.Text = "Kaydediliyor...";
            dialog.UpdateRequest.EmployeeId = selected.EmployeeId;
            dialog.UpdateRequest.UserId = selected.UserId;
            var ok = await _repo.UpdateAsync(dialog.UpdateRequest);
            _lblStatus.Text = ok ? "Kaydedildi" : "Güncellenemedi";
            await LoadEmployeesAsync();
            if (ok)
                MessageBox.Show("Çalışan başarıyla güncellendi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show("Çalışan güncellenemedi.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "Hata";
            MessageBox.Show($"Çalışan güncellenemedi: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task DeleteSelectedAsync()
    {
        if (!_authService.IsAdmin)
        {
            MessageBox.Show("Bu işlem için yönetici yetkisi gerekir.", "Erişim Reddedildi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var selected = GetSelected();
        if (selected == null)
        {
            MessageBox.Show("Lütfen bir çalışan seçin.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var confirm = MessageBox.Show(
            $"Seçili çalışan silinsin mi?\n\n{selected.FullName} ({selected.Username})\n\nNot: Bu işlem bazı kayıtları (örn. izin talepleri) etkileyebilir.",
            "Onay",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        try
        {
            _lblStatus.Text = "Siliniyor...";
            var result = await _repo.DeleteAsync(selected.EmployeeId);
            await LoadEmployeesAsync();

            if (result == EmployeeDeleteResult.Deleted)
            {
                MessageBox.Show("Çalışan silindi.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (result == EmployeeDeleteResult.Deactivated)
            {
                MessageBox.Show("Silme işlemi yapılamadı (bağlı kayıtlar var). Hesap pasif hale getirildi.", "Bilgi",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else if (result == EmployeeDeleteResult.NotFound)
            {
                MessageBox.Show("Çalışan bulunamadı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show("Çalışan silinemedi.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = "Hata";
            MessageBox.Show($"Çalışan silinemedi: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}


