using TechnologyStoreAutomation.backend.trendCalculator.data;
using TechnologyStoreAutomation.ui;
using Timer = System.Windows.Forms.Timer;

namespace TechnologyStoreAutomation
{
    public partial class MainForm : Form
    {
        private readonly IProductRepository _repository;
        private readonly HealthCheckService _healthCheckService;
        private readonly UiSettings _uiSettings;
        private readonly ApplicationSettings _appSettings;
        private readonly Timer _refreshTimer;
        private DataGridView? _gridInventory;
        private Label? _lblStatus;
        private Button? _btnSimulate;
        private Button? _btnRecordSale;
        private Button? _btnHealthCheck;

        /// <summary>
        /// Creates a new MainForm with injected dependencies
        /// </summary>
        /// <param name="repository">Product repository for data access</param>
        /// <param name="healthCheckService">Health check service for diagnostics</param>
        /// <param name="uiSettings">UI configuration settings</param>
        /// <param name="appSettings">Application settings</param>
        public MainForm(
            IProductRepository repository, 
            HealthCheckService healthCheckService,
            UiSettings uiSettings, 
            ApplicationSettings appSettings)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _healthCheckService = healthCheckService ?? throw new ArgumentNullException(nameof(healthCheckService));
            _uiSettings = uiSettings ?? throw new ArgumentNullException(nameof(uiSettings));
            _appSettings = appSettings ?? throw new ArgumentNullException(nameof(appSettings));
            
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
                MessageBox.Show($"Refresh failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

            // Toolbar Panel
            var toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = _uiSettings.ToolbarHeight;
            toolbar.BackColor = Color.FromArgb(240, 240, 240);
            this.Controls.Add(toolbar);

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

            // Grid
            _gridInventory = new DataGridView();
            _gridInventory.Dock = DockStyle.Fill;
            _gridInventory.AutoGenerateColumns = false;
            _gridInventory.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridInventory.ReadOnly = true;
            _gridInventory.AllowUserToAddRows = false;
            _gridInventory.RowHeadersVisible = false;
            _gridInventory.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);

            // Define Columns
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Product", DataPropertyName = "Name", Width = 250 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Phase", DataPropertyName = "Phase", Width = 100 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Stock", DataPropertyName = "CurrentStock", Width = 80 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "7-Day Sales", DataPropertyName = "SalesLast7Days", Width = 100 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "Runway (Days)", DataPropertyName = "RunwayDays", Width = 120 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
                { HeaderText = "AI Recommendation", DataPropertyName = "Recommendation", Width = 300 });

            this.Controls.Add(_gridInventory);

            _gridInventory.BringToFront();
            toolbar.BringToFront();
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
                MessageBox.Show($"Failed to simulate launch: {ex.Message}", "Error", 
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
                MessageBox.Show($"Failed to record sale: {ex.Message}", "Error", 
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
                MessageBox.Show($"Health check failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (_btnHealthCheck != null) _btnHealthCheck.Enabled = true;
            }
        }
    }
}