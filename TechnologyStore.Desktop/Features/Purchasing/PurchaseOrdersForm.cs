using System.ComponentModel;
using Microsoft.Extensions.Logging;
using TechnologyStore.Desktop.Features.Auth;
using TechnologyStore.Shared.Models;
using TechnologyStore.Shared.Services;
using IPurchaseOrderService = TechnologyStore.Shared.Interfaces.IPurchaseOrderService;

namespace TechnologyStore.Desktop.Features.Purchasing;

/// <summary>
/// Form for viewing and managing purchase orders - approve, send, receive
/// </summary>
public class PurchaseOrdersForm : Form
{
    private readonly IPurchaseOrderService _purchaseOrderService;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<PurchaseOrdersForm> _logger;

    // UI Components
    private DataGridView _gridOrders = null!;
    private ComboBox _cmbStatusFilter = null!;
    private Button _btnApprove = null!;
    private Button _btnSend = null!;
    private Button _btnReceive = null!;
    private Button _btnCancel = null!;
    private Button _btnViewDetails = null!;
    private Label _lblStatus = null!;

    private readonly BindingList<PurchaseOrderViewModel> _orders = new();

    private const string ErrorTitle = "Hata";

    public PurchaseOrdersForm(IPurchaseOrderService purchaseOrderService, IAuthenticationService authService)
    {
        _purchaseOrderService = purchaseOrderService ?? throw new ArgumentNullException(nameof(purchaseOrderService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = AppLogger.CreateLogger<PurchaseOrdersForm>();

        InitializeComponent();
        _ = LoadOrdersAsync();
    }

    private void InitializeComponent()
    {
        Text = "Satın Alma Siparişleri";
        Size = new Size(1100, 650);
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(800, 500);

        // Main layout
        var mainPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(10)
        };
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Filter row
        mainPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
        mainPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize)); // Buttons

        // Filter panel
        var filterPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 10)
        };

        filterPanel.Controls.Add(new Label { Text = "Durum Filtresi:", AutoSize = true, Margin = new Padding(0, 5, 5, 0) });
        _cmbStatusFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 150
        };
        _cmbStatusFilter.Items.AddRange(new object[] { "Tümü", "Beklemede", "Onaylandı", "Gönderildi", "Teslim Alındı", "İptal" });
        _cmbStatusFilter.SelectedIndex = 0;
        _cmbStatusFilter.SelectedIndexChanged += async (s, e) => await LoadOrdersAsync();
        filterPanel.Controls.Add(_cmbStatusFilter);

        mainPanel.Controls.Add(filterPanel, 0, 0);

        // Data grid
        _gridOrders = new DataGridView
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
        _gridOrders.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { Name = "OrderNumber", DataPropertyName = "OrderNumber", HeaderText = "SAS No", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "SupplierName", DataPropertyName = "SupplierName", HeaderText = "Tedarikçi", FillWeight = 20 },
            new DataGridViewTextBoxColumn { Name = "Status", DataPropertyName = "StatusDisplay", HeaderText = "Durum", FillWeight = 12 },
            new DataGridViewTextBoxColumn { Name = "TotalAmount", DataPropertyName = "TotalAmountDisplay", HeaderText = "Toplam", FillWeight = 12 },
            new DataGridViewTextBoxColumn { Name = "ItemCount", DataPropertyName = "ItemCount", HeaderText = "Kalem", FillWeight = 8 },
            new DataGridViewTextBoxColumn { Name = "CreatedAt", DataPropertyName = "CreatedAtDisplay", HeaderText = "Oluşturma", FillWeight = 15 },
            new DataGridViewTextBoxColumn { Name = "ExpectedDelivery", DataPropertyName = "ExpectedDeliveryDisplay", HeaderText = "Beklenen Teslim", FillWeight = 15 },
        });

        _gridOrders.DataSource = _orders;
        _gridOrders.SelectionChanged += GridOrders_SelectionChanged;
        mainPanel.Controls.Add(_gridOrders, 0, 1);

        // Button panel
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true,
            Padding = new Padding(0, 10, 0, 0)
        };

        _btnApprove = CreateButton("Onayla", Color.FromArgb(46, 204, 113));
        _btnApprove.Click += BtnApprove_Click;
        buttonPanel.Controls.Add(_btnApprove);

        _btnSend = CreateButton("Tedarikçiye Gönder", Color.FromArgb(52, 152, 219));
        _btnSend.Click += BtnSend_Click;
        buttonPanel.Controls.Add(_btnSend);

        _btnReceive = CreateButton("Teslim Alındı", Color.FromArgb(155, 89, 182));
        _btnReceive.Click += BtnReceive_Click;
        buttonPanel.Controls.Add(_btnReceive);

        _btnCancel = CreateButton("İptal", Color.FromArgb(231, 76, 60));
        _btnCancel.Click += BtnCancel_Click;
        buttonPanel.Controls.Add(_btnCancel);

        _btnViewDetails = CreateButton("Detayları Gör", Color.FromArgb(52, 73, 94));
        _btnViewDetails.Click += BtnViewDetails_Click;
        buttonPanel.Controls.Add(_btnViewDetails);

        var btnRefresh = CreateButton("Yenile", Color.FromArgb(149, 165, 166));
        btnRefresh.Click += async (s, e) => await LoadOrdersAsync();
        buttonPanel.Controls.Add(btnRefresh);

        _lblStatus = new Label
        {
            AutoSize = true,
            Margin = new Padding(20, 8, 0, 0),
            ForeColor = Color.Gray
        };
        buttonPanel.Controls.Add(_lblStatus);

        mainPanel.Controls.Add(buttonPanel, 0, 2);
        Controls.Add(mainPanel);
    }

    private static Button CreateButton(string text, Color backColor)
    {
        return new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(100, 35),
            FlatStyle = FlatStyle.Flat,
            BackColor = backColor,
            ForeColor = Color.White,
            Cursor = Cursors.Hand,
            Margin = new Padding(0, 0, 10, 0)
        };
    }

    private async Task LoadOrdersAsync()
    {
        try
        {
            _lblStatus.Text = "Yükleniyor...";

            PurchaseOrderStatus? filter = _cmbStatusFilter.SelectedIndex switch
            {
                1 => PurchaseOrderStatus.Pending,
                2 => PurchaseOrderStatus.Approved,
                3 => PurchaseOrderStatus.Sent,
                4 => PurchaseOrderStatus.Received,
                5 => PurchaseOrderStatus.Cancelled,
                _ => null
            };

            var orders = await _purchaseOrderService.GetAllAsync(filter);

            _orders.Clear();
            foreach (var order in orders)
            {
                _orders.Add(new PurchaseOrderViewModel(order));
            }

            _lblStatus.Text = $"{_orders.Count} sipariş";
            UpdateButtonStates();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load purchase orders");
            _lblStatus.Text = "Siparişler yüklenemedi";
            MessageBox.Show($"Siparişler yüklenemedi: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void GridOrders_SelectionChanged(object? sender, EventArgs e)
    {
        UpdateButtonStates();
    }

    private void UpdateButtonStates()
    {
        var selected = GetSelectedOrder();

        _btnApprove.Enabled = selected?.Status == PurchaseOrderStatus.Pending;
        _btnSend.Enabled = selected?.Status == PurchaseOrderStatus.Approved;
        _btnReceive.Enabled = selected?.Status == PurchaseOrderStatus.Sent;
        _btnCancel.Enabled = selected?.Status == PurchaseOrderStatus.Pending || selected?.Status == PurchaseOrderStatus.Approved;
        _btnViewDetails.Enabled = selected != null;
    }

    private PurchaseOrderViewModel? GetSelectedOrder()
    {
        return _gridOrders.CurrentRow?.DataBoundItem as PurchaseOrderViewModel;
    }

    private async void BtnApprove_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedOrder();
        if (selected == null) return;

        var currentUser = _authService.CurrentUser;
        if (currentUser == null)
        {
            MessageBox.Show("Siparişleri onaylamak için giriş yapmalısınız.", "Giriş Gerekli", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        // Check role - Manager or Admin required
        var roleString = currentUser.Role.ToString();
        if (roleString != "Manager" && roleString != "Admin")
        {
            MessageBox.Show("Satın alma siparişlerini yalnızca Yöneticiler ve Adminler onaylayabilir.", "Erişim Reddedildi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var result = await _purchaseOrderService.ApproveAsync(selected.Id, currentUser.Id);
            if (result.Success)
            {
                _logger.LogInformation("Approved PO: {OrderNumber}", selected.OrderNumber);
                await LoadOrdersAsync();
            }
            else
            {
                MessageBox.Show(result.ErrorMessage ?? "Sipariş onaylanamadı.", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve order");
            MessageBox.Show($"Hata: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnSend_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedOrder();
        if (selected == null) return;

        var confirm = MessageBox.Show(
            $"{selected.OrderNumber} numaralı satın alma siparişi tedarikçiye gönderilsin mi?\n\nBu işlem siparişi e-posta ile tedarikçiye gönderir.",
            "Gönderimi Onayla",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            _lblStatus.Text = "E-posta gönderiliyor...";
            var result = await _purchaseOrderService.SendToSupplierAsync(selected.Id);

            if (result.Success)
            {
                _logger.LogInformation("Sent PO to supplier: {OrderNumber}", selected.OrderNumber);
                MessageBox.Show("Satın alma siparişi başarıyla gönderildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await LoadOrdersAsync();
            }
            else
            {
                MessageBox.Show(result.ErrorMessage ?? "Sipariş gönderilemedi.", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send order");
            MessageBox.Show($"Hata: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnReceive_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedOrder();
        if (selected == null) return;

        var confirm = MessageBox.Show(
            $"{selected.OrderNumber} numaralı satın alma siparişi teslim alındı olarak işaretlensin mi?",
            "Teslimi Onayla",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (confirm != DialogResult.Yes) return;

        try
        {
            var result = await _purchaseOrderService.MarkAsReceivedAsync(selected.Id);
            if (result.Success)
            {
                _logger.LogInformation("Marked PO as received: {OrderNumber}", selected.OrderNumber);
                await LoadOrdersAsync();
            }
            else
            {
                MessageBox.Show(result.ErrorMessage ?? "Teslim alındı olarak işaretlenemedi.", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark order as received");
            MessageBox.Show($"Hata: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnCancel_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedOrder();
        if (selected == null) return;

        var confirm = MessageBox.Show(
            $"{selected.OrderNumber} numaralı satın alma siparişi iptal edilsin mi?\n\nBu işlem geri alınamaz.",
            "İptali Onayla",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        try
        {
            var result = await _purchaseOrderService.CancelAsync(selected.Id);
            if (result.Success)
            {
                _logger.LogInformation("Cancelled PO: {OrderNumber}", selected.OrderNumber);
                await LoadOrdersAsync();
            }
            else
            {
                MessageBox.Show(result.ErrorMessage ?? "Sipariş iptal edilemedi.", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel order");
            MessageBox.Show($"Hata: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnViewDetails_Click(object? sender, EventArgs e)
    {
        var selected = GetSelectedOrder();
        if (selected == null) return;

        try
        {
            var order = await _purchaseOrderService.GetByIdAsync(selected.Id);
            if (order == null)
            {
                MessageBox.Show("Sipariş bulunamadı.", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            using var dialog = new PurchaseOrderDetailsDialog(order);
            dialog.ShowDialog(this);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load order details");
            MessageBox.Show($"Hata: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

/// <summary>
/// ViewModel for displaying purchase orders in the grid
/// </summary>
internal class PurchaseOrderViewModel
{
    public int Id { get; }
    public string OrderNumber { get; }
    public string SupplierName { get; }
    public PurchaseOrderStatus Status { get; }
    public string StatusDisplay => GetStatusDisplayTr(Status);
    public decimal TotalAmount { get; }
    public string TotalAmountDisplay => $"${TotalAmount:N2}";
    public int ItemCount { get; }
    public DateTime CreatedAt { get; }
    public string CreatedAtDisplay => CreatedAt.ToString("dd.MM.yyyy");
    public DateTime? ExpectedDelivery { get; }
    public string ExpectedDeliveryDisplay => ExpectedDelivery?.ToString("dd.MM.yyyy") ?? "-";

    public PurchaseOrderViewModel(PurchaseOrder order)
    {
        Id = order.Id;
        OrderNumber = order.OrderNumber;
        SupplierName = order.Supplier?.Name ?? $"Tedarikçi #{order.SupplierId}";
        Status = order.Status;
        TotalAmount = order.TotalAmount;
        ItemCount = order.Items?.Count ?? order.ItemCount;
        CreatedAt = order.CreatedAt;
        ExpectedDelivery = order.ExpectedDeliveryDate;
    }

    private static string GetStatusDisplayTr(PurchaseOrderStatus status)
    {
        return status switch
        {
            PurchaseOrderStatus.Pending => "Beklemede",
            PurchaseOrderStatus.Approved => "Onaylandı",
            PurchaseOrderStatus.Sent => "Gönderildi",
            PurchaseOrderStatus.Received => "Teslim Alındı",
            PurchaseOrderStatus.Cancelled => "İptal",
            _ => status.ToString()
        };
    }
}

/// <summary>
/// Dialog to view purchase order line items
/// </summary>
internal class PurchaseOrderDetailsDialog : Form
{
    public PurchaseOrderDetailsDialog(PurchaseOrder order)
    {
        Text = $"Satın Alma Siparişi: {order.OrderNumber}";
        Size = new Size(600, 450);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(15),
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        // Header info
        var infoPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };
        var statusTr = order.Status switch
        {
            PurchaseOrderStatus.Pending => "Beklemede",
            PurchaseOrderStatus.Approved => "Onaylandı",
            PurchaseOrderStatus.Sent => "Gönderildi",
            PurchaseOrderStatus.Received => "Teslim Alındı",
            PurchaseOrderStatus.Cancelled => "İptal",
            _ => order.Status.ToString()
        };
        infoPanel.Controls.Add(new Label { Text = $"Tedarikçi: {order.Supplier?.Name ?? "Bilinmiyor"}", AutoSize = true, Font = new Font(Font, FontStyle.Bold) });
        infoPanel.Controls.Add(new Label { Text = $"Durum: {statusTr}", AutoSize = true });
        infoPanel.Controls.Add(new Label { Text = $"Oluşturma: {order.CreatedAt:dd.MM.yyyy HH:mm}", AutoSize = true });
        if (order.ExpectedDeliveryDate.HasValue)
            infoPanel.Controls.Add(new Label { Text = $"Beklenen: {order.ExpectedDeliveryDate:dd.MM.yyyy}", AutoSize = true });
        if (!string.IsNullOrEmpty(order.Notes))
            infoPanel.Controls.Add(new Label { Text = $"Notlar: {order.Notes}", AutoSize = true, MaximumSize = new Size(550, 0) });
        layout.Controls.Add(infoPanel, 0, 0);

        // Items grid
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoGenerateColumns = false,
            AllowUserToAddRows = false,
            ReadOnly = true,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        grid.Columns.AddRange(new DataGridViewColumn[]
        {
            new DataGridViewTextBoxColumn { DataPropertyName = "ProductSku", HeaderText = "SKU", FillWeight = 15 },
            new DataGridViewTextBoxColumn { DataPropertyName = "ProductName", HeaderText = "Ürün", FillWeight = 40 },
            new DataGridViewTextBoxColumn { DataPropertyName = "Quantity", HeaderText = "Adet", FillWeight = 10 },
            new DataGridViewTextBoxColumn { DataPropertyName = "UnitCostDisplay", HeaderText = "Birim Maliyet", FillWeight = 15 },
            new DataGridViewTextBoxColumn { DataPropertyName = "LineTotalDisplay", HeaderText = "Toplam", FillWeight = 15 },
        });

        var itemVMs = order.Items.Select(i => new
        {
            i.ProductSku,
            i.ProductName,
            i.Quantity,
            UnitCostDisplay = $"${i.UnitCost:N2}",
            LineTotalDisplay = $"${i.LineTotal:N2}"
        }).ToList();
        grid.DataSource = itemVMs;
        layout.Controls.Add(grid, 0, 1);

        // Footer with total
        var footerPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.RightToLeft, AutoSize = true };
        footerPanel.Controls.Add(new Label { Text = $"Toplam: ${order.TotalAmount:N2}", AutoSize = true, Font = new Font(Font.FontFamily, 12, FontStyle.Bold) });
        var btnClose = new Button { Text = "Kapat", DialogResult = DialogResult.OK, Width = 80 };
        footerPanel.Controls.Add(btnClose);
        layout.Controls.Add(footerPanel, 0, 2);

        Controls.Add(layout);
        AcceptButton = btnClose;
    }
}
