using DBSyncApp.Models;
using DBSyncApp.Services;

namespace DBSyncApp.Forms
{
    public class ConnectionDialog : Form
    {
        private TextBox txtName = new(), txtServer = new(), txtPort = new(),
                        txtUsername = new(), txtPassword = new();
        private ComboBox cboDbType = new(), cboDatabase = new();
        private CheckBox chkWinAuth = new();
        private Button btnTest = new(), btnSave = new(), btnCancel = new();
        private Button btnLoadDbs = new();
        private Label lblStatus = new();

        public ConnectionProfile? Result { get; private set; }
        private ConnectionProfile? _editing;

        public ConnectionDialog(ConnectionProfile? existing = null)
        {
            _editing = existing;
            InitUI();
            if (existing != null) LoadProfile(existing);
        }

        private void InitUI()
        {
            Text = "Database Connection";
            Size = new Size(500, 480);
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            BackColor = Color.FromArgb(18, 18, 28);
            ForeColor = Color.FromArgb(220, 220, 235);
            Font = new Font("Segoe UI", 9.5f);

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(24, 20, 24, 16),
                RowCount = 11,
                ColumnCount = 2,
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 130));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            panel.BackColor = Color.Transparent;

            // DB Type
            cboDbType.Items.AddRange(new object[] { "SQL Server", "PostgreSQL" });
            cboDbType.SelectedIndex = 0;
            cboDbType.DropDownStyle = ComboBoxStyle.DropDownList;
            cboDbType.BackColor = Color.FromArgb(30, 30, 46);
            cboDbType.ForeColor = Color.FromArgb(220, 220, 235);
            cboDbType.FlatStyle = FlatStyle.Flat;
            cboDbType.Dock = DockStyle.Fill;
            cboDbType.SelectedIndexChanged += (s, e) =>
            {
                txtPort.Text = cboDbType.SelectedIndex == 0 ? "1433" : "5432";
                chkWinAuth.Visible = cboDbType.SelectedIndex == 0;
                UpdateAuthFields();
                ClearDatabases();
            };

            StyleTextBox(txtName);
            StyleTextBox(txtServer);
            StyleTextBox(txtPort); txtPort.Text = "1433";
            StyleTextBox(txtUsername);
            StyleTextBox(txtPassword); txtPassword.UseSystemPasswordChar = true;

            // Database row: combobox + load button side by side
            cboDatabase.DropDownStyle = ComboBoxStyle.DropDownList;
            cboDatabase.BackColor = Color.FromArgb(30, 30, 46);
            cboDatabase.ForeColor = Color.FromArgb(220, 220, 235);
            cboDatabase.FlatStyle = FlatStyle.Flat;
            cboDatabase.Dock = DockStyle.Fill;
            cboDatabase.Items.Add("— enter server & credentials, then Load —");
            cboDatabase.SelectedIndex = 0;

            btnLoadDbs.Text = "Load ↓";
            btnLoadDbs.BackColor = Color.FromArgb(40, 80, 160);
            btnLoadDbs.ForeColor = Color.White;
            btnLoadDbs.FlatStyle = FlatStyle.Flat;
            btnLoadDbs.FlatAppearance.BorderSize = 0;
            btnLoadDbs.Font = new Font("Segoe UI", 8.5f);
            btnLoadDbs.Cursor = Cursors.Hand;
            btnLoadDbs.Width = 68;
            btnLoadDbs.Height = 28;
            btnLoadDbs.Click += async (s, e) => await LoadDatabasesAsync();

            // Database row panel (combobox + button)
            var dbRowPanel = new Panel { Dock = DockStyle.Fill, Height = 30 };
            cboDatabase.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            cboDatabase.Dock = DockStyle.None;
            btnLoadDbs.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            dbRowPanel.Controls.Add(cboDatabase);
            dbRowPanel.Controls.Add(btnLoadDbs);
            dbRowPanel.Resize += (s, e) =>
            {
                btnLoadDbs.Width = 68;
                btnLoadDbs.Left = dbRowPanel.Width - btnLoadDbs.Width;
                btnLoadDbs.Top = 0;
                cboDatabase.Left = 0;
                cboDatabase.Width = dbRowPanel.Width - btnLoadDbs.Width - 6;
                cboDatabase.Top = 1;
            };

            chkWinAuth.Text = "Windows Authentication";
            chkWinAuth.ForeColor = Color.FromArgb(160, 160, 200);
            chkWinAuth.BackColor = Color.Transparent;
            chkWinAuth.CheckedChanged += (s, e) => UpdateAuthFields();

            int row = 0;
            AddRow(panel, "Profile Name:", txtName, row++);
            AddRow(panel, "Database Type:", cboDbType, row++);
            AddRow(panel, "Server / Host:", txtServer, row++);
            AddRow(panel, "Port:", txtPort, row++);
            AddRow(panel, "", chkWinAuth, row++);
            AddRow(panel, "Username:", txtUsername, row++);
            AddRow(panel, "Password:", txtPassword, row++);
            AddRow(panel, "Database:", dbRowPanel, row++);

            lblStatus.ForeColor = Color.FromArgb(100, 200, 150);
            lblStatus.Font = new Font("Segoe UI", 8.5f);
            lblStatus.AutoSize = true;
            AddRow(panel, "", lblStatus, row++);

            // Buttons
            var btnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                Height = 52,
                Padding = new Padding(16, 10, 16, 10),
                BackColor = Color.FromArgb(24, 24, 36),
            };

            StyleButton(btnTest, "Test Connection", Color.FromArgb(40, 120, 200));
            StyleButton(btnSave, "Save", Color.FromArgb(50, 160, 100));
            StyleButton(btnCancel, "Cancel", Color.FromArgb(80, 80, 100));

            btnTest.Click += async (s, e) => await TestConnectionAsync();
            btnSave.Click += SaveClick;
            btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

            btnPanel.Controls.AddRange(new Control[] { btnCancel, btnSave, btnTest });

            Controls.Add(panel);
            Controls.Add(btnPanel);
        }

        private void StyleTextBox(TextBox tb)
        {
            tb.BackColor = Color.FromArgb(30, 30, 46);
            tb.ForeColor = Color.FromArgb(220, 220, 235);
            tb.BorderStyle = BorderStyle.FixedSingle;
            tb.Dock = DockStyle.Fill;
        }

        private void StyleButton(Button btn, string text, Color bg)
        {
            btn.Text = text;
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Padding = new Padding(12, 0, 12, 0);
            btn.Height = 32;
            btn.AutoSize = true;
            btn.Cursor = Cursors.Hand;
        }

        private void AddRow(TableLayoutPanel panel, string label, Control control, int row)
        {
            if (!string.IsNullOrEmpty(label))
            {
                var lbl = new Label
                {
                    Text = label,
                    ForeColor = Color.FromArgb(160, 160, 200),
                    TextAlign = ContentAlignment.MiddleRight,
                    Dock = DockStyle.Fill,
                    Padding = new Padding(0, 4, 8, 0)
                };
                panel.Controls.Add(lbl, 0, row);
            }
            control.Margin = new Padding(0, 4, 0, 4);
            panel.Controls.Add(control, 1, row);
        }

        private void UpdateAuthFields()
        {
            bool winAuth = chkWinAuth.Checked && cboDbType.SelectedIndex == 0;
            txtUsername.Enabled = !winAuth;
            txtPassword.Enabled = !winAuth;
        }

        private void ClearDatabases()
        {
            cboDatabase.Items.Clear();
            cboDatabase.Items.Add("— enter server & credentials, then Load —");
            cboDatabase.SelectedIndex = 0;
        }

        private async Task LoadDatabasesAsync()
        {
            if (string.IsNullOrWhiteSpace(txtServer.Text))
            {
                lblStatus.ForeColor = Color.FromArgb(220, 120, 60);
                lblStatus.Text = "Enter a server address first.";
                return;
            }

            lblStatus.ForeColor = Color.FromArgb(200, 180, 60);
            lblStatus.Text = "Connecting to server...";
            btnLoadDbs.Enabled = false;

            // Build a temporary profile pointing to the system db to list databases
            var tempProfile = BuildProfile(systemDb: true);
            var svc = new DatabaseService(tempProfile);

            try
            {
                var dbs = await svc.GetDatabasesAsync();
                cboDatabase.Items.Clear();

                if (!dbs.Any())
                {
                    cboDatabase.Items.Add("No databases found");
                    lblStatus.ForeColor = Color.FromArgb(220, 120, 60);
                    lblStatus.Text = "Connected but no databases returned.";
                }
                else
                {
                    foreach (var db in dbs)
                        cboDatabase.Items.Add(db);
                    cboDatabase.SelectedIndex = 0;
                    lblStatus.ForeColor = Color.FromArgb(80, 200, 120);
                    lblStatus.Text = $"✓ Loaded {dbs.Count} databases.";
                }
            }
            catch (Exception ex)
            {
                lblStatus.ForeColor = Color.FromArgb(220, 80, 80);
                lblStatus.Text = $"✗ Failed: {ex.Message[..Math.Min(60, ex.Message.Length)]}";
                cboDatabase.Items.Clear();
                cboDatabase.Items.Add("— connection failed —");
                cboDatabase.SelectedIndex = 0;
            }

            btnLoadDbs.Enabled = true;
        }

        private void LoadProfile(ConnectionProfile p)
        {
            txtName.Text = p.Name;
            cboDbType.SelectedIndex = p.DbType == DbType.SqlServer ? 0 : 1;
            txtServer.Text = p.Server;
            txtPort.Text = p.Port.ToString();
            txtUsername.Text = p.Username;
            chkWinAuth.Checked = p.UseWindowsAuth;

            // Pre-populate the database dropdown with the saved value
            cboDatabase.Items.Clear();
            cboDatabase.Items.Add(p.Database);
            cboDatabase.SelectedIndex = 0;
        }

        private async Task TestConnectionAsync()
        {
            lblStatus.ForeColor = Color.FromArgb(200, 180, 80);
            lblStatus.Text = "Testing...";
            btnTest.Enabled = false;
            var profile = BuildProfile();
            var svc = new DatabaseService(profile);
            bool ok = await svc.TestConnectionAsync();
            lblStatus.ForeColor = ok ? Color.FromArgb(80, 200, 120) : Color.FromArgb(220, 80, 80);
            lblStatus.Text = ok ? "✓ Connected successfully!" : "✗ Connection failed. Check settings.";
            btnTest.Enabled = true;
        }

        private void SaveClick(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtServer.Text))
            {
                MessageBox.Show("Server address is required.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (cboDatabase.SelectedItem == null || cboDatabase.SelectedItem.ToString()!.StartsWith("—"))
            {
                MessageBox.Show("Please load and select a database.", "Validation",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            Result = BuildProfile();
            DialogResult = DialogResult.OK;
            Close();
        }

        private ConnectionProfile BuildProfile(bool systemDb = false)
        {
            string dbName = systemDb
                ? (cboDbType.SelectedIndex == 0 ? "master" : "postgres")
                : (cboDatabase.SelectedItem?.ToString() ?? "");

            return new ConnectionProfile
            {
                Name = txtName.Text.Trim(),
                DbType = cboDbType.SelectedIndex == 0 ? DbType.SqlServer : DbType.PostgreSQL,
                Server = txtServer.Text.Trim(),
                Port = int.TryParse(txtPort.Text, out int p) ? p :
                       (cboDbType.SelectedIndex == 0 ? 1433 : 5432),
                Database = dbName,
                Username = txtUsername.Text.Trim(),
                Password = txtPassword.Text,
                UseWindowsAuth = chkWinAuth.Checked
            };
        }
    }
}
