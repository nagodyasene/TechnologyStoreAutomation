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
    private enum ReportType
    {
        Daily,
        Weekly,
        Monthly,
        CustomRange
    }

    private sealed record ReportTypeOption(string Text, ReportType Value)
    {
        public override string ToString() => Text;
    }

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
        this.Text = "Satış Raporları";
        this.StartPosition = FormStartPosition.CenterParent;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        int yPos = 20;

        // Title
        var lblTitle = new Label
        {
            Text = "Satış Raporları",
            Location = new Point(20, yPos),
            Width = 300,
            Font = new Font(this.Font.FontFamily, 14, FontStyle.Bold)
        };
        this.Controls.Add(lblTitle);

        yPos += 45;

        // Report Type
        var lblType = new Label { Text = "Rapor Türü:", Location = new Point(20, yPos + 3), Width = 100 };
        this.Controls.Add(lblType);

        _cmbReportType = new ComboBox
        {
            Location = new Point(130, yPos),
            Width = 150,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbReportType.Items.AddRange(new object[]
        {
            new ReportTypeOption("Günlük", ReportType.Daily),
            new ReportTypeOption("Haftalık", ReportType.Weekly),
            new ReportTypeOption("Aylık", ReportType.Monthly),
            new ReportTypeOption("Özel Aralık", ReportType.CustomRange)
        });
        _cmbReportType.SelectedIndex = 0;
        _cmbReportType.SelectedIndexChanged += CmbReportType_SelectedIndexChanged;
        this.Controls.Add(_cmbReportType);

        yPos += 40;

        // Start Date
        var lblStart = new Label { Text = "Başlangıç:", Location = new Point(20, yPos + 3), Width = 100 };
        this.Controls.Add(lblStart);

        _dtpStartDate = new DateTimePicker
        {
            Location = new Point(130, yPos),
            Width = 150,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today,
            MaxDate = DateTime.Today
        };
        this.Controls.Add(_dtpStartDate);

        // End Date (for custom range)
        var lblEnd = new Label { Text = "Bitiş:", Location = new Point(300, yPos + 3), Width = 80 };
        this.Controls.Add(lblEnd);

        _dtpEndDate = new DateTimePicker
        {
            Location = new Point(390, yPos),
            Width = 150,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today,
            MaxDate = DateTime.Today,
            Enabled = false // Only enabled for custom range
        };
        this.Controls.Add(_dtpEndDate);

        yPos += 40;

        // Buttons
        _btnGenerate = new Button
        {
            Text = "Rapor Oluştur",
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
            Text = "CSV Dışa Aktar",
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
            Text = "Rapor türünü ve tarihi seçin, ardından 'Rapor Oluştur'a tıklayın."
        };
        this.Controls.Add(_lblSummary);

        yPos += 95;

        // Product Breakdown Label
        var lblBreakdown = new Label
        {
            Text = "Ürün Kırılımı",
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

        _gridBreakdown.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ürün", DataPropertyName = "ProductName", Width = 250 });
        _gridBreakdown.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Satılan Adet", DataPropertyName = "UnitsSold", Width = 100 });
        _gridBreakdown.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Ciro", DataPropertyName = "Revenue", Width = 120 });
        _gridBreakdown.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "% Toplam", DataPropertyName = "PercentageOfTotal", Width = 100 });

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
        ApplyReportTypeDateBehavior();
    }

    private void ApplyReportTypeDateBehavior()
    {
        if (_dtpStartDate == null || _dtpEndDate == null || _cmbReportType == null) return;

        var today = DateTime.Today;
        _dtpStartDate.MaxDate = today;
        _dtpEndDate.MaxDate = today;

        var reportType = _cmbReportType.SelectedItem is ReportTypeOption opt ? opt.Value : ReportType.Daily;

        switch (reportType)
        {
            case ReportType.Weekly:
                // Rolling last 7 days ending today (inclusive)
                _dtpStartDate.Value = today.AddDays(-6);
                _dtpEndDate.Value = today;
                _dtpStartDate.Enabled = false;
                _dtpEndDate.Enabled = false;
                break;

            case ReportType.Monthly:
                // Rolling last 30 days ending today (inclusive)
                _dtpStartDate.Value = today.AddDays(-29);
                _dtpEndDate.Value = today;
                _dtpStartDate.Enabled = false;
                _dtpEndDate.Enabled = false;
                break;

            case ReportType.CustomRange:
                // Allow manual range selection, but never allow future dates
                _dtpStartDate.Enabled = true;
                _dtpEndDate.Enabled = true;

                if (_dtpStartDate.Value.Date > today) _dtpStartDate.Value = today;
                if (_dtpEndDate.Value.Date > today) _dtpEndDate.Value = today;
                if (_dtpEndDate.Value.Date < _dtpStartDate.Value.Date) _dtpEndDate.Value = _dtpStartDate.Value.Date;
                break;

            default:
                // Daily (and fallback): pick a single date (no future)
                _dtpStartDate.Enabled = true;
                _dtpEndDate.Enabled = false;
                _dtpEndDate.Value = _dtpStartDate.Value.Date > today ? today : _dtpStartDate.Value.Date;
                if (_dtpStartDate.Value.Date > today) _dtpStartDate.Value = today;
                break;
        }
    }

    private async void BtnGenerate_Click(object? sender, EventArgs e)
    {
        if (_cmbReportType == null || _dtpStartDate == null || _dtpEndDate == null) return;

        SetFormEnabled(false);
        if (_lblStatus != null) _lblStatus.Text = "Rapor oluşturuluyor...";

        try
        {
            var selectedOpt = _cmbReportType.SelectedItem as ReportTypeOption;
            var reportType = selectedOpt?.Value ?? ReportType.Daily;
            var today = DateTime.Today;

            // Determine the actual query range (and prevent future dates).
            DateTime startDate;
            DateTime endDate;

            switch (reportType)
            {
                case ReportType.Weekly:
                    endDate = today;
                    startDate = today.AddDays(-6);
                    break;
                case ReportType.Monthly:
                    endDate = today;
                    startDate = today.AddDays(-29);
                    break;
                case ReportType.CustomRange:
                    startDate = _dtpStartDate.Value.Date;
                    endDate = _dtpEndDate.Value.Date;
                    if (startDate > today || endDate > today)
                        throw new ArgumentException("Tarih aralığı gelecekteki günleri içeremez.");
                    if (startDate > endDate)
                        throw new ArgumentException("Başlangıç tarihi bitiş tarihinden sonra olamaz.");
                    break;
                default:
                    startDate = _dtpStartDate.Value.Date;
                    if (startDate > today)
                        throw new ArgumentException("Tarih gelecekte olamaz.");
                    endDate = startDate;
                    break;
            }

            _currentReport = reportType switch
            {
                // Always drive via explicit date ranges so behavior is unambiguous.
                ReportType.Weekly => await _reportService.GetCustomRangeReportAsync(startDate, endDate),
                ReportType.Monthly => await _reportService.GetCustomRangeReportAsync(startDate, endDate),
                ReportType.CustomRange => await _reportService.GetCustomRangeReportAsync(startDate, endDate),
                _ => await _reportService.GetDailyReportAsync(startDate)
            };

            // Ensure report type label matches UI selection
            if (_currentReport != null)
            {
                _currentReport.ReportType = GetReportTypeLabelTr(reportType);
            }

            DisplayReport(_currentReport);

            if (_btnExport != null) _btnExport.Enabled = true;
            if (_lblStatus != null) _lblStatus.Text = $"Rapor oluşturuldu: {DateTime.Now:HH:mm:ss}";
        }
        catch (ArgumentException ex)
        {
            // Input validation error (e.g. invalid date range)
            MessageBox.Show(ex.Message, "Geçersiz Girdi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            if (_lblStatus != null) _lblStatus.Text = "Rapor oluşturulamadı.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating sales report");
            MessageBox.Show("Rapor oluşturulurken bir hata oluştu. Lütfen logları kontrol edin.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (_lblStatus != null) _lblStatus.Text = "Rapor oluşturulurken hata oluştu.";
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
        _lblSummary.Text = $"{report.ReportType}: {report.StartDate:yyyy-MM-dd} - {report.EndDate:yyyy-MM-dd}\n\n" +
                          $"İşlem: {report.TotalTransactions}    |    " +
                          $"Satılan Adet: {report.TotalUnitsSold}    |    " +
                          $"Ciro: {report.TotalRevenue:C}    |    " +
                          $"Ortalama Satış: {report.AverageSaleAmount:C}";

        // Update grid
        _gridBreakdown.DataSource = report.ProductBreakdown;
    }

    private void BtnExport_Click(object? sender, EventArgs e)
    {
        if (_currentReport == null)
        {
            MessageBox.Show("Lütfen önce bir rapor oluşturun.", "Rapor Yok", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Security Check: Only Admin or authorized roles can export detailed data
        if (!_authService.IsAdmin)
        {
            MessageBox.Show("Rapor dışa aktarma yetkiniz yok.", "Erişim Reddedildi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var saveDialog = new SaveFileDialog
            {
                Filter = "CSV Dosyaları (*.csv)|*.csv",
                FileName = $"SatisRaporu_{_currentReport.ReportType}_{_currentReport.StartDate:yyyyMMdd}.csv",
                Title = "Satış Raporunu Dışa Aktar"
            };

            if (saveDialog.ShowDialog() == DialogResult.OK)
            {
                var csv = _reportService.ExportToCsv(_currentReport);
                File.WriteAllText(saveDialog.FileName, csv);
                MessageBox.Show($"Rapor dışa aktarıldı:\n{saveDialog.FileName}", "Dışa Aktarma Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting sales report to CSV");
            MessageBox.Show($"Dışa aktarma sırasında hata oluştu: {ex.Message}", "Dışa Aktarma Hatası", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void SetFormEnabled(bool enabled)
    {
        if (_cmbReportType != null) _cmbReportType.Enabled = enabled;
        if (enabled)
        {
            ApplyReportTypeDateBehavior();
        }
        if (_btnGenerate != null)
        {
            _btnGenerate.Enabled = enabled;
            _btnGenerate.Text = enabled ? "Rapor Oluştur" : "Oluşturuluyor...";
        }
    }

    private static string GetReportTypeLabelTr(ReportType reportType)
    {
        return reportType switch
        {
            ReportType.Daily => "Günlük Rapor",
            ReportType.Weekly => "Haftalık Rapor",
            ReportType.Monthly => "Aylık Rapor",
            ReportType.CustomRange => "Özel Aralık Raporu",
            _ => "Rapor"
        };
    }
}
