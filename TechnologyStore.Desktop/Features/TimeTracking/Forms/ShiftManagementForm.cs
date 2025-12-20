using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using TechnologyStore.Desktop.Features.Auth;
using TechnologyStore.Shared.Interfaces;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Desktop.Features.TimeTracking.Forms;

public class ShiftManagementForm : Form
{
    private readonly IWorkShiftRepository _shiftRepository;
    private readonly IUserRepository _userRepository;
    private readonly AuthenticationService _authService;

    private ComboBox _cbEmployees;
    private DateTimePicker _dtpDate;
    private DateTimePicker _dtpStartTime;
    private DateTimePicker _dtpEndTime;
    private DataGridView _gridShifts;

    public ShiftManagementForm(IWorkShiftRepository shiftRepository, IUserRepository userRepository, AuthenticationService authService)
    {
        _shiftRepository = shiftRepository;
        _userRepository = userRepository;
        _authService = authService;

        InitializeComponent();
    }

    private void InitializeComponent()
    {
        this.Text = "Shift Management";
        this.Size = new Size(900, 600);
        this.StartPosition = FormStartPosition.CenterScreen;

        var mainLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(10)
        };
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 300)); // Input Panel
        mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // Grid Panel

        // --- SECTION 1: Assignment Panel ---
        var inputGroup = new GroupBox
        {
            Text = "Assign New Shift",
            Dock = DockStyle.Fill,
            Padding = new Padding(10)
        };
        
        var inputPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };

        inputPanel.Controls.Add(new Label { Text = "Employee:", AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
        _cbEmployees = new ComboBox { Width = 250, DropDownStyle = ComboBoxStyle.DropDownList };
        inputPanel.Controls.Add(_cbEmployees);

        inputPanel.Controls.Add(new Label { Text = "Date:", AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
        _dtpDate = new DateTimePicker { Width = 250, Format = DateTimePickerFormat.Short };
        inputPanel.Controls.Add(_dtpDate);

        inputPanel.Controls.Add(new Label { Text = "Start Time:", AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
        _dtpStartTime = new DateTimePicker { Width = 250, Format = DateTimePickerFormat.Time, ShowUpDown = true };
        inputPanel.Controls.Add(_dtpStartTime);

        inputPanel.Controls.Add(new Label { Text = "End Time:", AutoSize = true, Margin = new Padding(0, 10, 0, 0) });
        _dtpEndTime = new DateTimePicker { Width = 250, Format = DateTimePickerFormat.Time, ShowUpDown = true };
        inputPanel.Controls.Add(_dtpEndTime);

        var btnAssign = new Button
        {
            Text = "Assign Shift",
            BackColor = Color.SteelBlue,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Size = new Size(250, 40),
            Margin = new Padding(0, 20, 0, 0)
        };
        btnAssign.Click += async (s, e) => await AssignShiftAsync();
        inputPanel.Controls.Add(btnAssign);

        inputGroup.Controls.Add(inputPanel);
        mainLayout.Controls.Add(inputGroup, 0, 0);

        // --- SECTION 2: Shift Grid ---
        var gridGroup = new GroupBox
        {
            Text = "Scheduled Shifts (This Week)",
            Dock = DockStyle.Fill
        };

        _gridShifts = new DataGridView
        {
            Dock = DockStyle.Fill,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false
        };
        
        gridGroup.Controls.Add(_gridShifts);
        mainLayout.Controls.Add(gridGroup, 1, 0);

        this.Controls.Add(mainLayout);
        this.Load += async (s, e) => await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // Load employees
            var employees = await _userRepository.GetAllAsync(); // Ensure UserRepo has this method or similar
            _cbEmployees.DisplayMember = "FullName";
            _cbEmployees.ValueMember = "Id";
            _cbEmployees.DataSource = employees.ToList();

            await LoadShiftsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading data: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadShiftsAsync()
    {
        var startOfWeek = DateTime.Today.AddDays(-(int)DateTime.Today.DayOfWeek);
        var endOfWeek = startOfWeek.AddDays(7);

        var shifts = await _shiftRepository.GetAllAsync(startOfWeek, endOfWeek);
        
        _gridShifts.DataSource = shifts.Select(s => new
        {
            s.EmployeeName,
            Date = s.StartTime.ToString("yyyy-MM-dd"),
            Start = s.StartTime.ToString("HH:mm"),
            End = s.EndTime.ToString("HH:mm"),
            s.Status
        }).ToList();
    }

    private async Task AssignShiftAsync()
    {
        if (_cbEmployees.SelectedValue == null)
        {
            MessageBox.Show("Please select an employee.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var userId = (int)_cbEmployees.SelectedValue;
        var date = _dtpDate.Value.Date;
        
        var start = date.Add(_dtpStartTime.Value.TimeOfDay);
        var end = date.Add(_dtpEndTime.Value.TimeOfDay);

        if (end <= start)
        {
             MessageBox.Show("End time must be after start time.", "Validation Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
             return;
        }

        var shift = new WorkShift
        {
            UserId = userId,
            StartTime = start,
            EndTime = end,
            CreatedBy = _authService.CurrentUser?.Id,
            Notes = "Assigned via Manager Dashboard"
        };

        try
        {
            await _shiftRepository.CreateAsync(shift);
            MessageBox.Show("Shift assigned successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await LoadShiftsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to assign shift: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
