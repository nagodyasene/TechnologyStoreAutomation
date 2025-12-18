using TechnologyStore.Desktop.Services;
using System.Drawing;
using System.Windows.Forms;
using TechnologyStore.Desktop.Features.Auth;
using TechnologyStore.Desktop.Features.Leave;
using Microsoft.Extensions.Logging;

namespace TechnologyStore.Desktop.UI.Forms;

/// <summary>
/// Form for admins to approve or reject leave requests
/// </summary>
public partial class LeaveApprovalForm : Form
{
    private readonly ILeaveRepository _leaveRepository;
    private readonly IAuthenticationService _authService;
    private readonly ILogger<LeaveApprovalForm> _logger;

    private DataGridView? _gridRequests;
    private ComboBox? _cmbStatusFilter;
    private Button? _btnApprove;
    private Button? _btnReject;
    private Button? _btnRefresh;
    private Label? _lblStatus;

    public LeaveApprovalForm(ILeaveRepository leaveRepository, IAuthenticationService authService)
    {
        _leaveRepository = leaveRepository ?? throw new ArgumentNullException(nameof(leaveRepository));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = AppLogger.CreateLogger<LeaveApprovalForm>();

        InitializeComponent();
        SetupUI();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(800, 500);
        this.Name = "LeaveApprovalForm";
        this.Text = "Leave Request Approval";
        this.StartPosition = FormStartPosition.CenterParent;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        // Title
        var lblTitle = new Label
        {
            Text = "✅ Leave Request Approval",
            Location = new Point(20, 15),
            Width = 300,
            Font = new Font(this.Font.FontFamily, 14, FontStyle.Bold)
        };
        this.Controls.Add(lblTitle);

        // Toolbar Panel
        var toolbar = new Panel
        {
            Location = new Point(0, 50),
            Size = new Size(800, 45),
            BackColor = Color.FromArgb(240, 240, 240)
        };

        // Status Filter
        var lblFilter = new Label { Text = "Filter:", Location = new Point(20, 12), Width = 40 };
        toolbar.Controls.Add(lblFilter);

        _cmbStatusFilter = new ComboBox
        {
            Location = new Point(65, 8),
            Width = 120,
            DropDownStyle = ComboBoxStyle.DropDownList
        };
        _cmbStatusFilter.Items.AddRange(new object[] { "All", "Pending", "Approved", "Rejected" });
        _cmbStatusFilter.SelectedIndex = 1; // Default to Pending
        _cmbStatusFilter.SelectedIndexChanged += async (s, e) => await LoadRequestsAsync();
        toolbar.Controls.Add(_cmbStatusFilter);

        // Approve Button
        _btnApprove = new Button
        {
            Text = "✓ Approve",
            Location = new Point(200, 7),
            Size = new Size(100, 30),
            BackColor = Color.FromArgb(76, 175, 80),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnApprove.FlatAppearance.BorderSize = 0;
        _btnApprove.Click += BtnApprove_Click;
        toolbar.Controls.Add(_btnApprove);

        // Reject Button
        _btnReject = new Button
        {
            Text = "✗ Reject",
            Location = new Point(310, 7),
            Size = new Size(100, 30),
            BackColor = Color.FromArgb(244, 67, 54),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnReject.FlatAppearance.BorderSize = 0;
        _btnReject.Click += BtnReject_Click;
        toolbar.Controls.Add(_btnReject);

        // Refresh Button
        _btnRefresh = new Button
        {
            Text = "🔄 Refresh",
            Location = new Point(420, 7),
            Size = new Size(100, 30),
            FlatStyle = FlatStyle.Flat
        };
        _btnRefresh.Click += async (s, e) => await LoadRequestsAsync();
        toolbar.Controls.Add(_btnRefresh);

        this.Controls.Add(toolbar);

        // Grid
        _gridRequests = new DataGridView
        {
            Location = new Point(20, 105),
            Size = new Size(760, 340),
            AutoGenerateColumns = false,
            ReadOnly = true,
            AllowUserToAddRows = false,
            RowHeadersVisible = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            MultiSelect = false
        };

        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ID", DataPropertyName = "Id", Width = 40 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Employee", DataPropertyName = "EmployeeName", Width = 130 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Type", DataPropertyName = "LeaveType", Width = 70 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "From", DataPropertyName = "StartDate", Width = 85 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "To", DataPropertyName = "EndDate", Width = 85 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Days", DataPropertyName = "TotalDays", Width = 45 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Status", DataPropertyName = "Status", Width = 70 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Reason", DataPropertyName = "Reason", Width = 150 });
        _gridRequests.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Submitted", DataPropertyName = "CreatedAt", Width = 85 });

        // Color rows by status
        _gridRequests.CellFormatting += GridRequests_CellFormatting;

        this.Controls.Add(_gridRequests);

        // Status Label
        _lblStatus = new Label
        {
            Location = new Point(20, 455),
            Width = 500,
            Text = "Select a pending request to approve or reject."
        };
        this.Controls.Add(_lblStatus);
    }

    protected override async void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        await LoadRequestsAsync();
    }

    private async Task LoadRequestsAsync()
    {
        if (_gridRequests == null || _cmbStatusFilter == null) return;

        try
        {
            if (_lblStatus != null) _lblStatus.Text = "Loading...";

            LeaveStatus? statusFilter = _cmbStatusFilter.SelectedItem?.ToString() switch
            {
                "Pending" => LeaveStatus.Pending,
                "Approved" => LeaveStatus.Approved,
                "Rejected" => LeaveStatus.Rejected,
                _ => null
            };

            var requests = await _leaveRepository.GetAllRequestsAsync(statusFilter);
            var list = requests.ToList();
            _gridRequests.DataSource = list;

            if (_lblStatus != null) _lblStatus.Text = $"Showing {list.Count} request(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading leave requests");
            MessageBox.Show("An error occurred while loading requests. Please try again later.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void GridRequests_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (_gridRequests == null || e.RowIndex < 0) return;

        var row = _gridRequests.Rows[e.RowIndex];
        var request = row.DataBoundItem as LeaveRequest;
        if (request == null) return;

        switch (request.Status)
        {
            case LeaveStatus.Approved:
                row.DefaultCellStyle.BackColor = Color.FromArgb(200, 255, 200);
                break;
            case LeaveStatus.Rejected:
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 200, 200);
                break;
            case LeaveStatus.Pending:
                row.DefaultCellStyle.BackColor = Color.FromArgb(255, 255, 200);
                break;
        }
    }

    private async void BtnApprove_Click(object? sender, EventArgs e)
    {
        var request = GetSelectedRequest();
        if (request == null) return;

        if (request.Status != LeaveStatus.Pending)
        {
            MessageBox.Show("Only pending requests can be approved.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var result = MessageBox.Show(
            $"Approve leave request from {request.EmployeeName}?\n\n" +
            $"Type: {request.LeaveType}\n" +
            $"Dates: {request.StartDate:d} to {request.EndDate:d}\n" +
            $"Days: {request.TotalDays}",
            "Confirm Approval",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question);

        if (result != DialogResult.Yes) return;

        if (_authService.CurrentUser == null)
        {
            MessageBox.Show("You must be logged in to approve requests.", "Authentication Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            await _leaveRepository.ApproveAsync(request.Id, _authService.CurrentUser.Id);
            MessageBox.Show("Leave request approved!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadRequestsAsync();
        }
        catch (InvalidOperationException ex)
        {
            // Business validation errors (like insufficient balance) should be shown to user
            MessageBox.Show(ex.Message, "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error approving leave request {RequestId}", request.Id);
            MessageBox.Show("An unexpected error occurred while approving the request.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnReject_Click(object? sender, EventArgs e)
    {
        var request = GetSelectedRequest();
        if (request == null) return;

        if (request.Status != LeaveStatus.Pending)
        {
            MessageBox.Show("Only pending requests can be rejected.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        // Show input dialog for rejection reason
        var comment = ShowInputDialog("Rejection Reason", "Please provide a reason for rejection:");
        if (string.IsNullOrWhiteSpace(comment))
        {
            MessageBox.Show("A rejection reason is required.", "Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (_authService.CurrentUser == null)
        {
            MessageBox.Show("You must be logged in to reject requests.", "Authentication Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            await _leaveRepository.RejectAsync(request.Id, _authService.CurrentUser.Id, comment);
            MessageBox.Show("Leave request rejected.", "Done", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadRequestsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error rejecting leave request {RequestId}", request.Id);
            MessageBox.Show("An unexpected error occurred while rejection the request.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private LeaveRequest? GetSelectedRequest()
    {
        if (_gridRequests == null || _gridRequests.SelectedRows.Count == 0)
        {
            MessageBox.Show("Please select a request first.", "Selection Required", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
        }

        return _gridRequests.SelectedRows[0].DataBoundItem as LeaveRequest;
    }

    private static string? ShowInputDialog(string title, string prompt)
    {
        var form = new Form
        {
            Width = 400,
            Height = 180,
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false
        };

        var label = new Label { Left = 20, Top = 20, Width = 350, Text = prompt };
        var textBox = new TextBox { Left = 20, Top = 50, Width = 350, Height = 60, Multiline = true };
        var btnOk = new Button { Text = "OK", Left = 200, Top = 105, Width = 80, DialogResult = DialogResult.OK };
        var btnCancel = new Button { Text = "Cancel", Left = 290, Top = 105, Width = 80, DialogResult = DialogResult.Cancel };

        form.Controls.AddRange(new Control[] { label, textBox, btnOk, btnCancel });
        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;

        return form.ShowDialog() == DialogResult.OK ? textBox.Text : null;
    }
}
