using System.Drawing;
using System.Windows.Forms;
using TechnologyStore.Shared.Interfaces;

using static TechnologyStore.Customer.UiConstants;

namespace TechnologyStore.Customer.Forms;

/// <summary>
/// Customer registration form
/// </summary>
public partial class RegistrationForm : Form
{
    private readonly ICustomerAuthService _authService;
    
    private TextBox? _txtEmail;
    private TextBox? _txtPassword;
    private TextBox? _txtConfirmPassword;
    private TextBox? _txtFullName;
    private TextBox? _txtPhone;
    private Button? _btnRegister;
    private Button? _btnCancel;
    private Label? _lblError;

    public RegistrationForm(ICustomerAuthService authService)
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
        this.ClientSize = new Size(450, 520);
        this.Name = "RegistrationForm";
        this.Text = "Create Account";
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.BackColor = Color.White;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        int yPos = 25;
        int centerX = (this.ClientSize.Width - 300) / 2;

        // Title
        var lblTitle = new Label
        {
            Text = "📝 Create Your Account",
            Location = new Point(0, yPos),
            Width = this.ClientSize.Width,
            Font = new Font(DefaultFontFamily, 16, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Color.FromArgb(0, 120, 212)
        };
        this.Controls.Add(lblTitle);

        yPos += 50;

        // Full Name
        AddField("Full Name *", ref yPos, centerX, out _txtFullName, false);
        
        // Email
        AddField("Email Address *", ref yPos, centerX, out _txtEmail, false);
        
        // Phone
        AddField("Phone (optional)", ref yPos, centerX, out _txtPhone, false);
        
        // Password
        AddField("Password *", ref yPos, centerX, out _txtPassword, true);
        
        // Confirm Password
        AddField("Confirm Password *", ref yPos, centerX, out _txtConfirmPassword, true);

        // Error label
        _lblError = new Label
        {
            Location = new Point(centerX, yPos),
            Width = 300,
            Height = 45,
            ForeColor = Color.Red,
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(DefaultFontFamily, 9),
            Visible = false
        };
        this.Controls.Add(_lblError);

        yPos += 50;

        // Register button
        _btnRegister = new Button
        {
            Text = "Create Account",
            Location = new Point(centerX, yPos),
            Width = 145,
            Height = 40,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(DefaultFontFamily, 10, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnRegister.FlatAppearance.BorderSize = 0;
        _btnRegister.Click += BtnRegister_Click;
        this.Controls.Add(_btnRegister);

        // Cancel button
        _btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(centerX + 155, yPos),
            Width = 145,
            Height = 40,
            BackColor = Color.FromArgb(108, 117, 125),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(DefaultFontFamily, 10),
            Cursor = Cursors.Hand
        };
        _btnCancel.FlatAppearance.BorderSize = 0;
        _btnCancel.Click += (s, e) => { this.DialogResult = DialogResult.Cancel; this.Close(); };
        this.Controls.Add(_btnCancel);

        yPos += 55;

        // Password requirements
        var lblRequirements = new Label
        {
            Text = "* Password must be at least 6 characters",
            Location = new Point(centerX, yPos),
            Width = 300,
            Font = new Font(DefaultFontFamily, 8),
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleCenter
        };
        this.Controls.Add(lblRequirements);

        this.AcceptButton = _btnRegister;
        this.CancelButton = _btnCancel;
    }

    private void AddField(string labelText, ref int yPos, int centerX, out TextBox textBox, bool isPassword)
    {
        var label = new Label
        {
            Text = labelText,
            Location = new Point(centerX, yPos),
            Width = 300,
            Font = new Font(DefaultFontFamily, 9, FontStyle.Bold)
        };
        this.Controls.Add(label);

        yPos += 22;

        textBox = new TextBox
        {
            Location = new Point(centerX, yPos),
            Width = 300,
            Height = 28,
            Font = new Font(DefaultFontFamily, 10)
        };
        
        if (isPassword)
        {
            textBox.PasswordChar = '●';
        }
        
        this.Controls.Add(textBox);

        yPos += 40;
    }

    private async void BtnRegister_Click(object? sender, EventArgs e)
    {
        ClearError();

        var fullName = _txtFullName?.Text?.Trim() ?? string.Empty;
        var email = _txtEmail?.Text?.Trim() ?? string.Empty;
        var phone = _txtPhone?.Text?.Trim();
        var password = _txtPassword?.Text ?? string.Empty;
        var confirmPassword = _txtConfirmPassword?.Text ?? string.Empty;

        // Validation
        if (string.IsNullOrWhiteSpace(fullName))
        {
            ShowError("Please enter your full name.");
            _txtFullName?.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(email))
        {
            ShowError("Please enter your email address.");
            _txtEmail?.Focus();
            return;
        }

        if (!IsValidEmail(email))
        {
            ShowError("Please enter a valid email address.");
            _txtEmail?.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Please enter a password.");
            _txtPassword?.Focus();
            return;
        }

        if (password.Length < 6)
        {
            ShowError("Password must be at least 6 characters.");
            _txtPassword?.Focus();
            return;
        }

        if (password != confirmPassword)
        {
            ShowError("Passwords do not match.");
            _txtConfirmPassword?.Clear();
            _txtConfirmPassword?.Focus();
            return;
        }

        SetFormEnabled(false);

        try
        {
            var result = await _authService.RegisterAsync(email, password, fullName, phone);

            if (result.Success)
            {
                MessageBox.Show(
                    "Account created successfully! You are now logged in.",
                    "Welcome!",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                ShowError(result.ErrorMessage ?? "Registration failed.");
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

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
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
        if (_txtFullName != null) _txtFullName.Enabled = enabled;
        if (_txtEmail != null) _txtEmail.Enabled = enabled;
        if (_txtPhone != null) _txtPhone.Enabled = enabled;
        if (_txtPassword != null) _txtPassword.Enabled = enabled;
        if (_txtConfirmPassword != null) _txtConfirmPassword.Enabled = enabled;
        if (_btnRegister != null)
        {
            _btnRegister.Enabled = enabled;
            _btnRegister.Text = enabled ? "Create Account" : "Creating...";
        }
        if (_btnCancel != null) _btnCancel.Enabled = enabled;
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        _txtFullName?.Focus();
    }
}
