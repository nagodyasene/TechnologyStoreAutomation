using System.Drawing;
using System.Windows.Forms;
using TechnologyStore.Shared.Models;
using CustomerModel = TechnologyStore.Shared.Models.Customer;

using static TechnologyStore.Customer.UiConstants;

namespace TechnologyStore.Customer.Forms;

/// <summary>
/// Order confirmation form displayed after successful order placement
/// </summary>
public partial class OrderConfirmationForm : Form
{
    private readonly Order _order;
    private readonly CustomerModel _customer;

    public OrderConfirmationForm(Order order, CustomerModel customer)
    {
        _order = order;
        _customer = customer;

        InitializeComponent();
        SetupUI();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(500, 550);
        this.Name = "OrderConfirmationForm";
        this.Text = "Order Confirmed!";
        this.StartPosition = FormStartPosition.CenterParent;
        this.BackColor = Color.White;
        this.FormBorderStyle = FormBorderStyle.FixedDialog;
        this.MaximizeBox = false;
        this.MinimizeBox = false;
        this.ResumeLayout(false);
    }

    private void SetupUI()
    {
        int yPos = 30;

        // Success icon
        var lblIcon = new Label
        {
            Text = "✅",
            Location = new Point(0, yPos),
            Width = this.ClientSize.Width,
            Font = new Font(DefaultFontFamily, 48),
            TextAlign = ContentAlignment.MiddleCenter
        };
        this.Controls.Add(lblIcon);

        yPos += 80;

        // Success message
        var lblSuccess = new Label
        {
            Text = "Order Placed Successfully!",
            Location = new Point(0, yPos),
            Width = this.ClientSize.Width,
            Font = new Font(DefaultFontFamily, 18, FontStyle.Bold),
            ForeColor = Color.FromArgb(40, 167, 69),
            TextAlign = ContentAlignment.MiddleCenter
        };
        this.Controls.Add(lblSuccess);

        yPos += 50;

        // Order number
        var orderNumberPanel = new Panel
        {
            Location = new Point(50, yPos),
            Size = new Size(400, 60),
            BackColor = Color.FromArgb(0, 120, 212),
            Padding = new Padding(10)
        };
        this.Controls.Add(orderNumberPanel);

        var lblOrderNumber = new Label
        {
            Text = _order.OrderNumber,
            Dock = DockStyle.Fill,
            Font = new Font(DefaultFontFamily, 22, FontStyle.Bold),
            ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter
        };
        orderNumberPanel.Controls.Add(lblOrderNumber);

        yPos += 80;

        // Order details
        var lblDetails = new Label
        {
            Text = "Order Details",
            Location = new Point(50, yPos),
            Width = 400,
            Font = new Font(DefaultFontFamily, 11, FontStyle.Bold)
        };
        this.Controls.Add(lblDetails);

        yPos += 25;

        var detailsPanel = new Panel
        {
            Location = new Point(50, yPos),
            Size = new Size(400, 100),
            BackColor = Color.FromArgb(248, 249, 250),
            BorderStyle = BorderStyle.FixedSingle
        };
        this.Controls.Add(detailsPanel);

        var lblItemCount = new Label
        {
            Text = $"Items ordered: {_order.Items.Count} product(s), {_order.Items.Sum(i => i.Quantity)} unit(s)",
            Location = new Point(15, 12),
            AutoSize = true,
            Font = new Font(DefaultFontFamily, 10)
        };
        detailsPanel.Controls.Add(lblItemCount);

        var lblTotalAmount = new Label
        {
            Text = $"Total amount: ${_order.Total:N2}",
            Location = new Point(15, 35),
            AutoSize = true,
            Font = new Font(DefaultFontFamily, 10, FontStyle.Bold)
        };
        detailsPanel.Controls.Add(lblTotalAmount);

        var pickupText = _order.PickupDate.HasValue
            ? $"Pickup date: {_order.PickupDate.Value:MMMM dd, yyyy}"
            : "Pickup: As soon as possible";
        var lblPickup = new Label
        {
            Text = pickupText,
            Location = new Point(15, 58),
            AutoSize = true,
            Font = new Font(DefaultFontFamily, 10)
        };
        detailsPanel.Controls.Add(lblPickup);

        var lblPayment = new Label
        {
            Text = "Payment: Cash at pickup",
            Location = new Point(15, 78),
            AutoSize = true,
            Font = new Font(DefaultFontFamily, 10),
            ForeColor = Color.Gray
        };
        detailsPanel.Controls.Add(lblPayment);

        yPos += 120;

        // Email confirmation
        var emailPanel = new Panel
        {
            Location = new Point(50, yPos),
            Size = new Size(400, 50),
            BackColor = Color.FromArgb(212, 237, 218),
            BorderStyle = BorderStyle.FixedSingle
        };
        this.Controls.Add(emailPanel);

        var lblEmail = new Label
        {
            Text = $"📧 Invoice sent to: {_customer.Email}",
            Dock = DockStyle.Fill,
            Font = new Font(DefaultFontFamily, 10),
            ForeColor = Color.FromArgb(21, 87, 36),
            TextAlign = ContentAlignment.MiddleCenter
        };
        emailPanel.Controls.Add(lblEmail);

        yPos += 70;

        // Instructions
        var lblInstructions = new Label
        {
            Text = "📍 Please bring your invoice or order number when picking up your order.",
            Location = new Point(50, yPos),
            Width = 400,
            Height = 40,
            Font = new Font(DefaultFontFamily, 9),
            ForeColor = Color.Gray,
            TextAlign = ContentAlignment.MiddleCenter
        };
        this.Controls.Add(lblInstructions);

        yPos += 60;

        // Close button
        var btnClose = new Button
        {
            Text = "Continue Shopping",
            Location = new Point(150, yPos),
            Width = 200,
            Height = 45,
            BackColor = Color.FromArgb(0, 120, 212),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(DefaultFontFamily, 11, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnClose.FlatAppearance.BorderSize = 0;
        btnClose.Click += (s, e) => this.Close();
        this.Controls.Add(btnClose);

        this.AcceptButton = btnClose;
    }
}
