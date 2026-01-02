using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TechnologyStore.Desktop.Services;
using TechnologyStore.Shared.Models;
using TechnologyStore.Desktop.Features.Auth; // For IAuthenticationService
using TechnologyStore.Shared.Interfaces; // For IPayrollService (if defined there) or ensure IPayrollService is visible
using TechnologyStore.Desktop.Config;

// IPayrollService is in TechnologyStore.Desktop.Features.Payroll namespace
using TechnologyStore.Desktop.Features.Payroll;
using IAuthenticationService = TechnologyStore.Desktop.Features.Auth.IAuthenticationService;

namespace TechnologyStore.Desktop.Features.Payroll.Forms
{
    public class PayrollForm : Form
    {
        private readonly IPayrollService _payrollService;
        private readonly IAuthenticationService _authService;
        private readonly string _connectionString;

        private DateTimePicker _dtStart;
        private DateTimePicker _dtEnd;
        private DataGridView _grid;
        // _btnPreview removed (local)
        private Button _btnCommit;
        private Button _btnExport;
        private Button _btnManageRates;
        private Label _lblStatus;
        private MenuStrip? _menuStrip;

        private List<PayrollEntry> _currentPreview = new List<PayrollEntry>();

        public PayrollForm(IPayrollService payrollService, IAuthenticationService authService)
        {
            _payrollService = payrollService;
            _authService = authService;
            _connectionString = DatabaseConfig.BuildConnectionStringFromEnv();
            if (string.IsNullOrWhiteSpace(_connectionString))
            {
                throw new InvalidOperationException("Database connection string not configured.");
            }
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Payroll Management";
            this.Size = new System.Drawing.Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

            // Menu Strip
            _menuStrip = new MenuStrip();
            var toolsMenu = new ToolStripMenuItem("Tools");
            var manageRatesMenuItem = new ToolStripMenuItem("Manage Hourly Rates...", null, (s, e) => OpenHourlyRateForm());
            toolsMenu.DropDownItems.Add(manageRatesMenuItem);
            _menuStrip.Items.Add(toolsMenu);
            this.MainMenuStrip = _menuStrip;
            this.Controls.Add(_menuStrip);

            var mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.RowCount = 3;
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60)); // Controls
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // Grid
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); // Status
            this.Controls.Add(mainLayout);

            // Controls Panel
            var pnlControls = new FlowLayoutPanel();
            pnlControls.Dock = DockStyle.Fill;
            pnlControls.Padding = new Padding(10);
            pnlControls.AutoSize = true;
            mainLayout.Controls.Add(pnlControls, 0, 0);

            var lblStart = new Label { Text = "Start Date:", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleRight, Padding = new Padding(0, 6, 0, 0) };
            _dtStart = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(-15) };

            var lblEnd = new Label { Text = "End Date:", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleRight, Padding = new Padding(0, 6, 0, 0) };
            _dtEnd = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today };

            var btnPreview = new Button { Text = "Preview Run", AutoSize = true };
            btnPreview.Click += async (s, e) => await LoadPreviewAsync();

            _btnCommit = new Button { Text = "Finalize & Save", AutoSize = true, Enabled = false, BackColor = System.Drawing.Color.LightGreen };
            _btnCommit.Click += async (s, e) => await CommitRunAsync();

            _btnExport = new Button { Text = "Export CSV", AutoSize = true, Enabled = false };
            _btnExport.Click += (s, e) => ExportCsv();

            _btnManageRates = new Button { Text = "💰 Manage Hourly Rates", AutoSize = true };
            _btnManageRates.Click += (s, e) => OpenHourlyRateForm();

            pnlControls.Controls.AddRange(new Control[] { lblStart, _dtStart, lblEnd, _dtEnd, btnPreview, _btnCommit, _btnExport, _btnManageRates });

            // Grid
            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.ReadOnly = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Employee", DataPropertyName = "EmployeeName" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Total Hours", DataPropertyName = "TotalHours", DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Hourly Rate", DataPropertyName = "HourlyRate", DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Gross Pay", DataPropertyName = "GrossPay", DefaultCellStyle = new DataGridViewCellStyle { Format = "C2", Font = new System.Drawing.Font(DefaultFont, System.Drawing.FontStyle.Bold) } });

            mainLayout.Controls.Add(_grid, 0, 1);

            // Status
            _lblStatus = new Label { Text = "Ready", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            mainLayout.Controls.Add(_lblStatus, 0, 2);
        }

        private async Task LoadPreviewAsync()
        {
            try
            {
                _lblStatus.Text = "Calculating payroll...";
                _currentPreview = await _payrollService.PreviewPayrollAsync(_dtStart.Value, _dtEnd.Value);

                _grid.DataSource = null;
                _grid.DataSource = _currentPreview;

                decimal totalPayout = _currentPreview.Sum(x => x.GrossPay);
                _lblStatus.Text = $"Preview generated. Total Payout: {totalPayout:C2}";

                _btnCommit.Enabled = _currentPreview.Any();
                _btnExport.Enabled = _currentPreview.Any();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error generating preview: " + ex.Message);
            }
        }

        private async Task CommitRunAsync()
        {
            if (MessageBox.Show("Are you sure you want to finalize this payroll run? This will save the record to the database.", "Confirm", MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

            try
            {
                var run = new PayrollRun
                {
                    StartDate = _dtStart.Value,
                    EndDate = _dtEnd.Value,
                    CreatedBy = _authService.CurrentUser?.Id,
                    Notes = $"Generated on {DateTime.Now}"
                };

                await _payrollService.CommitPayrollRunAsync(run, _currentPreview);

                MessageBox.Show("Payroll run saved successfully!");
                _btnCommit.Enabled = false; // Prevent double submit
                _lblStatus.Text = "Saved.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving payroll: " + ex.Message);
            }
        }

        private void ExportCsv()
        {
            if (!_currentPreview.Any()) return;

            using (var sfd = new SaveFileDialog { Filter = "CSV Files|*.csv", FileName = $"payroll_{DateTime.Now:yyyyMMdd}.csv" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Employee,Total Hours,Hourly Rate,Gross Pay");
                    foreach (var item in _currentPreview)
                    {
                        sb.AppendLine($"{item.EmployeeName},{item.TotalHours},{item.HourlyRate},{item.GrossPay}");
                    }
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString());
                    MessageBox.Show("Export complete.");
                }
            }
        }

        private void OpenHourlyRateForm()
        {
            try
            {
                var form = new EmployeeHourlyRateForm(_connectionString);
                form.ShowDialog(this);
                
                // If preview is loaded, refresh it to show updated rates
                if (_currentPreview.Any())
                {
                    LoadPreviewAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening hourly rate form: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
