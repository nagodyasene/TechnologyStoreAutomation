using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using TechnologyStoreAutomation.backend.trendCalculator.data;

namespace TechnologyStoreAutomation.ui;

public partial class SalesEntryForm : Form
{
    private readonly IProductRepository _repository;
    private ComboBox? _cmbProduct;
    private NumericUpDown? _numQuantity;
    private DateTimePicker? _dtpSaleDate;
    private TextBox? _txtUnitPrice;
    private Label? _lblTotal;
    private Button? _btnRecord;
    private Button? _btnCancel;

    public SalesEntryForm(IProductRepository repository)
    {
        _repository = repository;
        InitializeComponent();
        SetupUI();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(450, 300);
        this.Name = "SalesEntryForm";
        this.Text = "Record Sale";
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        int yPos = 20;
        int labelWidth = 120;
        int controlLeft = labelWidth + 30;

        // Product Label & ComboBox
        var lblProduct = new Label
        {
            Text = "Product:",
            Location = new Point(20, yPos),
            Width = labelWidth
        };
        this.Controls.Add(lblProduct);

        _cmbProduct = new ComboBox
        {
            Location = new Point(controlLeft, yPos),
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbProduct.SelectedIndexChanged += CmbProduct_SelectedIndexChanged;
        this.Controls.Add(_cmbProduct);

        yPos += 40;

        // Quantity Label & NumericUpDown
        var lblQuantity = new Label
        {
            Text = "Quantity:",
            Location = new Point(20, yPos),
            Width = labelWidth
        };
        this.Controls.Add(lblQuantity);

        _numQuantity = new NumericUpDown
        {
            Location = new Point(controlLeft, yPos),
            Width = 100,
            Minimum = 1,
            Maximum = 1000,
            Value = 1
        };
        _numQuantity.ValueChanged += UpdateTotal;
        this.Controls.Add(_numQuantity);

        yPos += 40;

        // Sale Date Label & DateTimePicker
        var lblSaleDate = new Label
        {
            Text = "Sale Date:",
            Location = new Point(20, yPos),
            Width = labelWidth
        };
        this.Controls.Add(lblSaleDate);

        _dtpSaleDate = new DateTimePicker
        {
            Location = new Point(controlLeft, yPos),
            Width = 200,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today
        };
        this.Controls.Add(_dtpSaleDate);

        yPos += 40;

        // Unit Price Label & TextBox
        var lblUnitPrice = new Label
        {
            Text = "Unit Price:",
            Location = new Point(20, yPos),
            Width = labelWidth
        };
        this.Controls.Add(lblUnitPrice);

        _txtUnitPrice = new TextBox
        {
            Location = new Point(controlLeft, yPos),
            Width = 150,
            ReadOnly = true
        };
        this.Controls.Add(_txtUnitPrice);

        yPos += 40;

        // Total Label
        var lblTotalLabel = new Label
        {
            Text = "Total Amount:",
            Location = new Point(20, yPos),
            Width = labelWidth,
            Font = new Font(this.Font, FontStyle.Bold)
        };
        this.Controls.Add(lblTotalLabel);

        _lblTotal = new Label
        {
            Location = new Point(controlLeft, yPos),
            Width = 150,
            Font = new Font(this.Font, FontStyle.Bold),
            Text = "$0.00"
        };
        this.Controls.Add(_lblTotal);

        yPos += 50;

        // Buttons
        _btnRecord = new Button
        {
            Text = "Record Sale",
            Location = new Point(controlLeft, yPos),
            Width = 120,
            Height = 35
        };
        _btnRecord.Click += BtnRecord_Click;
        this.Controls.Add(_btnRecord);

        _btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(controlLeft + 130, yPos),
            Width = 100,
            Height = 35
        };
        _btnCancel.Click += (s, e) => this.DialogResult = DialogResult.Cancel;
        this.Controls.Add(_btnCancel);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await LoadProducts();
    }

    private async Task LoadProducts()
    {
        try
        {
            var products = await _repository.GetAllProductsAsync();
            var activeProducts = products.Where(p => p.LifecyclePhase == "ACTIVE" || p.LifecyclePhase == "LEGACY").ToList();

            if (_cmbProduct != null)
            {
                _cmbProduct.DataSource = activeProducts;
                _cmbProduct.DisplayMember = "Name";
                _cmbProduct.ValueMember = "Id";
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading products: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CmbProduct_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_cmbProduct?.SelectedItem is Product product && _txtUnitPrice != null)
        {
            _txtUnitPrice.Text = $"${product.UnitPrice:F2}";
            UpdateTotal(sender, e);
        }
    }

    private void UpdateTotal(object? sender, EventArgs e)
    {
        if (_cmbProduct?.SelectedItem is Product product && _numQuantity != null && _lblTotal != null)
        {
            decimal total = product.UnitPrice * _numQuantity.Value;
            _lblTotal.Text = $"${total:F2}";
        }
    }

    private async void BtnRecord_Click(object? sender, EventArgs e)
    {
        if (_cmbProduct?.SelectedItem is not Product product)
        {
            MessageBox.Show("Please select a product.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_numQuantity == null || _dtpSaleDate == null) return;

        int quantity = (int)_numQuantity.Value;
        
        // Check if enough stock
        if (quantity > product.CurrentStock)
        {
            var result = MessageBox.Show(
                $"Warning: Only {product.CurrentStock} units in stock. Record sale anyway?",
                "Low Stock",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result != DialogResult.Yes) return;
        }

        try
        {
            if (_btnRecord != null) _btnRecord.Enabled = false;

            decimal totalAmount = product.UnitPrice * quantity;
            await _repository.RecordSaleAsync(product.Id, quantity, totalAmount, _dtpSaleDate.Value);

            MessageBox.Show("Sale recorded successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error recording sale: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (_btnRecord != null) _btnRecord.Enabled = true;
        }
    }
}

