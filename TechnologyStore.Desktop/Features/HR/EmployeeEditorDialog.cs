using System.Globalization;

namespace TechnologyStore.Desktop.Features.HR;

public sealed class EmployeeEditorDialog : Form
{
    private readonly bool _isEdit;

    private TextBox _txtUsername = null!;
    private TextBox _txtFullName = null!;
    private ComboBox _cmbRole = null!;
    private CheckBox _chkActive = null!;
    private TextBox _txtEmployeeCode = null!;
    private TextBox _txtDepartment = null!;
    private DateTimePicker _dtHire = null!;
    private NumericUpDown _numLeave = null!;
    private TextBox _txtHourlyRate = null!;
    private TextBox _txtPassword = null!;

    public EmployeeManagementCreateRequest? CreateRequest { get; private set; }
    public EmployeeManagementUpdateRequest? UpdateRequest { get; private set; }

    public EmployeeEditorDialog()
    {
        _isEdit = false;
        InitializeComponent();
    }

    public EmployeeEditorDialog(EmployeeManagementRow row)
    {
        _isEdit = true;
        InitializeComponent();
        Populate(row);
    }

    private void InitializeComponent()
    {
        Text = _isEdit ? "Çalışan Düzenle" : "Yeni Çalışan";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 560;
        Height = 520;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            ColumnCount = 2,
            RowCount = 11
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int r = 0;
        AddRow(layout, r++, "Kullanıcı Adı *", out _txtUsername);
        AddRow(layout, r++, "Ad Soyad *", out _txtFullName);

        layout.Controls.Add(new Label { Text = "Rol", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, r);
        _cmbRole = new ComboBox { Dock = DockStyle.Left, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbRole.Items.AddRange(new object[] { "Çalışan", "Yönetici" });
        _cmbRole.SelectedIndex = 0;
        layout.Controls.Add(_cmbRole, 1, r++);

        layout.Controls.Add(new Label { Text = "Aktif", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, r);
        _chkActive = new CheckBox { Text = "Hesap aktif", AutoSize = true, Checked = true, Margin = new Padding(0, 6, 0, 0) };
        layout.Controls.Add(_chkActive, 1, r++);

        AddRow(layout, r++, "Çalışan Kodu *", out _txtEmployeeCode);
        AddRow(layout, r++, "Departman", out _txtDepartment);

        layout.Controls.Add(new Label { Text = "İşe Giriş Tarihi", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, r);
        _dtHire = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today };
        layout.Controls.Add(_dtHire, 1, r++);

        layout.Controls.Add(new Label { Text = "Kalan İzin (Gün)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, r);
        _numLeave = new NumericUpDown { Minimum = 0, Maximum = 365, Value = 14, Width = 120 };
        layout.Controls.Add(_numLeave, 1, r++);

        layout.Controls.Add(new Label { Text = "Saatlik Ücret", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, r);
        _txtHourlyRate = new TextBox { Dock = DockStyle.Left, Width = 180, Text = "15,00" };
        layout.Controls.Add(_txtHourlyRate, 1, r++);

        layout.Controls.Add(new Label
        {
            Text = _isEdit ? "Yeni Şifre (ops.)" : "Şifre *",
            AutoSize = true,
            Padding = new Padding(0, 6, 0, 0)
        }, 0, r);
        _txtPassword = new TextBox { Dock = DockStyle.Left, Width = 220, UseSystemPasswordChar = true };
        layout.Controls.Add(_txtPassword, 1, r++);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnOk = new Button { Text = _isEdit ? "Kaydet" : "Oluştur", Width = 110 };
        var btnCancel = new Button { Text = "İptal", Width = 110, DialogResult = DialogResult.Cancel };
        btnOk.Click += (_, _) => OnOk();
        buttonPanel.Controls.Add(btnCancel);
        buttonPanel.Controls.Add(btnOk);
        layout.Controls.Add(buttonPanel, 1, r);

        Controls.Add(layout);
        AcceptButton = btnOk;
        CancelButton = btnCancel;
    }

    private static void AddRow(TableLayoutPanel layout, int row, string label, out TextBox textBox)
    {
        layout.Controls.Add(new Label { Text = label, AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        textBox = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(textBox, 1, row);
    }

    private void Populate(EmployeeManagementRow row)
    {
        _txtUsername.Text = row.Username;
        _txtFullName.Text = row.FullName;
        _cmbRole.SelectedIndex = row.RoleText.Trim().ToUpperInvariant() == "ADMIN" ? 1 : 0;
        _chkActive.Checked = row.IsActive;
        _txtEmployeeCode.Text = row.EmployeeCode;
        _txtDepartment.Text = row.Department ?? string.Empty;
        _dtHire.Value = row.HireDate.Date == default ? DateTime.Today : row.HireDate.Date;
        _numLeave.Value = Math.Max(_numLeave.Minimum, Math.Min(_numLeave.Maximum, row.RemainingLeaveDays));
        _txtHourlyRate.Text = row.HourlyRate.ToString("0.00", CultureInfo.CurrentCulture);
    }

    private void OnOk()
    {
        var username = (_txtUsername.Text ?? string.Empty).Trim();
        var fullName = (_txtFullName.Text ?? string.Empty).Trim();
        var employeeCode = (_txtEmployeeCode.Text ?? string.Empty).Trim();
        var department = (_txtDepartment.Text ?? string.Empty).Trim();
        var password = (_txtPassword.Text ?? string.Empty);

        if (string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show("Kullanıcı adı zorunludur.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtUsername.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(fullName))
        {
            MessageBox.Show("Ad soyad zorunludur.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtFullName.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(employeeCode))
        {
            MessageBox.Show("Çalışan kodu zorunludur.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtEmployeeCode.Focus();
            return;
        }

        if (!_isEdit && string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show("Şifre zorunludur.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtPassword.Focus();
            return;
        }

        if (!string.IsNullOrWhiteSpace(password) && password.Length < 6)
        {
            MessageBox.Show("Şifre en az 6 karakter olmalıdır.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtPassword.Focus();
            return;
        }

        if (!decimal.TryParse((_txtHourlyRate.Text ?? string.Empty).Trim(), NumberStyles.Number, CultureInfo.CurrentCulture, out var hourlyRate) &&
            !decimal.TryParse((_txtHourlyRate.Text ?? string.Empty).Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out hourlyRate))
        {
            MessageBox.Show("Saatlik ücret geçerli bir sayı olmalıdır.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtHourlyRate.Focus();
            return;
        }

        if (hourlyRate <= 0)
        {
            MessageBox.Show("Saatlik ücret 0'dan büyük olmalıdır.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtHourlyRate.Focus();
            return;
        }

        var roleText = _cmbRole.SelectedIndex == 1 ? "ADMIN" : "EMPLOYEE";

        if (_isEdit)
        {
            // Caller will fill EmployeeId/UserId based on selected row
            UpdateRequest = new EmployeeManagementUpdateRequest
            {
                Username = username,
                FullName = fullName,
                RoleText = roleText,
                IsActive = _chkActive.Checked,
                NewPassword = string.IsNullOrWhiteSpace(password) ? null : password,
                EmployeeCode = employeeCode,
                Department = string.IsNullOrWhiteSpace(department) ? null : department,
                HireDate = _dtHire.Value.Date,
                RemainingLeaveDays = (int)_numLeave.Value,
                HourlyRate = hourlyRate
            };
        }
        else
        {
            CreateRequest = new EmployeeManagementCreateRequest
            {
                Username = username,
                FullName = fullName,
                Password = password,
                RoleText = roleText,
                IsActive = _chkActive.Checked,
                EmployeeCode = employeeCode,
                Department = string.IsNullOrWhiteSpace(department) ? null : department,
                HireDate = _dtHire.Value.Date,
                RemainingLeaveDays = (int)_numLeave.Value,
                HourlyRate = hourlyRate
            };
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}


