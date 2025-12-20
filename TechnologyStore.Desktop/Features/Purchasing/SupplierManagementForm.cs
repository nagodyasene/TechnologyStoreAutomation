using System.ComponentModel;
using Microsoft.Extensions.Logging;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Services;

namespace TechnologyStore.Desktop.Features.Purchasing;

/// <summary>
/// Form for managing suppliers - CRUD operations
/// </summary>
public class SupplierManagementForm : Form
{
    private readonly ISupplierRepository _supplierRepository;
    private readonly ILogger<SupplierManagementForm> _logger;

    // UI Components
    private DataGridView _gridSuppliers = null!;
    private Button _btnAdd = null!;
    private Button _btnEdit = null!;
    private Button _btnDelete = null!;
    private Button _btnRefresh = null!;
    private Label _lblStatus = null!;

    private readonly BindingList<Supplier> _suppliers = new();

    public SupplierManagementForm(ISupplierRepository supplierRepository)
    {
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));
        _logger = AppLogger.CreateLogger<SupplierManagementForm>();

        InitializeComponent();
        LoadSuppliersAsync();
    }

    private void InitializeComponent()
    {
        Text = "Supplier Management";
        Size = new Size(900, 600);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(700, 400);

        // Main layout
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(10)
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Data grid
        _gridSuppliers = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            ReadOnly = true,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.Fixed3D,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        // Columns
        _gridSuppliers.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "Id", DataPropertyName = "Id", HeaderText = "ID", Width = 50, FillWeight = 10 },
            new DataGridViewTextBoxColumn { Name = "Name", DataPropertyName = "Name", HeaderText = "Name", FillWeight = 25 },
            new DataGridViewTextBoxColumn { Name = "Email", DataPropertyName = "Email", HeaderText = "Email", FillWeight = 25 },
            new DataGridViewTextBoxColumn { Name = "Phone", DataPropertyName = "Phone", HeaderText = "Phone", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "ContactPerson", DataPropertyName = "ContactPerson", HeaderText = "Contact", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "LeadTimeDays", DataPropertyName = "LeadTimeDays", HeaderText = "Lead Time", Width = 80, FillWeight = 10 },
        });

        _gridSuppliers.DataSource = _suppliers;
        mainPanel.Controls.Add(_gridSuppliers, 0, 0);

        // Button panel
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        _btnAdd = CreateButton("➕ Add Supplier", Color.FromArgb(46, 204, 113));
        _btnAdd.Click += BtnAdd_Click;
        buttonPanel.Controls.Add(_btnAdd);

        _btnEdit = CreateButton("✏️ Edit", Color.FromArgb(52, 152, 219));
        _btnEdit.Click += BtnEdit_Click;
        buttonPanel.Controls.Add(_btnEdit);

        _btnDelete = CreateButton("🗑️ Delete", Color.FromArgb(231, 76, 60));
        _btnDelete.Click += BtnDelete_Click;
        buttonPanel.Controls.Add(_btnDelete);

        _btnRefresh = CreateButton("🔄 Refresh", Color.FromArgb(149, 165, 166));
        _btnRefresh.Click += async (s, e) => await LoadSuppliersAsync();
        buttonPanel.Controls.Add(_btnRefresh);

        _lblStatus = new Label
        {
            AutoSize = true,
            Margin = new Padding(20, 8, 0, 0),
            ForeColor = Color.Gray
        };
        buttonPanel.Controls.Add(_lblStatus);

        mainPanel.Controls.Add(buttonPanel, 0, 1);
        Controls.Add(mainPanel);
    }

    private static Button CreateButton(string text, Color backColor)
    {
        return new Button
        {
            Text = text,
            Size = new Size(120, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 0)
        };
    }

    private async Task LoadSuppliersAsync()
    {
        try
        {
            _lblStatus.Text = "Loading...";
            var suppliers = await _supplierRepository.GetAllAsync(activeOnly: false);

            _suppliers.Clear();
            foreach (var supplier in suppliers)
            {
                _suppliers.Add(supplier);
            }

            _lblStatus.Text = $"{_suppliers.Count} suppliers";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load suppliers");
            _lblStatus.Text = "Error loading suppliers";
            MessageBox.Show($"Failed to load suppliers: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnAdd_Click(object? sender, EventArgs e)
    {
        using var dialog = new SupplierEditDialog(null);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Supplier != null)
        {
            SaveSupplierAsync(dialog.Supplier, isNew: true);
        }
    }

    private void BtnEdit_Click(object? sender, EventArgs e)
    {
        if (_gridSuppliers.CurrentRow?.DataBoundItem is not Supplier selected)
        {
            MessageBox.Show("Please select a supplier to edit.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new SupplierEditDialog(selected);
        if (dialog.ShowDialog(this) == DialogResult.OK && dialog.Supplier != null)
        {
            SaveSupplierAsync(dialog.Supplier, isNew: false);
        }
    }

    private async void SaveSupplierAsync(Supplier supplier, bool isNew)
    {
        try
        {
            if (isNew)
            {
                await _supplierRepository.CreateAsync(supplier);
                _logger.LogInformation("Created supplier: {Name}", supplier.Name);
            }
            else
            {
                await _supplierRepository.UpdateAsync(supplier);
                _logger.LogInformation("Updated supplier: {Name}", supplier.Name);
            }
            await LoadSuppliersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save supplier");
            MessageBox.Show($"Failed to save supplier: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnDelete_Click(object? sender, EventArgs e)
    {
        if (_gridSuppliers.CurrentRow?.DataBoundItem is not Supplier selected)
        {
            MessageBox.Show("Please select a supplier to delete.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Are you sure you want to delete '{selected.Name}'?\n\nThis will deactivate the supplier (soft delete).",
            "Confirm Delete",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            try
            {
                await _supplierRepository.DeleteAsync(selected.Id);
                _logger.LogInformation("Deleted supplier: {Name}", selected.Name);
                await LoadSuppliersAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to delete supplier");
                MessageBox.Show($"Failed to delete supplier: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}

/// <summary>
/// Dialog for adding/editing a supplier
/// </summary>
internal class SupplierEditDialog : Form
{
    public Supplier? Supplier { get; private set; }

    private TextBox _txtName = null!;
    private TextBox _txtEmail = null!;
    private TextBox _txtPhone = null!;
    private TextBox _txtContact = null!;
    private TextBox _txtAddress = null!;
    private NumericUpDown _numLeadTime = null!;
    private CheckBox _chkActive = null!;

    public SupplierEditDialog(Supplier? existing)
    {
        Supplier = existing;
        InitializeComponent();
        if (existing != null) PopulateFields(existing);
    }

    private void InitializeComponent()
    {
        Text = Supplier == null ? "Add Supplier" : "Edit Supplier";
        Size = new Size(400, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            ColumnCount = 2,
            RowCount = 8
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        layout.Controls.Add(new Label { Text = "Name *", AutoSize = true }, 0, row);
        _txtName = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_txtName, 1, row++);

        layout.Controls.Add(new Label { Text = "Email *", AutoSize = true }, 0, row);
        _txtEmail = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_txtEmail, 1, row++);

        layout.Controls.Add(new Label { Text = "Phone", AutoSize = true }, 0, row);
        _txtPhone = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_txtPhone, 1, row++);

        layout.Controls.Add(new Label { Text = "Contact", AutoSize = true }, 0, row);
        _txtContact = new TextBox { Dock = DockStyle.Fill };
        layout.Controls.Add(_txtContact, 1, row++);

        layout.Controls.Add(new Label { Text = "Address", AutoSize = true }, 0, row);
        _txtAddress = new TextBox { Dock = DockStyle.Fill, Multiline = true, Height = 60 };
        layout.Controls.Add(_txtAddress, 1, row++);

        layout.Controls.Add(new Label { Text = "Lead Time", AutoSize = true }, 0, row);
        _numLeadTime = new NumericUpDown { Minimum = 1, Maximum = 90, Value = 7, Width = 60 };
        layout.Controls.Add(_numLeadTime, 1, row++);

        layout.Controls.Add(new Label { Text = "Active", AutoSize = true }, 0, row);
        _chkActive = new CheckBox { Checked = true };
        layout.Controls.Add(_chkActive, 1, row++);

        // Buttons
        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        var btnCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 80 };
        var btnSave = new Button { Text = "Save", Width = 80 };
        btnSave.Click += BtnSave_Click;
        buttonPanel.Controls.Add(btnCancel);
        buttonPanel.Controls.Add(btnSave);
        layout.Controls.Add(buttonPanel, 1, row);

        Controls.Add(layout);
        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    private void PopulateFields(Supplier s)
    {
        _txtName.Text = s.Name;
        _txtEmail.Text = s.Email;
        _txtPhone.Text = s.Phone;
        _txtContact.Text = s.ContactPerson;
        _txtAddress.Text = s.Address;
        _numLeadTime.Value = s.LeadTimeDays;
        _chkActive.Checked = s.IsActive;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtName.Text))
        {
            MessageBox.Show("Name is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtEmail.Text) || !_txtEmail.Text.Contains('@'))
        {
            MessageBox.Show("Valid email is required.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtEmail.Focus();
            return;
        }

        Supplier = new Supplier
        {
            Id = Supplier?.Id ?? 0,
            Name = _txtName.Text.Trim(),
            Email = _txtEmail.Text.Trim(),
            Phone = string.IsNullOrWhiteSpace(_txtPhone.Text) ? null : _txtPhone.Text.Trim(),
            ContactPerson = string.IsNullOrWhiteSpace(_txtContact.Text) ? null : _txtContact.Text.Trim(),
            Address = string.IsNullOrWhiteSpace(_txtAddress.Text) ? null : _txtAddress.Text.Trim(),
            LeadTimeDays = (int)_numLeadTime.Value,
            IsActive = _chkActive.Checked
        };

        DialogResult = DialogResult.OK;
        Close();
    }
}
