using TechnologyStore.Desktop.Services;
using System.Drawing;
using System.Windows.Forms;
using TechnologyStore.Desktop.Features.Reporting;
using TechnologyStore.Desktop.Features.Auth;
using Microsoft.Extensions.Logging;

namespace TechnologyStore.Desktop.UI.Forms;

/// <summary>
/// Form for generating and viewing sales reports
/// </summary>
public partial class SalesReportForm : Form
{
    private readonly ISalesReportService _reportService;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<SalesReportForm> _logger;

    private ComboBox? _cmbReportType;
    private DateTimePicker? _dtpStartDate;
    private DateTimePicker? _dtpEndDate;
    private Button? _btnGenerate;
    private Button? _btnExport;
    private DataGridView? _gridBreakdown;
    private Label? _lblSummary;
    private Label? _lblStatus;
    private SalesReportDto? _currentReport;

    public SalesReportForm(ISalesReportService reportService, IAuthenticationService authService)
    {
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = AppLogger.CreateLogger<SalesReportForm>();
        InitializeComponent();
        SetupUI();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(700, 550);
        this.Name = "SalesReportForm";
        this.Text = "Sales Reports";
        this.StartPosition = FormStartPosition.CenterParent;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        int yPos = 20;

        // Title
        var lblTitle = new Label
        {
            Text = "📊 Sales Reports",
            Location = new Point(20, yPos),
            Width = 300,
            Font = new Font(this.Font.FontFamily, 14, FontStyle.Bold)
        };
        this.Controls.Add(lblTitle);

        yPos += 45;

        // Report Type
        var lblType = new Label { Text = "Report Type:", Location = new Point(20, yPos + 3), Width = 100 };
        this.Controls.Add(lblType);

        _cmbReportType = new ComboBox
        {
            Location = new Point(130, yPos),
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbReportType.Items.AddRange(new object[] { "Daily", "Weekly", "Monthly", "Custom Range" });
        _cmbReportType.SelectedIndex = 0;
        _cmbReportType.SelectedIndexChanged += CmbReportType_SelectedIndexChanged;
        this.Controls.Add(_cmbReportType);

        yPos += 40;

        // Start Date
        var lblStart = new Label { Text = "Start Date:", Location = new Point(20, yPos + 3), Width = 100 };
        this.Controls.Add(lblStart);

        _dtpStartDate = new DateTimePicker
        {
            Location = new Point(130, yPos),
            Width = 150,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today
        };
        this.Controls.Add(_dtpStartDate);

        // End Date (for custom range)
        var lblEnd = new Label { Text = "End Date:", Location = new Point(300, yPos + 3), Width = 80 };
        this.Controls.Add(lblEnd);

        _dtpEndDate = new DateTimePicker
        {
            Location = new Point(390, yPos),
            Width = 150,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today,
            Enabled = false // Only enabled for custom range
        };
        this.Controls.Add(_dtpEndDate);

        yPos += 40;

        // Buttons
        _btnGenerate = new Button
        {
            Text = "📈 Generate Report",
            Location = new Point(130, yPos),
            Size = new Size(140, 35),
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnGenerate.FlatAppearance.BorderSize = 0;
        _btnGenerate.Click += BtnGenerate_Click;
        this.Controls.Add(_btnGenerate);

        _btnExport = new Button
        {
            Text = "💾 Export CSV",
            Location = new Point(280, yPos),
            Size = new Size(120, 35),
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _btnExport.Click += BtnExport_Click;
        this.Controls.Add(_btnExport);

        yPos += 50;

        // Summary Label
        _lblSummary = new Label
        {
            Location = new Point(20, yPos),
            Size = new Size(660, 80),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(240, 248, 255),
            Padding = new Padding(10),
            Text = "Select report type and date, then click Generate."
        };
        this.Controls.Add(_lblSummary);

        yPos += 95;

        // Product Breakdown Label
        var lblBreakdown = new Label
        {
            Text = "📦 Product Breakdown",
            Location = new Point(20, yPos),
            Width = 200,
            Font = new Font(this.Font.FontFamily, 11, FontStyle.Bold)
        };
        this.Controls.Add(lblBreakdown);

        yPos += 30;

        // Grid
        _gridBreakdown = new DataGridView
        {
            Location = new Point(20, yPos),
            Size = new Size(660, 200),
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        _gridBreakdown.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Product", DataPropertyName = "ProductName", Width = 250 });
        _gridBreakdown.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Units Sold", DataPropertyName = "UnitsSold", Width = 100 });
        _gridBreakdown.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Revenue", DataPropertyName = "Revenue", Width = 120 });
        _gridBreakdown.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "% of Total", DataPropertyName = "PercentageOfTotal", Width = 100 });

        this.Controls.Add(_gridBreakdown);

        yPos += 210;

        // Status
        _lblStatus = new Label
        {
            Location = new Point(20, yPos),
            Width = 400,
            Text = ""
        };
        this.Controls.Add(_lblStatus);
    }

    private void CmbReportType_SelectedIndexChanged(object? sender, EventArgs e)
    {
        // Enable end date only for custom range
        if (_dtpEndDate != null && _cmbReportType != null)
        {
            _dtpEndDate.Enabled = _cmbReportType.SelectedItem?.ToString() == "Custom Range";
        }
    }

    private async void BtnGenerate_Click(object? sender, EventArgs e)
    {
        if (_cmbReportType == null || _dtpStartDate == null || _dtpEndDate == null) return;

        SetFormEnabled(false);
        if (_lblStatus != null) _lblStatus.Text = "Generating report...";

        try
        {
            var reportType = _cmbReportType.SelectedItem?.ToString();

            _currentReport = reportType switch
            {
                "Daily" => await _reportService.GetDailyReportAsync(_dtpStartDate.Value.Date),
                "Weekly" => await _reportService.GetWeeklyReportAsync(_dtpStartDate.Value.Date),
                "Monthly" => await _reportService.GetMonthlyReportAsync(_dtpStartDate.Value.Year, _dtpStartDate.Value.Month),
                "Custom Range" => await _reportService.GetCustomRangeReportAsync(_dtpStartDate.Value.Date, _dtpEndDate.Value.Date),
                _ => await _reportService.GetDailyReportAsync(_dtpStartDate.Value.Date)
            };

            DisplayReport(_currentReport);

            if (_btnExport != null) _btnExport.Enabled = true;
            if (_lblStatus != null) _lblStatus.Text = $"Report generated at {DateTime.Now:HH:mm:ss}";
        }
        catch (ArgumentException ex)
        {
            // Input validation error (e.g. invalid date range)
            MessageBox.Show(ex.Message, "Invalid Input", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (_lblStatus != null) _lblStatus.Text = "Report generation failed.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sales report");
            MessageBox.Show("An error occurred while generating the report. Please check the logs.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (_lblStatus != null) _lblStatus.Text = "Error generating report.";
        }
        finally
        {
            SetFormEnabled(true);
        }
    }

    private void DisplayReport(SalesReportDto report)
    {
        if (_lblSummary == null || _gridBreakdown == null) return;

        // Update summary
        _lblSummary.Text = $"📅 {report.ReportType} Report: {report.StartDate:yyyy-MM-dd} to {report.EndDate:yyyy-MM-dd}\n\n" +
                          $"📊 Transactions: {report.TotalTransactions}    |    " +
                          $"📦 Units Sold: {report.TotalUnitsSold}    |    " +
                          $"💰 Revenue: {report.TotalRevenue:C}    |    " +
                          $"📈 Avg Sale: {report.AverageSaleAmount:C}";

        // Update grid
        _gridBreakdown.DataSource = report.ProductBreakdown;
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        if (_currentReport == null)
        {
            MessageBox.Show("Please generate a report first.", "No Report", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Security Check: Only Admin or authorized roles can export detailed data
        if (!_authService.IsAdmin)
        {
            MessageBox.Show("You do not have permission to export reports.", "Access Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV Files (*.csv)|*.csv",
                FileName = $"SalesReport_{_currentReport.ReportType}_{_currentReport.StartDate:yyyyMMdd}.csv",
                Title = "Export Sales Report"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                var csv = _reportService.ExportToCsv(_currentReport);
                File.WriteAllText(saveDialog.FileName, csv);
                MessageBox.Show($"Report exported to:\n{saveDialog.FileName}", "Export Successful", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting sales report to CSV");
            MessageBox.Show($"And error occurred during export: {ex.Message}", "Export Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetFormEnabled(bool enabled)
    {
        if (_cmbReportType != null) _cmbReportType.Enabled = enabled;
        if (_dtpStartDate != null) _dtpStartDate.Enabled = enabled;
        if (_dtpEndDate != null && _cmbReportType?.SelectedItem?.ToString() == "Custom Range")
            _dtpEndDate.Enabled = enabled;
        if (_btnGenerate != null)
        {
            _btnGenerate.Enabled = enabled;
            _btnGenerate.Text = enabled ? "📈 Generate Report" : "Generating...";
        }
    }
}
