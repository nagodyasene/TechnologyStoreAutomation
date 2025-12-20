using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Threading.Tasks;

namespace TechnologyStore.Kiosk
{
    public class PaymentForm : Form
    {
        private readonly List<CartItem> _cart;
        private readonly IProductRepository _repository;
        private Label _lblStatus;

        public PaymentForm(List<CartItem> cart, IProductRepository repository)
        {
            _cart = cart;
            _repository = repository;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Checkout";
            this.Size = new Size(800, 600);
            this.StartPosition = FormStartPosition.CenterScreen; // Or CenterParent
            this.FormBorderStyle = FormBorderStyle.None;
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.White;

            var mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.ColumnCount = 2;
            mainLayout.RowCount = 3;
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60F)); // Left: Cart
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40F)); // Right: Actions
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F)); // Header
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Content
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60F)); // Footer (Back)

            this.Controls.Add(mainLayout);

            // 1. Header
            var lblHeader = new Label();
            lblHeader.Text = "Review Your Order";
            lblHeader.Font = new Font("Segoe UI", 24, FontStyle.Bold);
            lblHeader.AutoSize = true;
            lblHeader.TextAlign = ContentAlignment.MiddleLeft;
            mainLayout.Controls.Add(lblHeader, 0, 0);
            mainLayout.SetColumnSpan(lblHeader, 2);

            // 2. Left: Cart List
            var cartGrid = new DataGridView();
            cartGrid.Dock = DockStyle.Fill;
            cartGrid.DataSource = _cart;
            cartGrid.ReadOnly = true;
            cartGrid.RowHeadersVisible = false;
            cartGrid.AllowUserToAddRows = false;
            cartGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            cartGrid.BackgroundColor = Color.WhiteSmoke;
            cartGrid.BorderStyle = BorderStyle.None;
            // Larger font for readability
            cartGrid.DefaultCellStyle.Font = new Font("Segoe UI", 14);
            cartGrid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            cartGrid.RowTemplate.Height = 40;
            mainLayout.Controls.Add(cartGrid, 0, 1);

            // 3. Right: Payment Totals & Buttons
            var rightPanel = new Panel();
            rightPanel.Dock = DockStyle.Fill;
            rightPanel.Padding = new Padding(20);
            mainLayout.Controls.Add(rightPanel, 1, 1);

            decimal total = _cart.Sum(x => x.Price * x.Quantity);

            var lblTotal = new Label();
            lblTotal.Text = $"Total Due:\n${total:F2}";
            lblTotal.Font = new Font("Segoe UI", 32, FontStyle.Bold);
            lblTotal.ForeColor = Color.DarkGreen;
            lblTotal.AutoSize = true;
            lblTotal.Location = new Point(20, 20);
            rightPanel.Controls.Add(lblTotal);

            var lblPrompt = new Label();
            lblPrompt.Text = "Select Payment Method:";
            lblPrompt.Font = new Font("Segoe UI", 16);
            lblPrompt.Location = new Point(20, 150);
            lblPrompt.AutoSize = true;
            rightPanel.Controls.Add(lblPrompt);

            // Buttons
            var btnCard = CreatePaymentButton("💳 Credit Card", Color.FromArgb(0, 120, 215), 200);
            btnCard.Click += async (s, e) => await ProcessPayment("Credit Card");
            rightPanel.Controls.Add(btnCard);

            var btnNfc = CreatePaymentButton("📱 NFC / Mobile", Color.Black, 280);
            btnNfc.Click += async (s, e) => await ProcessPayment("NFC");
            rightPanel.Controls.Add(btnNfc);

            var btnCash = CreatePaymentButton("💵 Cash", Color.Green, 360);
            btnCash.Click += async (s, e) => await ProcessPayment("Cash");
            rightPanel.Controls.Add(btnCash);
            
            // Status Label
            _lblStatus = new Label();
            _lblStatus.Text = "";
            _lblStatus.Font = new Font("Segoe UI", 14, FontStyle.Italic);
            _lblStatus.ForeColor = Color.Blue;
            _lblStatus.AutoSize = true;
            _lblStatus.Location = new Point(20, 450);
            rightPanel.Controls.Add(_lblStatus);


            // 4. Footer: Back Button
            var btnBack = new Button();
            btnBack.Text = "← Back to Scan";
            btnBack.FlatStyle = FlatStyle.Flat;
            btnBack.Font = new Font("Segoe UI", 14);
            btnBack.Size = new Size(200, 50);
            btnBack.Click += (s, e) => this.Close(); // Just close, ScanForm is underneath
            mainLayout.Controls.Add(btnBack, 0, 2);
        }

        private Button CreatePaymentButton(string text, Color color, int y)
        {
            var btn = new Button();
            btn.Text = text;
            btn.BackColor = color;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.Font = new Font("Segoe UI", 16, FontStyle.Bold);
            btn.Size = new Size(250, 60);
            btn.Location = new Point(20, y);
            btn.Cursor = Cursors.Hand;
            return btn;
        }

        private async Task ProcessPayment(string method)
        {
            try
            {
                _lblStatus.Text = $"Processing {method}...";
                _lblStatus.Refresh();
                
                // Simulate network delay
                await Task.Delay(2000); 

                // Record Sale
                foreach (var item in _cart)
                {
                    await _repository.RecordSaleAsync(item.ProductId, item.Quantity, item.Price * item.Quantity);
                }

                _lblStatus.Text = "Approved!";
                _lblStatus.ForeColor = Color.Green;
                await Task.Delay(500);

                GenerateReceipt(method);

                MessageBox.Show("Payment Successful! Please take your receipt.");
                this.DialogResult = DialogResult.OK; // Signal success to ScanForm
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Payment Failed: {ex.Message}");
                _lblStatus.Text = "Failed. Try again.";
                _lblStatus.ForeColor = Color.Red;
            }
        }

        private void GenerateReceipt(string method)
        {
            decimal total = _cart.Sum(x => x.Price * x.Quantity);
            string receiptContent = $"--- TECHNOLOGY STORE ---\n";
            receiptContent += $"Date: {DateTime.Now}\n";
            receiptContent += $"Method: {method}\n\n";
            receiptContent += "ITEMS:\n";
            foreach (var item in _cart)
            {
                receiptContent += $"{item.Name}\n  {item.Quantity} x ${item.Price:F2} = ${(item.Price * item.Quantity):F2}\n";
            }
            receiptContent += "\n------------------------\n";
            receiptContent += $"TOTAL: ${total:F2}\n";
            receiptContent += "------------------------\n";
            receiptContent += "Thank you for shopping!";

            string fileName = $"receipt_{DateTime.Now:yyyyMMddHHmmss}.txt";
            System.IO.File.WriteAllText(fileName, receiptContent);
        }
    }
}
