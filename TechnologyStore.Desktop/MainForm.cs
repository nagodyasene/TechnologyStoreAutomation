using TechnologyStore.Desktop.Config;
using TechnologyStore.Desktop.Services;
using TechnologyStore.Desktop.Features.Auth;
using TechnologyStore.Desktop.Features.Leave;
using TechnologyStore.Desktop.Features.Reporting;
using TechnologyStore.Desktop.Features.Orders;
using TechnologyStore.Desktop.Features.Purchasing;
using TechnologyStore.Desktop.Features.Products.Data;
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
using IProductRepository = TechnologyStore.Desktop.Features.Products.Data.IProductRepository;
using IAuthenticationService = TechnologyStore.Desktop.Features.Auth.IAuthenticationService;
using IUserRepository = TechnologyStore.Desktop.Features.Auth.IUserRepository;

namespace TechnologyStore.Desktop
{
    public partial class MainForm : Form
    {
        private readonly IProductRepository _repository;
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
        private Label? _lblUser;
        private Button? _btnSimulate;
        private Button? _btnRecordSale;
        private Button? _btnHealthCheck;
        private Button? _btnLogout;
        private Button? _btnLeaveRequest;
        private Button? _btnLeaveApproval;
        private Button? _btnReports;
        private Button? _btnOrders;
        private Button? _btnSuppliers;
        private Button? _btnPurchaseOrders;
        private Button? _btnSettings;

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

            // Status Label
            _lblStatus = new Label();
            _lblStatus.Dock = DockStyle.Bottom;
            _lblStatus.Height = _uiSettings.StatusBarHeight;
            _lblStatus.Text = "Ready";
            this.Controls.Add(_lblStatus);

            // Toolbar Panel (create now but add later so docking layout is correct)
            var toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = _uiSettings.ToolbarHeight;
            toolbar.BackColor = Color.FromArgb(240, 240, 240);
            // DO NOT add to Controls yet - add after grid so docking/layout places header below toolbar

            // Record Sale Button
            _btnRecordSale = new Button();
            _btnRecordSale.Text = "📝 Record Sale";
            _btnRecordSale.Location = new Point(10, 8);
            _btnRecordSale.Size = new Size(140, 35);
            _btnRecordSale.BackColor = Color.FromArgb(76, 175, 80);
            _btnRecordSale.ForeColor = Color.White;
            _btnRecordSale.FlatStyle = FlatStyle.Flat;
            _btnRecordSale.Click += BtnRecordSale_Click;
            toolbar.Controls.Add(_btnRecordSale);

            // Simulation Button
            _btnSimulate = new Button();
            _btnSimulate.Text = "🚀 Simulate Launch Event";
            _btnSimulate.Location = new Point(160, 8);
            _btnSimulate.Size = new Size(180, 35);
            _btnSimulate.BackColor = Color.FromArgb(33, 150, 243);
            _btnSimulate.ForeColor = Color.White;
            _btnSimulate.FlatStyle = FlatStyle.Flat;
            _btnSimulate.Click += btnSimulateLaunch_Click;
            toolbar.Controls.Add(_btnSimulate);

            // Refresh Button
            var btnRefresh = new Button();
            btnRefresh.Text = "🔄 Refresh";
            btnRefresh.Location = new Point(350, 8);
            btnRefresh.Size = new Size(100, 35);
            btnRefresh.FlatStyle = FlatStyle.Flat;
            btnRefresh.Click += OnRefreshButtonClick;
            toolbar.Controls.Add(btnRefresh);

            // Health Check Button
            _btnHealthCheck = new Button();
            _btnHealthCheck.Text = "🏥 Health";
            _btnHealthCheck.Location = new Point(460, 8);
            _btnHealthCheck.Size = new Size(90, 35);
            _btnHealthCheck.FlatStyle = FlatStyle.Flat;
            _btnHealthCheck.Click += BtnHealthCheck_Click;
            toolbar.Controls.Add(_btnHealthCheck);

            // Logout Button (right-aligned)
            _btnLogout = new Button();
            _btnLogout.Text = "🚪 Logout";
            _btnLogout.Location = new Point(560, 8);
            _btnLogout.Size = new Size(90, 35);
            _btnLogout.FlatStyle = FlatStyle.Flat;
            _btnLogout.BackColor = Color.FromArgb(244, 67, 54);
            _btnLogout.ForeColor = Color.White;
            _btnLogout.FlatAppearance.BorderSize = 0;
            _btnLogout.Click += BtnLogout_Click;
            toolbar.Controls.Add(_btnLogout);

            // Leave Request Button (visible to all)
            _btnLeaveRequest = new Button();
            _btnLeaveRequest.Text = "📅 Leave";
            _btnLeaveRequest.Location = new Point(660, 8);
            _btnLeaveRequest.Size = new Size(80, 35);
            _btnLeaveRequest.FlatStyle = FlatStyle.Flat;
            _btnLeaveRequest.BackColor = Color.FromArgb(156, 39, 176);
            _btnLeaveRequest.ForeColor = Color.White;
            _btnLeaveRequest.FlatAppearance.BorderSize = 0;
            _btnLeaveRequest.Click += BtnLeaveRequest_Click;
            toolbar.Controls.Add(_btnLeaveRequest);

            // Leave Approval Button (admin only)
            if (_authService.IsAdmin)
            {
                _btnLeaveApproval = new Button();
                _btnLeaveApproval.Text = "✅ Approvals";
                _btnLeaveApproval.Location = new Point(750, 8);
                _btnLeaveApproval.Size = new Size(100, 35);
                _btnLeaveApproval.FlatStyle = FlatStyle.Flat;
                _btnLeaveApproval.BackColor = Color.FromArgb(255, 152, 0);
                _btnLeaveApproval.ForeColor = Color.White;
                _btnLeaveApproval.FlatAppearance.BorderSize = 0;
                _btnLeaveApproval.Click += BtnLeaveApproval_Click;
                toolbar.Controls.Add(_btnLeaveApproval);
            }

            // Reports Button
            _btnReports = new Button();
            _btnReports.Text = "📊 Reports";
            _btnReports.Location = new Point(_authService.IsAdmin ? 860 : 750, 8);
            _btnReports.Size = new Size(90, 35);
            _btnReports.FlatStyle = FlatStyle.Flat;
            _btnReports.BackColor = Color.FromArgb(96, 125, 139);
            _btnReports.ForeColor = Color.White;
            _btnReports.FlatAppearance.BorderSize = 0;
            _btnReports.Click += BtnReports_Click;
            toolbar.Controls.Add(_btnReports);

            // Orders Button
            _btnOrders = new Button();
            _btnOrders.Text = "📦 Orders";
            _btnOrders.Location = new Point(_authService.IsAdmin ? 960 : 850, 8);
            _btnOrders.Size = new Size(90, 35);
            _btnOrders.FlatStyle = FlatStyle.Flat;
            _btnOrders.BackColor = Color.FromArgb(103, 58, 183);
            _btnOrders.ForeColor = Color.White;
            _btnOrders.FlatAppearance.BorderSize = 0;
            _btnOrders.Click += BtnOrders_Click;
            toolbar.Controls.Add(_btnOrders);

            // Suppliers Button (admin only)
            if (_authService.IsAdmin)
            {
                _btnSuppliers = new Button();
                _btnSuppliers.Text = "🏭 Suppliers";
                _btnSuppliers.Location = new Point(1060, 8);
                _btnSuppliers.Size = new Size(100, 35);
                _btnSuppliers.FlatStyle = FlatStyle.Flat;
                _btnSuppliers.BackColor = Color.FromArgb(0, 150, 136);
                _btnSuppliers.ForeColor = Color.White;
                _btnSuppliers.FlatAppearance.BorderSize = 0;
                _btnSuppliers.Click += BtnSuppliers_Click;
                toolbar.Controls.Add(_btnSuppliers);

                _btnPurchaseOrders = new Button();
                _btnPurchaseOrders.Text = "📋 POs";
                _btnPurchaseOrders.Location = new Point(1170, 8);
                _btnPurchaseOrders.Size = new Size(80, 35);
                _btnPurchaseOrders.FlatStyle = FlatStyle.Flat;
                _btnPurchaseOrders.BackColor = Color.FromArgb(255, 87, 34);
                _btnPurchaseOrders.ForeColor = Color.White;
                _btnPurchaseOrders.FlatAppearance.BorderSize = 0;
                _btnPurchaseOrders.Click += BtnPurchaseOrders_Click;
                toolbar.Controls.Add(_btnPurchaseOrders);
            }

            // Settings Button
            _btnSettings = new Button();
            _btnSettings.Text = "⚙️ Settings";
            _btnSettings.Location = new Point(_authService.IsAdmin ? 1260 : 950, 8);
            _btnSettings.Size = new Size(90, 35);
            _btnSettings.FlatStyle = FlatStyle.Flat;
            _btnSettings.BackColor = Color.FromArgb(117, 117, 117);
            _btnSettings.ForeColor = Color.White;
            _btnSettings.FlatAppearance.BorderSize = 0;
            _btnSettings.Click += BtnSettings_Click;
            toolbar.Controls.Add(_btnSettings);

            // Time Clock Button (visible to all)
            var btnTimeClock = new Button();
            btnTimeClock.Text = "⏱️ Time Clock";
            btnTimeClock.Location = new Point(1360, 8);
            btnTimeClock.Size = new Size(110, 35);
            btnTimeClock.FlatStyle = FlatStyle.Flat;
            btnTimeClock.BackColor = Color.FromArgb(0, 188, 212); // Cyan
            btnTimeClock.ForeColor = Color.White;
            btnTimeClock.FlatAppearance.BorderSize = 0;
            btnTimeClock.Click += BtnTimeClock_Click;
            toolbar.Controls.Add(btnTimeClock);

            // Shift Management (Admin Only)
            if (_authService.IsAdmin)
            {
                var btnShifts = new Button();
                btnShifts.Text = "📅 Shifts";
                btnShifts.Location = new Point(1480, 8);
                btnShifts.Size = new Size(90, 35);
                btnShifts.FlatStyle = FlatStyle.Flat;
                btnShifts.BackColor = Color.FromArgb(63, 81, 181); // Indigo
                btnShifts.ForeColor = Color.White;
                btnShifts.FlatAppearance.BorderSize = 0;
                btnShifts.Click += BtnShiftManagement_Click;
                toolbar.Controls.Add(btnShifts);
            }

            // User Info Label (right side of toolbar)
            _lblUser = new Label();
            _lblUser.Location = new Point(1060, 15);
            _lblUser.Size = new Size(300, 20);
            _lblUser.TextAlign = ContentAlignment.MiddleRight;
            if (_authService.CurrentUser != null)
            {
                var roleIcon = _authService.IsAdmin ? "👑" : "👤";
                _lblUser.Text = $"{roleIcon} {_authService.CurrentUser.FullName} ({_authService.CurrentUser.Role})";
            }
            toolbar.Controls.Add(_lblUser);

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

            // Add grid first, then toolbar so dock layout places the toolbar at the top and grid fills remaining area
            this.Controls.Add(_gridInventory);
            this.Controls.Add(toolbar);

            // No explicit BringToFront required; docking order now correct
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
            try
            {
                if (_lblStatus != null) _lblStatus.Text = "Running health checks...";
                if (_btnHealthCheck != null) _btnHealthCheck.Enabled = false;

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
                if (_btnHealthCheck != null) _btnHealthCheck.Enabled = true;
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