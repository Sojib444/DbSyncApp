using DBSyncApp.Models;
using DBSyncApp.Services;

namespace DBSyncApp.Forms
{
    public class MainForm : Form
    {
        // Connection panels
        private Panel _srcPanel = new(), _tgtPanel = new();
        private ComboBox _cboSource = new(), _cboTarget = new();
        private Button _btnAddSrc = new(), _btnAddTgt = new(), _btnEditSrc = new(), _btnEditTgt = new();
        private Button _btnConnSrc = new(), _btnConnTgt = new();
        private Label _lblSrcStatus = new(), _lblTgtStatus = new();

        // Table selection
        private CheckedListBox _lstTables = new();
        private Button _btnSelectAll = new(), _btnSelectNone = new();
        private TextBox _txtSearch = new();

        // Action
        private Button _btnDiff = new(), _btnSync = new();
        private ProgressBar _progress = new();
        private Label _lblProgressMsg = new();
        private RichTextBox _txtLog = new();

        // State
        private List<ConnectionProfile> _profiles = new();
        private DatabaseService? _sourceDb, _targetDb;
        private ConnectionProfile? _sourceProfile, _targetProfile;
        private List<TableDiff> _lastDiffs = new();
        private List<string> _allTables = new();

        public MainForm()
        {
            _profiles = ProfileStore.Load();
            InitUI();
            RefreshProfileDropdowns();
        }

        private void InitUI()
        {
            Text = "DBSync — Database Synchronisation Tool";
            Size = new Size(1100, 780);
            MinimumSize = new Size(850, 600);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(13, 13, 22);
            ForeColor = Color.FromArgb(210, 210, 228);
            Font = new Font("Segoe UI", 9.5f);
            Icon = SystemIcons.Application;

            // Title bar strip
            var titleBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 52,
                BackColor = Color.FromArgb(16, 16, 30)
            };
            var lblTitle = new Label
            {
                Text = "⇄ ESS  DBSync",
                Font = new Font("Segoe UI", 15f, FontStyle.Bold),
                ForeColor = Color.FromArgb(80, 160, 255),
                AutoSize = true,
                Location = new Point(20, 12)
            };
            var lblSub = new Label
            {
                Text = "SQL Server  ·  PostgreSQL",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.FromArgb(100, 100, 140),
                AutoSize = true,
                Location = new Point(165, 18)
            };
            titleBar.Controls.AddRange(new Control[] { lblTitle, lblSub });

            // Main layout: left (connections+tables) | right (log)
            var mainLayout = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 680,
                BackColor = Color.FromArgb(30, 30, 50)
            };
            mainLayout.Panel1.BackColor = Color.FromArgb(13, 13, 22);
            mainLayout.Panel2.BackColor = Color.FromArgb(10, 10, 18);

            // Left panel layout
            var leftLayout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 2,
                Padding = new Padding(16, 12, 12, 12)
            };
            leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            leftLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 200));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));

            // Source panel
            _srcPanel = BuildConnectionPanel("SOURCE", _cboSource, _btnAddSrc, _btnEditSrc,
                _btnConnSrc, _lblSrcStatus, true);
            leftLayout.Controls.Add(_srcPanel, 0, 0);

            // Target panel
            _tgtPanel = BuildConnectionPanel("TARGET", _cboTarget, _btnAddTgt, _btnEditTgt,
                _btnConnTgt, _lblTgtStatus, false);
            leftLayout.Controls.Add(_tgtPanel, 1, 0);

            // Table selection (spans both columns)
            var tablePanel = BuildTablePanel();
            leftLayout.SetColumnSpan(tablePanel, 2);
            leftLayout.Controls.Add(tablePanel, 0, 1);

            // Action bar
            var actionBar = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            _btnDiff.Text = "⟳  Compute Diff";
            _btnSync.Text = "▶  Sync to Target";
            StyleActionBtn(_btnDiff, Color.FromArgb(40, 100, 180));
            StyleActionBtn(_btnSync, Color.FromArgb(30, 130, 70));
            _btnSync.Enabled = false;

            _progress.Dock = DockStyle.Bottom;
            _progress.Height = 4;
            _progress.Style = ProgressBarStyle.Marquee;
            _progress.MarqueeAnimationSpeed = 0;
            _progress.BackColor = Color.FromArgb(20, 20, 35);
            _progress.ForeColor = Color.FromArgb(60, 140, 220);

            _lblProgressMsg.ForeColor = Color.FromArgb(140, 140, 180);
            _lblProgressMsg.Font = new Font("Segoe UI", 8.5f);
            _lblProgressMsg.AutoSize = true;
            _lblProgressMsg.Location = new Point(0, 36);

            actionBar.Controls.AddRange(new Control[] { _btnDiff, _btnSync, _lblProgressMsg, _progress });
            _btnDiff.Location = new Point(0, 8); _btnDiff.Width = 160;
            _btnSync.Location = new Point(170, 8); _btnSync.Width = 160;

            leftLayout.SetColumnSpan(actionBar, 2);
            leftLayout.Controls.Add(actionBar, 0, 2);

            mainLayout.Panel1.Controls.Add(leftLayout);

            // Log panel
            var logLabel = new Label
            {
                Text = "Activity Log",
                Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
                ForeColor = Color.FromArgb(120, 120, 160),
                Dock = DockStyle.Top,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                BackColor = Color.FromArgb(16, 16, 28)
            };
            _txtLog.Dock = DockStyle.Fill;
            _txtLog.BackColor = Color.FromArgb(8, 8, 15);
            _txtLog.ForeColor = Color.FromArgb(140, 200, 140);
            _txtLog.Font = new Font("Consolas", 8.5f);
            _txtLog.BorderStyle = BorderStyle.None;
            _txtLog.ReadOnly = true;
            _txtLog.ScrollBars = RichTextBoxScrollBars.Vertical;
            mainLayout.Panel2.Controls.Add(_txtLog);
            mainLayout.Panel2.Controls.Add(logLabel);

            Controls.Add(mainLayout);
            Controls.Add(titleBar);

            // Wire events
            _btnAddSrc.Click += (s, e) => AddProfile(true);
            _btnAddTgt.Click += (s, e) => AddProfile(false);
            _btnEditSrc.Click += (s, e) => EditProfile(true);
            _btnEditTgt.Click += (s, e) => EditProfile(false);
            _btnConnSrc.Click += async (s, e) => await ConnectAsync(true);
            _btnConnTgt.Click += async (s, e) => await ConnectAsync(false);
            _btnDiff.Click += async (s, e) => await RunDiffAsync();
            _btnSync.Click += async (s, e) => await RunSyncAsync();
            _txtSearch.TextChanged += (s, e) => FilterTables();
            _btnSelectAll.Click += (s, e) => SetAllChecked(true);
            _btnSelectNone.Click += (s, e) => SetAllChecked(false);
        }

        private Panel BuildConnectionPanel(string role, ComboBox cbo, Button btnAdd, Button btnEdit,
            Button btnConn, Label lblStatus, bool isSource)
        {
            var panel = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(0, 0, isSource ? 8 : 0, 8),
                BackColor = Color.Transparent
            };

            var box = new GroupBox
            {
                Text = role,
                Dock = DockStyle.Fill,
                ForeColor = isSource ? Color.FromArgb(80, 180, 255) : Color.FromArgb(80, 220, 140),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(18, 18, 32),
                Padding = new Padding(8)
            };

            var inner = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 5,
                ColumnCount = 1,
                Padding = new Padding(8, 14, 8, 8)
            };

            var lbl1 = new Label { Text = "Saved connections:", ForeColor = Color.FromArgb(140, 140, 170), AutoSize = true };
            cbo.Dock = DockStyle.Fill;
            cbo.DropDownStyle = ComboBoxStyle.DropDownList;
            cbo.BackColor = Color.FromArgb(26, 26, 42);
            cbo.ForeColor = Color.FromArgb(210, 210, 230);
            cbo.FlatStyle = FlatStyle.Flat;
            cbo.Height = 28;

            var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 34, FlowDirection = FlowDirection.LeftToRight };
            StyleSmallBtn(btnAdd, "+ New", Color.FromArgb(50, 80, 140));
            StyleSmallBtn(btnEdit, "Edit", Color.FromArgb(60, 70, 100));
            StyleSmallBtn(btnConn, "Connect", isSource ? Color.FromArgb(40, 100, 180) : Color.FromArgb(40, 140, 80));
            btnRow.Controls.AddRange(new Control[] { btnAdd, btnEdit, btnConn });

            lblStatus.ForeColor = Color.FromArgb(100, 100, 130);
            lblStatus.Font = new Font("Segoe UI", 8.5f);
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.Text = "Not connected";
            lblStatus.TextAlign = ContentAlignment.MiddleLeft;
            lblStatus.Height = 22;

            inner.Controls.Add(lbl1);
            inner.Controls.Add(cbo);
            inner.Controls.Add(btnRow);
            inner.Controls.Add(lblStatus);
            box.Controls.Add(inner);
            panel.Controls.Add(box);
            return panel;
        }

        private Panel BuildTablePanel()
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            var box = new GroupBox
            {
                Text = "Tables to Sync",
                Dock = DockStyle.Fill,
                ForeColor = Color.FromArgb(180, 180, 220),
                Font = new Font("Segoe UI", 9f, FontStyle.Bold),
                BackColor = Color.FromArgb(18, 18, 32)
            };
            var inner = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                RowCount = 3,
                ColumnCount = 1,
                Padding = new Padding(8, 14, 8, 8)
            };
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            inner.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            inner.RowStyles.Add(new RowStyle(SizeType.Absolute, 32));

            // Search bar
            _txtSearch.Dock = DockStyle.Fill;
            _txtSearch.PlaceholderText = "Search tables...";
            _txtSearch.BackColor = Color.FromArgb(26, 26, 42);
            _txtSearch.ForeColor = Color.FromArgb(200, 200, 220);
            _txtSearch.BorderStyle = BorderStyle.FixedSingle;

            // List
            _lstTables.Dock = DockStyle.Fill;
            _lstTables.BackColor = Color.FromArgb(16, 16, 28);
            _lstTables.ForeColor = Color.FromArgb(200, 200, 220);
            _lstTables.BorderStyle = BorderStyle.None;
            _lstTables.Font = new Font("Consolas", 9f);
            _lstTables.CheckOnClick = true;

            // Buttons
            var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
            StyleSmallBtn(_btnSelectAll, "Select All", Color.FromArgb(50, 80, 120));
            StyleSmallBtn(_btnSelectNone, "Clear All", Color.FromArgb(60, 50, 80));
            btnRow.Controls.AddRange(new Control[] { _btnSelectAll, _btnSelectNone });

            inner.Controls.Add(_txtSearch);
            inner.Controls.Add(_lstTables);
            inner.Controls.Add(btnRow);
            box.Controls.Add(inner);
            panel.Controls.Add(box);
            return panel;
        }

        private void StyleActionBtn(Button btn, Color bg)
        {
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btn.Height = 36;
            btn.Cursor = Cursors.Hand;
        }

        private void StyleSmallBtn(Button btn, string text, Color bg)
        {
            btn.Text = text;
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("Segoe UI", 8.5f);
            btn.Height = 28;
            btn.AutoSize = true;
            btn.Padding = new Padding(8, 0, 8, 0);
            btn.Margin = new Padding(0, 0, 5, 0);
            btn.Cursor = Cursors.Hand;
        }

        // ─── Connection Management ───────────────────────────────────────

        private void RefreshProfileDropdowns()
        {
            _cboSource.Items.Clear();
            _cboTarget.Items.Clear();
            foreach (var p in _profiles)
            {
                _cboSource.Items.Add(p.DisplayName);
                _cboTarget.Items.Add(p.DisplayName);
            }
        }

        private void AddProfile(bool isSource)
        {
            using var dlg = new ConnectionDialog();
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
            {
                _profiles.Add(dlg.Result);
                ProfileStore.Save(_profiles);
                RefreshProfileDropdowns();
                var idx = _profiles.Count - 1;
                if (isSource) _cboSource.SelectedIndex = idx;
                else _cboTarget.SelectedIndex = idx;
                Log($"Profile '{dlg.Result.DisplayName}' saved.");
            }
        }

        private void EditProfile(bool isSource)
        {
            var cbo = isSource ? _cboSource : _cboTarget;
            if (cbo.SelectedIndex < 0) { Log("Select a profile to edit."); return; }
            var profile = _profiles[cbo.SelectedIndex];
            using var dlg = new ConnectionDialog(profile);
            if (dlg.ShowDialog(this) == DialogResult.OK && dlg.Result != null)
            {
                _profiles[cbo.SelectedIndex] = dlg.Result;
                ProfileStore.Save(_profiles);
                RefreshProfileDropdowns();
                cbo.SelectedIndex = cbo.SelectedIndex;
                Log($"Profile updated.");
            }
        }

        private async Task ConnectAsync(bool isSource)
        {
            var cbo = isSource ? _cboSource : _cboTarget;
            var lbl = isSource ? _lblSrcStatus : _lblTgtStatus;

            if (cbo.SelectedIndex < 0)
            {
                MessageBox.Show("Please select or add a connection profile first.", "No Profile",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var profile = _profiles[cbo.SelectedIndex];
            lbl.Text = "Connecting...";
            lbl.ForeColor = Color.FromArgb(200, 180, 60);
            SetBusy(true);

            var svc = new DatabaseService(profile);
            bool ok = await svc.TestConnectionAsync();

            if (ok)
            {
                if (isSource) { _sourceDb = svc; _sourceProfile = profile; }
                else { _targetDb = svc; _targetProfile = profile; }

                lbl.Text = $"✓ {profile.Database}  [{profile.DbType}]";
                lbl.ForeColor = Color.FromArgb(80, 210, 130);
                Log($"{(isSource ? "Source" : "Target")} connected: {profile.DisplayName}");

                if (isSource) await LoadTablesAsync();
            }
            else
            {
                lbl.Text = "✗ Connection failed";
                lbl.ForeColor = Color.FromArgb(220, 80, 80);
                Log($"Failed to connect to {profile.DisplayName}. Check server, credentials, and firewall.");
            }
            SetBusy(false);
        }

        private async Task LoadTablesAsync()
        {
            if (_sourceDb == null) return;
            SetBusy(true, "Loading tables...");
            try
            {
                _allTables = await _sourceDb.GetTablesAsync();
                _lstTables.Items.Clear();
                foreach (var t in _allTables)
                    _lstTables.Items.Add(t, false);
                Log($"Loaded {_allTables.Count} tables from source.");
            }
            catch (Exception ex) { Log($"Error loading tables: {ex.Message}"); }
            SetBusy(false);
        }

        private void FilterTables()
        {
            var q = _txtSearch.Text.Trim().ToLower();
            _lstTables.Items.Clear();
            var filtered = string.IsNullOrEmpty(q)
                ? _allTables
                : _allTables.Where(t => t.ToLower().Contains(q)).ToList();
            foreach (var t in filtered)
                _lstTables.Items.Add(t, false);
        }

        private void SetAllChecked(bool value)
        {
            for (int i = 0; i < _lstTables.Items.Count; i++)
                _lstTables.SetItemChecked(i, value);
        }

        // ─── Diff & Sync ─────────────────────────────────────────────────

        private async Task RunDiffAsync()
        {
            if (_sourceDb == null || _targetDb == null)
            {
                MessageBox.Show("Please connect both source and target databases.", "Not Connected",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var selected = _lstTables.CheckedItems.Cast<string>().ToList();
            if (!selected.Any())
            {
                MessageBox.Show("Please select at least one table to compare.", "No Tables",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            SetBusy(true);
            _btnSync.Enabled = false;
            _lastDiffs.Clear();

            var progress = new Progress<string>(msg => {
                _lblProgressMsg.Text = msg;
                Log(msg);
            });

            try
            {
                Log($"Starting diff for {selected.Count} tables...");
                foreach (var table in selected)
                {
                    var diff = await DiffService.ComputeDiffAsync(_sourceDb, _targetDb, table, progress);
                    _lastDiffs.Add(diff);
                    Log($"  {table}: +{diff.InsertCount} insert  ~{diff.UpdateCount} update  -{diff.DeleteCount} delete");
                }

                int totalChanges = _lastDiffs.Sum(d => d.Rows.Count);
                Log($"\nDiff complete. {totalChanges} total changes found across {_lastDiffs.Count(d => d.HasChanges)} tables.");

                if (totalChanges == 0)
                {
                    MessageBox.Show("No differences found. Databases are in sync!", "Up to Date",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    _btnSync.Enabled = true;
                    Log("Click 'Sync to Target' to review changes and apply.");
                }
            }
            catch (Exception ex) { Log($"Error during diff: {ex.Message}"); }
            SetBusy(false);
        }

        private async Task RunSyncAsync()
        {
            if (!_lastDiffs.Any()) return;

            using var reviewForm = new DiffReviewForm(_lastDiffs);
            if (reviewForm.ShowDialog(this) != DialogResult.OK || !reviewForm.Confirmed) return;

            var rows = reviewForm.ConfirmedRows;
            SetBusy(true, $"Applying {rows.Count} changes...");
            Log($"\nApplying {rows.Count} confirmed changes to target...");

            try
            {
                var sqls = rows.Select(r => r.FinalSql).ToList();
                var (success, failed, errors) = await _targetDb!.ExecuteSqlBatchAsync(sqls);

                Log($"\n── Sync Complete ──────────────────────");
                Log($"  ✓ {success} statements applied successfully");
                if (failed > 0)
                {
                    Log($"  ✗ {failed} statements failed:");
                    foreach (var err in errors) Log($"    {err}");
                }

                string msg = failed == 0
                    ? $"Sync complete! {success} changes applied successfully."
                    : $"Sync finished with {failed} errors. Check the log for details.";
                MessageBox.Show(msg, "Sync Complete",
                    MessageBoxButtons.OK,
                    failed == 0 ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

                _btnSync.Enabled = false;
                _lastDiffs.Clear();
            }
            catch (Exception ex)
            {
                Log($"Fatal error during sync (rolled back): {ex.Message}");
                MessageBox.Show($"Sync failed and was rolled back.\n\n{ex.Message}", "Sync Failed",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            SetBusy(false);
        }

        // ─── Helpers ─────────────────────────────────────────────────────

        private void SetBusy(bool busy, string? msg = null)
        {
            _btnDiff.Enabled = !busy;
            _progress.MarqueeAnimationSpeed = busy ? 30 : 0;
            _progress.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            if (msg != null) _lblProgressMsg.Text = msg;
            else if (!busy) _lblProgressMsg.Text = "";
            Application.DoEvents();
        }

        private void Log(string msg)
        {
            var time = DateTime.Now.ToString("HH:mm:ss");
            _txtLog.AppendText($"[{time}] {msg}\n");
            _txtLog.ScrollToCaret();
        }
    }
}
