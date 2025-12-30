using TechnologyStore.Desktop.Services;
using TechnologyStore.Desktop.UI;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Desktop.Features.Orders;

/// <summary>
/// Staff order management form for viewing and processing customer orders
/// </summary>
public partial class OrderManagementForm : Form
{
    private const string StatusColumnName = "Status";
    private readonly IOrderRepository _orderRepository;

    private DataGridView? _gridOrders;
    private DataGridView? _gridOrderItems;
    private ComboBox? _cboStatusFilter;
    private Button? _btnRefresh;
    private Label? _lblOrderDetails;
    private TextBox? _txtSearch;

    private Order? _selectedOrder;

    public OrderManagementForm(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));

        InitializeComponent();
        SetupUI();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(1200, 750);
        this.Name = "OrderManagementForm";
        this.Text = "Order Management";
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = Color.FromArgb(245, 247, 250);
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        // Header panel
        var headerPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.FromArgb(0, 120, 212),
            Padding = new Padding(15, 0, 15, 0)
        };
        this.Controls.Add(headerPanel);

        var lblTitle = new Label
        {
            Text = "📦 Order Management",
            Location = new Point(20, 15),
            AutoSize = true,
            Font = new Font(UiConstants.DefaultFontFamily, 18, FontStyle.Bold),
            ForeColor = Color.White
        };
        headerPanel.Controls.Add(lblTitle);

        // Filter panel
        var filterPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.White,
            Padding = new Padding(15, 10, 15, 10)
        };
        this.Controls.Add(filterPanel);

        var lblStatus = new Label
        {
            Text = "Status:",
            Location = new Point(15, 18),
            AutoSize = true,
            Font = new Font(UiConstants.DefaultFontFamily, 10)
        };
        filterPanel.Controls.Add(lblStatus);

        _cboStatusFilter = new ComboBox
        {
            Location = new Point(70, 14),
            Width = 180,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font = new Font(UiConstants.DefaultFontFamily, 10)
        };
        _cboStatusFilter.Items.AddRange(new object[] {
            "All Orders",
            OrderStatus.Pending,
            OrderStatus.Confirmed,
            OrderStatus.ReadyForPickup,
            OrderStatus.Completed,
            OrderStatus.Cancelled
        });
        _cboStatusFilter.SelectedIndex = 0;
        _cboStatusFilter.SelectedIndexChanged += (s, e) => _ = LoadOrdersAsync();
        filterPanel.Controls.Add(_cboStatusFilter);

        var lblSearch = new Label
        {
            Text = "Search:",
            Location = new Point(280, 18),
            AutoSize = true,
            Font = new Font(UiConstants.DefaultFontFamily, 10)
        };
        filterPanel.Controls.Add(lblSearch);

        _txtSearch = new TextBox
        {
            Location = new Point(340, 14),
            Width = 200,
            Font = new Font(UiConstants.DefaultFontFamily, 10),
            PlaceholderText = "Order # or customer..."
        };
        _txtSearch.TextChanged += (s, e) => _ = LoadOrdersAsync();
        filterPanel.Controls.Add(_txtSearch);

        _btnRefresh = new Button
        {
            Text = "🔄 Refresh",
            Location = new Point(560, 10),
            Width = 100,
            Height = 32,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            Font = new Font(UiConstants.DefaultFontFamily, 9),
            Cursor = Cursors.Hand
        };
        _btnRefresh.FlatAppearance.BorderSize = 0;
        _btnRefresh.Click += async (s, e) => await LoadOrdersAsync();
        filterPanel.Controls.Add(_btnRefresh);

        // Split container for orders and details
        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 350,
            BackColor = Color.FromArgb(220, 220, 220),
            Panel1MinSize = 200,
            Panel2MinSize = 150
        };
        this.Controls.Add(splitContainer);

        // Orders grid (top panel)
        _gridOrders = new DataGridView
        {
            Dock = DockStyle.Fill,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _gridOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", Visible = false });
        _gridOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "OrderNumber", HeaderText = "Order #", FillWeight = 80 });
        _gridOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "CustomerName", HeaderText = "Customer", FillWeight = 100 });
        _gridOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = StatusColumnName, HeaderText = StatusColumnName, FillWeight = 80 });
        _gridOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "ItemCount", HeaderText = "Items", FillWeight = 40 });
        _gridOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "Total", HeaderText = "Total", FillWeight = 60 });
        _gridOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "PickupDate", HeaderText = "Pickup Date", FillWeight = 80 });
        _gridOrders.Columns.Add(new DataGridViewTextBoxColumn { Name = "CreatedAt", HeaderText = "Order Date", FillWeight = 100 });

        _gridOrders.SelectionChanged += GridOrders_SelectionChanged;
        _gridOrders.CellFormatting += GridOrders_CellFormatting;

        // Style the grid
        _gridOrders.DefaultCellStyle.Font = new Font(UiConstants.DefaultFontFamily, 10);
        _gridOrders.DefaultCellStyle.Padding = new Padding(5);
        _gridOrders.ColumnHeadersDefaultCellStyle.Font = new Font(UiConstants.DefaultFontFamily, 10, FontStyle.Bold);
        _gridOrders.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(0, 120, 212);
        _gridOrders.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
        _gridOrders.RowTemplate.Height = 38;
        _gridOrders.EnableHeadersVisualStyles = false;

        splitContainer.Panel1.Controls.Add(_gridOrders);

        // Details panel (bottom panel)
        var detailsPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15)
        };
        splitContainer.Panel2.Controls.Add(detailsPanel);

        _lblOrderDetails = new Label
        {
            Text = "Select an order to view details",
            Location = new Point(15, 10),
            AutoSize = true,
            Font = new Font(UiConstants.DefaultFontFamily, 12, FontStyle.Bold),
            ForeColor = Color.FromArgb(0, 120, 212)
        };
        detailsPanel.Controls.Add(_lblOrderDetails);

        // Order items grid
        _gridOrderItems = new DataGridView
        {
            Location = new Point(15, 45),
            Size = new Size(detailsPanel.Width - 200, detailsPanel.Height - 60),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };

        _gridOrderItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "ProductName", HeaderText = "Product", FillWeight = 150 });
        _gridOrderItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Qty", FillWeight = 40 });
        _gridOrderItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "UnitPrice", HeaderText = "Unit Price", FillWeight = 60 });
        _gridOrderItems.Columns.Add(new DataGridViewTextBoxColumn { Name = "LineTotal", HeaderText = "Line Total", FillWeight = 60 });

        _gridOrderItems.DefaultCellStyle.Font = new Font(UiConstants.DefaultFontFamily, 9);
        _gridOrderItems.ColumnHeadersDefaultCellStyle.Font = new Font(UiConstants.DefaultFontFamily, 9, FontStyle.Bold);
        _gridOrderItems.RowTemplate.Height = 30;

        detailsPanel.Controls.Add(_gridOrderItems);

        // Action buttons panel
        var actionPanel = new Panel
        {
            Location = new Point(detailsPanel.Width - 170, 45),
            Size = new Size(155, detailsPanel.Height - 60),
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right
        };
        detailsPanel.Controls.Add(actionPanel);

        var lblActions = new Label
        {
            Text = "Update Status:",
            Location = new Point(0, 0),
            AutoSize = true,
            Font = new Font(UiConstants.DefaultFontFamily, 10, FontStyle.Bold)
        };
        actionPanel.Controls.Add(lblActions);

        // Status update buttons
        AddStatusButton(actionPanel, "✅ Confirm", OrderStatus.Confirmed, 30, Color.FromArgb(76, 175, 80));
        AddStatusButton(actionPanel, "📦 Ready", OrderStatus.ReadyForPickup, 75, Color.FromArgb(33, 150, 243));
        AddStatusButton(actionPanel, "✔️ Complete", OrderStatus.Completed, 120, Color.FromArgb(0, 150, 136));
        AddStatusButton(actionPanel, "❌ Cancel", OrderStatus.Cancelled, 165, Color.FromArgb(244, 67, 54));
    }

    private void AddStatusButton(Panel parent, string text, string status, int yPos, Color color)
    {
        var btn = new Button
        {
            Text = text,
            Tag = status,
            Location = new Point(0, yPos),
            Width = 140,
            Height = 35,
            FlatStyle = FlatStyle.Flat,
            BackColor = color,
            ForeColor = Color.White,
            Font = new Font(UiConstants.DefaultFontFamily, 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += BtnUpdateStatus_Click;
        parent.Controls.Add(btn);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        try
        {
            if (_gridOrders == null) return;

            _gridOrders.Rows.Clear();

            var orders = await GetFilteredOrdersAsync();

            foreach (var order in orders.OrderByDescending(o => o.CreatedAt))
            {
                AddOrderToGrid(order);
            }

            TryReselectPreviousOrder();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading orders: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task<IEnumerable<Order>> GetFilteredOrdersAsync()
    {
        var statusFilter = _cboStatusFilter?.SelectedIndex > 0
            ? _cboStatusFilter.SelectedItem?.ToString()
            : null;

        var searchText = _txtSearch?.Text?.Trim().ToLowerInvariant() ?? string.Empty;

        var orders = await _orderRepository.GetAllOrdersAsync(statusFilter);

        if (string.IsNullOrEmpty(searchText))
            return orders;

        return orders.Where(o =>
            o.OrderNumber.ToLowerInvariant().Contains(searchText) ||
            (o.Notes?.ToLowerInvariant().Contains(searchText) ?? false)
        ).ToList();
    }

    private void AddOrderToGrid(Order order)
    {
        if (_gridOrders == null) return;

        var pickupText = order.PickupDate.HasValue
            ? order.PickupDate.Value.ToString("MMM dd, yyyy")
            : "ASAP";

        _gridOrders.Rows.Add(
            order.Id,
            order.OrderNumber,
            $"Customer #{order.CustomerId}",
            order.Status,
            order.Items.Count,
            $"${order.Total:N2}",
            pickupText,
            order.CreatedAt.ToString("MMM dd, yyyy HH:mm")
        );
    }

    private void TryReselectPreviousOrder()
    {
        if (_selectedOrder == null || _gridOrders == null || _gridOrders.Rows.Count == 0)
            return;

        foreach (DataGridViewRow row in _gridOrders.Rows)
        {
            if ((int)row.Cells["Id"].Value == _selectedOrder.Id)
            {
                row.Selected = true;
                break;
            }
        }
    }

    private async void GridOrders_SelectionChanged(object? sender, EventArgs e)
    {
        if (_gridOrders == null || _gridOrders.SelectedRows.Count == 0) return;

        var orderId = (int)_gridOrders.SelectedRows[0].Cells["Id"].Value;
        await LoadOrderDetailsAsync(orderId);
    }

    private async Task LoadOrderDetailsAsync(int orderId)
    {
        try
        {
            _selectedOrder = await _orderRepository.GetByIdAsync(orderId);

            if (_selectedOrder == null || _lblOrderDetails == null || _gridOrderItems == null) return;

            _lblOrderDetails.Text = $"Order {_selectedOrder.OrderNumber} - {_selectedOrder.Status}";

            _gridOrderItems.Rows.Clear();
            foreach (var item in _selectedOrder.Items)
            {
                _gridOrderItems.Rows.Add(
                    item.ProductName,
                    item.Quantity,
                    $"${item.UnitPrice:N2}",
                    $"${item.LineTotal:N2}"
                );
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading order details: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnUpdateStatus_Click(object? sender, EventArgs e)
    {
        if (_selectedOrder == null)
        {
            MessageBox.Show("Please select an order first.", "No Order Selected",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var button = sender as Button;
        var newStatus = button?.Tag?.ToString();

        if (string.IsNullOrEmpty(newStatus)) return;

        // Validate status transition
        if (!IsValidStatusTransition(_selectedOrder.Status, newStatus))
        {
            MessageBox.Show(
                $"Cannot change status from '{_selectedOrder.Status}' to '{newStatus}'.",
                "Invalid Status Change",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        var result = MessageBox.Show(
            $"Update order {_selectedOrder.OrderNumber} status to '{newStatus}'?",
            "Confirm Status Change",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        try
        {
            await _orderRepository.UpdateStatusAsync(_selectedOrder.Id, newStatus);

            MessageBox.Show(
                $"Order {_selectedOrder.OrderNumber} has been updated to '{newStatus}'.",
                "Status Updated",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            await LoadOrdersAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error updating order status: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static bool IsValidStatusTransition(string currentStatus, string newStatus)
    {
        // Define valid transitions
        return currentStatus switch
        {
            OrderStatus.Pending => newStatus == OrderStatus.Confirmed || newStatus == OrderStatus.Cancelled,
            OrderStatus.Confirmed => newStatus == OrderStatus.ReadyForPickup || newStatus == OrderStatus.Cancelled,
            OrderStatus.ReadyForPickup => newStatus == OrderStatus.Completed || newStatus == OrderStatus.Cancelled,
            OrderStatus.Completed => false, // Cannot change from completed
            OrderStatus.Cancelled => false, // Cannot change from cancelled
            _ => false
        };
    }

    private static readonly Dictionary<string, (Color BackColor, Color ForeColor)> StatusColorMap = new()
    {
        { OrderStatus.Pending, (Color.FromArgb(255, 243, 224), Color.FromArgb(230, 126, 34)) },
        { OrderStatus.Confirmed, (Color.FromArgb(227, 242, 253), Color.FromArgb(33, 150, 243)) },
        { OrderStatus.ReadyForPickup, (Color.FromArgb(232, 245, 233), Color.FromArgb(76, 175, 80)) },
        { OrderStatus.Completed, (Color.FromArgb(224, 247, 250), Color.FromArgb(0, 150, 136)) },
        { OrderStatus.Cancelled, (Color.FromArgb(255, 235, 238), Color.FromArgb(244, 67, 54)) }
    };

    private void GridOrders_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_gridOrders == null || e.RowIndex < 0) return;
        if (_gridOrders.Columns[e.ColumnIndex].Name != StatusColumnName) return;

        var statusCell = _gridOrders.Rows[e.RowIndex].Cells[StatusColumnName];
        var status = statusCell?.Value?.ToString();

        if (status != null && StatusColorMap.TryGetValue(status, out var colors))
        {
            e.CellStyle!.BackColor = colors.BackColor;
            e.CellStyle.ForeColor = colors.ForeColor;
        }
    }
}
