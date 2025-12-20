using System.Drawing;
using System.Windows.Forms;
using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Customer.Services;
using CustomerModel = TechnologyStore.Shared.Models.Customer;

using static TechnologyStore.Customer.UiConstants;

namespace TechnologyStore.Customer.Forms;

/// <summary>
/// Checkout form for finalizing orders
/// </summary>
public partial class CheckoutForm : Form
{
    private readonly ShoppingCartService _cartService;
    private readonly ICustomerAuthService _authService;
    private readonly IOrderService _orderService;
    private readonly IEmailService _emailService;
    private readonly InvoiceGenerator _invoiceGenerator;

    // Guest info fields
    private TextBox? _txtGuestEmail;
    private TextBox? _txtGuestName;
    private TextBox? _txtGuestPhone;

    // Order fields
    private TextBox? _txtNotes;
    private DateTimePicker? _dtpPickupDate;
    private CheckBox? _chkSpecifyDate;

    private Button? _btnPlaceOrder;
    private Label? _lblError;

    public CheckoutForm(
        ShoppingCartService cartService,
        ICustomerAuthService authService,
        IOrderService orderService,
        IEmailService emailService,
        InvoiceGenerator invoiceGenerator,
        ICustomerRepository customerRepository)
    {
        _cartService = cartService;
        _authService = authService;
        _orderService = orderService;
        _emailService = emailService;
        _invoiceGenerator = invoiceGenerator;

        InitializeComponent();
        SetupUI();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(550, 650);
        this.Name = "CheckoutForm";
        this.Text = "Checkout";
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = Color.White;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        int yPos = 20;
        int leftMargin = 30;
        int fieldWidth = 490;

        // Title
        var lblTitle = new Label
        {
            Text = "📋 Checkout",
            Location = new Point(leftMargin, yPos),
            AutoSize = true,
            Font = new Font(DefaultFontFamily, 18, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 212)
        };
        this.Controls.Add(lblTitle);

        yPos += 50;

        // Order Summary
        var lblSummary = new Label
        {
            Text = "Order Summary",
            Location = new Point(leftMargin, yPos),
            Width = fieldWidth,
            Font = new Font(DefaultFontFamily, 12, FontStyle.Bold)
        };
        this.Controls.Add(lblSummary);

        yPos += 30;

        var summaryPanel = new Panel
        {
            Location = new Point(leftMargin, yPos),
            Size = new Size(fieldWidth, 80),
            BackColor = Color.FromArgb(248, 249, 250),
            BorderStyle = BorderStyle.FixedSingle
        };
        this.Controls.Add(summaryPanel);

        var itemsText = $"{_cartService.UniqueItemCount} item(s), {_cartService.ItemCount} unit(s)";

        var lblItems = new Label
        {
            Text = itemsText,
            Location = new Point(15, 15),
            Width = 300,
            Font = new Font(DefaultFontFamily, 10)
        };
        summaryPanel.Controls.Add(lblItems);

        var lblTotal = new Label
        {
            Text = $"Total: ${_cartService.Total:N2}",
            Location = new Point(15, 45),
            Width = 300,
            Font = new Font(DefaultFontFamily, 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 212)
        };
        summaryPanel.Controls.Add(lblTotal);

        yPos += 100;

        // Guest info section (only shown for guests)
        if (_authService.IsGuest || _authService.CurrentCustomer == null)
        {
            var lblGuestInfo = new Label
            {
                Text = "Your Information",
                Location = new Point(leftMargin, yPos),
                Width = fieldWidth,
                Font = new Font(DefaultFontFamily, 12, FontStyle.Bold)
            };
            this.Controls.Add(lblGuestInfo);

            yPos += 30;

            // Email
            AddField("Email Address *", ref yPos, leftMargin, fieldWidth, out _txtGuestEmail, false);

            // Name
            AddField("Full Name *", ref yPos, leftMargin, fieldWidth, out _txtGuestName, false);

            // Phone
            AddField("Phone (optional)", ref yPos, leftMargin, fieldWidth, out _txtGuestPhone, false);
        }
        else
        {
            // Show logged-in customer info
            var lblCustomerInfo = new Label
            {
                Text = "Billing Information",
                Location = new Point(leftMargin, yPos),
                Width = fieldWidth,
                Font = new Font(DefaultFontFamily, 12, FontStyle.Bold)
            };
            this.Controls.Add(lblCustomerInfo);

            yPos += 30;

            var customerPanel = new Panel
            {
                Location = new Point(leftMargin, yPos),
                Size = new Size(fieldWidth, 60),
                BackColor = Color.FromArgb(248, 249, 250),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(customerPanel);

            var customer = _authService.CurrentCustomer;
            var lblName = new Label
            {
                Text = $"👤 {customer.FullName}",
                Location = new Point(15, 12),
                AutoSize = true,
                Font = new Font(DefaultFontFamily, 10)
            };
            customerPanel.Controls.Add(lblName);

            var lblEmail = new Label
            {
                Text = $"✉️ {customer.Email}",
                Location = new Point(15, 35),
                AutoSize = true,
                Font = new Font(DefaultFontFamily, 10),
                ForeColor = Color.Gray
            };
            customerPanel.Controls.Add(lblEmail);

            yPos += 80;
        }

        // Pickup date section
        var lblPickup = new Label
        {
            Text = "Pickup Details",
            Location = new Point(leftMargin, yPos),
            Width = fieldWidth,
            Font = new Font(DefaultFontFamily, 12, FontStyle.Bold)
        };
        this.Controls.Add(lblPickup);

        yPos += 30;

        _chkSpecifyDate = new CheckBox
        {
            Text = "Specify a preferred pickup date",
            Location = new Point(leftMargin, yPos),
            Width = fieldWidth,
            Font = new Font(DefaultFontFamily, 10)
        };
        _chkSpecifyDate.CheckedChanged += (s, e) =>
        {
            if (_dtpPickupDate != null)
                _dtpPickupDate.Enabled = _chkSpecifyDate.Checked;
        };
        this.Controls.Add(_chkSpecifyDate);

        yPos += 30;

        _dtpPickupDate = new DateTimePicker
        {
            Location = new Point(leftMargin, yPos),
            Width = 200,
            Font = new Font(DefaultFontFamily, 10),
            MinDate = DateTime.Today.AddDays(1),
            Enabled = false
        };
        this.Controls.Add(_dtpPickupDate);

        yPos += 45;

        // Notes
        var lblNotes = new Label
        {
            Text = "Order Notes (optional)",
            Location = new Point(leftMargin, yPos),
            Width = fieldWidth,
            Font = new Font(DefaultFontFamily, 10, FontStyle.Bold)
        };
        this.Controls.Add(lblNotes);

        yPos += 25;

        _txtNotes = new TextBox
        {
            Location = new Point(leftMargin, yPos),
            Size = new Size(fieldWidth, 60),
            Multiline = true,
            Font = new Font(DefaultFontFamily, 10),
            PlaceholderText = "Any special requests or instructions..."
        };
        this.Controls.Add(_txtNotes);

        yPos += 75;

        // Error label
        _lblError = new Label
        {
            Location = new Point(leftMargin, yPos),
            Width = fieldWidth,
            Height = 40,
            ForeColor = Color.Red,
            Font = new Font(DefaultFontFamily, 9),
            Visible = false
        };
        this.Controls.Add(_lblError);

        yPos += 45;

        // Place order button
        _btnPlaceOrder = new Button
        {
            Text = "🛍️ Place Order",
            Location = new Point(leftMargin, yPos),
            Width = fieldWidth,
            Height = 45,
            BackColor = Color.FromArgb(40, 167, 69),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(DefaultFontFamily, 12, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnPlaceOrder.FlatAppearance.BorderSize = 0;
        _btnPlaceOrder.Click += BtnPlaceOrder_Click;
        this.Controls.Add(_btnPlaceOrder);

        yPos += 55;

        // Cancel button
        var btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(leftMargin, yPos),
            Width = 100,
            Height = 35,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.Gray,
            Font = new Font(DefaultFontFamily, 10),
            Cursor = Cursors.Hand
        };
        btnCancel.FlatAppearance.BorderColor = Color.Gray;
        btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
        this.Controls.Add(btnCancel);
    }

    private void AddField(string label, ref int yPos, int leftMargin, int width, out TextBox textBox, bool isPassword)
    {
        var lbl = new Label
        {
            Text = label,
            Location = new Point(leftMargin, yPos),
            Width = width,
            Font = new Font(DefaultFontFamily, 9, FontStyle.Bold)
        };
        this.Controls.Add(lbl);

        yPos += 22;

        textBox = new TextBox
        {
            Location = new Point(leftMargin, yPos),
            Width = width,
            Height = 28,
            Font = new Font(DefaultFontFamily, 10)
        };

        if (isPassword)
        {
            textBox.PasswordChar = '●';
        }

        this.Controls.Add(textBox);
        yPos += 40;
    }

    private async void BtnPlaceOrder_Click(object? sender, EventArgs e)
    {
        ClearError();

        var customer = await GetOrValidateCustomerAsync();
        if (customer == null) return;

        SetFormEnabled(false);

        try
        {
            var notes = _txtNotes?.Text?.Trim();
            DateTime? pickupDate = _chkSpecifyDate?.Checked == true ? _dtpPickupDate?.Value : null;

            var cartItems = _cartService.GetItems().ToList();
            var result = await _orderService.PlaceOrderAsync(customer.Id, cartItems, notes, pickupDate);

            if (result.Success && result.Order != null)
            {
                await HandleOrderSuccessAsync(result.Order, customer);
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Failed to place order.");
            }
        }
        catch (Exception ex)
        {
            ShowError($"An error occurred: {ex.Message}");
        }
        finally
        {
            SetFormEnabled(true);
        }
    }

    private async Task<CustomerModel?> GetOrValidateCustomerAsync()
    {
        var customer = _authService.CurrentCustomer;

        if (customer != null && !_authService.IsGuest)
            return customer;

        // Guest checkout validation
        var email = _txtGuestEmail?.Text?.Trim() ?? string.Empty;
        var name = _txtGuestName?.Text?.Trim() ?? string.Empty;
        var phone = _txtGuestPhone?.Text?.Trim();

        if (!ValidateGuestInfo(email, name))
            return null;

        try
        {
            return await _authService.GetOrCreateGuestAsync(email, name, phone);
        }
        catch (Exception ex)
        {
            ShowError(ex.Message);
            return null;
        }
    }

    private bool ValidateGuestInfo(string email, string name)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError("Please enter your email address.");
            _txtGuestEmail?.Focus();
            return false;
        }

        if (!IsValidEmail(email))
        {
            ShowError("Please enter a valid email address.");
            _txtGuestEmail?.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowError("Please enter your full name.");
            _txtGuestName?.Focus();
            return false;
        }

        return true;
    }

    private async Task HandleOrderSuccessAsync(Order order, CustomerModel customer)
    {
        await TrySendInvoiceEmailAsync(order, customer);

        _cartService.Clear();

        using var confirmForm = new OrderConfirmationForm(order, customer);
        confirmForm.ShowDialog(this);

        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    private async Task TrySendInvoiceEmailAsync(Order order, CustomerModel customer)
    {
        try
        {
            var invoiceHtml = _invoiceGenerator.GenerateInvoice(order, customer);
            var subject = _invoiceGenerator.GenerateSubject(order);
            await _emailService.SendEmailAsync(customer.Email, subject, invoiceHtml);
        }
        catch (Exception emailEx)
        {
            Console.WriteLine($"Failed to send invoice email: {emailEx.Message}");
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }

    private void ShowError(string message)
    {
        if (_lblError != null)
        {
            _lblError.Text = message;
            _lblError.Visible = true;
        }
    }

    private void ClearError()
    {
        if (_lblError != null)
        {
            _lblError.Text = string.Empty;
            _lblError.Visible = false;
        }
    }

    private void SetFormEnabled(bool enabled)
    {
        if (_txtGuestEmail != null) _txtGuestEmail.Enabled = enabled;
        if (_txtGuestName != null) _txtGuestName.Enabled = enabled;
        if (_txtGuestPhone != null) _txtGuestPhone.Enabled = enabled;
        if (_txtNotes != null) _txtNotes.Enabled = enabled;
        if (_chkSpecifyDate != null) _chkSpecifyDate.Enabled = enabled;
        if (_dtpPickupDate != null && _chkSpecifyDate?.Checked == true) _dtpPickupDate.Enabled = enabled;
        if (_btnPlaceOrder != null)
        {
            _btnPlaceOrder.Enabled = enabled;
            _btnPlaceOrder.Text = enabled ? "🛍️ Place Order" : "Processing...";
        }
    }
}
