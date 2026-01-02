using TechnologyStore.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Desktop.UI.Forms;

public partial class SalesEntryForm : Form
{
    private readonly IProductRepository _repository;
    private readonly ILogger<SalesEntryForm> _logger;
    
    private ComboBox? _cmbProduct;
    private NumericUpDown? _numQuantity;
    private DateTimePicker? _dtpSaleDate;
    private TextBox? _txtUnitPrice;
    private Label? _lblTotal;
    private Button? _btnRecord;
    private Button? _btnCancel;
    
    #region Validation Constants
    
    private const int MinQuantity = 1;
    private const int MaxQuantity = 1000;
    private const int MaxFutureDays = 0; // Don't allow future dates
    private const int MaxPastDays = 30;  // Allow up to 30 days in the past
    
    #endregion

    public SalesEntryForm(IProductRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = AppLogger.CreateLogger<SalesEntryForm>();
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
            // Show full error chain including inner exceptions
            var errorMessage = GetFullErrorMessage(ex);
            _logger.LogError(ex, "Error loading products");
            MessageBox.Show($"Error loading products: {errorMessage}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    /// <summary>
    /// Gets the full error message including inner exceptions
    /// </summary>
    private static string GetFullErrorMessage(Exception ex)
    {
        var messages = new List<string>();
        var current = ex;
        while (current != null)
        {
            messages.Add(current.Message);
            current = current.InnerException;
        }
        return string.Join(" → ", messages);
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
        // Validate input
        var validationErrors = ValidateInput();
        if (validationErrors.Count > 0)
        {
            ShowValidationErrors(validationErrors);
            return;
        }

        var product = (Product)_cmbProduct!.SelectedItem!;
        int quantity = (int)_numQuantity!.Value;
        
        // Check if enough stock (warning, not error)
        if (quantity > product.CurrentStock)
        {
            var result = MessageBox.Show(
                $"Warning: Only {product.CurrentStock} units in stock. Record sale anyway?",
                "Low Stock",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            
            if (result != DialogResult.Yes) return;
        }

        await RecordSaleAsync(product, quantity);
    }

    /// <summary>
    /// Validates all form inputs and returns a list of validation errors
    /// </summary>
    private List<string> ValidateInput()
    {
        var errors = new List<string>();

        // Product validation
        if (_cmbProduct?.SelectedItem is not Product)
        {
            errors.Add("Please select a product.");
        }

        // Quantity validation
        if (_numQuantity == null)
        {
            errors.Add("Quantity control not initialized.");
        }
        else if (_numQuantity.Value < MinQuantity)
        {
            errors.Add($"Quantity must be at least {MinQuantity}.");
        }
        else if (_numQuantity.Value > MaxQuantity)
        {
            errors.Add($"Quantity cannot exceed {MaxQuantity}.");
        }

        // Date validation - only allow today's date
        if (_dtpSaleDate == null)
        {
            errors.Add("Date control not initialized.");
        }
        else
        {
            var selectedDate = _dtpSaleDate.Value.Date;
            var today = DateTime.Today;

            if (selectedDate != today)
            {
                errors.Add("Sale date must be today's date. Recording past or future dates is not allowed.");
            }
        }

        return errors;
    }

    /// <summary>
    /// Displays validation errors to the user
    /// </summary>
    private static void ShowValidationErrors(List<string> errors)
    {
        var message = string.Join(Environment.NewLine, errors.Select(e => $"• {e}"));
        MessageBox.Show(
            message,
            "Validation Errors",
            MessageBoxButtons.OK,
            MessageBoxIcon.Warning);
    }

    /// <summary>
    /// Records the sale to the database
    /// </summary>
    private async Task RecordSaleAsync(Product product, int quantity)
    {
        try
        {
            SetFormEnabled(false);
            
            _logger.LogInformation("Recording sale: Product={ProductId}, Quantity={Quantity}", 
                product.Id, quantity);

            decimal totalAmount = product.UnitPrice * quantity;
            await _repository.RecordSaleAsync(product.Id, quantity, totalAmount, _dtpSaleDate!.Value);

            _logger.LogInformation("Sale recorded successfully: Product={ProductId}, Total={Total}", 
                product.Id, totalAmount);

            MessageBox.Show("Sale recorded successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording sale for product {ProductId}", product.Id);
            MessageBox.Show($"Error recording sale: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetFormEnabled(true);
        }
    }

    /// <summary>
    /// Enables or disables form controls during processing
    /// </summary>
    private void SetFormEnabled(bool enabled)
    {
        if (_btnRecord != null) _btnRecord.Enabled = enabled;
        if (_btnCancel != null) _btnCancel.Enabled = enabled;
        if (_cmbProduct != null) _cmbProduct.Enabled = enabled;
        if (_numQuantity != null) _numQuantity.Enabled = enabled;
        if (_dtpSaleDate != null) _dtpSaleDate.Enabled = enabled;
    }
}

