using System;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace TechnologyStore.Kiosk
{
    public class AttractForm : Form
    {
        private readonly IServiceProvider _serviceProvider;

        public AttractForm(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "Technology Store Kiosk";
            this.WindowState = FormWindowState.Maximized;
            this.FormBorderStyle = FormBorderStyle.None;
            this.BackColor = Color.FromArgb(0, 122, 204); // Brand Blue

            var lblTitle = new Label();
            lblTitle.Text = "Values Technology Store";
            lblTitle.Font = new Font("Segoe UI", 48, FontStyle.Bold);
            lblTitle.ForeColor = Color.White;
            lblTitle.AutoSize = true;
            lblTitle.Location = new Point(100, 200);

            var lblSubtitle = new Label();
            lblSubtitle.Text = "Touch Screen to Start Checkout";
            lblSubtitle.Font = new Font("Segoe UI", 24, FontStyle.Regular);
            lblSubtitle.ForeColor = Color.White;
            lblSubtitle.AutoSize = true;
            lblSubtitle.Location = new Point(100, 300);

            this.Controls.Add(lblTitle);
            this.Controls.Add(lblSubtitle);

            this.Click += AttractForm_Click;
            lblTitle.Click += AttractForm_Click;
            lblSubtitle.Click += AttractForm_Click;
        }

        private void AttractForm_Click(object? sender, EventArgs e)
        {
            var scanForm = _serviceProvider.GetRequiredService<ScanForm>();
            scanForm.Show();
            this.Hide();
            
            // When scan form closes, show this again
            scanForm.FormClosed += (s, args) => this.Show();
        }
    }
}
