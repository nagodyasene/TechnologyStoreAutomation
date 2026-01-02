using TechnologyStore.Desktop.Config;
using TechnologyStore.Desktop.Services;
using TechnologyStore.Desktop.Features.Auth;
using TechnologyStore.Desktop.Features.Leave;
using TechnologyStore.Desktop.Features.Reporting;
using TechnologyStore.Desktop.Features.Orders;
using TechnologyStore.Desktop.Features.Purchasing;
using TechnologyStore.Shared.Models;
using TechnologyStore.Desktop.Features.TimeTracking;
using TechnologyStore.Desktop.Features.TimeTracking.Forms;
using TechnologyStore.Desktop.Features.Payroll.Forms;
using TechnologyStore.Desktop.Features.Payroll;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Desktop.UI.Forms;
using IOrderRepository = TechnologyStore.Shared.Interfaces.IOrderRepository;
using ISupplierRepository = TechnologyStore.Shared.Interfaces.ISupplierRepository;
using IPurchaseOrderService = TechnologyStore.Shared.Interfaces.IPurchaseOrderService;
using Timer = System.Windows.Forms.Timer;
// Resolve ambiguities favoring Desktop versions
using IAuthenticationService = TechnologyStore.Desktop.Features.Auth.IAuthenticationService;
using IUserRepository = TechnologyStore.Desktop.Features.Auth.IUserRepository;

namespace TechnologyStore.Desktop
{
    public partial class MainForm : Form
    {
        private readonly TechnologyStore.Shared.Interfaces.IProductRepository _repository;
        private readonly IHealthCheckService _healthCheckService;
        private readonly IAuthenticationService _authService;
        private readonly ILeaveRepository _leaveRepository;
        private readonly ISalesReportService _salesReportService;
        private readonly IOrderRepository _orderRepository;
        private readonly ISupplierRepository _supplierRepository;
        private readonly IPurchaseOrderService _purchaseOrderService;
        private readonly EmailSettings _emailSettings;
        private readonly UiSettings _uiSettings;
        private readonly ApplicationSettings _appSettings;
        private readonly ITimeTrackingService _timeTrackingService;
        private readonly IWorkShiftRepository _workShiftRepository;
        private readonly IUserRepository _userRepository;
        private readonly IPayrollService _payrollService;
        private readonly Timer _refreshTimer;
        private DataGridView? _gridInventory;
        private Label? _lblStatus;
        private MenuStrip? _menuStrip;

        private const string ErrorTitle = "Error";

        /// <summary>
        /// Creates a new MainForm with injected dependencies
        /// </summary>
        /// <param name="deps">Aggregated dependencies for MainForm</param>
        public MainForm(MainFormDependencies deps)
        {
            if (deps == null) throw new ArgumentNullException(nameof(deps));

            _repository = deps.Repository;
            _healthCheckService = deps.HealthCheckService;
            _authService = deps.AuthService;
            _leaveRepository = deps.LeaveRepository;
            _salesReportService = deps.SalesReportService;
            _orderRepository = deps.OrderRepository;
            _supplierRepository = deps.SupplierRepository;
            _purchaseOrderService = deps.PurchaseOrderService;
            _emailSettings = deps.EmailSettings;
            _uiSettings = deps.UiSettings;
            _appSettings = deps.AppSettings;

            _timeTrackingService = deps.TimeTrackingService;
            _workShiftRepository = deps.WorkShiftRepository;
            _userRepository = deps.UserRepository;
            _payrollService = deps.PayrollService;

            InitializeComponent();
            SetupDynamicUi();

            // Initialize refresh timer from configuration
            _refreshTimer = new Timer();
            _refreshTimer.Interval = _uiSettings.RefreshIntervalMs;
            _refreshTimer.Tick += OnRefreshTimerTick;
            _refreshTimer.Start();
        }

        /// <summary>
        /// Safe async void event handler for timer tick - wraps async call with proper exception handling
        /// </summary>
        private async void OnRefreshTimerTick(object? sender, EventArgs e)
        {
            try
            {
                await LoadDashboardData();
            }
            catch (Exception ex)
            {
                // Log the exception and update status
                GlobalExceptionHandler.ReportException(ex, "Dashboard Auto-Refresh");
                UpdateStatusBar($"Refresh failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Safe async void event handler for refresh button click
        /// </summary>
        private async void OnRefreshButtonClick(object? sender, EventArgs e)
        {
            try
            {
                await LoadDashboardData();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Manual Dashboard Refresh");
                MessageBox.Show($"Refresh failed: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void SetupDynamicUi()
        {
            this.Size = new Size(_uiSettings.WindowWidth, _uiSettings.WindowHeight);
            this.Text = _appSettings.Name;

            // Status Bar at the bottom
            _lblStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = _uiSettings.StatusBarHeight,
                Text = GetStatusBarText(),
                BackColor = Color.FromArgb(240, 240, 240),
                Padding = new Padding(5, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft
            };
            this.Controls.Add(_lblStatus);

            // Menu Strip
            _menuStrip = new MenuStrip
            {
                BackColor = Color.FromArgb(240, 240, 240),
                RenderMode = ToolStripRenderMode.Professional
            };

            // === FILE MENU ===
            var fileMenu = new ToolStripMenuItem("File");
            fileMenu.DropDownItems.Add(CreateMenuItem("Settings", "Ctrl+,", BtnSettings_Click));
            fileMenu.DropDownItems.Add(new ToolStripSeparator());
            fileMenu.DropDownItems.Add(CreateMenuItem("Logout", "Ctrl+L", BtnLogout_Click));
            fileMenu.DropDownItems.Add(CreateMenuItem("Exit", "Alt+F4", (s, e) => Application.Exit()));
            _menuStrip.Items.Add(fileMenu);

            // === VIEW MENU ===
            var viewMenu = new ToolStripMenuItem("View");
            viewMenu.DropDownItems.Add(CreateMenuItem("Refresh", "F5", OnRefreshButtonClick));
            viewMenu.DropDownItems.Add(new ToolStripSeparator());
            viewMenu.DropDownItems.Add(CreateMenuItem("Health Check", "Ctrl+H", BtnHealthCheck_Click));
            _menuStrip.Items.Add(viewMenu);

            // === SALES MENU ===
            var salesMenu = new ToolStripMenuItem("Sales");
            salesMenu.DropDownItems.Add(CreateMenuItem("Record Sale", "Ctrl+N", BtnRecordSale_Click));
            salesMenu.DropDownItems.Add(CreateMenuItem("Sales Reports", "Ctrl+R", BtnReports_Click));
            _menuStrip.Items.Add(salesMenu);

            // === INVENTORY MENU ===
            var inventoryMenu = new ToolStripMenuItem("Inventory");
            inventoryMenu.DropDownItems.Add(CreateMenuItem("Simulate Launch Event", "Ctrl+Shift+L", btnSimulateLaunch_Click));
            _menuStrip.Items.Add(inventoryMenu);

            // === ORDERS MENU ===
            var ordersMenu = new ToolStripMenuItem("Orders");
            ordersMenu.DropDownItems.Add(CreateMenuItem("Customer Orders", "Ctrl+O", BtnOrders_Click));
            if (_authService.IsAdmin)
            {
                ordersMenu.DropDownItems.Add(new ToolStripSeparator());
                ordersMenu.DropDownItems.Add(CreateMenuItem("Suppliers", null, BtnSuppliers_Click));
                ordersMenu.DropDownItems.Add(CreateMenuItem("Purchase Orders", null, BtnPurchaseOrders_Click));
            }
            _menuStrip.Items.Add(ordersMenu);

            // === HR MENU ===
            var hrMenu = new ToolStripMenuItem("HR");
            hrMenu.DropDownItems.Add(CreateMenuItem("Time Clock", "Ctrl+T", BtnTimeClock_Click));
            hrMenu.DropDownItems.Add(CreateMenuItem("Leave Request", null, BtnLeaveRequest_Click));
            if (_authService.IsAdmin)
            {
                hrMenu.DropDownItems.Add(new ToolStripSeparator());
                hrMenu.DropDownItems.Add(CreateMenuItem("Shift Management", null, BtnShiftManagement_Click));
                hrMenu.DropDownItems.Add(CreateMenuItem("Leave Approvals", null, BtnLeaveApproval_Click));
                hrMenu.DropDownItems.Add(CreateMenuItem("Payroll", "Ctrl+P", BtnPayroll_Click));
            }
            _menuStrip.Items.Add(hrMenu);

            // === HELP MENU ===
            var helpMenu = new ToolStripMenuItem("Help");
            helpMenu.DropDownItems.Add(CreateMenuItem("About", null, (s, e) =>
            {
                MessageBox.Show(
                    $"{_appSettings.Name}\nVersion {_appSettings.Version}\n\n© 2025 Technology Store",
                    "About",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }));
            _menuStrip.Items.Add(helpMenu);

            this.MainMenuStrip = _menuStrip;
            this.Controls.Add(_menuStrip);

            // Grid
            _gridInventory = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ReadOnly = true,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing,
                AllowUserToResizeColumns = true,
                AllowUserToResizeRows = false,
                ColumnHeadersVisible = true,
                EnableHeadersVisualStyles = false,
                BorderStyle = BorderStyle.None,
                BackgroundColor = Color.White
            };

            _gridInventory.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);
            _gridInventory.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 230, 230);
            _gridInventory.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            _gridInventory.ColumnHeadersDefaultCellStyle.Font = new Font(this.Font.FontFamily, 10f, FontStyle.Bold);
            _gridInventory.ColumnHeadersHeight = 28;
            _gridInventory.RowTemplate.Height = 22;

            // Define Columns
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Product", DataPropertyName = "Name", FillWeight = 20 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Category", DataPropertyName = "Category", FillWeight = 10 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Phase", DataPropertyName = "Phase", FillWeight = 7 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Price", DataPropertyName = "UnitPrice", FillWeight = 8, DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Stock", DataPropertyName = "CurrentStock", FillWeight = 7 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "7-Day Sales", DataPropertyName = "SalesLast7Days", FillWeight = 8 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Runway (Days)", DataPropertyName = "RunwayDays", FillWeight = 9 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "AI Recommendation", DataPropertyName = "Recommendation", FillWeight = 31 });

            this.Controls.Add(_gridInventory);

            // Setup context menu for right-click editing
            SetupContextMenu();

            // Ensure proper z-order: menu at top, then grid fills remaining space
            _gridInventory.BringToFront();
        }

        /// <summary>
        /// Sets up the context menu for the inventory grid
        /// </summary>
        private void SetupContextMenu()
        {
            if (_gridInventory == null) return;

            var contextMenu = new ContextMenuStrip();
            
            var editMenuItem = new ToolStripMenuItem("Edit Product...");
            editMenuItem.Click += EditProduct_Click;
            contextMenu.Items.Add(editMenuItem);

            // Add delete option for admins only (will be shown/hidden dynamically)
            var separator = new ToolStripSeparator();
            var deleteMenuItem = new ToolStripMenuItem("Delete Product...");
            deleteMenuItem.Click += DeleteProduct_Click;
            contextMenu.Items.Add(separator);
            contextMenu.Items.Add(deleteMenuItem);

            // Update menu visibility when opening based on current admin status
            contextMenu.Opening += (sender, e) =>
            {
                if (sender is ContextMenuStrip menu)
                {
                    // Show/hide delete option based on admin status
                    deleteMenuItem.Visible = _authService.IsAdmin;
                    separator.Visible = _authService.IsAdmin;
                }
            };

            _gridInventory.ContextMenuStrip = contextMenu;
        }

        /// <summary>
        /// Handles the Edit Product context menu item click
        /// </summary>
        private async void EditProduct_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_gridInventory == null || _gridInventory.CurrentRow == null)
                {
                    MessageBox.Show("Please select a product row first.", "No Selection", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var selectedProduct = _gridInventory.CurrentRow.DataBoundItem as ProductDashboardDto;
                if (selectedProduct == null)
                {
                    MessageBox.Show("Unable to get product information.", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Get full product details from repository
                var product = await _repository.GetByIdAsync(selectedProduct.Id);
                if (product == null)
                {
                    MessageBox.Show($"Product with ID {selectedProduct.Id} not found.", "Not Found", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Show edit form (pass auth service for admin check)
                var editForm = new EditProductForm(_repository, product, _authService);
                if (editForm.ShowDialog(this) == DialogResult.OK && editForm.ProductUpdated)
                {
                    // Refresh dashboard to show updated data
                    await LoadDashboardData();
                }
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Edit Product");
                MessageBox.Show($"Failed to edit product: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Handles the Delete Product context menu item click
        /// </summary>
        private async void DeleteProduct_Click(object? sender, EventArgs e)
        {
            ProductDashboardDto? selectedProduct = null;
            
            try
            {
                if (_gridInventory == null || _gridInventory.CurrentRow == null)
                {
                    MessageBox.Show("Please select a product row first.", "No Selection", 
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                selectedProduct = _gridInventory.CurrentRow.DataBoundItem as ProductDashboardDto;
                if (selectedProduct == null)
                {
                    MessageBox.Show("Unable to get product information.", "Error", 
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Confirm deletion (soft delete - hides from view but keeps in database)
                var result = MessageBox.Show(
                    $"Are you sure you want to hide '{selectedProduct.Name}' from the inventory?\n\n" +
                    "The product will be hidden from the dashboard but will remain in the database to maintain data integrity. " +
                    "All sales and order history will be preserved.",
                    "Confirm Hide Product",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning);

                if (result != DialogResult.Yes)
                    return;

                // Soft delete the product (set is_deleted flag)
                var deleted = await _repository.DeleteAsync(selectedProduct.Id);
                
                if (deleted)
                {
                    MessageBox.Show($"Product '{selectedProduct.Name}' has been hidden from the inventory.", 
                        "Product Hidden", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Refresh dashboard to show updated data
                    await LoadDashboardData();
                }
                else
                {
                    MessageBox.Show($"Failed to hide product '{selectedProduct.Name}'. It may have already been hidden.", 
                        "Hide Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Delete Product");
                MessageBox.Show($"Failed to delete product: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Creates a menu item with optional shortcut key display
        /// </summary>
        private static ToolStripMenuItem CreateMenuItem(string text, string? shortcut, EventHandler onClick)
        {
            var item = new ToolStripMenuItem(text);
            item.Click += onClick;

            if (!string.IsNullOrEmpty(shortcut))
            {
                item.ShortcutKeyDisplayString = shortcut;

                // Parse and set actual shortcut keys for common ones
                if (shortcut == "F5")
                    item.ShortcutKeys = Keys.F5;
                else if (shortcut == "Ctrl+N")
                    item.ShortcutKeys = Keys.Control | Keys.N;
                else if (shortcut == "Ctrl+R")
                    item.ShortcutKeys = Keys.Control | Keys.R;
                else if (shortcut == "Ctrl+O")
                    item.ShortcutKeys = Keys.Control | Keys.O;
                else if (shortcut == "Ctrl+T")
                    item.ShortcutKeys = Keys.Control | Keys.T;
                else if (shortcut == "Ctrl+P")
                    item.ShortcutKeys = Keys.Control | Keys.P;
                else if (shortcut == "Ctrl+H")
                    item.ShortcutKeys = Keys.Control | Keys.H;
                else if (shortcut == "Ctrl+L")
                    item.ShortcutKeys = Keys.Control | Keys.L;
                else if (shortcut == "Ctrl+,")
                    item.ShortcutKeys = Keys.Control | Keys.Oemcomma;
                else if (shortcut == "Ctrl+Shift+L")
                    item.ShortcutKeys = Keys.Control | Keys.Shift | Keys.L;
            }

            return item;
        }

        /// <summary>
        /// Gets the status bar text including user info
        /// </summary>
        private string GetStatusBarText()
        {
            var userInfo = "";
            if (_authService.CurrentUser != null)
            {
                var roleIcon = _authService.IsAdmin ? "👑" : "👤";
                userInfo = $" | {roleIcon} {_authService.CurrentUser.FullName} ({_authService.CurrentUser.Role})";
            }
            return $"Ready{userInfo}";
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            await LoadDashboardData();
        }

        private async Task LoadDashboardData()
        {
            try
            {
                UpdateStatusBar("Refreshing data...");

                var data = await _repository.GetDashboardDataAsync();

                // Remove duplicate products based on product name (case-insensitive)
                var uniqueData = RemoveDuplicateProducts(data).ToList();

                if (_gridInventory != null)
                {
                    if (_gridInventory.InvokeRequired)
                    {
                        _gridInventory.Invoke(new Action(() => 
                        {
                            _gridInventory.DataSource = null; // Clear first
                            _gridInventory.DataSource = uniqueData;
                            ColorRows();
                        }));
                    }
                    else
                    {
                        _gridInventory.DataSource = null; // Clear first
                        _gridInventory.DataSource = uniqueData;
                        ColorRows();
                    }
                }

                UpdateStatusBar($"Last Updated: {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Load Dashboard Data");
                MessageBox.Show($"Error loading data: {ex.Message}\n\nPlease check your database connection.",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Removes duplicate products from the data collection
        /// Deduplicates by product name (case-insensitive), keeping the entry with the highest stock
        /// </summary>
        private static List<ProductDashboardDto> RemoveDuplicateProducts(IEnumerable<ProductDashboardDto> data)
        {
            if (data == null) return new List<ProductDashboardDto>();

            var dataList = data.ToList();
            var originalCount = dataList.Count;

            // Group by product name (case-insensitive) and keep the one with highest stock
            var grouped = dataList
                .Where(item => !string.IsNullOrWhiteSpace(item.Name))
                .GroupBy(item => item.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(group => 
                {
                    // If multiple entries with same name, keep the one with highest stock
                    // If stock is equal, keep the one with highest ID
                    return group.OrderByDescending(p => p.CurrentStock)
                               .ThenByDescending(p => p.Id)
                               .First();
                })
                .OrderBy(p => p.Name) // Sort for consistent display
                .ToList();

            // Log if duplicates were removed (for debugging)
            if (originalCount > grouped.Count)
            {
                System.Diagnostics.Debug.WriteLine($"Removed {originalCount - grouped.Count} duplicate product(s) from dashboard");
            }

            return grouped;
        }

        /// <summary>
        /// Updates the status bar with a message while preserving user info
        /// </summary>
        private void UpdateStatusBar(string message)
        {
            if (_lblStatus == null) return;

            var userInfo = "";
            if (_authService.CurrentUser != null)
            {
                var roleIcon = _authService.IsAdmin ? "👑" : "👤";
                userInfo = $" | {roleIcon} {_authService.CurrentUser.FullName} ({_authService.CurrentUser.Role})";
            }
            _lblStatus.Text = $"{message}{userInfo}";
        }

        private void ColorRows()
        {
            if (_gridInventory == null) return;

            foreach (DataGridViewRow row in _gridInventory.Rows)
            {
                var item = row.DataBoundItem as ProductDashboardDto;
                if (item == null) continue;

                if (item.Phase == "OBSOLETE")
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 200, 200);
                    row.DefaultCellStyle.ForeColor = Color.DarkRed;
                }
                else if (item.Phase == "LEGACY")
                {
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 240, 200);
                    row.DefaultCellStyle.ForeColor = Color.DarkOrange;
                }
                else
                {
                    row.DefaultCellStyle.BackColor = Color.White;
                    row.DefaultCellStyle.ForeColor = Color.Black;
                }
            }
        }

        private async void btnSimulateLaunch_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_gridInventory == null)
                {
                    MessageBox.Show("Grid inventory is not initialized.");
                    return;
                }

                var selectedProduct = _gridInventory.CurrentRow?.DataBoundItem as ProductDashboardDto;
                if (selectedProduct == null)
                {
                    MessageBox.Show("Please select a product row first.");
                    return;
                }

                if (MessageBox.Show($"Simulate new model launch for {selectedProduct.Name}?", "Confirm",
                        MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    // Update the old product to LEGACY phase
                    await _repository.UpdateProductPhaseAsync(selectedProduct.Id, "LEGACY",
                        "Manual Simulation Triggered by User");
                    
                    // Show dialog to create new product
                    var newProductForm = new NewProductForm(_repository, selectedProduct.Category);
                    if (newProductForm.ShowDialog(this) == DialogResult.OK && newProductForm.ProductCreated)
                    {
                        // Refresh dashboard to show the new product
                        await LoadDashboardData();
                    }
                }
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Simulate Launch");
                MessageBox.Show($"Failed to simulate launch: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnRecordSale_Click(object? sender, EventArgs e)
        {
            try
            {
                var salesForm = new SalesEntryForm(_repository);
                if (salesForm.ShowDialog() == DialogResult.OK)
                {
                    // Refresh dashboard after recording sale
                    await LoadDashboardData();
                }
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Record Sale");
                MessageBox.Show($"Failed to record sale: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnHealthCheck_Click(object? sender, EventArgs e)
        {
            try
            {
                UpdateStatusBar("Running health checks...");

                var report = await _healthCheckService.CheckAllAsync();

                // Determine icon based on overall status
                var icon = report.OverallStatus switch
                {
                    HealthStatus.Healthy => MessageBoxIcon.Information,
                    HealthStatus.Degraded => MessageBoxIcon.Warning,
                    HealthStatus.Unhealthy => MessageBoxIcon.Error,
                    _ => MessageBoxIcon.Question
                };

                MessageBox.Show(
                    report.GetSummary(),
                    $"Health Check - {report.OverallStatus}",
                    MessageBoxButtons.OK,
                    icon);

                var statusIcon = report.OverallStatus switch
                {
                    HealthStatus.Healthy => "✅",
                    HealthStatus.Degraded => "⚠️",
                    HealthStatus.Unhealthy => "❌",
                    _ => "❓"
                };
                UpdateStatusBar($"Health: {statusIcon} {report.OverallStatus} | Last Check: {DateTime.Now:HH:mm:ss}");
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Health Check");
                MessageBox.Show($"Health check failed: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnLogout_Click(object? sender, EventArgs e)
        {
            var result = MessageBox.Show(
                "Are you sure you want to logout?",
                "Confirm Logout",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                _authService.Logout();
                this.DialogResult = DialogResult.Abort; // Signal to restart login
                this.Close();
            }
        }

        private async void BtnLeaveRequest_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_authService.CurrentUser == null)
                {
                    MessageBox.Show("You must be logged in.", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Get employee record for current user
                var employee = await _leaveRepository.GetEmployeeByUserIdAsync(_authService.CurrentUser.Id);
                if (employee == null)
                {
                    MessageBox.Show("No employee record found for your account.\nPlease contact an administrator.",
                        "Employee Not Found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var leaveForm = new LeaveRequestForm(_leaveRepository, _authService, employee);
                leaveForm.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Leave Request");
                MessageBox.Show($"Error opening leave request form: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnLeaveApproval_Click(object? sender, EventArgs e)
        {
            try
            {
                if (!_authService.IsAdmin)
                {
                    MessageBox.Show("Only administrators can access this feature.", "Access Denied",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var approvalForm = new LeaveApprovalForm(_leaveRepository, _authService);
                approvalForm.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Leave Approval");
                MessageBox.Show($"Error opening leave approval form: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnReports_Click(object? sender, EventArgs e)
        {
            try
            {
                var reportForm = new SalesReportForm(_salesReportService, _authService);
                reportForm.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Sales Reports");
                MessageBox.Show($"Error opening sales reports: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnOrders_Click(object? sender, EventArgs e)
        {
            try
            {
                var ordersForm = new OrderManagementForm(_orderRepository);
                ordersForm.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Order Management");
                MessageBox.Show($"Error opening order management: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSettings_Click(object? sender, EventArgs e)
        {
            try
            {
                var settingsForm = new SettingsForm(_emailSettings);
                settingsForm.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Settings");
                MessageBox.Show($"Error opening settings: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnTimeClock_Click(object sender, EventArgs e)
        {
            try
            {
                var form = new TimeTrackingForm(_timeTrackingService, (AuthenticationService)_authService);
                form.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Time Tracking");
                MessageBox.Show($"Error opening time clock: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnShiftManagement_Click(object sender, EventArgs e)
        {
            try
            {
                if (!_authService.IsAdmin) return;
                var form = new ShiftManagementForm(_workShiftRepository, _userRepository, (AuthenticationService)_authService);
                form.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Shift Management");
                MessageBox.Show($"Error opening shift management: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnPayroll_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_payrollService == null)
                {
                    MessageBox.Show("Payroll service is not available.", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                var form = new PayrollForm(_payrollService, _authService);
                form.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Payroll");
                MessageBox.Show($"Error opening payroll: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnSuppliers_Click(object? sender, EventArgs e)
        {
            try
            {
                var suppliersForm = new SupplierManagementForm(_supplierRepository);
                suppliersForm.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Supplier Management");
                MessageBox.Show($"Error opening supplier management: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnPurchaseOrders_Click(object? sender, EventArgs e)
        {
            try
            {
                var poForm = new PurchaseOrdersForm(_purchaseOrderService, _authService);
                poForm.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Purchase Orders");
                MessageBox.Show($"Error opening purchase orders: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}