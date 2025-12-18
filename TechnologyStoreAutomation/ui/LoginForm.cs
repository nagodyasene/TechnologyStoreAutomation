using System.Drawing;
using System.Windows.Forms;
using TechnologyStoreAutomation.backend.auth;

namespace TechnologyStoreAutomation.ui;

/// <summary>
/// Login form for user authentication
/// </summary>
public partial class LoginForm : Form
{
    private readonly IAuthenticationService _authService;
    
    private TextBox? _txtUsername;
    private TextBox? _txtPassword;
    private Button? _btnLogin;
    private Button? _btnCancel;
    private Label? _lblError;
    private CheckBox? _chkRememberMe;

    public LoginForm(IAuthenticationService authService)
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
        this.ClientSize = new Size(400, 280);
        this.Name = "LoginForm";
        this.Text = "Technology Store - Login";
        this.StartPosition = FormStartPosition.CenterScreen;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        int yPos = 30;
        int labelWidth = 100;
        int controlLeft = labelWidth + 40;

        // Title
        var lblTitle = new Label
        {
            Text = "🔐 Please Sign In",
            Location = new Point(20, yPos),
            Width = 360,
            Font = new Font(this.Font.FontFamily, 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter
        };
        this.Controls.Add(lblTitle);

        yPos += 50;

        // Username Label & TextBox
        var lblUsername = new Label
        {
            Text = "Username:",
            Location = new Point(20, yPos + 3),
            Width = labelWidth
        };
        this.Controls.Add(lblUsername);

        _txtUsername = new TextBox
        {
            Location = new Point(controlLeft, yPos),
            Width = 220,
            MaxLength = 100
        };
        _txtUsername.KeyDown += OnTextBoxKeyDown;
        this.Controls.Add(_txtUsername);

        yPos += 40;

        // Password Label & TextBox
        var lblPassword = new Label
        {
            Text = "Password:",
            Location = new Point(20, yPos + 3),
            Width = labelWidth
        };
        this.Controls.Add(lblPassword);

        _txtPassword = new TextBox
        {
            Location = new Point(controlLeft, yPos),
            Width = 220,
            PasswordChar = '●',
            MaxLength = 100
        };
        _txtPassword.KeyDown += OnTextBoxKeyDown;
        this.Controls.Add(_txtPassword);

        yPos += 40;

        // Remember Me Checkbox
        _chkRememberMe = new CheckBox
        {
            Text = "Remember me",
            Location = new Point(controlLeft, yPos),
            Width = 150,
            Visible = true
        };
        this.Controls.Add(_chkRememberMe);

        // Error Label
        _lblError = new Label
        {
            Location = new Point(20, yPos),
            Width = 360,
            Height = 40,
            ForeColor = Color.Red,
            TextAlign = ContentAlignment.MiddleCenter,
            Visible = false
        };
        this.Controls.Add(_lblError);

        yPos += 50;

        // Login Button
        _btnLogin = new Button
        {
            Text = "Login",
            Location = new Point(controlLeft, yPos),
            Width = 100,
            Height = 35,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnLogin.FlatAppearance.BorderSize = 0;
        _btnLogin.Click += BtnLogin_Click;
        this.Controls.Add(_btnLogin);

        // Cancel Button
        _btnCancel = new Button
        {
            Text = "Exit",
            Location = new Point(controlLeft + 110, yPos),
            Width = 100,
            Height = 35,
            FlatStyle = FlatStyle.Flat
        };
        _btnCancel.Click += (s, e) =>
        {
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        };
        this.Controls.Add(_btnCancel);

        // Set accept and cancel buttons
        this.AcceptButton = _btnLogin;
        this.CancelButton = _btnCancel;
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

        var username = _txtUsername?.Text?.Trim() ?? string.Empty;
        var password = _txtPassword?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Please enter your username.");
            _txtUsername?.Focus();
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
            var result = await _authService.LoginAsync(username, password);

            if (result.Success)
            {
                // Save 'Remember Me' preference
                if (_chkRememberMe != null)
                {
                    UserPreferences.Save(username, _chkRememberMe.Checked);
                }

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
        if (_txtUsername != null) _txtUsername.Enabled = enabled;
        if (_txtPassword != null) _txtPassword.Enabled = enabled;
        if (_btnLogin != null)
        {
            _btnLogin.Enabled = enabled;
            _btnLogin.Text = enabled ? "Login" : "Logging in...";
        }
        if (_btnCancel != null) _btnCancel.Enabled = enabled;
        if (_chkRememberMe != null) _chkRememberMe.Enabled = enabled;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);

        // Load 'Remember Me' preference
        var prefs = UserPreferences.Load();
        if (prefs.RememberMe && !string.IsNullOrEmpty(prefs.Username))
        {
            if (_txtUsername != null) _txtUsername.Text = prefs.Username;
            if (_chkRememberMe != null) _chkRememberMe.Checked = true;
            _txtPassword?.Focus();
        }
        else
        {
            _txtUsername?.Focus();
        }
    }
}
