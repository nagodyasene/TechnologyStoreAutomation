using TechnologyStore.Desktop.Services;
using TechnologyStore.Desktop.Config;
using Microsoft.Extensions.Logging;

namespace TechnologyStore.Desktop.UI.Forms;

/// <summary>
/// Settings form for configuring email sending mode and other application settings
/// </summary>
public partial class SettingsForm : Form
{
    private readonly EmailSettings _emailSettings;
    private readonly ILogger<SettingsForm> _logger;
    
    private CheckBox? _chkTestMode;
    private TextBox? _txtSenderEmail;
    private Label? _lblStatus;
    private Button? _btnSave;
    private Button? _btnCancel;

    public SettingsForm(EmailSettings emailSettings)
    {
        _emailSettings = emailSettings ?? throw new ArgumentNullException(nameof(emailSettings));
        _logger = AppLogger.CreateLogger<SettingsForm>();
        InitializeComponent();
        SetupUI();
        LoadCurrentSettings();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(450, 280);
        this.Name = "SettingsForm";
        this.Text = "Settings";
        this.StartPosition = FormStartPosition.CenterParent;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        int yPos = 20;
        int labelWidth = 150;
        int controlLeft = labelWidth + 30;

        // Title
        var lblTitle = new Label
        {
            Text = "⚙️ Email Settings",
            Location = new Point(20, yPos),
            Width = 400,
            Font = new Font(this.Font.FontFamily, 14, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        this.Controls.Add(lblTitle);

        yPos += 50;

        // Test Mode Toggle
        var lblTestMode = new Label
        {
            Text = "Email Test Mode:",
            Location = new Point(20, yPos + 3),
            Width = labelWidth
        };
        this.Controls.Add(lblTestMode);

        _chkTestMode = new CheckBox
        {
            Text = "Enabled (log emails instead of sending)",
            Location = new Point(controlLeft, yPos),
            Width = 280,
            Checked = true
        };
        this.Controls.Add(_chkTestMode);

        yPos += 40;

        // Status indicator
        _lblStatus = new Label
        {
            Location = new Point(controlLeft, yPos),
            Width = 280,
            Height = 25,
            ForeColor = Color.Green,
            Font = new Font(this.Font.FontFamily, 9, FontStyle.Italic)
        };
        UpdateStatusLabel();
        this.Controls.Add(_lblStatus);

        _chkTestMode.CheckedChanged += (s, e) => UpdateStatusLabel();

        yPos += 40;

        // Sender Email
        var lblSenderEmail = new Label
        {
            Text = "Sender Email:",
            Location = new Point(20, yPos + 3),
            Width = labelWidth
        };
        this.Controls.Add(lblSenderEmail);

        _txtSenderEmail = new TextBox
        {
            Location = new Point(controlLeft, yPos),
            Width = 250,
            PlaceholderText = "store@example.com"
        };
        this.Controls.Add(_txtSenderEmail);

        yPos += 60;

        // Info Panel
        var infoPanel = new Panel
        {
            Location = new Point(20, yPos),
            Width = 400,
            Height = 45,
            BackColor = Color.FromArgb(240, 248, 255)
        };
        this.Controls.Add(infoPanel);

        var lblInfo = new Label
        {
            Text = "💡 Test Mode logs emails to console instead of sending.\n" +
                   "   Turn off when ready to send real purchase order emails.",
            Location = new Point(10, 5),
            Width = 380,
            Height = 35,
            ForeColor = Color.FromArgb(70, 130, 180)
        };
        infoPanel.Controls.Add(lblInfo);

        yPos += 60;

        // Save Button
        _btnSave = new Button
        {
            Text = "Save Settings",
            Location = new Point(controlLeft, yPos),
            Width = 120,
            Height = 35,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnSave.FlatAppearance.BorderSize = 0;
        _btnSave.Click += BtnSave_Click;
        this.Controls.Add(_btnSave);

        // Cancel Button
        _btnCancel = new Button
        {
            Text = "Cancel",
            Location = new Point(controlLeft + 130, yPos),
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

        this.AcceptButton = _btnSave;
        this.CancelButton = _btnCancel;
    }

    private void UpdateStatusLabel()
    {
        if (_lblStatus == null || _chkTestMode == null) return;

        if (_chkTestMode.Checked)
        {
            _lblStatus.Text = "🔒 Safe Mode - Emails will be logged only";
            _lblStatus.ForeColor = Color.Green;
        }
        else
        {
            _lblStatus.Text = "⚠️ Live Mode - Emails will be sent to suppliers";
            _lblStatus.ForeColor = Color.OrangeRed;
        }
    }

    private void LoadCurrentSettings()
    {
        if (_chkTestMode != null)
            _chkTestMode.Checked = _emailSettings.TestMode;
        
        if (_txtSenderEmail != null)
            _txtSenderEmail.Text = _emailSettings.SenderEmail;
    }

    private void BtnSave_Click(object? sender, EventArgs e)
    {
        // Update the settings object
        _emailSettings.TestMode = _chkTestMode?.Checked ?? true;
        _emailSettings.SenderEmail = _txtSenderEmail?.Text?.Trim() ?? string.Empty;

        // Save to appsettings.json
        try
        {
            SaveSettingsToFile();
            _logger.LogInformation("Settings saved - TestMode: {TestMode}, SenderEmail: {Email}", 
                _emailSettings.TestMode, _emailSettings.SenderEmail);
            
            MessageBox.Show(
                "Settings saved successfully!\n\n" +
                $"Email Test Mode: {(_emailSettings.TestMode ? "ON (emails will be logged)" : "OFF (emails will be sent)")}",
                "Settings Saved",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save settings");
            MessageBox.Show(
                $"Failed to save settings: {ex.Message}",
                "Error",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void SaveSettingsToFile()
    {
        var appSettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
        
        if (!File.Exists(appSettingsPath))
        {
            throw new FileNotFoundException("appsettings.json not found", appSettingsPath);
        }

        var json = File.ReadAllText(appSettingsPath);
        using var doc = System.Text.Json.JsonDocument.Parse(json);
        
        var options = new System.Text.Json.JsonWriterOptions { Indented = true };
        using var stream = new MemoryStream();
        using (var writer = new System.Text.Json.Utf8JsonWriter(stream, options))
        {
            writer.WriteStartObject();
            
            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (property.Name == "Email")
                {
                    writer.WritePropertyName("Email");
                    writer.WriteStartObject();
                    writer.WriteBoolean("TestMode", _emailSettings.TestMode);
                    writer.WriteString("SenderEmail", _emailSettings.SenderEmail);
                    writer.WriteString("GmailCredentialsPath", _emailSettings.GmailCredentialsPath);
                    writer.WriteString("TokenStorePath", _emailSettings.TokenStorePath);
                    writer.WriteEndObject();
                }
                else
                {
                    property.WriteTo(writer);
                }
            }
            
            writer.WriteEndObject();
        }

        var newJson = System.Text.Encoding.UTF8.GetString(stream.ToArray());
        File.WriteAllText(appSettingsPath, newJson);
    }
}
