using System.Drawing;
using System.Windows.Forms;
using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Customer.Services;

namespace TechnologyStore.Customer.Forms;

/// <summary>
/// Main product catalog form for browsing and adding products to cart
/// </summary>
public partial class CatalogForm : Form
{
    private readonly IProductRepository _productRepository;
    private readonly ICustomerAuthService _authService;
    private readonly ShoppingCartService _cartService;
    private readonly IOrderService _orderService;
    private readonly IEmailService _emailService;
    private readonly InvoiceGenerator _invoiceGenerator;
    private readonly ICustomerRepository _customerRepository;
    
    private DataGridView? _gridProducts;
    private TextBox? _txtSearch;
    private ComboBox? _cboCategory;
    private Button? _btnViewCart;
    private Label? _lblCartCount;
    private Label? _lblWelcome;
    private Button? _btnLogout;
    
    private List<Product> _allProducts = new();

    public CatalogForm(
        IProductRepository productRepository,
        ICustomerAuthService authService,
        ShoppingCartService cartService,
        IOrderService orderService,
        IEmailService emailService,
        InvoiceGenerator invoiceGenerator,
        ICustomerRepository customerRepository)
    {
        _productRepository = productRepository;
        _authService = authService;
        _cartService = cartService;
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
        this.ClientSize = new Size(1100, 700);
        this.Name = "CatalogForm";
        this.Text = "Technology Store - Product Catalog";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        // Header panel
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(0, 120, 212),
            Padding = new Padding(20, 0, 20, 0)
        };
        this.Controls.Add(headerPanel);

        // Logo
        var lblLogo = new Label
        {
            Text = "🛒 Technology Store",
            Location = new Point(20, 15),
            AutoSize = true,
            Font = new Font("Segoe UI", 18, FontStyle.Bold),
            ForeColor = Color.White
        };
        headerPanel.Controls.Add(lblLogo);

        // Welcome label
        _lblWelcome = new Label
        {
            Location = new Point(300, 25),
            AutoSize = true,
            Font = new Font("Segoe UI", 10),
            ForeColor = Color.White
        };
        headerPanel.Controls.Add(_lblWelcome);

        // Logout button
        _btnLogout = new Button
        {
            Text = "Logout",
            Location = new Point(this.ClientSize.Width - 100, 20),
            Width = 80,
            Height = 30,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.Transparent,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnLogout.FlatAppearance.BorderColor = Color.White;
        _btnLogout.Click += BtnLogout_Click;
        headerPanel.Controls.Add(_btnLogout);

        // Search/Filter panel
        var filterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };
        this.Controls.Add(filterPanel);

        var lblSearch = new Label
        {
            Text = "Search:",
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 10)
        };
        filterPanel.Controls.Add(lblSearch);

        _txtSearch = new TextBox
        {
            Location = new Point(80, 17),
            Width = 250,
            Height = 28,
            Font = new Font("Segoe UI", 10),
            PlaceholderText = "Search products..."
        };
        _txtSearch.TextChanged += (s, e) => FilterProducts();
        filterPanel.Controls.Add(_txtSearch);

        var lblCategory = new Label
        {
            Text = "Category:",
            Location = new Point(360, 20),
            AutoSize = true,
            Font = new Font("Segoe UI", 10)
        };
        filterPanel.Controls.Add(lblCategory);

        _cboCategory = new ComboBox
        {
            Location = new Point(435, 17),
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font("Segoe UI", 10)
        };
        _cboCategory.SelectedIndexChanged += (s, e) => FilterProducts();
        filterPanel.Controls.Add(_cboCategory);

        // Cart button
        _btnViewCart = new Button
        {
            Text = "🛒 View Cart",
            Location = new Point(this.ClientSize.Width - 180, 12),
            Width = 150,
            Height = 36,
            BackColor = Color.FromArgb(40, 167, 69),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        _btnViewCart.FlatAppearance.BorderSize = 0;
        _btnViewCart.Click += BtnViewCart_Click;
        filterPanel.Controls.Add(_btnViewCart);

        _lblCartCount = new Label
        {
            Location = new Point(this.ClientSize.Width - 30, 12),
            Width = 25,
            Height = 25,
            BackColor = Color.Red,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Text = "0",
            Visible = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        filterPanel.Controls.Add(_lblCartCount);

        // Products grid
        _gridProducts = new DataGridView
        {
            Location = new Point(20, 150),
            Size = new Size(this.ClientSize.Width - 40, this.ClientSize.Height - 170),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AllowUserToResizeRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        // Configure columns
        _gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID", Width = 50, Visible = false });
        _gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Product Name", FillWeight = 150 });
        _gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Category", HeaderText = "Category", FillWeight = 80 });
        _gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Price", HeaderText = "Price", FillWeight = 60 });
        _gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Stock", HeaderText = "In Stock", FillWeight = 50 });
        _gridProducts.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Status", FillWeight = 60 });
        
        var btnColumn = new DataGridViewButtonColumn
        {
            Name = "AddToCart",
            HeaderText = "",
            Text = "Add to Cart",
            UseColumnTextForButtonValue = true,
            FillWeight = 60
        };
        _gridProducts.Columns.Add(btnColumn);

        _gridProducts.CellClick += GridProducts_CellClick;
        _gridProducts.CellFormatting += GridProducts_CellFormatting;

        // Style the grid
        _gridProducts.DefaultCellStyle.Font = new Font("Segoe UI", 10);
        _gridProducts.DefaultCellStyle.Padding = new Padding(5);
        _gridProducts.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
        _gridProducts.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 120, 212);
        _gridProducts.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _gridProducts.RowTemplate.Height = 45;
        _gridProducts.EnableHeadersVisualStyles = false;

        this.Controls.Add(_gridProducts);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        UpdateWelcomeMessage();
        await LoadProductsAsync();
    }

    private void UpdateWelcomeMessage()
    {
        if (_lblWelcome != null)
        {
            if (_authService.CurrentCustomer != null)
            {
                var name = _authService.CurrentCustomer.FullName;
                var prefix = _authService.IsGuest ? "Guest: " : "Welcome, ";
                _lblWelcome.Text = $"{prefix}{name}";
            }
            else
            {
                _lblWelcome.Text = "Welcome, Guest";
            }
        }
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            _allProducts = (await _productRepository.GetAvailableProductsAsync()).ToList();
            
            // Populate category filter
            var categories = _allProducts.Select(p => p.Category ?? "Other").Distinct().OrderBy(c => c).ToList();
            categories.Insert(0, "All Categories");
            
            _cboCategory?.Items.Clear();
            foreach (var cat in categories)
            {
                _cboCategory?.Items.Add(cat);
            }
            if (_cboCategory != null && _cboCategory.Items.Count > 0)
            {
                _cboCategory.SelectedIndex = 0;
            }

            FilterProducts();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to load products: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void FilterProducts()
    {
        var searchText = _txtSearch?.Text?.ToLowerInvariant() ?? string.Empty;
        var selectedCategory = _cboCategory?.SelectedItem?.ToString() ?? "All Categories";

        var filtered = _allProducts.Where(p =>
        {
            var matchesSearch = string.IsNullOrEmpty(searchText) ||
                               p.Name.ToLowerInvariant().Contains(searchText) ||
                               p.Sku.ToLowerInvariant().Contains(searchText);
            
            var matchesCategory = selectedCategory == "All Categories" ||
                                 (p.Category ?? "Other") == selectedCategory;

            return matchesSearch && matchesCategory;
        }).ToList();

        DisplayProducts(filtered);
    }

    private void DisplayProducts(List<Product> products)
    {
        if (_gridProducts == null) return;

        _gridProducts.Rows.Clear();

        foreach (var product in products)
        {
            var stockText = product.CurrentStock > 0 
                ? product.CurrentStock.ToString() 
                : "Out of Stock";
            
            var status = product.LifecyclePhase == "LEGACY" ? "🏷️ Clearance" : "✅ Available";
            
            _gridProducts.Rows.Add(
                product.Id,
                product.Name,
                product.Category ?? "Other",
                $"${product.UnitPrice:N2}",
                stockText,
                status
            );
        }
    }

    private void GridProducts_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_gridProducts == null || e.RowIndex < 0) return;

        var stockCell = _gridProducts.Rows[e.RowIndex].Cells["Stock"];
        if (stockCell?.Value?.ToString() == "Out of Stock")
        {
            _gridProducts.Rows[e.RowIndex].DefaultCellStyle.ForeColor = Color.Gray;
        }
    }

    private async void GridProducts_CellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (_gridProducts == null || e.RowIndex < 0) return;

        if (_gridProducts.Columns[e.ColumnIndex].Name == "AddToCart")
        {
            var productId = (int)_gridProducts.Rows[e.RowIndex].Cells["Id"].Value;
            var productName = _gridProducts.Rows[e.RowIndex].Cells["Name"].Value?.ToString() ?? "Product";
            var stockText = _gridProducts.Rows[e.RowIndex].Cells["Stock"].Value?.ToString();

            if (stockText == "Out of Stock")
            {
                MessageBox.Show("This product is currently out of stock.", "Out of Stock", 
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var added = await _cartService.AddItemAsync(productId, 1);
            if (added)
            {
                UpdateCartBadge();
                // Brief visual feedback
                _gridProducts.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.LightGreen;
                await Task.Delay(200);
                _gridProducts.Rows[e.RowIndex].DefaultCellStyle.BackColor = Color.White;
            }
            else
            {
                MessageBox.Show($"Cannot add more '{productName}' - not enough stock.", "Stock Limit", 
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    private void UpdateCartBadge()
    {
        if (_lblCartCount == null || _btnViewCart == null) return;

        var count = _cartService.ItemCount;
        _lblCartCount.Text = count.ToString();
        _lblCartCount.Visible = count > 0;
        _btnViewCart.Text = count > 0 ? $"🛒 View Cart ({count})" : "🛒 View Cart";
    }

    private void BtnViewCart_Click(object? sender, EventArgs e)
    {
        using var cartForm = new CartForm(_cartService, _authService, _orderService, 
            _emailService, _invoiceGenerator, _customerRepository);
        cartForm.ShowDialog(this);
        UpdateCartBadge();
    }

    private void BtnLogout_Click(object? sender, EventArgs e)
    {
        if (_cartService.ItemCount > 0)
        {
            var result = MessageBox.Show(
                "You have items in your cart. Are you sure you want to logout?",
                "Confirm Logout",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);
            
            if (result != DialogResult.Yes) return;
        }

        _authService.Logout();
        _cartService.Clear();
        this.DialogResult = DialogResult.Cancel;
        this.Close();
    }
}
