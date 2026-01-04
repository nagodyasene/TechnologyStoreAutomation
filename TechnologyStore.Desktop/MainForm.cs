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
        private ToolStripMenuItem? _assignSupplierMenuItem;
        private ToolStripMenuItem? _addStockMenuItem;
        private ProductDashboardDto? _rightClickedProduct;

        private const string ErrorTitle = "Hata";
        private const string InfoTitle = "Bilgi";
        private const string ConfirmTitle = "Onay";

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
                if (_lblStatus != null) _lblStatus.Text = $"Yenileme başarısız: {ex.Message}";
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
                MessageBox.Show($"Yenileme başarısız: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private void SetupDynamicUi()
        {
            this.Size = new Size(_uiSettings.WindowWidth, _uiSettings.WindowHeight);
            this.Text = _appSettings.Name;

            // Create StatusStrip
            _statusStrip = new StatusStrip();
            _lblStatus = new ToolStripStatusLabel("Hazır");
            _lblStatus.Spring = true;
            _lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            _statusStrip.Items.Add(_lblStatus);

            // User Info Label (right side of status bar)
            _lblUser = new ToolStripStatusLabel();
            if (_authService.CurrentUser != null)
            {
                var roleTr = GetRoleDisplayTr(_authService.CurrentUser.Role.ToString(), _authService.IsAdmin);
                _lblUser.Text = $"{_authService.CurrentUser.FullName} ({roleTr})";
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
            _gridInventory.MultiSelect = true;
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
            { HeaderText = "Ürün", DataPropertyName = "Name", FillWeight = 25 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Kategori", DataPropertyName = "Category", FillWeight = 12 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Aşama", DataPropertyName = "Phase", FillWeight = 8 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Stok", DataPropertyName = "CurrentStock", FillWeight = 8 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "7 Gün Satış", DataPropertyName = "SalesLast7Days", FillWeight = 9 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "Stok Ömrü (Gün)", DataPropertyName = "RunwayDays", FillWeight = 10 });
            _gridInventory.Columns.Add(new DataGridViewTextBoxColumn
            { HeaderText = "AI Önerisi", DataPropertyName = "Recommendation", FillWeight = 28 });

            _gridInventory.CellFormatting += GridInventory_CellFormatting;

            // Add grid and menu strip
            this.Controls.Add(_gridInventory);
            this.MainMenuStrip = _mainMenuStrip;
            this.Controls.Add(_mainMenuStrip);

            // Context menu: supplier assignment is admin-only, but "Stok Ekle" is available to employees too.
            AttachInventoryContextMenu();
        }

        private void AttachInventoryContextMenu()
        {
            if (_gridInventory == null) return;

            var ctx = new ContextMenuStrip();

            // Right-click should select the row under the cursor (so the action applies to the right-clicked item)
            _gridInventory.CellMouseDown += (_, e) =>
            {
                if (e.Button != MouseButtons.Right) return;
                if (e.RowIndex < 0) return;
                try
                {
                    _gridInventory.ClearSelection();
                    _gridInventory.Rows[e.RowIndex].Selected = true;
                    _gridInventory.CurrentCell = _gridInventory.Rows[e.RowIndex].Cells[Math.Max(0, e.ColumnIndex)];
                    _rightClickedProduct = _gridInventory.Rows[e.RowIndex].DataBoundItem as ProductDashboardDto;
                }
                catch
                {
                    _rightClickedProduct = null;
                }
            };

            _addStockMenuItem = new ToolStripMenuItem("Stok Ekle", null,
                async (_, _) => await AddStockForRightClickedProductAsync());
            ctx.Items.Add(_addStockMenuItem);
            ctx.Items.Add(new ToolStripSeparator());

            // Only add supplier assignment for admins. Avoid toggling Visible at runtime; WinForms can
            // get "stuck" with a hidden item, which matches the observed behavior in logs.
            if (_authService.IsAdmin)
            {
                _assignSupplierMenuItem = new ToolStripMenuItem("Tedarikçi Ata…", null,
                    async (_, _) => await AssignSupplierToSelectedProductsAsync());
                // Ensure defaults are on
                _assignSupplierMenuItem.Visible = true;
                _assignSupplierMenuItem.Available = true;
                ctx.Items.Add(_assignSupplierMenuItem);
            }

            ctx.Opening += (_, e) =>
            {
                var hasSelection = _gridInventory.SelectedRows.Count > 0;
                var dto = _rightClickedProduct ?? (_gridInventory.SelectedRows.Count > 0
                    ? _gridInventory.SelectedRows[0].DataBoundItem as ProductDashboardDto
                    : null);

                // "Stok Ekle" should only be shown when the right-clicked item has zero stock
                if (_addStockMenuItem != null)
                {
                    var allow = dto != null && dto.CurrentStock <= 0 && _authService.CurrentUser != null;
                    _addStockMenuItem.Visible = allow;
                    _addStockMenuItem.Enabled = allow;
                }

                // Supplier assignment stays admin-only (menu item is only added for admins)
                if (_assignSupplierMenuItem != null)
                {
                    // Defensive: Visible can remain "stuck" false if it was hidden earlier in the same app session.
                    // Always force visible for admins once the item exists.
                    _assignSupplierMenuItem.Available = true;
                    _assignSupplierMenuItem.Visible = true;
                    _assignSupplierMenuItem.Enabled = hasSelection;
                }
                e.Cancel = !hasSelection;
            };

            _gridInventory.ContextMenuStrip = ctx;
        }

        private async Task AddStockForRightClickedProductAsync()
        {
            if (_gridInventory == null) return;

            var dto = _rightClickedProduct ?? (_gridInventory.SelectedRows.Count > 0
                ? _gridInventory.SelectedRows[0].DataBoundItem as ProductDashboardDto
                : null);

            if (dto == null)
            {
                MessageBox.Show("Lütfen bir ürün seçin.", InfoTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (dto.CurrentStock > 0)
            {
                MessageBox.Show("Bu seçenek yalnızca stok 0 olan ürünler için kullanılabilir.", InfoTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                if (_lblStatus != null) _lblStatus.Text = "Satın alma siparişi oluşturuluyor...";

                var product = await _repository.GetByIdAsync(dto.Id);
                if (product == null)
                {
                    MessageBox.Show("Ürün bulunamadı.", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                if (!product.SupplierId.HasValue || product.SupplierId.Value <= 0)
                {
                    MessageBox.Show(
                        "Bu ürün için tedarikçi atanmadı.\n\nSatın alma siparişi oluşturmak için önce bir yönetici tedarikçi atamalı.",
                        "Tedarikçi Gerekli",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }

                // Auto quantity: aim for ~30 days of stock based on last 7 days sales (fallback to min 10)
                var dailySales = dto.SalesLast7Days > 0 ? (dto.SalesLast7Days / 7.0) : 0.0;
                var targetDays = 30;
                var targetStock = dailySales > 0 ? (int)Math.Ceiling(dailySales * targetDays) : 10;
                var orderQty = Math.Max(10, targetStock - dto.CurrentStock);

                var unitCost = product.UnitPrice * 0.6m;
                var notes = $"Stok 0 olduğu için 'Stok Ekle' ile oluşturuldu. Ürün: {product.Name} (ID: {product.Id})";

                var result = await _purchaseOrderService.CreateManualPurchaseOrderAsync(
                    product.SupplierId.Value,
                    new List<(int ProductId, int Quantity, decimal UnitCost)> { (product.Id, orderQty, unitCost) },
                    notes);

                if (!result.Success || result.PurchaseOrder == null)
                {
                    MessageBox.Show(result.ErrorMessage ?? "Satın alma siparişi oluşturulamadı.", ErrorTitle,
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show(
                    $"Satın alma siparişi oluşturuldu (Onay bekliyor).\n\nSipariş No: {result.PurchaseOrder.OrderNumber}",
                    "Stok Ekle",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                var openNow = MessageBox.Show("Satın Alma Siparişleri şimdi açılsın mı?", "Stok Ekle",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (openNow == DialogResult.Yes)
                {
                    var poForm = new PurchaseOrdersForm(_purchaseOrderService, _authService);
                    poForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Add Stock (Create PO)");
                MessageBox.Show($"Satın alma siparişi oluşturulamadı: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                if (_lblStatus != null) _lblStatus.Text = "Hazır";
            }
        }

        private async Task AssignSupplierToSelectedProductsAsync()
        {
            if (_gridInventory == null) return;

            var selectedProductIds = _gridInventory.SelectedRows
                .Cast<DataGridViewRow>()
                .Select(r => r.DataBoundItem as ProductDashboardDto)
                .Where(dto => dto != null)
                .Select(dto => dto!.Id)
                .Distinct()
                .ToList();

            if (selectedProductIds.Count == 0)
            {
                MessageBox.Show("Lütfen önce bir veya daha fazla ürün seçin.", "Tedarikçi Ata",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var suppliers = (await _supplierRepository.GetAllAsync(activeOnly: true)).ToList();
            if (suppliers.Count == 0)
            {
                MessageBox.Show("Aktif tedarikçi bulunamadı. Lütfen önce bir tedarikçi oluşturun.", "Tedarikçi Ata",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dialog = new SupplierPickerDialog(suppliers, selectedProductIds.Count);
            if (dialog.ShowDialog(this) != DialogResult.OK) return;

            var supplierId = dialog.SelectedSupplierId;
            if (!supplierId.HasValue) return;

            await _repository.AssignSupplierAsync(selectedProductIds, supplierId.Value);
            await LoadDashboardData();

            MessageBox.Show($"{selectedProductIds.Count} ürüne tedarikçi atandı.", "Tedarikçi Ata",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private sealed class SupplierPickerDialog : Form
        {
            private readonly ComboBox _cmbSuppliers = new();
            public int? SelectedSupplierId => _cmbSuppliers.SelectedValue is int id ? id : null;

            public SupplierPickerDialog(IReadOnlyList<Supplier> suppliers, int selectedProductCount)
            {
                Text = "Tedarikçi Ata";
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MaximizeBox = false;
                MinimizeBox = false;
                Width = 420;
                Height = 180;

                var lbl = new Label
                {
                    Text = $"{selectedProductCount} seçili ürüne tedarikçi ata:",
                    AutoSize = true,
                    Left = 15,
                    Top = 15
                };

                _cmbSuppliers.DropDownStyle = ComboBoxStyle.DropDownList;
                _cmbSuppliers.Left = 15;
                _cmbSuppliers.Top = 45;
                _cmbSuppliers.Width = 370;
                _cmbSuppliers.DataSource = suppliers.ToList();
                _cmbSuppliers.DisplayMember = "Name";
                _cmbSuppliers.ValueMember = "Id";

                var btnOk = new Button { Text = "Tamam", DialogResult = DialogResult.OK, Width = 90, Left = 205, Top = 85 };
                var btnCancel = new Button { Text = "İptal", DialogResult = DialogResult.Cancel, Width = 90, Left = 295, Top = 85 };

                Controls.AddRange(new Control[] { lbl, _cmbSuppliers, btnOk, btnCancel });
                AcceptButton = btnOk;
                CancelButton = btnCancel;
            }
        }

        private void SetupMenuStrip()
        {
            if (_mainMenuStrip == null) return;

            // File Menu
            var fileMenu = new ToolStripMenuItem("&Dosya");
            if (_authService.IsAdmin)
            {
                var newProductItem = new ToolStripMenuItem("&Yeni Ürün…", null, BtnNewProduct_Click);
                fileMenu.DropDownItems.Add(newProductItem);
                fileMenu.DropDownItems.Add(new ToolStripSeparator());
            }
            var recordSaleItem = new ToolStripMenuItem("&Satış Kaydet", null, BtnRecordSale_Click);
            var refreshItem = new ToolStripMenuItem("&Yenile", null, OnRefreshButtonClick);
            var separator1 = new ToolStripSeparator();
            var logoutItem = new ToolStripMenuItem("&Çıkış Yap", null, BtnLogout_Click);
            fileMenu.DropDownItems.AddRange(new ToolStripItem[] { recordSaleItem, refreshItem, separator1, logoutItem });

            // Operations Menu
            var operationsMenu = new ToolStripMenuItem("&İşlemler");
            var simulateItem = new ToolStripMenuItem("&Lansman Simülasyonu", null, btnSimulateLaunch_Click);
            var healthCheckItem = new ToolStripMenuItem("&Sağlık Kontrolü", null, BtnHealthCheck_Click);
            operationsMenu.DropDownItems.AddRange(new ToolStripItem[] { simulateItem, healthCheckItem });

            // Orders Menu
            var ordersMenu = new ToolStripMenuItem("&Siparişler");
            var ordersItem = new ToolStripMenuItem("&Siparişleri Yönet", null, BtnOrders_Click);
            ordersMenu.DropDownItems.Add(ordersItem);
            if (_authService.IsAdmin)
            {
                var purchaseOrdersItem = new ToolStripMenuItem("&Satın Alma Siparişleri", null, BtnPurchaseOrders_Click);
                ordersMenu.DropDownItems.Add(purchaseOrdersItem);

                ordersMenu.DropDownItems.Add(new ToolStripSeparator());
                var generateCriticalPosItem = new ToolStripMenuItem("&Kritik Satın Alma Siparişlerini Şimdi Oluştur", null, BtnGenerateCriticalPurchaseOrders_Click);
                ordersMenu.DropDownItems.Add(generateCriticalPosItem);

                var generateUrgentPosItem = new ToolStripMenuItem("&Acil Satın Alma Siparişlerini Şimdi Oluştur", null, BtnGenerateUrgentPurchaseOrders_Click);
                ordersMenu.DropDownItems.Add(generateUrgentPosItem);
            }

            // Suppliers Menu (Admin only)
            ToolStripMenuItem? suppliersMenu = null;
            if (_authService.IsAdmin)
            {
                suppliersMenu = new ToolStripMenuItem("&Tedarikçiler");
                var suppliersItem = new ToolStripMenuItem("&Tedarikçileri Yönet", null, BtnSuppliers_Click);
                suppliersMenu.DropDownItems.Add(suppliersItem);
            }

            // Reports Menu
            var reportsMenu = new ToolStripMenuItem("&Raporlar");
            var reportsItem = new ToolStripMenuItem("&Satış Raporları", null, BtnReports_Click);
            reportsMenu.DropDownItems.Add(reportsItem);

            // HR Menu
            var hrMenu = new ToolStripMenuItem("&İK");
            var leaveRequestItem = new ToolStripMenuItem("&İzin Talebi", null, BtnLeaveRequest_Click);
            hrMenu.DropDownItems.Add(leaveRequestItem);
            if (_authService.IsAdmin)
            {
                var leaveApprovalItem = new ToolStripMenuItem("&İzin Onayları", null, BtnLeaveApproval_Click);
                hrMenu.DropDownItems.Add(leaveApprovalItem);

                var employeeManagementItem = new ToolStripMenuItem("&Çalışan Yönetimi", null, BtnEmployeeManagement_Click);
                hrMenu.DropDownItems.Add(employeeManagementItem);
            }
            var separator2 = new ToolStripSeparator();
            var timeClockItem = new ToolStripMenuItem("&Puantaj", null, BtnTimeClock_Click);
            hrMenu.DropDownItems.Add(separator2);
            hrMenu.DropDownItems.Add(timeClockItem);
            if (_authService.IsAdmin)
            {
                var shiftsItem = new ToolStripMenuItem("&Vardiya Yönetimi", null, BtnShiftManagement_Click);
                hrMenu.DropDownItems.Add(shiftsItem);
            }

            // Tools Menu
            var toolsMenu = new ToolStripMenuItem("&Araçlar");
            var settingsItem = new ToolStripMenuItem("&Ayarlar", null, BtnSettings_Click);
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

        private async void BtnNewProduct_Click(object? sender, EventArgs e)
        {
            if (!_authService.IsAdmin)
            {
                MessageBox.Show("Bu işlem için yönetici yetkisi gerekir.", "Erişim Reddedildi",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                using var dialog = new TechnologyStore.Desktop.UI.Forms.NewProductForm(_repository, _supplierRepository);
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    await LoadDashboardData();
                    MessageBox.Show("Ürün başarıyla oluşturuldu.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Create Product");
                MessageBox.Show($"Ürün oluşturulurken hata oluştu: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
                if (_lblStatus != null) _lblStatus.Text = "Veriler yenileniyor...";

                var data = await _repository.GetDashboardDataAsync();
                var dedupedData = DeduplicateDashboardRows(data);

                if (_gridInventory != null)
                {
                    if (_gridInventory.InvokeRequired)
                    {
                        _gridInventory.Invoke(new Action(() => _gridInventory.DataSource = dedupedData));
                    }
                    else
                    {
                        _gridInventory.DataSource = dedupedData;
                    }

                    ColorRows();
                }

                if (_lblStatus != null) _lblStatus.Text = $"Son Güncelleme: {DateTime.Now.ToShortTimeString()}";
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Load Dashboard Data");
                MessageBox.Show($"Veriler yüklenirken hata oluştu: {ex.Message}\n\nLütfen veritabanı bağlantınızı kontrol edin.",
                    "Veritabanı Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static List<ProductDashboardDto> DeduplicateDashboardRows(IEnumerable<ProductDashboardDto> data)
        {
            // The DB can contain multiple product rows with the same display name/category/phase
            // (e.g., duplicated seed data). The dashboard grid doesn't show SKU/ID, so collapse those
            // duplicates to a single visible row to avoid confusing users.
            //
            // We keep the row with the highest stock (then highest 7-day sales) to make the result stable.
            static string Norm(string? s) => (s ?? string.Empty).Trim();

            return data
                .GroupBy(d => (Name: Norm(d.Name), Category: Norm(d.Category), Phase: Norm(d.Phase)))
                .Select(g => g
                    .OrderByDescending(x => x.CurrentStock)
                    .ThenByDescending(x => x.SalesLast7Days)
                    .First())
                .OrderBy(d => d.Category)
                .ThenBy(d => d.Name)
                .ToList();
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
                    MessageBox.Show("Envanter tablosu başlatılmadı.", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var selectedProduct = _gridInventory.CurrentRow?.DataBoundItem as ProductDashboardDto;
                if (selectedProduct == null)
                {
                    MessageBox.Show("Lütfen önce bir ürün satırı seçin.", InfoTitle, MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (MessageBox.Show($"{selectedProduct.Name} için yeni model lansmanı simüle edilsin mi?", ConfirmTitle,
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
                MessageBox.Show($"Lansman simülasyonu başarısız: {ex.Message}", ErrorTitle,
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
                MessageBox.Show($"Satış kaydı başarısız: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnHealthCheck_Click(object? sender, EventArgs e)
        {
            var menuItem = sender as ToolStripMenuItem;
            try
            {
                if (_lblStatus != null) _lblStatus.Text = "Sağlık kontrolleri çalıştırılıyor...";
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
                    $"Sağlık Kontrolü - {GetHealthStatusDisplayTr(report.OverallStatus)}",
                    MessageBoxButtons.OK,
                    icon);

                if (_lblStatus != null)
                {
                    _lblStatus.Text = $"Sağlık: {GetHealthStatusDisplayTr(report.OverallStatus)} | Son Güncelleme: {DateTime.Now:HH:mm:ss}";
                }
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Health Check");
                MessageBox.Show($"Sağlık kontrolü başarısız: {ex.Message}", ErrorTitle,
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
                "Çıkış yapmak istediğinizden emin misiniz?",
                "Çıkışı Onayla",
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
                    MessageBox.Show("Giriş yapmanız gerekiyor.", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Get employee record for current user
                var employee = await _leaveRepository.GetEmployeeByUserIdAsync(_authService.CurrentUser.Id);
                if (employee == null)
                {
                    MessageBox.Show("Hesabınız için çalışan kaydı bulunamadı.\nLütfen bir yöneticiyle iletişime geçin.",
                        "Çalışan Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var leaveForm = new LeaveRequestForm(_leaveRepository, _authService, employee);
                leaveForm.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Leave Request");
                MessageBox.Show($"İzin talebi formu açılırken hata oluştu: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnLeaveApproval_Click(object? sender, EventArgs e)
        {
            try
            {
                if (!_authService.IsAdmin)
                {
                    MessageBox.Show("Bu özelliğe yalnızca yöneticiler erişebilir.", "Erişim Reddedildi",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var approvalForm = new LeaveApprovalForm(_leaveRepository, _authService);
                approvalForm.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Leave Approval");
                MessageBox.Show($"İzin onay formu açılırken hata oluştu: {ex.Message}", ErrorTitle,
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
                MessageBox.Show($"Satış raporları açılırken hata oluştu: {ex.Message}", ErrorTitle,
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
                MessageBox.Show($"Sipariş yönetimi açılırken hata oluştu: {ex.Message}", ErrorTitle,
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
                MessageBox.Show($"Ayarlar açılırken hata oluştu: {ex.Message}", ErrorTitle,
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
                MessageBox.Show($"Puantaj ekranı açılırken hata oluştu: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show($"Vardiya yönetimi açılırken hata oluştu: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnPayroll_Click(object? sender, EventArgs e)
        {
            try
            {
                if (_payrollService == null)
                {
                    MessageBox.Show("Bordro servisi kullanılamıyor.", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                var form = new PayrollForm(_payrollService, _authService);
                form.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Payroll");
                MessageBox.Show($"Bordro ekranı açılırken hata oluştu: {ex.Message}", ErrorTitle, MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                MessageBox.Show($"Tedarikçi yönetimi açılırken hata oluştu: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnEmployeeManagement_Click(object? sender, EventArgs e)
        {
            if (!_authService.IsAdmin)
            {
                MessageBox.Show("Bu özelliğe yalnızca yöneticiler erişebilir.", "Erişim Reddedildi",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                var connectionString = DatabaseConfig.GetRequiredConnectionString();
                var form = new TechnologyStore.Desktop.Features.HR.EmployeeManagementForm(connectionString, _authService);
                form.ShowDialog();
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Employee Management");
                MessageBox.Show($"Çalışan yönetimi açılırken hata oluştu: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnGenerateCriticalPurchaseOrders_Click(object? sender, EventArgs e)
        {
            if (!_authService.IsAdmin)
            {
                MessageBox.Show("Bu özelliğe yalnızca yöneticiler erişebilir.", "Erişim Reddedildi",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (_lblStatus != null) _lblStatus.Text = "Kritik satın alma siparişleri oluşturuluyor...";

                var generated = (await _purchaseOrderService.GeneratePurchaseOrdersForCriticalStockAsync()).ToList();

                if (_lblStatus != null) _lblStatus.Text = "Hazır";

                if (generated.Count == 0)
                {
                    await ShowReorderDiagnosticsAsync("Kritik", thresholdDays: 3);
                    return;
                }

                var openNow = MessageBox.Show(
                    $"{generated.Count} satın alma siparişi oluşturuldu (Onay bekliyor).\n\nSatın Alma Siparişleri şimdi açılsın mı?",
                    "Kritik Satın Alma Siparişi Oluştur",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (openNow == DialogResult.Yes)
                {
                    var poForm = new PurchaseOrdersForm(_purchaseOrderService, _authService);
                    poForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Generate Critical Purchase Orders");
                MessageBox.Show($"Kritik satın alma siparişleri oluşturulamadı: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async void BtnGenerateUrgentPurchaseOrders_Click(object? sender, EventArgs e)
        {
            if (!_authService.IsAdmin)
            {
                MessageBox.Show("Bu özelliğe yalnızca yöneticiler erişebilir.", "Erişim Reddedildi",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                if (_lblStatus != null) _lblStatus.Text = "Acil satın alma siparişleri oluşturuluyor...";

                var generated = (await _purchaseOrderService.GeneratePurchaseOrdersForUrgentStockAsync()).ToList();

                if (_lblStatus != null) _lblStatus.Text = "Hazır";

                if (generated.Count == 0)
                {
                    await ShowReorderDiagnosticsAsync("Acil", thresholdDays: 7);
                    return;
                }

                var openNow = MessageBox.Show(
                    $"{generated.Count} satın alma siparişi oluşturuldu (Onay bekliyor).\n\nSatın Alma Siparişleri şimdi açılsın mı?",
                    "Acil Satın Alma Siparişi Oluştur",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (openNow == DialogResult.Yes)
                {
                    var poForm = new PurchaseOrdersForm(_purchaseOrderService, _authService);
                    poForm.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                GlobalExceptionHandler.ReportException(ex, "Generate Urgent Purchase Orders");
                MessageBox.Show($"Acil satın alma siparişleri oluşturulamadı: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task ShowReorderDiagnosticsAsync(string labelTr, int thresholdDays)
        {
            try
            {
                var dashboard = (await _repository.GetDashboardDataAsync()).ToList();
                var activeMatching = dashboard
                    .Where(p => string.Equals(p.Phase, "ACTIVE", StringComparison.OrdinalIgnoreCase) && p.RunwayDays <= thresholdDays)
                    .ToList();

                if (activeMatching.Count == 0)
                {
                    MessageBox.Show(
                        $"Şu anda {labelTr.ToLowerInvariant()} stokta olup yeni satın alma siparişi gerektiren ürün yok.",
                        $"{labelTr} Satın Alma Siparişi Oluştur",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                var products = (await _repository.GetAllAsync()).ToList();
                var dict = products.ToDictionary(p => p.Id);
                var missingSupplierProducts = activeMatching
                    .Where(p => !dict.ContainsKey(p.Id) || !dict[p.Id].SupplierId.HasValue)
                    .Select(p => p.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList();

                var listed = missingSupplierProducts.Take(12).ToList();
                var remaining = Math.Max(0, missingSupplierProducts.Count - listed.Count);
                var missingSupplierText = listed.Count == 0
                    ? "Yok"
                    : string.Join("\n", listed.Select(n => $"- {n}")) + (remaining > 0 ? $"\n- … (+{remaining} adet daha)" : string.Empty);

                MessageBox.Show(
                    $"{activeMatching.Count} adet {labelTr.ToLowerInvariant()} ürün tespit edildi, ancak satın alma siparişi oluşturulamadı.\n\n" +
                    "Tedarikçi atanmamış ürünler:\n" +
                    $"{missingSupplierText}\n\n" +
                    "Not: Otomatik PO oluşturma için ürünlere önce tedarikçi atanmalıdır (Tedarikçi Ata…).",
                    $"{labelTr} Satın Alma Siparişi Oluştur",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch
            {
                // Fallback message if diagnostics fail
                MessageBox.Show(
                    $"Şu anda {labelTr.ToLowerInvariant()} stokta olup yeni satın alma siparişi oluşturulamadı.",
                    $"{labelTr} Satın Alma Siparişi Oluştur",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
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
                MessageBox.Show($"Satın alma siparişleri açılırken hata oluştu: {ex.Message}", ErrorTitle,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static string GetRoleDisplayTr(string? role, bool isAdmin)
        {
            if (isAdmin) return "Yönetici";
            if (string.IsNullOrWhiteSpace(role)) return "Kullanıcı";

            return role.Trim().ToUpperInvariant() switch
            {
                "ADMIN" => "Yönetici",
                "USER" => "Kullanıcı",
                _ => "Kullanıcı"
            };
        }

        private static string GetHealthStatusDisplayTr(HealthStatus status)
        {
            return status switch
            {
                HealthStatus.Healthy => "Sağlıklı",
                HealthStatus.Degraded => "Kısmen Sorunlu",
                HealthStatus.Unhealthy => "Sorunlu",
                _ => "Bilinmiyor"
            };
        }

        private void GridInventory_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (_gridInventory == null) return;
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var col = _gridInventory.Columns[e.ColumnIndex];
            if (col?.DataPropertyName == "Phase" && e.Value is string phase)
            {
                e.Value = phase.Trim().ToUpperInvariant() switch
                {
                    "NEW" => "YENİ",
                    "ACTIVE" => "AKTİF",
                    "LEGACY" => "ESKİ",
                    "OBSOLETE" => "KULLANIMDIŞI",
                    _ => phase
                };
                e.FormattingApplied = true;
            }
        }
    }
}