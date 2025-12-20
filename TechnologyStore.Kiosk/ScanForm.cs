using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using ZXing;
using ZXing.Windows.Compatibility;
using System.Media;
using Microsoft.Extensions.DependencyInjection;
using TechnologyStore.Shared.Models;

namespace TechnologyStore.Kiosk
{
    public class ScanForm : Form
    {
        private FilterInfoCollection? _videoDevices;
        private VideoCaptureDevice? _videoSource;
        private readonly IProductRepository _respository;
        private readonly IServiceProvider _serviceProvider;

        // UI Controls
        private PictureBox _pbCam;
        private TextBox _txtSku;
        private DataGridView _gridCart;
        private Label _lblTotal;
        private Button _btnPay;
        private ComboBox _cbCameras;
        private System.Windows.Forms.Timer _decodingTimer;

        // Cart State
        private List<CartItem> _cart = new List<CartItem>();

        public ScanForm(IProductRepository repository, IServiceProvider serviceProvider)
        {
            _respository = repository;
            _serviceProvider = serviceProvider;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Scan Items";
            this.WindowState = FormWindowState.Maximized;
            this.BackColor = Color.White;

            // Layout Layout
            var mainLayout = new TableLayoutPanel();
            mainLayout.Dock = DockStyle.Fill;
            mainLayout.ColumnCount = 2;
            mainLayout.RowCount = 2;
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // Left: Camera
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F)); // Right: Cart
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F)); // Top bar
            mainLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F)); // Content
            this.Controls.Add(mainLayout);

            // Camera Select
            _cbCameras = new ComboBox();
            _cbCameras.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(_cbCameras, 0, 0);

            // Webcam Viewfinder
            _pbCam = new PictureBox();
            _pbCam.Dock = DockStyle.Fill;
            _pbCam.SizeMode = PictureBoxSizeMode.Zoom;
            _pbCam.BorderStyle = BorderStyle.FixedSingle;
            mainLayout.Controls.Add(_pbCam, 0, 1);

            // Right Side Panel
            var rightPanel = new Panel();
            rightPanel.Dock = DockStyle.Fill;
            mainLayout.Controls.Add(rightPanel, 1, 1);
            mainLayout.SetRowSpan(rightPanel, 2); // Span top bar if needed, or adjust. Let's keep it simple.

            // Manual Entry
            var lblManual = new Label() { Text = "Manual SKU Entry:", Location = new Point(10, 10), AutoSize = true, Font = new Font("Segoe UI", 12) };
            rightPanel.Controls.Add(lblManual);

            _txtSku = new TextBox();
            _txtSku.Location = new Point(10, 40);
            _txtSku.Size = new Size(300, 30);
            _txtSku.Font = new Font("Segoe UI", 12);
            _txtSku.KeyDown += TxtSku_KeyDown;
            rightPanel.Controls.Add(_txtSku);

            var btnAdd = new Button() { Text = "Add", Location = new Point(320, 38), Size = new Size(80, 34), BackColor = Color.LightGray };
            btnAdd.Click += (s, e) => AddProductToCart(_txtSku.Text);
            rightPanel.Controls.Add(btnAdd);

            // Cart Grid
            _gridCart = new DataGridView();
            _gridCart.Location = new Point(10, 90);
            _gridCart.Size = new Size(500, 400); // Should be dynamic in real app
            _gridCart.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom;
            _gridCart.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            rightPanel.Controls.Add(_gridCart);

            // Total
            _lblTotal = new Label();
            _lblTotal.Text = "Total: $0.00";
            _lblTotal.Font = new Font("Segoe UI", 24, FontStyle.Bold);
            _lblTotal.Location = new Point(10, 520);
            _lblTotal.AutoSize = true;
            _lblTotal.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            rightPanel.Controls.Add(_lblTotal);

            // Pay Button
            _btnPay = new Button();
            _btnPay.Text = "Pay Now";
            _btnPay.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            _btnPay.BackColor = Color.Green;
            _btnPay.ForeColor = Color.White;
            _btnPay.Size = new Size(200, 60);
            _btnPay.Location = new Point(300, 510);
            _btnPay.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnPay.Click += BtnPay_Click;
            rightPanel.Controls.Add(_btnPay);

            // Logic Init
            this.Load += VariableInit;
            this.FormClosing += Cleanup;

            // Timer for barcode scanning delay (to prevent rapid duplicates)
            _decodingTimer = new System.Windows.Forms.Timer();
            _decodingTimer.Interval = 1000;
            _decodingTimer.Tick += (s, e) => { _decodingTimer.Stop(); };
        }

        private void VariableInit(object? sender, EventArgs e)
        {
            try
            {
                _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (_videoDevices.Count == 0)
                {
                    MessageBox.Show("No camera found.");
                    return;
                }

                foreach (FilterInfo device in _videoDevices)
                {
                    _cbCameras.Items.Add(device.Name);
                }
                _cbCameras.SelectedIndex = 0;

                StartCamera();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error initializing camera: " + ex.Message);
            }
        }

        private void StartCamera()
        {
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.WaitForStop();
            }

            _videoSource = new VideoCaptureDevice(_videoDevices[_cbCameras.SelectedIndex].MonikerString);
            _videoSource.NewFrame += VideoSource_NewFrame;
            _videoSource.Start();
        }

        private void Cleanup(object? sender, FormClosingEventArgs e)
        {
            if (_videoSource != null && _videoSource.IsRunning)
            {
                _videoSource.SignalToStop();
                _videoSource.WaitForStop();
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap bitmap = (Bitmap)eventArgs.Frame.Clone();
            _pbCam.Image = bitmap; // Show feed

            if (_decodingTimer.Enabled) return; // Wait until cooldown

            try
            {
                BarcodeReader reader = new BarcodeReader();
                var result = reader.Decode(bitmap);
                if (result != null)
                {
                    _decodingTimer.Start(); // Cooldown
                    this.Invoke(new Action(() =>
                    {
                        SystemSounds.Beep.Play();
                        AddProductToCart(result.Text);
                    }));
                }
            }
            catch { }
        }

        private async Task AddProductToCart(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku)) return;

            var product = await _respository.GetBySkuAsync(sku);
            if (product == null)
            {
                MessageBox.Show($"Product not found: {sku}");
                return;
            }

            var item = new CartItem // Using anonymous or local class
            {
                ProductId = product.Id,
                Name = product.Name,
                Price = product.UnitPrice,
                Quantity = 1
            };

            _cart.Add(item);
            RefreshCart();
            _txtSku.Clear();
        }

        private void TxtSku_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                AddProductToCart(_txtSku.Text);
                e.SuppressKeyPress = true; // Prevent ding
            }
        }

        private void RefreshCart()
        {
            _gridCart.DataSource = null;
            _gridCart.DataSource = _cart;

            decimal total = _cart.Sum(x => x.Price * x.Quantity);
            _lblTotal.Text = $"Total: ${total:F2}";
        }

        private void BtnPay_Click(object? sender, EventArgs e)
        {
            if (_cart.Count == 0) return;

            // Open separate Payment Form
            using (var paymentForm = new PaymentForm(_cart, _respository))
            {
                if (paymentForm.ShowDialog() == DialogResult.OK)
                {
                    // Payment successful (handled in PaymentForm) - just clear local cart
                    _cart.Clear();
                    RefreshCart();

                    // Go back to Attract screen
                    this.Close();
                }
            }
        }
    }

    public class CartItem
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }
}
