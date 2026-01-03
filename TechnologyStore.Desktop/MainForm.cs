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
        private StatusStrip? _statusStrip;
        private ToolStripStatusLabel? _lblStatus;
        private ToolStripStatusLabel? _lblUser;
        private MenuStrip? _mainMenuStrip;

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
                if (_lblStatus != null) _lblStatus.Text = $"Refresh failed: {ex.Message}";
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

            // Create StatusStrip
            _statusStrip = new StatusStrip();
            _lblStatus = new ToolStripStatusLabel("Ready");
            _lblStatus.Spring = true;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _statusStrip.Items.Add(_lblStatus);

            // User Info Label (right side of status bar)
            _lblUser = new ToolStripStatusLabel();
            if (_authService.CurrentUser != null)
            {
                var roleIcon = _authService.IsAdmin ? "👑" : "👤";
                _lblUser.Text = $"{roleIcon} {_authService.CurrentUser.FullName} ({_authService.CurrentUser.Role})";
            }
            _lblUser.TextAlign = ContentAlignment.MiddleRight;
            _statusStrip.Items.Add(_lblUser);
            this.Controls.Add(_statusStrip);

            // Create MenuStrip
            _mainMenuStrip = new MenuStrip();
            SetupMenuStrip();

            // Grid
            _gridInventory = new DataGridView();
            _gridInventory.Dock = DockStyle.Fill;
            _gridInventory.AutoGenerateColumns = false;
            _gridInventory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridInventory.ReadOnly = true;
            _gridInventory.AllowUserToAddRows = false;
            _gridInventory.RowHeadersVisible = false;
            _gridInventory.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);

            // Make columns expand to fill available width and size headers
            _gridInventory.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridInventory.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;
            _gridInventory.AllowUserToResizeColumns = true;
            _gridInventory.AllowUserToResizeRows = false;

            // Ensure headers are visible and styled for readability
            _gridInventory.ColumnHeadersVisible = true;
            _gridInventory.EnableHeadersVisualStyles = false;
            _gridInventory.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 230, 230);
            _gridInventory.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
            _gridInventory.ColumnHeadersDefaultCellStyle.Font = new Font(this.Font.FontFamily, 10f, FontStyle.Bold);

            // Explicit header and row heights to ensure everything fits without font scaling
            _gridInventory.ColumnHeadersHeight = 28; // header height in pixels
            _gridInventory.RowTemplate.Height = 22;   // row height in pixels

            // Define Columns (use FillWeight to control relative widths)
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Product", DataPropertyName = "Name", FillWeight = 25 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Category", DataPropertyName = "Category", FillWeight = 12 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Phase", DataPropertyName = "Phase", FillWeight = 8 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Stock", DataPropertyName = "CurrentStock", FillWeight = 8 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "7-Day Sales", DataPropertyName = "SalesLast7Days", FillWeight = 9 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Runway (Days)", DataPropertyName = "RunwayDays", FillWeight = 10 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "AI Recommendation", DataPropertyName = "Recommendation", FillWeight = 28 });

            // Add grid and menu strip
            this.Controls.Add(_gridInventory);
            this.MainMenuStrip = _mainMenuStrip;
            this.Controls.Add(_mainMenuStrip);
        }

        private void SetupMenuStrip()
        {
            if (_mainMenuStrip == null) return;

            // File Menu
            var fileMenu = new ToolStripMenuItem("&File");
            var recordSaleItem = new ToolStripMenuItem("&Record Sale", null, BtnRecordSale_Click);
            var refreshItem = new ToolStripMenuItem("&Refresh", null, OnRefreshButtonClick);
            var separator1 = new ToolStripSeparator();
            var logoutItem = new ToolStripMenuItem("&Logout", null, BtnLogout_Click);
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { recordSaleItem, refreshItem, separator1, logoutItem });

            // Operations Menu
            var operationsMenu = new ToolStripMenuItem("&Operations");
            var simulateItem = new ToolStripMenuItem("&Simulate Launch Event", null, btnSimulateLaunch_Click);
            var healthCheckItem = new ToolStripMenuItem("&Health Check", null, BtnHealthCheck_Click);
            operationsMenu.DropDownItems.AddRange(new ToolStripItem[] { simulateItem, healthCheckItem });

            // Orders Menu
            var ordersMenu = new ToolStripMenuItem("&Orders");
            var ordersItem = new ToolStripMenuItem("&Manage Orders", null, BtnOrders_Click);
            ordersMenu.DropDownItems.Add(ordersItem);
            if (_authService.IsAdmin)
            {
                var purchaseOrdersItem = new ToolStripMenuItem("&Purchase Orders", null, BtnPurchaseOrders_Click);
                ordersMenu.DropDownItems.Add(purchaseOrdersItem);
            }

            // Suppliers Menu (Admin only)
            ToolStripMenuItem? suppliersMenu = null;
            if (_authService.IsAdmin)
            {
                suppliersMenu = new ToolStripMenuItem("&Suppliers");
                var suppliersItem = new ToolStripMenuItem("&Manage Suppliers", null, BtnSuppliers_Click);
                suppliersMenu.DropDownItems.Add(suppliersItem);
            }

            // Reports Menu
            var reportsMenu = new ToolStripMenuItem("&Reports");
            var reportsItem = new ToolStripMenuItem("&Sales Reports", null, BtnReports_Click);
            reportsMenu.DropDownItems.Add(reportsItem);

            // HR Menu
            var hrMenu = new ToolStripMenuItem("&HR");
            var leaveRequestItem = new ToolStripMenuItem("&Leave Request", null, BtnLeaveRequest_Click);
            hrMenu.DropDownItems.Add(leaveRequestItem);
            if (_authService.IsAdmin)
            {
                var leaveApprovalItem = new ToolStripMenuItem("&Leave Approvals", null, BtnLeaveApproval_Click);
                hrMenu.DropDownItems.Add(leaveApprovalItem);
            }
            var separator2 = new ToolStripSeparator();
            var timeClockItem = new ToolStripMenuItem("&Time Clock", null, BtnTimeClock_Click);
            hrMenu.DropDownItems.Add(separator2);
            hrMenu.DropDownItems.Add(timeClockItem);
            if (_authService.IsAdmin)
            {
                var shiftsItem = new ToolStripMenuItem("&Shift Management", null, BtnShiftManagement_Click);
                hrMenu.DropDownItems.Add(shiftsItem);
            }

            // Tools Menu
            var toolsMenu = new ToolStripMenuItem("&Tools");
            var settingsItem = new ToolStripMenuItem("&Settings", null, BtnSettings_Click);
            toolsMenu.DropDownItems.Add(settingsItem);

            // Add all menus to MenuStrip
            var menuItems = new List<ToolStripItem> { fileMenu, operationsMenu, ordersMenu };
            if (suppliersMenu != null)
            {
                menuItems.Add(suppliersMenu);
            }
            menuItems.Add(reportsMenu);
            menuItems.Add(hrMenu);
            menuItems.Add(toolsMenu);

            _mainMenuStrip.Items.AddRange(menuItems.ToArray());
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
                if (_lblStatus != null) _lblStatus.Text = "Refreshing data...";

                var data = await _repository.GetDashboardDataAsync();

                if (_gridInventory != null)
                {
                    if (_gridInventory.InvokeRequired)
                    {
                        _gridInventory.Invoke(new Action(() => _gridInventory.DataSource = data));
                    }
                    else
                    {
                        _gridInventory.DataSource = data;
                    }

                    ColorRows();
                }

                if (_lblStatus != null) _lblStatus.Text = $"Last Updated: {DateTime.Now.ToShortTimeString()}";
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Load Dashboard Data");
                MessageBox.Show($"Error loading data: {ex.Message}\n\nPlease check your database connection.",
                    "Database Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                    await _repository.UpdateProductPhaseAsync(selectedProduct.Id, "LEGACY",
                        "Manual Simulation Triggered by User");
                    await LoadDashboardData();
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
            var menuItem = sender as ToolStripMenuItem;
            try
            {
                if (_lblStatus != null) _lblStatus.Text = "Running health checks...";
                if (menuItem != null) menuItem.Enabled = false;

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

                if (_lblStatus != null)
                {
                    var statusIcon = report.OverallStatus switch
                    {
                        HealthStatus.Healthy => "✅",
                        HealthStatus.Degraded => "⚠️",
                        HealthStatus.Unhealthy => "❌",
                        _ => "❓"
                    };
                    _lblStatus.Text = $"Health: {statusIcon} {report.OverallStatus} | Last Updated: {DateTime.Now:HH:mm:ss}";
                }
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Health Check");
                MessageBox.Show($"Health check failed: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (menuItem != null) menuItem.Enabled = true;
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

        private void BtnTimeClock_Click(object? sender, EventArgs e)
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

        private void BtnShiftManagement_Click(object? sender, EventArgs e)
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