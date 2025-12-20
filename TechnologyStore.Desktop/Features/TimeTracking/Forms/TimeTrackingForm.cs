using System;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TechnologyStore.Desktop.Features.Auth;

namespace TechnologyStore.Desktop.Features.TimeTracking.Forms;

public class TimeTrackingForm : Form
{
    private readonly ITimeTrackingService _timeTrackingService;
    private readonly AuthenticationService _authService;
    
    private Label _lblStatus;
    private Label _lblTimer;
    private Button _btnClockIn;
    private Button _btnClockOut;
    private Button _btnStartLunch;
    private Button _btnEndLunch;
    private DataGridView _gridHistory;
    private Timer _timer;

    public TimeTrackingForm(ITimeTrackingService timeTrackingService, AuthenticationService authService)
    {
        _timeTrackingService = timeTrackingService;
        _authService = authService;

        InitializeComponent();
        InitializeTimer();
    }

    private void InitializeComponent()
    {
        this.Text = "Time Tracking";
        this.Size = new Size(800, 600);
        this.StartPosition = FormStartPosition.CenterScreen;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(20)
        };
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100)); // Header & Status
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));  // Buttons
        mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));  // History Grid

        // --- SECTION 1: Status & Timer ---
        var statusPanel = new Panel { Dock = DockStyle.Fill };
        
        _lblStatus = new Label
        {
            Text = "Status: Loading...",
            Font = new Font("Segoe UI", 16, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(10, 10)
        };

        _lblTimer = new Label
        {
            Text = "Daily Hours: 00:00:00",
            Font = new Font("Segoe UI", 14),
            AutoSize = true,
            Location = new Point(10, 50),
            ForeColor = Color.Gray
        };

        statusPanel.Controls.Add(_lblStatus);
        statusPanel.Controls.Add(_lblTimer);
        mainLayout.Controls.Add(statusPanel, 0, 0);

        // --- SECTION 2: Action Buttons ---
        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            AutoSize = true
        };

        _btnClockIn = CreateActionButton("Clock In", Color.Green, BtnClockIn_Click);
        _btnClockOut = CreateActionButton("Clock Out", Color.Red, BtnClockOut_Click);
        _btnStartLunch = CreateActionButton("Start Lunch", Color.Orange, BtnStartLunch_Click);
        _btnEndLunch = CreateActionButton("End Lunch", Color.Teal, BtnEndLunch_Click);

        buttonPanel.Controls.AddRange(new Control[] { _btnClockIn, _btnClockOut, _btnStartLunch, _btnEndLunch });
        mainLayout.Controls.Add(buttonPanel, 0, 1);

        // --- SECTION 3: History Grid ---
        var historyGroup = new GroupBox
        {
            Text = "Recent Activity",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10)
        };

        _gridHistory = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            ReadOnly = true,
            AllowUserToAddRows = false
        };
        
        historyGroup.Controls.Add(_gridHistory);
        mainLayout.Controls.Add(historyGroup, 0, 2);

        this.Controls.Add(mainLayout);
        this.Load += async (s, e) => await LoadDataAsync();
    }

    private Button CreateActionButton(string text, Color color, EventHandler onClick)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = color,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10, FontStyle.Bold),
            Size = new Size(120, 40),
            Margin = new Padding(0, 0, 10, 0),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += onClick;
        return btn;
    }

    private void InitializeTimer()
    {
        _timer = new Timer { Interval = 60000 }; // Update every minute
        _timer.Tick += async (s, e) => await UpdateTimerAsync();
        _timer.Start();
    }

    private async Task LoadDataAsync()
    {
        if (_authService.CurrentUser == null) return;

        try
        {
            await RefreshStatusAsync();
            await RefreshHistoryAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task RefreshStatusAsync()
    {
        var userId = _authService.CurrentUser!.Id;
        var status = await _timeTrackingService.GetCurrentStatusAsync(userId);
        
        UpdateButtonsState(status?.EventType);
        await UpdateTimerAsync();
    }

    private void UpdateButtonsState(Shared.Models.TimeEntryType? lastType)
    {
        // Reset all
        _btnClockIn.Enabled = false;
        _btnClockOut.Enabled = false;
        _btnStartLunch.Enabled = false;
        _btnEndLunch.Enabled = false;

        _btnClockIn.BackColor = Color.Gray;
        _btnClockOut.BackColor = Color.Gray;
        _btnStartLunch.BackColor = Color.Gray;
        _btnEndLunch.BackColor = Color.Gray;

        if (lastType == null || lastType == Shared.Models.TimeEntryType.ClockOut)
        {
            // Not working -> Can Clock In
            EnableButton(_btnClockIn, Color.Green);
            _lblStatus.Text = "Status: Not Working";
            _lblStatus.ForeColor = Color.Black;
        }
        else if (lastType == Shared.Models.TimeEntryType.ClockIn || lastType == Shared.Models.TimeEntryType.EndLunch)
        {
            // Working -> Can Clock Out OR Start Lunch
            EnableButton(_btnClockOut, Color.Red);
            EnableButton(_btnStartLunch, Color.Orange);
            _lblStatus.Text = "Status: Working";
            _lblStatus.ForeColor = Color.Green;
        }
        else if (lastType == Shared.Models.TimeEntryType.StartLunch)
        {
            // On Lunch -> Can End Lunch
            EnableButton(_btnEndLunch, Color.Teal);
            _lblStatus.Text = "Status: On Lunch Break";
            _lblStatus.ForeColor = Color.Orange;
        }
    }

    private void EnableButton(Button btn, Color color)
    {
        btn.Enabled = true;
        btn.BackColor = color;
    }

    private async Task RefreshHistoryAsync()
    {
        var userId = _authService.CurrentUser!.Id;
        var history = await _timeTrackingService.GetUserHistoryAsync(userId, DateTime.Today.AddDays(-7), DateTime.Now);

        _gridHistory.DataSource = history.Select(h => new 
        {
            h.Timestamp,
            Event = h.EventType.ToString(),
            h.Notes,
            Manual = h.IsManualEntry ? "Yes" : "No"
        }).ToList();
    }

    private async Task UpdateTimerAsync()
    {
        if (_authService.CurrentUser == null) return;
        
        var hours = await _timeTrackingService.CalculateDailyHoursAsync(_authService.CurrentUser.Id, DateTime.Today);
        _lblTimer.Text = $"Daily Hours: {hours:hh\\:mm\\:ss}";
    }

    private async void BtnClockIn_Click(object sender, EventArgs e) => await HandleActionAsync(() => _timeTrackingService.ClockInAsync(_authService.CurrentUser!.Id));
    private async void BtnClockOut_Click(object sender, EventArgs e) => await HandleActionAsync(() => _timeTrackingService.ClockOutAsync(_authService.CurrentUser!.Id));
    private async void BtnStartLunch_Click(object sender, EventArgs e) => await HandleActionAsync(() => _timeTrackingService.StartLunchAsync(_authService.CurrentUser!.Id));
    private async void BtnEndLunch_Click(object sender, EventArgs e) => await HandleActionAsync(() => _timeTrackingService.EndLunchAsync(_authService.CurrentUser!.Id));

    private async Task HandleActionAsync(Func<Task> action)
    {
        try
        {
            await action();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Action Failed", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
