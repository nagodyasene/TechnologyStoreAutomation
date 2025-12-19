using System.Drawing;
using System.Windows.Forms;
using TechnologyStore.Shared.Interfaces;

namespace TechnologyStore.Customer.Forms;

/// <summary>
/// Customer login form with options for login, registration, and guest checkout
/// </summary>
public partial class CustomerLoginForm : Form
{
    private readonly ICustomerAuthService _authService;
    
    private TextBox? _txtEmail;
    private TextBox? _txtPassword;
    private Button? _btnLogin;
    private Button? _btnRegister;
    private Button? _btnGuest;
    private Label? _lblError;

    public bool IsGuestCheckout { get; private set; }

    public CustomerLoginForm(ICustomerAuthService authService)
    {
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        InitializeComponent();
        SetupUI();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(450, 400);
        this.Name = "CustomerLoginForm";
        this.Text = "Technology Store - Customer Login";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.White;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        int yPos = 30;
        int centerX = (this.ClientSize.Width - 300) / 2;

        // Logo/Title
        var lblTitle = new Label
        {
            Text = "🛒 Technology Store",
            Location = new Point(0, yPos),
            Width = this.ClientSize.Width,
            Font = new Font("Segoe UI", 20, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(0, 120, 212)
        };
        this.Controls.Add(lblTitle);

        yPos += 60;

        var lblSubtitle = new Label
        {
            Text = "Welcome! Please sign in to continue",
            Location = new Point(0, yPos),
            Width = this.ClientSize.Width,
            Font = new Font("Segoe UI", 10),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray
        };
        this.Controls.Add(lblSubtitle);

        yPos += 50;

        // Email field
        var lblEmail = new Label
        {
            Text = "Email Address",
            Location = new Point(centerX, yPos),
            Width = 300,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        this.Controls.Add(lblEmail);

        yPos += 22;

        _txtEmail = new TextBox
        {
            Location = new Point(centerX, yPos),
            Width = 300,
            Height = 30,
            Font = new Font("Segoe UI", 11)
        };
        _txtEmail.KeyDown += OnTextBoxKeyDown;
        this.Controls.Add(_txtEmail);

        yPos += 45;

        // Password field
        var lblPassword = new Label
        {
            Text = "Password",
            Location = new Point(centerX, yPos),
            Width = 300,
            Font = new Font("Segoe UI", 9, FontStyle.Bold)
        };
        this.Controls.Add(lblPassword);

        yPos += 22;

        _txtPassword = new TextBox
        {
            Location = new Point(centerX, yPos),
            Width = 300,
            Height = 30,
            Font = new Font("Segoe UI", 11),
            PasswordChar = '●'
        };
        _txtPassword.KeyDown += OnTextBoxKeyDown;
        this.Controls.Add(_txtPassword);

        yPos += 40;

        // Error label
        _lblError = new Label
        {
            Location = new Point(centerX, yPos),
            Width = 300,
            Height = 40,
            ForeColor = Color.Red,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font("Segoe UI", 9),
            Visible = false
        };
        this.Controls.Add(_lblError);

        yPos += 45;

        // Login button
        _btnLogin = new Button
        {
            Text = "Sign In",
            Location = new Point(centerX, yPos),
            Width = 300,
            Height = 40,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 11, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnLogin.FlatAppearance.BorderSize = 0;
        _btnLogin.Click += BtnLogin_Click;
        this.Controls.Add(_btnLogin);

        yPos += 55;

        // Register button
        _btnRegister = new Button
        {
            Text = "Create New Account",
            Location = new Point(centerX, yPos),
            Width = 300,
            Height = 40,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(0, 120, 212),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10),
            Cursor = Cursors.Hand
        };
        _btnRegister.FlatAppearance.BorderColor = Color.FromArgb(0, 120, 212);
        _btnRegister.Click += BtnRegister_Click;
        this.Controls.Add(_btnRegister);

        yPos += 55;

        // Divider
        var divider = new Label
        {
            Text = "─────────  OR  ─────────",
            Location = new Point(centerX, yPos),
            Width = 300,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.Gray
        };
        this.Controls.Add(divider);

        yPos += 35;

        // Guest checkout button
        _btnGuest = new Button
        {
            Text = "Continue as Guest",
            Location = new Point(centerX, yPos),
            Width = 300,
            Height = 35,
            BackColor = Color.FromArgb(108, 117, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10),
            Cursor = Cursors.Hand
        };
        _btnGuest.FlatAppearance.BorderSize = 0;
        _btnGuest.Click += BtnGuest_Click;
        this.Controls.Add(_btnGuest);

        this.AcceptButton = _btnLogin;
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            BtnLogin_Click(sender, e);
        }
    }

    private async void BtnLogin_Click(object? sender, EventArgs e)
    {
        ClearError();

        var email = _txtEmail?.Text?.Trim() ?? string.Empty;
        var password = _txtPassword?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError("Please enter your email address.");
            _txtEmail?.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Please enter your password.");
            _txtPassword?.Focus();
            return;
        }

        SetFormEnabled(false);

        try
        {
            var result = await _authService.LoginAsync(email, password);

            if (result.Success)
            {
                IsGuestCheckout = false;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Login failed.");
                _txtPassword?.Clear();
                _txtPassword?.Focus();
            }
        }
        catch (Exception ex)
        {
            ShowError($"An error occurred: {ex.Message}");
        }
        finally
        {
            SetFormEnabled(true);
        }
    }

    private void BtnRegister_Click(object? sender, EventArgs e)
    {
        using var registerForm = new RegistrationForm(_authService);
        if (registerForm.ShowDialog(this) == DialogResult.OK)
        {
            IsGuestCheckout = false;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    private void BtnGuest_Click(object? sender, EventArgs e)
    {
        IsGuestCheckout = true;
        this.DialogResult = DialogResult.OK;
        this.Close();
    }

    private void ShowError(string message)
    {
        if (_lblError != null)
        {
            _lblError.Text = message;
            _lblError.Visible = true;
        }
    }

    private void ClearError()
    {
        if (_lblError != null)
        {
            _lblError.Text = string.Empty;
            _lblError.Visible = false;
        }
    }

    private void SetFormEnabled(bool enabled)
    {
        if (_txtEmail != null) _txtEmail.Enabled = enabled;
        if (_txtPassword != null) _txtPassword.Enabled = enabled;
        if (_btnLogin != null)
        {
            _btnLogin.Enabled = enabled;
            _btnLogin.Text = enabled ? "Sign In" : "Signing in...";
        }
        if (_btnRegister != null) _btnRegister.Enabled = enabled;
        if (_btnGuest != null) _btnGuest.Enabled = enabled;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _txtEmail?.Focus();
    }
}
