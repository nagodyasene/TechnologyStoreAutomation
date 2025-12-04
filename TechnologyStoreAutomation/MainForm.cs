using TechnologyStoreAutomation.backend.trendCalculator.data;
using TechnologyStoreAutomation.ui;
using Timer = System.Windows.Forms.Timer;

namespace TechnologyStoreAutomation
{
    public partial class MainForm : Form
    {
        private readonly IProductRepository _repository;
        private Timer _refreshTimer;
        private DataGridView? _gridInventory;
        private Label? _lblStatus;
        private Button? _btnSimulate;
        private Button? _btnRecordSale;

        public MainForm()
        {
            InitializeComponent();

            // Initialize Repository (read connection string from environment variables)
            string connStr = BuildConnectionStringFromEnv();
            if (string.IsNullOrWhiteSpace(connStr))
            {
                MessageBox.Show(
                    "Database connection is not configured.\n\nPlease set one of the following environment variable options:\n" +
                    "1) DB_CONNECTION_STRING (full libpq string),\n" +
                    "2) DATABASE_URL (postgres://user:pass@host:port/dbname), or\n" +
                    "3) DB_HOST / DB_NAME / DB_USER / DB_PASSWORD (and optional DB_PORT).\n\n" +
                    "The application cannot continue without a configured database connection.",
                    "Configuration required",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);

                // Fail fast - do not proceed with a hardcoded fallback
                throw new InvalidOperationException(
                    "Database connection string not configured via environment variables.");
            }

            _repository = new ProductRepository(connStr);

            SetupDynamicUi();

            // FIX: Explicitly use System.Windows.Forms.Timer
            _refreshTimer = new Timer();
            _refreshTimer.Interval = 300000; // 5 mins
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
                // Log or handle exception - async void exceptions would otherwise crash the app
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
                MessageBox.Show($"Refresh failed: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Build connection string from common environment variables (supports PG*/DB_* names, DATABASE_URL, or DB_CONNECTION_STRING)
        private static string BuildConnectionStringFromEnv()
        {
            // 1) Full connection string provided directly
            var direct = Environment.GetEnvironmentVariable("DB_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(direct)) return direct;

            // 2) Heroku-style DATABASE_URL: postgres://user:pass@host:port/dbname
            var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
            if (!string.IsNullOrWhiteSpace(databaseUrl))
            {
                if (Uri.TryCreate(databaseUrl, UriKind.Absolute, out var uri) &&
                    !string.IsNullOrWhiteSpace(uri.UserInfo))
                {
                    var userInfo = uri.UserInfo.Split(':');
                    var user = Uri.UnescapeDataString(userInfo[0]);
                    var pass = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
                    var host = uri.Host;
                    var port = uri.Port > 0 ? uri.Port.ToString() : "5432";
                    var db = uri.AbsolutePath.TrimStart('/');

                    if (!string.IsNullOrWhiteSpace(user) && !string.IsNullOrWhiteSpace(pass) &&
                        !string.IsNullOrWhiteSpace(db))
                    {
                        return $"Host={host};Port={port};Database={db};Username={user};Password={pass};";
                    }
                }
            }

            // 3) Individual environment variables
            var hostEnv = Environment.GetEnvironmentVariable("DB_HOST") ?? Environment.GetEnvironmentVariable("PGHOST");
            var dbEnv = Environment.GetEnvironmentVariable("DB_NAME") ??
                        Environment.GetEnvironmentVariable("PGDATABASE");
            var userEnv = Environment.GetEnvironmentVariable("DB_USER") ?? Environment.GetEnvironmentVariable("PGUSER");
            var passEnv = Environment.GetEnvironmentVariable("DB_PASSWORD") ??
                          Environment.GetEnvironmentVariable("PGPASSWORD");
            var portEnv = Environment.GetEnvironmentVariable("DB_PORT") ??
                          Environment.GetEnvironmentVariable("PGPORT") ?? "5432";

            if (string.IsNullOrWhiteSpace(hostEnv) || string.IsNullOrWhiteSpace(dbEnv) ||
                string.IsNullOrWhiteSpace(userEnv) || string.IsNullOrWhiteSpace(passEnv))
            {
                return string.Empty;
            }

            return $"Host={hostEnv};Port={portEnv};Database={dbEnv};Username={userEnv};Password={passEnv};";
        }

        private void SetupDynamicUi()
        {
            this.Size = new Size(1200, 700);
            this.Text = "TechTrend Automation Dashboard";

            // Status Label
            _lblStatus = new Label();
            _lblStatus.Dock = DockStyle.Bottom;
            _lblStatus.Height = 30;
            _lblStatus.Text = "Ready";
            this.Controls.Add(_lblStatus);

            // Toolbar Panel
            var toolbar = new Panel();
            toolbar.Dock = DockStyle.Top;
            toolbar.Height = 50;
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
                MessageBox.Show($"Error loading data: {ex.Message} \n\nCheck your Connection String in MainForm.cs!",
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

        private async void BtnRecordSale_Click(object? sender, EventArgs e)
        {
            var salesForm = new SalesEntryForm(_repository);
            if (salesForm.ShowDialog() == DialogResult.OK)
            {
                // Refresh dashboard after recording sale
                await LoadDashboardData();
            }
        }
    }
}