using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;
using Newtonsoft.Json;
using Microsoft.Win32;

namespace WinFormsApp1
{
    public partial class Form1 : Form
    {

        private void txtSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                // Prevent the "Ding" sound Windows makes when pressing Enter in a single-line textbox
                e.SuppressKeyPress = true;

                // Skip if it's just the placeholder
                if (txtSearch.Text == "Search apps...") return;

                string query = txtSearch.Text.ToLower();
                var filtered = allApps.Where(a => a.Name.ToLower().Contains(query)).ToList();
                UpdateList(filtered);
            }
        }

        public void RegisterProtocol()
        {
            // The name of your protocol
            string protocol = "ar-store";
            string filePath = Application.ExecutablePath;

            // Create the keys in HKEY_CLASSES_ROOT
            using (var key = Registry.ClassesRoot.CreateSubKey(protocol))
            {
                key.SetValue("", "URL:AR Store Protocol");
                key.SetValue("URL Protocol", "");

                using (var shellKey = key.CreateSubKey("shell"))
                using (var openKey = shellKey.CreateSubKey("open"))
                using (var commandKey = openKey.CreateSubKey("command"))
                {
                    // %1 allows for deep-linking (e.g., ar-store:browse)
                    commandKey.SetValue("", $"\"{filePath}\" \"%1\"");
                }
            }
        }



        public class StoreApp
        {
            public string Name { get; set; }
            public string Version { get; set; }
            public string Publisher { get; set; }
            public string Description { get; set; }
            public string AppxUrl { get; set; }
            public string FileName { get; set; }
            public override string ToString() => Name;
        }

        private Panel sidePanel, mainContent, headerPanel, searchPanel;
        private ListBox lstApps;
        private TextBox txtSearch;
        private Label lblTitle, lblPublisher, lblDesc, lblStatus, lblStoreBranding, lblVersionCheck;
        private Button btnInstall, btnOpenFolder, btnRefresh;
        private ProgressBar pBar;

        private List<StoreApp> allApps = new List<StoreApp>();
        private const string DestPath = @"C:\APPX";
        private const string ApiLink = "https://raw.githubusercontent.com/argaming19099/arstore-api/refs/heads/main/arstore.json";

        public Form1()
        {
            // Security Protocol 3072 = TLS 1.2 (Required for Win7 GitHub access)
            System.Net.ServicePointManager.SecurityProtocol = (System.Net.SecurityProtocolType)3072;
            this.AutoScaleMode = AutoScaleMode.Dpi;
            InitializeARStoreUI();
            _ = LoadStoreData();
            this.Icon = AR_Store.Properties.Resources.This;
        }
        public class AboutStoreWindow : Form
        {
            public AboutStoreWindow()
            {
                this.Text = "About AR Store";
                this.Size = new Size(420, 280);
                this.BackColor = Color.FromArgb(18, 18, 18); // AR Signature Dark
                this.ForeColor = Color.White;
                this.FormBorderStyle = FormBorderStyle.FixedDialog;
                this.MaximizeBox = false;
                this.MinimizeBox = false;
                this.StartPosition = FormStartPosition.CenterParent;

                Label lblTitle = new Label
                {
                    Text = "AR Store v0.9.4",
                    Font = new Font("Segoe UI", 16, FontStyle.Bold),
                    Top = 20,
                    Left = 20,
                    Width = 380,
                    Height = 40
                };

                Label lblInfo = new Label
                {
                    Text = "Ecosystem Hub: AR Suite\n" +
                           "Connectivity: TLS 1.2 API Enforced\n" +
                           "Status: Beta Release (RC1)\n\n" +
                           "Developed by AR Technologies\n" +
                           "A Dedicated 3-Man Engineering Team",
                    Font = new Font("Segoe UI", 10),
                    Top = 70,
                    Left = 20,
                    Width = 380,
                    Height = 120
                };

                Button btnClose = new Button
                {
                    Text = "Close",
                    Top = 200,
                    Left = 300,
                    Width = 80,
                    Height = 30,
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(45, 45, 45)
                };
                btnClose.FlatAppearance.BorderSize = 0;
                btnClose.Click += (s, e) => this.Close();

                this.Controls.Add(lblTitle);
                this.Controls.Add(lblInfo);
                this.Controls.Add(btnClose);
            }
        }
        
        private async Task LoadStoreData()
        {
            lblStatus.Text = "LOADING...";
            lblStatus.ForeColor = Color.Cyan;
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string syncUrl = ApiLink + "?t=" + DateTime.Now.Ticks;
                    string json = await client.GetStringAsync(syncUrl);
                    allApps = JsonConvert.DeserializeObject<List<StoreApp>>(json);
                    UpdateList(allApps);
                    lblStatus.Text = $" {allApps.Count} APPS";
                }
            }
            catch (Exception ex)
            {
                lblStatus.Text = "CONNECTION ERROR: " + ex.Message;
                lblStatus.ForeColor = Color.Tomato;
            }
        }

        private void UpdateList(List<StoreApp> apps)
        {
            lstApps.DataSource = null;
            lstApps.DataSource = apps;
        }

        // 1. CLEAR ON CLICK
        private void txtSearch_Enter(object sender, EventArgs e)
        {
            if (txtSearch.Text == "Search apps...")
            {
                txtSearch.Text = "";
                txtSearch.ForeColor = Color.White;
            }
        }

        // 2. RESTORE ON LEAVE
        private void txtSearch_Leave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtSearch.Text))
            {
                txtSearch.Text = "Search apps...";
                txtSearch.ForeColor = Color.Gray;
            }
        }

        // 3. THE SEARCH (The part that usually crashes)
        private void txtSearch_TextChanged(object sender, EventArgs e)
        {
            // CRITICAL: If the text is the placeholder, STOP immediately.
            // This prevents the infinite loop/crash.
            if (txtSearch.Text == "Search apps...")
            {
                return;
            }

            string query = txtSearch.Text.ToLower();
            var filtered = allApps.Where(a => a.Name.ToLower().Contains(query)).ToList();
            UpdateList(filtered);
        }

        private void lstApps_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (lstApps.SelectedItem is StoreApp app)
            {
                lblTitle.Text = app.Name.ToUpper();
                lblPublisher.Text = $"SOURCE: {app.Publisher} | BUILD: {app.Version}";
                lblDesc.Text = app.Description;
                btnInstall.Visible = true;
                lblVersionCheck.Visible = true;

                string localFilePath = Path.Combine(DestPath, app.FileName);

                if (File.Exists(localFilePath))
                {
                    try
                    {
                        string rawLocal = FileVersionInfo.GetVersionInfo(localFilePath).FileVersion;

                        // Smart Version Comparison
                        Version vLocal = new Version(rawLocal);
                        Version vCloud = new Version(app.Version);

                        lblVersionCheck.Text = $"LOCAL: {vLocal} | CLOUD: {vCloud}";

                        if (vLocal < vCloud)
                        {
                            btnInstall.Text = "UPDATE AVAILABLE";
                            btnInstall.BackColor = Color.Orange;
                            lblStatus.Text = "NEW VERSION AVAILABLE FOR AN APP";
                            lblStatus.ForeColor = Color.Orange;
                        }
                        else
                        {
                            btnInstall.Text = "OPEN APP";
                            btnInstall.BackColor = Color.ForestGreen;
                            lblStatus.Text = "APP IS LATEST";
                            lblStatus.ForeColor = Color.Lime;
                        }
                    }
                    catch
                    {
                        lblVersionCheck.Text = "NO NEW UPDATES ARE AVAILABLE";
                        btnInstall.Text = "OPEN APP";
                        btnInstall.BackColor = Color.ForestGreen;
                    }
                }
                else
                {
                    lblVersionCheck.Text = "NOT INSTALLED";
                    btnInstall.Text = "INSTALL APP";
                    btnInstall.BackColor = Color.FromArgb(0, 120, 215);
                    lblStatus.Text = "";
                    lblStatus.ForeColor = Color.Cyan;
                }
            }
        }

        private async void btnInstall_Click(object sender, EventArgs e)
        {
            if (lstApps.SelectedItem is StoreApp selected)
            {
                string fullPath = Path.Combine(DestPath, selected.FileName);

                if (btnInstall.Text == "OPEN APP")
                {
                    try { Process.Start(fullPath); }
                    catch (Exception ex) { MessageBox.Show("Launch failed: " + ex.Message); }
                    return;
                }

                if (!Directory.Exists(DestPath)) Directory.CreateDirectory(DestPath);

                btnInstall.Enabled = false;
                btnInstall.Text = "DOWNLOADING...";

                try
                {
                    using (WebClient wc = new WebClient())
                    {
                        wc.DownloadProgressChanged += (s, ev) => { pBar.Value = ev.ProgressPercentage; };
                        await wc.DownloadFileTaskAsync(new Uri(selected.AppxUrl), fullPath);
                    }

                    lblStatus.Text = $"{selected.Name.ToUpper()} APP INSTALLED SUCCESSFULLY";
                    lblStatus.ForeColor = Color.Lime;

                    // Force refresh to update button to "OPEN APP"
                    lstApps_SelectedIndexChanged(null, null);
                    Process.Start(fullPath);
                }
                catch (Exception ex)
                {
                    lblStatus.Text = "ERROR: " + ex.Message;
                    btnInstall.Text = "RETRY";
                }
                finally
                {
                    btnInstall.Enabled = true;
                    pBar.Value = 0;
                }
            }
        }

        private void InitializeARStoreUI()
        {
            this.Size = new Size(1000, 650);
            this.BackColor = Color.FromArgb(18, 18, 22);
            this.ForeColor = Color.White;
            this.Font = new Font("Segoe UI", 10);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Text = "AR Store";



            headerPanel = new Panel { Dock = DockStyle.Top, Height = 80, BackColor = Color.FromArgb(24, 24, 30) };
            lblStoreBranding = new Label { Text = "AR STORE", Font = new Font("Segoe UI Semibold", 22, FontStyle.Bold), ForeColor = Color.Cyan, Location = new Point(30, 20), AutoSize = true };

            btnRefresh = new Button { Text = "REFRESH", Location = new Point(850, 25), Size = new Size(100, 35), FlatStyle = FlatStyle.Flat, ForeColor = Color.Cyan, Cursor = Cursors.Hand };
            btnRefresh.FlatAppearance.BorderColor = Color.Cyan;
            btnRefresh.Click += async (s, e) => await LoadStoreData();
            headerPanel.Controls.Add(lblStoreBranding);
            headerPanel.Controls.Add(btnRefresh);

            sidePanel = new Panel { Dock = DockStyle.Left, Width = 280, BackColor = Color.FromArgb(24, 24, 30) };
            searchPanel = new Panel { Dock = DockStyle.Top, Height = 70 };
            txtSearch = new TextBox { Location = new Point(20, 25), Width = 240, BackColor = Color.FromArgb(45, 45, 52), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Text = "Search apps...", Font = new Font("Segoe UI", 11) };
            txtSearch.TextChanged += txtSearch_TextChanged;
            searchPanel.Controls.Add(txtSearch);

            lstApps = new ListBox { Dock = DockStyle.Fill, BackColor = Color.FromArgb(24, 24, 30), ForeColor = Color.White, BorderStyle = BorderStyle.None, ItemHeight = 50, DrawMode = DrawMode.OwnerDrawFixed };
            lstApps.DrawItem += (s, e) => {
                if (e.Index < 0) return;
                e.DrawBackground();
                bool isSelected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
                e.Graphics.FillRectangle(new SolidBrush(isSelected ? Color.FromArgb(0, 120, 215) : Color.FromArgb(24, 24, 30)), e.Bounds);
                e.Graphics.DrawString(lstApps.Items[e.Index].ToString(), new Font("Segoe UI Semibold", 12), Brushes.White, e.Bounds.X + 25, e.Bounds.Y + 14);
                txtSearch.Enter += txtSearch_Enter;
                txtSearch.Leave += txtSearch_Leave;
                txtSearch.KeyDown += txtSearch_KeyDown;
            };
            lstApps.SelectedIndexChanged += lstApps_SelectedIndexChanged;
            sidePanel.Controls.AddRange(new Control[] { lstApps, searchPanel });

            mainContent = new Panel { Dock = DockStyle.Fill, Padding = new Padding(50, 50, 50, 60) };
            lblTitle = new Label { Text = "WELCOME TO AR STORE", Font = new Font("Segoe UI Light", 36), Location = new Point(50, 50), AutoSize = true };
            lblPublisher = new Label { Text = "", Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(55, 130), AutoSize = true, ForeColor = Color.FromArgb(0, 190, 255) };
            lblVersionCheck = new Label { Text = "", Font = new Font("Consolas", 9), Location = new Point(57, 155), AutoSize = true, ForeColor = Color.Gray, Visible = false };
            lblDesc = new Label { Text = "Click on an app from the left sidebar.", Location = new Point(55, 190), Size = new Size(550, 180), ForeColor = Color.DarkGray, Font = new Font("Segoe UI", 12) };

            btnInstall = new Button { Text = "INSTALL", Location = new Point(55, 390), Size = new Size(220, 60), BackColor = Color.FromArgb(0, 120, 215), FlatStyle = FlatStyle.Flat, Visible = false, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
            btnInstall.Click += btnInstall_Click;

            btnOpenFolder = new Button { Text = "LOCAL FILES", Location = new Point(290, 390), Size = new Size(180, 60), FlatStyle = FlatStyle.Flat, ForeColor = Color.Silver };
            btnOpenFolder.Click += (s, e) => { if (Directory.Exists(DestPath)) Process.Start("explorer.exe", DestPath); };

            mainContent.Controls.AddRange(new Control[] { lblTitle, lblPublisher, lblVersionCheck, lblDesc, btnInstall, btnOpenFolder });

            pBar = new ProgressBar { Dock = DockStyle.Bottom, Height = 4 };
            lblStatus = new Label { Dock = DockStyle.Bottom, Height = 40, Text = "READY", Padding = new Padding(20, 10, 0, 0), BackColor = Color.FromArgb(12, 12, 15), Font = new Font("Consolas", 10), ForeColor = Color.Cyan };

            this.Controls.AddRange(new Control[] { mainContent, sidePanel, headerPanel, pBar, lblStatus });
        }
    }
}