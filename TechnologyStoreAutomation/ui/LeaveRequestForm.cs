using System.Drawing;
using System.Windows.Forms;
using TechnologyStoreAutomation.backend.auth;
using TechnologyStoreAutomation.backend.leave;
using Microsoft.Extensions.Logging;

namespace TechnologyStoreAutomation.ui;

/// <summary>
/// Form for employees to submit leave requests
/// </summary>
public partial class LeaveRequestForm : Form
{
    private readonly ILeaveRepository _leaveRepository;
    private readonly IAuthenticationService _authService;
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

    public LeaveRequestForm(ILeaveRepository leaveRepository, IAuthenticationService authService, Employee? employee)
    {
        _leaveRepository = leaveRepository ?? throw new ArgumentNullException(nameof(leaveRepository));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
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
        this.Text = "Request Leave";
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
            Text = "📅 New Leave Request",
            Location = new Point(20, yPos),
            Width = 300,
            Font = new Font(this.Font.FontFamily, 14, FontStyle.Bold)
        };
        this.Controls.Add(lblTitle);

        yPos += 45;

        // Remaining Days Info
        _lblRemainingDays = new Label
        {
            Text = $"Remaining Leave Days: {_currentEmployee?.RemainingLeaveDays ?? 0}",
            Location = new Point(20, yPos),
            Width = 300,
            ForeColor = Color.FromArgb(0, 120, 212),
            Font = new Font(this.Font, FontStyle.Bold)
        };
        this.Controls.Add(_lblRemainingDays);

        yPos += 35;

        // Leave Type
        var lblType = new Label { Text = "Leave Type:", Location = new Point(20, yPos + 3), Width = labelWidth };
        this.Controls.Add(lblType);

        _cmbLeaveType = new ComboBox
        {
            Location = new Point(controlLeft, yPos),
            Width = 200,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbLeaveType.Items.AddRange(Enum.GetNames<LeaveType>());
        _cmbLeaveType.SelectedIndex = 0;
        this.Controls.Add(_cmbLeaveType);

        yPos += 40;

        // Start Date
        var lblStart = new Label { Text = "Start Date:", Location = new Point(20, yPos + 3), Width = labelWidth };
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
        var lblEnd = new Label { Text = "End Date:", Location = new Point(20, yPos + 3), Width = labelWidth };
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
        var lblTotalLabel = new Label { Text = "Total Days:", Location = new Point(20, yPos + 3), Width = labelWidth };
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
        var lblReason = new Label { Text = "Reason:", Location = new Point(20, yPos), Width = labelWidth };
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
            Text = "Submit Request",
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
            Text = "Cancel",
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
            Text = "📋 My Leave History",
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

        _gridHistory.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", DataPropertyName = "LeaveType", Width = 80 });
        _gridHistory.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "From", DataPropertyName = "StartDate", Width = 90 });
        _gridHistory.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "To", DataPropertyName = "EndDate", Width = 90 });
        _gridHistory.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Days", DataPropertyName = "TotalDays", Width = 50 });
        _gridHistory.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", Width = 80 });
        _gridHistory.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reason", DataPropertyName = "Reason", Width = 160 });

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
            MessageBox.Show("Unable to load leave history at this time.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            MessageBox.Show("Employee record not found.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        var totalDays = (_dtpEndDate!.Value.Date - _dtpStartDate!.Value.Date).Days + 1;

        // Validation
        if (totalDays > _currentEmployee.RemainingLeaveDays)
        {
            var result = MessageBox.Show(
                $"You are requesting {totalDays} days but only have {_currentEmployee.RemainingLeaveDays} remaining.\n\nSubmit anyway?",
                "Insufficient Leave Days",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes) return;
        }

        SetFormEnabled(false);

        try
        {
            var leaveType = Enum.Parse<LeaveType>(_cmbLeaveType!.SelectedItem!.ToString()!);

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

            MessageBox.Show("Leave request submitted successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (InvalidOperationException ex)
        {
            // Business logic errors (overlapping dates)
            MessageBox.Show(ex.Message, "Request Denied", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            SetFormEnabled(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error submitting request for employee {EmployeeId}", _currentEmployee.Id);
            MessageBox.Show($"An unexpected error occurred while submitting your request.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            _btnSubmit.Text = enabled ? "Submit Request" : "Submitting...";
        }
        if (_btnCancel != null) _btnCancel.Enabled = enabled;
    }
}
