using System.Globalization;
using System.Text.RegularExpressions;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Desktop.UI.Forms;

/// <summary>
/// Dialog for creating a new product (Turkish-only UI).
/// </summary>
public sealed class NewProductForm : Form
{
    private readonly TechnologyStore.Shared.Interfaces.IProductRepository _productRepository;
    private readonly ISupplierRepository _supplierRepository;

    private TextBox _txtName = null!;
    private TextBox _txtSku = null!;
    private TextBox _txtCategory = null!;
    private TextBox _txtUnitPrice = null!;
    private NumericUpDown _numStock = null!;
    private ComboBox _cmbPhase = null!;
    private ComboBox _cmbSupplier = null!;
    private Button _btnCreate = null!;
    private Button _btnCancel = null!;

    public Product? CreatedProduct { get; private set; }

    public NewProductForm(TechnologyStore.Shared.Interfaces.IProductRepository productRepository, ISupplierRepository supplierRepository)
    {
        _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
        _supplierRepository = supplierRepository ?? throw new ArgumentNullException(nameof(supplierRepository));

        InitializeComponent();
        _ = LoadSuppliersAsync();
    }

    private void InitializeComponent()
    {
        Text = "Yeni Ürün";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Width = 520;
        Height = 430;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            ColumnCount = 2,
            RowCount = 8
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        int row = 0;

        layout.Controls.Add(new Label { Text = "Ürün Adı *", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        _txtName = new TextBox { Dock = DockStyle.Fill, MaxLength = 200 };
        layout.Controls.Add(_txtName, 1, row++);

        layout.Controls.Add(new Label { Text = "SKU *", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        _txtSku = new TextBox { Dock = DockStyle.Fill, MaxLength = 100 };
        layout.Controls.Add(_txtSku, 1, row++);

        layout.Controls.Add(new Label { Text = "Kategori", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        _txtCategory = new TextBox { Dock = DockStyle.Fill, MaxLength = 100 };
        layout.Controls.Add(_txtCategory, 1, row++);

        layout.Controls.Add(new Label { Text = "Birim Fiyat *", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        _txtUnitPrice = new TextBox { Dock = DockStyle.Fill, PlaceholderText = "Örn: 999.99" };
        layout.Controls.Add(_txtUnitPrice, 1, row++);

        layout.Controls.Add(new Label { Text = "Başlangıç Stok", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        _numStock = new NumericUpDown { Dock = DockStyle.Left, Width = 120, Minimum = 0, Maximum = 1_000_000, Value = 0 };
        layout.Controls.Add(_numStock, 1, row++);

        layout.Controls.Add(new Label { Text = "Aşama", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        _cmbPhase = new ComboBox { Dock = DockStyle.Left, Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
        _cmbPhase.Items.AddRange(new object[] { "AKTİF", "ESKİ", "KULLANIMDIŞI" });
        _cmbPhase.SelectedIndex = 0;
        layout.Controls.Add(_cmbPhase, 1, row++);

        layout.Controls.Add(new Label { Text = "Tedarikçi (ops.)", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        _cmbSupplier = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        layout.Controls.Add(_cmbSupplier, 1, row++);

        var buttonPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft };
        _btnCreate = new Button { Text = "Oluştur", Width = 110 };
        _btnCancel = new Button { Text = "İptal", Width = 110, DialogResult = DialogResult.Cancel };
        _btnCreate.Click += BtnCreate_Click;
        buttonPanel.Controls.Add(_btnCancel);
        buttonPanel.Controls.Add(_btnCreate);
        layout.Controls.Add(buttonPanel, 1, row);

        Controls.Add(layout);

        AcceptButton = _btnCreate;
        CancelButton = _btnCancel;
    }

    private async Task LoadSuppliersAsync()
    {
        try
        {
            var suppliers = (await _supplierRepository.GetAllAsync(activeOnly: true)).ToList();
            var list = new List<SupplierOption> { new("Seçilmedi", null) };
            list.AddRange(suppliers.Select(s => new SupplierOption(s.Name, s.Id)));
            _cmbSupplier.DataSource = list;
            _cmbSupplier.DisplayMember = nameof(SupplierOption.Text);
            _cmbSupplier.ValueMember = nameof(SupplierOption.Value);
        }
        catch
        {
            // If suppliers cannot be loaded, still allow product creation without supplier.
            _cmbSupplier.DataSource = new List<SupplierOption> { new("Seçilmedi", null) };
        }
    }

    private async void BtnCreate_Click(object? sender, EventArgs e)
    {
        var name = (_txtName.Text ?? string.Empty).Trim();
        var sku = (_txtSku.Text ?? string.Empty).Trim();
        var category = (_txtCategory.Text ?? string.Empty).Trim();
        var unitPriceText = (_txtUnitPrice.Text ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show("Ürün adı zorunludur.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtName.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(sku))
        {
            MessageBox.Show("SKU zorunludur.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtSku.Focus();
            return;
        }

        // Keep SKU conservative (no spaces); DB enforces uniqueness.
        if (Regex.IsMatch(sku, "\\s"))
        {
            MessageBox.Show("SKU boşluk içeremez.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtSku.Focus();
            return;
        }

        if (!decimal.TryParse(unitPriceText, NumberStyles.Number, CultureInfo.CurrentCulture, out var unitPrice) &&
            !decimal.TryParse(unitPriceText, NumberStyles.Number, CultureInfo.InvariantCulture, out unitPrice))
        {
            MessageBox.Show("Birim fiyat geçerli bir sayı olmalıdır.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtUnitPrice.Focus();
            return;
        }

        if (unitPrice <= 0)
        {
            MessageBox.Show("Birim fiyat 0'dan büyük olmalıdır.", "Doğrulama", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtUnitPrice.Focus();
            return;
        }

        var phase = (_cmbPhase.SelectedItem?.ToString() ?? "AKTİF") switch
        {
            "AKTİF" => "ACTIVE",
            "ESKİ" => "LEGACY",
            "KULLANIMDIŞI" => "OBSOLETE",
            _ => "ACTIVE"
        };

        var supplierId = (_cmbSupplier.SelectedItem as SupplierOption)?.Value;

        try
        {
            SetEnabledState(false);

            var created = await _productRepository.CreateAsync(new Product
            {
                Name = name,
                Sku = sku,
                Category = string.IsNullOrWhiteSpace(category) ? null : category,
                UnitPrice = unitPrice,
                CurrentStock = (int)_numStock.Value,
                LifecyclePhase = phase,
                SupplierId = supplierId
            });

            CreatedProduct = created;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Ürün oluşturulamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetEnabledState(true);
        }
    }

    private void SetEnabledState(bool enabled)
    {
        _txtName.Enabled = enabled;
        _txtSku.Enabled = enabled;
        _txtCategory.Enabled = enabled;
        _txtUnitPrice.Enabled = enabled;
        _numStock.Enabled = enabled;
        _cmbPhase.Enabled = enabled;
        _cmbSupplier.Enabled = enabled;
        _btnCreate.Enabled = enabled;
        _btnCancel.Enabled = enabled;
        _btnCreate.Text = enabled ? "Oluştur" : "Oluşturuluyor...";
    }

    private sealed record SupplierOption(string Text, int? Value)
    {
        public override string ToString() => Text;
    }
}


