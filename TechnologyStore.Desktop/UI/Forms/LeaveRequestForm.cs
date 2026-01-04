using TechnologyStore.Desktop.Services;
using System.Drawing;
using System.Windows.Forms;
using TechnologyStore.Desktop.Features.Auth;
using TechnologyStore.Desktop.Features.Leave;
using Microsoft.Extensions.Logging;

namespace TechnologyStore.Desktop.UI.Forms;

/// <summary>
/// Form for employees to submit leave requests
/// </summary>
public partial class LeaveRequestForm : Form
{
    private readonly ILeaveRepository _leaveRepository;
    private readonly Employee? _currentEmployee;
    private readonly ILogger<LeaveRequestForm> _logger;

    private ComboBox? _cmbLeaveType;
    private DateTimePicker? _dtpStartDate;
    private DateTimePicker? _dtpEndDate;
    private Label? _lblTotalDays;
    private Label? _lblRemainingDays;
    private TextBox? _txtReason;
    private Button? _btnSubmit;
    private Button? _btnCancel;
    private DataGridView? _gridHistory;

    private sealed record LeaveTypeOption(string Text, LeaveType Value)
    {
        public override string ToString() => Text;
    }

    public LeaveRequestForm(ILeaveRepository leaveRepository, IAuthenticationService authService, Employee? employee)
    {
        _leaveRepository = leaveRepository ?? throw new ArgumentNullException(nameof(leaveRepository));
        ArgumentNullException.ThrowIfNull(authService); // Validate but don't store since unused currently
        _currentEmployee = employee;
        _logger = AppLogger.CreateLogger<LeaveRequestForm>();

        InitializeComponent();
        SetupUI();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(600, 550);
        this.Name = "LeaveRequestForm";
        this.Text = "İzin Talebi";
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        int yPos = 20;
        int labelWidth = 120;
        int controlLeft = labelWidth + 30;

        // Title
        var lblTitle = new Label
        {
            Text = "Yeni İzin Talebi",
            Location = new Point(20, yPos),
            Width = 300,
            Font = new Font(this.Font.FontFamily, 14, FontStyle.Bold)
        };
        this.Controls.Add(lblTitle);

        yPos += 45;

        // Remaining Days Info
        _lblRemainingDays = new Label
        {
            Text = $"Kalan İzin Günü: {_currentEmployee?.RemainingLeaveDays ?? 0}",
            Location = new Point(20, yPos),
            Width = 300,
            ForeColor = Color.FromArgb(0, 120, 212),
            Font = new Font(this.Font, FontStyle.Bold)
        };
        this.Controls.Add(_lblRemainingDays);

        yPos += 35;

        // Leave Type
        var lblType = new Label { Text = "İzin Türü:", Location = new Point(20, yPos + 3), Width = labelWidth };
        this.Controls.Add(lblType);

        _cmbLeaveType = new ComboBox
        {
            Location = new Point(controlLeft, yPos),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbLeaveType.Items.AddRange(new object[]
        {
            new LeaveTypeOption("Yıllık", LeaveType.Annual),
            new LeaveTypeOption("Hastalık", LeaveType.Sick),
            new LeaveTypeOption("Ücretsiz", LeaveType.Unpaid),
            new LeaveTypeOption("Mazeret", LeaveType.Personal)
        });
        _cmbLeaveType.SelectedIndex = 0;
        this.Controls.Add(_cmbLeaveType);

        yPos += 40;

        // Start Date
        var lblStart = new Label { Text = "Başlangıç Tarihi:", Location = new Point(20, yPos + 3), Width = labelWidth };
        this.Controls.Add(lblStart);

        _dtpStartDate = new DateTimePicker
        {
            Location = new Point(controlLeft, yPos),
            Width = 200,
            Format = DateTimePickerFormat.Short,
            MinDate = DateTime.Today
        };
        _dtpStartDate.ValueChanged += OnDateChanged;
        this.Controls.Add(_dtpStartDate);

        yPos += 40;

        // End Date
        var lblEnd = new Label { Text = "Bitiş Tarihi:", Location = new Point(20, yPos + 3), Width = labelWidth };
        this.Controls.Add(lblEnd);

        _dtpEndDate = new DateTimePicker
        {
            Location = new Point(controlLeft, yPos),
            Width = 200,
            Format = DateTimePickerFormat.Short,
            MinDate = DateTime.Today
        };
        _dtpEndDate.ValueChanged += OnDateChanged;
        this.Controls.Add(_dtpEndDate);

        yPos += 40;

        // Total Days Label
        var lblTotalLabel = new Label { Text = "Toplam Gün:", Location = new Point(20, yPos + 3), Width = labelWidth };
        this.Controls.Add(lblTotalLabel);

        _lblTotalDays = new Label
        {
            Text = "1",
            Location = new Point(controlLeft, yPos + 3),
            Width = 100,
            Font = new Font(this.Font, FontStyle.Bold)
        };
        this.Controls.Add(_lblTotalDays);

        yPos += 40;

        // Reason
        var lblReason = new Label { Text = "Gerekçe:", Location = new Point(20, yPos), Width = labelWidth };
        this.Controls.Add(lblReason);

        _txtReason = new TextBox
        {
            Location = new Point(controlLeft, yPos),
            Width = 380,
            Height = 60,
            Multiline = true,
            MaxLength = 500
        };
        this.Controls.Add(_txtReason);

        yPos += 80;

        // Buttons
        _btnSubmit = new Button
        {
            Text = "Talebi Gönder",
            Location = new Point(controlLeft, yPos),
            Width = 130,
            Height = 35,
            BackColor = Color.FromArgb(76, 175, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnSubmit.FlatAppearance.BorderSize = 0;
        _btnSubmit.Click += BtnSubmit_Click;
        this.Controls.Add(_btnSubmit);

        _btnCancel = new Button
        {
            Text = "İptal",
            Location = new Point(controlLeft + 140, yPos),
            Width = 100,
            Height = 35,
            FlatStyle = FlatStyle.Flat
        };
        _btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
        this.Controls.Add(_btnCancel);

        yPos += 55;

        // History Section
        var lblHistory = new Label
        {
            Text = "İzin Geçmişim",
            Location = new Point(20, yPos),
            Width = 200,
            Font = new Font(this.Font.FontFamily, 11, FontStyle.Bold)
        };
        this.Controls.Add(lblHistory);

        yPos += 30;

        _gridHistory = new DataGridView
        {
            Location = new Point(20, yPos),
            Size = new Size(560, 150),
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect
        };

        _gridHistory.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tür", DataPropertyName = "LeaveType", Width = 80 });
        _gridHistory.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Başlangıç", DataPropertyName = "StartDate", Width = 90 });
        _gridHistory.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Bitiş", DataPropertyName = "EndDate", Width = 90 });
        _gridHistory.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Gün", DataPropertyName = "TotalDays", Width = 50 });
        _gridHistory.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Durum", DataPropertyName = "Status", Width = 80 });
        _gridHistory.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Gerekçe", DataPropertyName = "Reason", Width = 160 });

        _gridHistory.CellFormatting += GridHistory_CellFormatting;

        this.Controls.Add(_gridHistory);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await LoadHistoryAsync();
    }

    private async Task LoadHistoryAsync()
    {
        if (_currentEmployee == null || _gridHistory == null) return;

        try
        {
            var history = await _leaveRepository.GetByEmployeeAsync(_currentEmployee.Id);
            _gridHistory.DataSource = history.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading leave history for employee {EmployeeId}", _currentEmployee.Id);
            MessageBox.Show("İzin geçmişi şu anda yüklenemiyor.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void OnDateChanged(object? sender, EventArgs e)
    {
        if (_dtpStartDate == null || _dtpEndDate == null || _lblTotalDays == null) return;

        if (_dtpEndDate.Value < _dtpStartDate.Value)
        {
            _dtpEndDate.Value = _dtpStartDate.Value;
        }

        var days = (_dtpEndDate.Value.Date - _dtpStartDate.Value.Date).Days + 1;
        _lblTotalDays.Text = days.ToString();

        // Warn if exceeding remaining days
        if (_currentEmployee != null && days > _currentEmployee.RemainingLeaveDays)
        {
            _lblTotalDays.ForeColor = Color.Red;
        }
        else
        {
            _lblTotalDays.ForeColor = Color.Black;
        }
    }

    private async void BtnSubmit_Click(object? sender, EventArgs e)
    {
        if (_currentEmployee == null)
        {
            MessageBox.Show("Çalışan kaydı bulunamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var totalDays = (_dtpEndDate!.Value.Date - _dtpStartDate!.Value.Date).Days + 1;

        // Validation
        if (totalDays > _currentEmployee.RemainingLeaveDays)
        {
            var result = MessageBox.Show(
                $"{totalDays} gün izin istiyorsunuz fakat yalnızca {_currentEmployee.RemainingLeaveDays} gününüz kaldı.\n\nYine de gönderilsin mi?",
                "Yetersiz İzin Günü",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes) return;
        }

        SetFormEnabled(false);

        try
        {
            var leaveType = (_cmbLeaveType!.SelectedItem as LeaveTypeOption)?.Value ?? LeaveType.Annual;

            var request = new LeaveRequest
            {
                EmployeeId = _currentEmployee.Id,
                LeaveType = leaveType,
                StartDate = _dtpStartDate.Value.Date,
                EndDate = _dtpEndDate.Value.Date,
                TotalDays = totalDays,
                Reason = _txtReason?.Text
            };

            await _leaveRepository.CreateLeaveRequestAsync(request);

            MessageBox.Show("İzin talebi başarıyla gönderildi!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (InvalidOperationException ex)
        {
            // Business logic errors (overlapping dates)
            MessageBox.Show(ex.Message, "Talep Reddedildi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetFormEnabled(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting request for employee {EmployeeId}", _currentEmployee.Id);
            MessageBox.Show("Talebiniz gönderilirken beklenmeyen bir hata oluştu.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            SetFormEnabled(true);
        }
    }

    private void SetFormEnabled(bool enabled)
    {
        if (_cmbLeaveType != null) _cmbLeaveType.Enabled = enabled;
        if (_dtpStartDate != null) _dtpStartDate.Enabled = enabled;
        if (_dtpEndDate != null) _dtpEndDate.Enabled = enabled;
        if (_txtReason != null) _txtReason.Enabled = enabled;
        if (_btnSubmit != null)
        {
            _btnSubmit.Enabled = enabled;
            _btnSubmit.Text = enabled ? "Talebi Gönder" : "Gönderiliyor...";
        }
        if (_btnCancel != null) _btnCancel.Enabled = enabled;
    }

    private void GridHistory_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_gridHistory == null) return;
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        var request = _gridHistory.Rows[e.RowIndex].DataBoundItem as LeaveRequest;
        if (request == null) return;

        var col = _gridHistory.Columns[e.ColumnIndex];
        if (col?.DataPropertyName == "Status")
        {
            e.Value = request.Status switch
            {
                LeaveStatus.Pending => "Beklemede",
                LeaveStatus.Approved => "Onaylandı",
                LeaveStatus.Rejected => "Reddedildi",
                _ => request.Status.ToString()
            };
            e.FormattingApplied = true;
        }
        else if (col?.DataPropertyName == "LeaveType")
        {
            e.Value = request.LeaveType switch
            {
                LeaveType.Annual => "Yıllık",
                LeaveType.Sick => "Hastalık",
                LeaveType.Unpaid => "Ücretsiz",
                LeaveType.Personal => "Mazeret",
                _ => request.LeaveType.ToString()
            };
            e.FormattingApplied = true;
        }
    }
}
