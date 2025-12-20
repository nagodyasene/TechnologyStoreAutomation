using System.Drawing;
using System.Windows.Forms;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Customer.Services;

using static TechnologyStore.Customer.UiConstants;

namespace TechnologyStore.Customer.Forms;

/// <summary>
/// Shopping cart form displaying cart items and totals
/// </summary>
public partial class CartForm : Form
{
    private readonly ShoppingCartService _cartService;
    private readonly ICustomerAuthService _authService;
    private readonly IOrderService _orderService;
    private readonly IEmailService _emailService;
    private readonly InvoiceGenerator _invoiceGenerator;
    private readonly ICustomerRepository _customerRepository;

    private DataGridView? _gridCart;
    private Label? _lblSubtotal;
    private Label? _lblTax;
    private Label? _lblTotal;
    private Button? _btnCheckout;
    private Button? _btnContinue;

    public CartForm(
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
        _customerRepository = customerRepository;

        InitializeComponent();
        SetupUI();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(700, 550);
        this.Name = "CartForm";
        this.Text = "Shopping Cart";
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        // Title
        var lblTitle = new Label
        {
            Text = "🛒 Your Shopping Cart",
            Location = new Point(20, 15),
            AutoSize = true,
            Font = new Font(DefaultFontFamily, 18, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 212)
        };
        this.Controls.Add(lblTitle);

        // Cart grid
        _gridCart = new DataGridView
        {
            Location = new Point(20, 60),
            Size = new Size(660, 300),
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _gridCart.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProductId", Visible = false });
        _gridCart.Columns.Add(new DataGridViewTextBoxColumn { Name = "Product", HeaderText = "Product", FillWeight = 150, ReadOnly = true });
        _gridCart.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitPrice", HeaderText = "Unit Price", FillWeight = 60, ReadOnly = true });

        var qtyColumn = new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Qty", FillWeight = 40 };
        _gridCart.Columns.Add(qtyColumn);

        _gridCart.Columns.Add(new DataGridViewTextBoxColumn { Name = "LineTotal", HeaderText = "Total", FillWeight = 60, ReadOnly = true });

        var removeColumn = new DataGridViewButtonColumn
        {
            Name = "Remove",
            HeaderText = "",
            Text = "✕",
            UseColumnTextForButtonValue = true,
            FillWeight = 30
        };
        _gridCart.Columns.Add(removeColumn);

        _gridCart.CellClick += GridCart_CellClick;
        _gridCart.CellEndEdit += GridCart_CellEndEdit;
        _gridCart.DefaultCellStyle.Font = new Font(DefaultFontFamily, 10);
        _gridCart.DefaultCellStyle.Padding = new Padding(5);
        _gridCart.ColumnHeadersDefaultCellStyle.Font = new Font(DefaultFontFamily, 10, FontStyle.Bold);
        _gridCart.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 120, 212);
        _gridCart.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _gridCart.RowTemplate.Height = 40;
        _gridCart.EnableHeadersVisualStyles = false;

        this.Controls.Add(_gridCart);

        // Totals panel
        var totalsPanel = new Panel
        {
            Location = new Point(420, 375),
            Size = new Size(260, 110),
            BackColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };
        this.Controls.Add(totalsPanel);

        _lblSubtotal = new Label
        {
            Text = "Subtotal: $0.00",
            Location = new Point(15, 15),
            Width = 230,
            Font = new Font(DefaultFontFamily, 11),
            TextAlign = ContentAlignment.MiddleRight
        };
        totalsPanel.Controls.Add(_lblSubtotal);

        _lblTax = new Label
        {
            Text = "Tax (10%): $0.00",
            Location = new Point(15, 40),
            Width = 230,
            Font = new Font(DefaultFontFamily, 11),
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleRight
        };
        totalsPanel.Controls.Add(_lblTax);

        _lblTotal = new Label
        {
            Text = "Total: $0.00",
            Location = new Point(15, 70),
            Width = 230,
            Font = new Font(DefaultFontFamily, 14, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 212),
            TextAlign = ContentAlignment.MiddleRight
        };
        totalsPanel.Controls.Add(_lblTotal);

        // Empty cart message
        var lblEmptyMessage = new Label
        {
            Name = "lblEmpty",
            Text = "Your cart is empty.\nStart shopping to add items!",
            Location = new Point(20, 375),
            Size = new Size(380, 60),
            Font = new Font(DefaultFontFamily, 11),
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleLeft
        };
        this.Controls.Add(lblEmptyMessage);

        // Buttons
        _btnContinue = new Button
        {
            Text = "← Continue Shopping",
            Location = new Point(20, 500),
            Width = 180,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(0, 120, 212),
            Font = new Font(DefaultFontFamily, 10),
            Cursor = Cursors.Hand
        };
        _btnContinue.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 212);
        _btnContinue.Click += (s, e) => this.Close();
        this.Controls.Add(_btnContinue);

        _btnCheckout = new Button
        {
            Text = "Proceed to Checkout →",
            Location = new Point(500, 500),
            Width = 180,
            Height = 40,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 167, 69),
            ForeColor = Color.White,
            Font = new Font(DefaultFontFamily, 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnCheckout.FlatAppearance.BorderSize = 0;
        _btnCheckout.Click += BtnCheckout_Click;
        this.Controls.Add(_btnCheckout);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        RefreshCart();
    }

    private void RefreshCart()
    {
        if (_gridCart == null) return;

        _gridCart.Rows.Clear();

        var items = _cartService.GetItems();
        foreach (var item in items)
        {
            _gridCart.Rows.Add(
                item.ProductId,
                item.ProductName,
                $"${item.UnitPrice:N2}",
                item.Quantity,
                $"${item.LineTotal:N2}"
            );
        }

        UpdateTotals();
        UpdateVisibility();
    }

    private void UpdateTotals()
    {
        if (_lblSubtotal != null) _lblSubtotal.Text = $"Subtotal: ${_cartService.Subtotal:N2}";
        if (_lblTax != null) _lblTax.Text = $"Tax (10%): ${_cartService.Tax:N2}";
        if (_lblTotal != null) _lblTotal.Text = $"Total: ${_cartService.Total:N2}";
    }

    private void UpdateVisibility()
    {
        var isEmpty = _cartService.IsEmpty;

        var lblEmpty = this.Controls.Find("lblEmpty", false).FirstOrDefault();
        if (lblEmpty != null) lblEmpty.Visible = isEmpty;

        if (_btnCheckout != null) _btnCheckout.Enabled = !isEmpty;
    }

    private void GridCart_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (_gridCart == null || e.RowIndex < 0) return;

        if (_gridCart.Columns[e.ColumnIndex].Name == "Remove")
        {
            var productId = (int)_gridCart.Rows[e.RowIndex].Cells["ProductId"].Value;
            _cartService.RemoveItem(productId);
            RefreshCart();
        }
    }

    private void GridCart_CellEndEdit(object? sender, DataGridViewCellEventArgs e)
    {
        if (_gridCart == null || e.RowIndex < 0) return;

        if (_gridCart.Columns[e.ColumnIndex].Name == "Quantity")
        {
            var productId = (int)_gridCart.Rows[e.RowIndex].Cells["ProductId"].Value;
            var qtyValue = _gridCart.Rows[e.RowIndex].Cells["Quantity"].Value;

            if (int.TryParse(qtyValue?.ToString(), out int newQty))
            {
                if (newQty <= 0)
                {
                    _cartService.RemoveItem(productId);
                }
                else if (!_cartService.UpdateQuantity(productId, newQty))
                {
                    MessageBox.Show("Not enough stock available.", "Stock Limit",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }

            RefreshCart();
        }
    }

    private void BtnCheckout_Click(object? sender, EventArgs e)
    {
        if (_cartService.IsEmpty)
        {
            MessageBox.Show("Your cart is empty.", "Empty Cart", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var checkoutForm = new CheckoutForm(
            _cartService, _authService, _orderService,
            _emailService, _invoiceGenerator, _customerRepository);

        if (checkoutForm.ShowDialog(this) == DialogResult.OK)
        {
            // Order placed successfully, close cart
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
