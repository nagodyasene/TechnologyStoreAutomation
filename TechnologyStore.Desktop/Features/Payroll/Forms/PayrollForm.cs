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

// IPayrollService is in TechnologyStore.Desktop.Features.Payroll namespace
using TechnologyStore.Desktop.Features.Payroll;
using IAuthenticationService = TechnologyStore.Desktop.Features.Auth.IAuthenticationService;

namespace TechnologyStore.Desktop.Features.Payroll.Forms
{
    public class PayrollForm : Form
    {
        private readonly IPayrollService _payrollService;
        private readonly IAuthenticationService _authService;

        private DateTimePicker _dtStart;
        private DateTimePicker _dtEnd;
        private DataGridView _grid;
        // _btnPreview removed (local)
        private Button _btnCommit;
        private Button _btnExport;
        private Label _lblStatus;

        private List<PayrollEntry> _currentPreview = new List<PayrollEntry>();

        public PayrollForm(IPayrollService payrollService, IAuthenticationService authService)
        {
            _payrollService = payrollService;
            _authService = authService;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Bordro Yönetimi";
            this.Size = new System.Drawing.Size(1000, 700);
            this.StartPosition = FormStartPosition.CenterScreen;

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

            var lblStart = new Label { Text = "Başlangıç:", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleRight, Padding = new Padding(0, 6, 0, 0) };
            _dtStart = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today.AddDays(-15) };

            var lblEnd = new Label { Text = "Bitiş:", AutoSize = true, TextAlign = System.Drawing.ContentAlignment.MiddleRight, Padding = new Padding(0, 6, 0, 0) };
            _dtEnd = new DateTimePicker { Format = DateTimePickerFormat.Short, Value = DateTime.Today };

            var btnPreview = new Button { Text = "Önizleme", AutoSize = true };
            btnPreview.Click += async (s, e) => await LoadPreviewAsync();

            _btnCommit = new Button { Text = "Kesinleştir ve Kaydet", AutoSize = true, Enabled = false, BackColor = System.Drawing.Color.LightGreen };
            _btnCommit.Click += async (s, e) => await CommitRunAsync();

            _btnExport = new Button { Text = "CSV Dışa Aktar", AutoSize = true, Enabled = false };
            _btnExport.Click += (s, e) => ExportCsv();

            pnlControls.Controls.AddRange(new Control[] { lblStart, _dtStart, lblEnd, _dtEnd, btnPreview, _btnCommit, _btnExport });

            // Grid
            _grid = new DataGridView();
            _grid.Dock = DockStyle.Fill;
            _grid.AutoGenerateColumns = false;
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _grid.ReadOnly = true;
            _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Çalışan", DataPropertyName = "EmployeeName" });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Toplam Saat", DataPropertyName = "TotalHours", DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Saatlik Ücret", DataPropertyName = "HourlyRate", DefaultCellStyle = new DataGridViewCellStyle { Format = "C2" } });
            _grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Brüt Ücret", DataPropertyName = "GrossPay", DefaultCellStyle = new DataGridViewCellStyle { Format = "C2", Font = new System.Drawing.Font(DefaultFont, System.Drawing.FontStyle.Bold) } });

            mainLayout.Controls.Add(_grid, 0, 1);

            // Status
            _lblStatus = new Label { Text = "Hazır", Dock = DockStyle.Fill, TextAlign = System.Drawing.ContentAlignment.MiddleLeft };
            mainLayout.Controls.Add(_lblStatus, 0, 2);
        }

        private async Task LoadPreviewAsync()
        {
            try
            {
                _lblStatus.Text = "Bordro hesaplanıyor...";
                _currentPreview = await _payrollService.PreviewPayrollAsync(_dtStart.Value, _dtEnd.Value);

                _grid.DataSource = null;
                _grid.DataSource = _currentPreview;

                decimal totalPayout = _currentPreview.Sum(x => x.GrossPay);
                _lblStatus.Text = $"Önizleme oluşturuldu. Toplam Ödeme: {totalPayout:C2}";

                _btnCommit.Enabled = _currentPreview.Any();
                _btnExport.Enabled = _currentPreview.Any();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Önizleme oluşturulamadı: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private async Task CommitRunAsync()
        {
            if (MessageBox.Show("Bu bordro çalışmasını kesinleştirmek istiyor musunuz? Bu işlem kaydı veritabanına kaydeder.", "Onay", MessageBoxButtons.YesNo) != DialogResult.Yes)
                return;

            try
            {
                var run = new PayrollRun
                {
                    StartDate = _dtStart.Value,
                    EndDate = _dtEnd.Value,
                    CreatedBy = _authService.CurrentUser?.Id,
                    Notes = $"Oluşturma: {DateTime.Now:dd.MM.yyyy HH:mm}"
                };

                await _payrollService.CommitPayrollRunAsync(run, _currentPreview);

                MessageBox.Show("Bordro kaydı başarıyla kaydedildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                _btnCommit.Enabled = false; // Prevent double submit
                _lblStatus.Text = "Kaydedildi.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Bordro kaydedilemedi: " + ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExportCsv()
        {
            if (!_currentPreview.Any()) return;

            using (var sfd = new SaveFileDialog { Filter = "CSV Dosyaları|*.csv", FileName = $"bordro_{DateTime.Now:yyyyMMdd}.csv" })
            {
                if (sfd.ShowDialog() == DialogResult.OK)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine("Çalışan,Toplam Saat,Saatlik Ücret,Brüt Ücret");
                    foreach (var item in _currentPreview)
                    {
                        sb.AppendLine($"{item.EmployeeName},{item.TotalHours},{item.HourlyRate},{item.GrossPay}");
                    }
                    System.IO.File.WriteAllText(sfd.FileName, sb.ToString());
                    MessageBox.Show("Dışa aktarma tamamlandı.", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }
    }
}
