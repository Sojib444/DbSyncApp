using DBSyncApp.Models;

namespace DBSyncApp.Forms
{
    public class DiffReviewForm : Form
    {
        private List<TableDiff> _diffs;
        private SplitContainer _mainSplit = new();
        private TreeView _treeNav = new();
        private DataGridView _gridDiffs = new();
        private RichTextBox _txtSqlEditor = new();
        private Label _lblSqlLabel = new();
        private Button _btnApplyEdit = new(), _btnResetEdit = new();
        private Label _lblSummary = new();
        private Button _btnConfirm = new(), _btnCancel = new();
        private Panel _sqlPanel = new();
        private RowDiff? _currentRow;

        public List<RowDiff> ConfirmedRows { get; private set; } = new();
        public bool Confirmed { get; private set; }

        public DiffReviewForm(List<TableDiff> diffs)
        {
            _diffs = diffs;
            InitUI();
            LoadTree();
            UpdateSummaryBar();
        }

        private void InitUI()
        {
            Text = "Review Changes Before Sync";
            Size = new Size(1200, 780);
            MinimumSize = new Size(900, 600);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(13, 13, 20);
            ForeColor = Color.FromArgb(210, 210, 225);
            Font = new Font("Segoe UI", 9f);

            // Top bar
            var topBar = new Panel
            {
                Dock = DockStyle.Top,
                Height = 48,
                BackColor = Color.FromArgb(20, 20, 35),
                Padding = new Padding(16, 10, 16, 10)
            };
            var lblTitle = new Label
            {
                Text = "⟳  Sync Preview — Review & Edit SQL",
                Font = new Font("Segoe UI", 11f, FontStyle.Bold),
                ForeColor = Color.FromArgb(100, 180, 255),
                AutoSize = true,
                Location = new Point(16, 12)
            };
            topBar.Controls.Add(lblTitle);

            // Summary bar
            _lblSummary.Dock = DockStyle.Top;
            _lblSummary.Height = 32;
            _lblSummary.BackColor = Color.FromArgb(20, 35, 20);
            _lblSummary.ForeColor = Color.FromArgb(120, 210, 140);
            _lblSummary.Font = new Font("Segoe UI", 9f);
            _lblSummary.TextAlign = ContentAlignment.MiddleLeft;
            _lblSummary.Padding = new Padding(16, 0, 0, 0);

            // Main split: left nav | right content
            _mainSplit.Dock = DockStyle.Fill;
            _mainSplit.Orientation = Orientation.Vertical;
            _mainSplit.SplitterDistance = 220;
            _mainSplit.BackColor = Color.FromArgb(30, 30, 50);
            _mainSplit.Panel1.BackColor = Color.FromArgb(16, 16, 26);
            _mainSplit.Panel2.BackColor = Color.FromArgb(13, 13, 20);

            // Tree nav
            _treeNav.Dock = DockStyle.Fill;
            _treeNav.BackColor = Color.FromArgb(16, 16, 26);
            _treeNav.ForeColor = Color.FromArgb(200, 200, 220);
            _treeNav.BorderStyle = BorderStyle.None;
            _treeNav.Font = new Font("Segoe UI", 9.5f);
            _treeNav.ItemHeight = 26;
            _treeNav.ShowLines = true;
            _treeNav.AfterSelect += TreeNav_AfterSelect;
            _mainSplit.Panel1.Controls.Add(_treeNav);

            // Right side: grid on top, SQL editor below
            var rightSplit = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 340,
                BackColor = Color.FromArgb(30, 30, 50)
            };

            // Grid
            _gridDiffs.Dock = DockStyle.Fill;
            _gridDiffs.BackgroundColor = Color.FromArgb(16, 16, 26);
            _gridDiffs.GridColor = Color.FromArgb(40, 40, 60);
            _gridDiffs.BorderStyle = BorderStyle.None;
            _gridDiffs.RowHeadersVisible = false;
            _gridDiffs.AllowUserToAddRows = false;
            _gridDiffs.AllowUserToDeleteRows = false;
            _gridDiffs.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            _gridDiffs.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            _gridDiffs.MultiSelect = false;
            _gridDiffs.Font = new Font("Consolas", 9f);
            _gridDiffs.DefaultCellStyle.BackColor = Color.FromArgb(20, 20, 32);
            _gridDiffs.DefaultCellStyle.ForeColor = Color.FromArgb(200, 200, 220);
            _gridDiffs.DefaultCellStyle.SelectionBackColor = Color.FromArgb(40, 80, 160);
            _gridDiffs.DefaultCellStyle.SelectionForeColor = Color.White;
            _gridDiffs.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(28, 28, 45);
            _gridDiffs.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(160, 160, 200);
            _gridDiffs.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _gridDiffs.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None;
            _gridDiffs.EnableHeadersVisualStyles = false;
            _gridDiffs.SelectionChanged += Grid_SelectionChanged;
            _gridDiffs.CellValueChanged += Grid_CellValueChanged;
            _gridDiffs.CurrentCellDirtyStateChanged += (s, e) => {
                if (_gridDiffs.IsCurrentCellDirty) _gridDiffs.CommitEdit(DataGridViewDataErrorContexts.Commit);
            };
            rightSplit.Panel1.Controls.Add(_gridDiffs);

            // SQL editor panel
            _sqlPanel.Dock = DockStyle.Fill;
            _sqlPanel.BackColor = Color.FromArgb(13, 13, 20);
            _sqlPanel.Padding = new Padding(8, 8, 8, 8);

            _lblSqlLabel.Text = "SQL — select a row to view and edit";
            _lblSqlLabel.ForeColor = Color.FromArgb(140, 140, 180);
            _lblSqlLabel.Font = new Font("Segoe UI", 8.5f, FontStyle.Bold);
            _lblSqlLabel.Dock = DockStyle.Top;
            _lblSqlLabel.Height = 24;

            _txtSqlEditor.Dock = DockStyle.Fill;
            _txtSqlEditor.BackColor = Color.FromArgb(10, 10, 18);
            _txtSqlEditor.ForeColor = Color.FromArgb(180, 220, 180);
            _txtSqlEditor.Font = new Font("Consolas", 10f);
            _txtSqlEditor.BorderStyle = BorderStyle.None;
            _txtSqlEditor.ScrollBars = RichTextBoxScrollBars.Both;
            _txtSqlEditor.WordWrap = false;

            var sqlBtnPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 36,
                FlowDirection = FlowDirection.LeftToRight,
                BackColor = Color.Transparent,
                Padding = new Padding(0, 4, 0, 0)
            };
            StyleSmallBtn(_btnApplyEdit, "Apply Edit", Color.FromArgb(50, 130, 80));
            StyleSmallBtn(_btnResetEdit, "Reset to Original", Color.FromArgb(80, 60, 100));
            _btnApplyEdit.Click += ApplyEdit_Click;
            _btnResetEdit.Click += ResetEdit_Click;
            sqlBtnPanel.Controls.AddRange(new Control[] { _btnApplyEdit, _btnResetEdit });

            _sqlPanel.Controls.Add(_txtSqlEditor);
            _sqlPanel.Controls.Add(sqlBtnPanel);
            _sqlPanel.Controls.Add(_lblSqlLabel);
            rightSplit.Panel2.Controls.Add(_sqlPanel);
            _mainSplit.Panel2.Controls.Add(rightSplit);

            // Bottom confirm bar
            var bottomBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = Color.FromArgb(18, 18, 30),
                Padding = new Padding(16, 10, 16, 10)
            };
            StyleButton(_btnConfirm, "▶  Confirm & Sync Selected", Color.FromArgb(30, 140, 80));
            StyleButton(_btnCancel, "Cancel", Color.FromArgb(70, 70, 90));
            _btnConfirm.Dock = DockStyle.Right;
            _btnCancel.Dock = DockStyle.Right;
            _btnConfirm.Width = 220;
            _btnCancel.Width = 100;
            _btnConfirm.Click += Confirm_Click;
            _btnCancel.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };
            bottomBar.Controls.Add(_btnConfirm);
            bottomBar.Controls.Add(_btnCancel);

            Controls.Add(_mainSplit);
            Controls.Add(_lblSummary);
            Controls.Add(topBar);
            Controls.Add(bottomBar);
        }

        private void StyleButton(Button btn, string text, Color bg)
        {
            btn.Text = text;
            btn.BackColor = bg;
            btn.ForeColor = Color.White;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            btn.Cursor = Cursors.Hand;
            btn.Height = 36;
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
            btn.Margin = new Padding(0, 0, 6, 0);
            btn.Cursor = Cursors.Hand;
        }

        private void LoadTree()
        {
            _treeNav.BeginUpdate();
            _treeNav.Nodes.Clear();
            int totalChanges = 0;

            foreach (var diff in _diffs)
            {
                if (!diff.HasChanges) continue;
                var node = new TreeNode($"{diff.TableName}  ({diff.Rows.Count})")
                {
                    ForeColor = Color.FromArgb(120, 190, 255),
                    Tag = diff
                };

                if (diff.InsertCount > 0)
                    node.Nodes.Add(new TreeNode($"+ {diff.InsertCount} INSERT") { ForeColor = Color.FromArgb(80, 200, 120), Tag = ("INSERT", diff) });
                if (diff.UpdateCount > 0)
                    node.Nodes.Add(new TreeNode($"~ {diff.UpdateCount} UPDATE") { ForeColor = Color.FromArgb(220, 180, 60), Tag = ("UPDATE", diff) });
                if (diff.DeleteCount > 0)
                    node.Nodes.Add(new TreeNode($"✕ {diff.DeleteCount} DELETE") { ForeColor = Color.FromArgb(220, 80, 80), Tag = ("DELETE", diff) });

                _treeNav.Nodes.Add(node);
                node.Expand();
                totalChanges += diff.Rows.Count;
            }

            _treeNav.EndUpdate();
        }

        private void TreeNav_AfterSelect(object? sender, TreeViewEventArgs e)
        {
            if (e.Node?.Tag == null) return;

            if (e.Node.Tag is TableDiff diff)
                LoadGrid(diff.Rows);
            else if (e.Node.Tag is ValueTuple<string, TableDiff> tuple)
            {
                var filtered = tuple.Item2.Rows
                    .Where(r => r.DiffType.ToString().Equals(tuple.Item1, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                LoadGrid(filtered);
            }
        }

        private void LoadGrid(List<RowDiff> rows)
        {
            _gridDiffs.Columns.Clear();
            _gridDiffs.Rows.Clear();

            _gridDiffs.Columns.Add(new DataGridViewCheckBoxColumn
            {
                Name = "Selected", HeaderText = "✓", Width = 36, FillWeight = 1
            });
            _gridDiffs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Type", HeaderText = "Type", Width = 70, ReadOnly = true, FillWeight = 1 });
            _gridDiffs.Columns.Add(new DataGridViewTextBoxColumn { Name = "PK", HeaderText = "Primary Key", Width = 100, ReadOnly = true });
            _gridDiffs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Changed", HeaderText = "Changed Columns", ReadOnly = true });
            _gridDiffs.Columns.Add(new DataGridViewTextBoxColumn { Name = "Modified", HeaderText = "SQL Modified", Width = 80, ReadOnly = true });

            foreach (var row in rows)
            {
                int idx = _gridDiffs.Rows.Add();
                var gridRow = _gridDiffs.Rows[idx];
                gridRow.Tag = row;
                gridRow.Cells["Selected"].Value = row.IsSelected;
                gridRow.Cells["Type"].Value = row.DiffType.ToString().ToUpper();
                gridRow.Cells["PK"].Value = row.PrimaryKey;
                gridRow.Cells["Changed"].Value = row.ChangedColumns.Any()
                    ? string.Join(", ", row.ChangedColumns)
                    : "(all columns)";
                gridRow.Cells["Modified"].Value = row.IsModified ? "✎ Yes" : "";

                // Color by type
                Color typeColor = row.DiffType switch
                {
                    DiffType.Insert => Color.FromArgb(20, 50, 30),
                    DiffType.Update => Color.FromArgb(45, 40, 10),
                    DiffType.Delete => Color.FromArgb(50, 18, 18),
                    _ => Color.FromArgb(20, 20, 32)
                };
                gridRow.DefaultCellStyle.BackColor = typeColor;
            }

            _currentRow = null;
            _txtSqlEditor.Clear();
            _lblSqlLabel.Text = $"SQL Editor — {rows.Count} rows shown";
            UpdateSummaryBar();
        }

        private void Grid_SelectionChanged(object? sender, EventArgs e)
        {
            if (_gridDiffs.SelectedRows.Count == 0) return;
            var row = _gridDiffs.SelectedRows[0].Tag as RowDiff;
            if (row == null) return;

            _currentRow = row;
            _txtSqlEditor.Text = row.FinalSql;
            _lblSqlLabel.Text = $"SQL for PK={row.PrimaryKey}  [{row.DiffType}]{(row.IsModified ? "  ✎ Modified" : "")}";

            // Syntax highlight basic keywords
            HighlightSql(_txtSqlEditor);
        }

        private void HighlightSql(RichTextBox rtb)
        {
            string text = rtb.Text;
            rtb.SelectAll();
            rtb.SelectionColor = Color.FromArgb(180, 220, 180);

            string[] keywords = { "INSERT", "UPDATE", "DELETE", "INTO", "SET", "WHERE", "VALUES", "FROM", "SELECT", "NULL" };
            foreach (var kw in keywords)
            {
                int start = 0;
                while ((start = text.IndexOf(kw, start, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    rtb.Select(start, kw.Length);
                    rtb.SelectionColor = Color.FromArgb(100, 160, 255);
                    start += kw.Length;
                }
            }
            rtb.SelectionStart = 0;
            rtb.SelectionLength = 0;
        }

        private void Grid_CellValueChanged(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndex < 0) return;
            if (_gridDiffs.Columns[e.ColumnIndex].Name == "Selected")
            {
                var row = _gridDiffs.Rows[e.RowIndex].Tag as RowDiff;
                if (row != null)
                    row.IsSelected = (bool)(_gridDiffs.Rows[e.RowIndex].Cells["Selected"].Value ?? false);
                UpdateSummaryBar();
            }
        }

        private void ApplyEdit_Click(object? sender, EventArgs e)
        {
            if (_currentRow == null) return;
            _currentRow.EditedSql = _txtSqlEditor.Text;
            _lblSqlLabel.Text = $"SQL for PK={_currentRow.PrimaryKey}  [{_currentRow.DiffType}]  ✎ Modified";

            // Update grid cell
            foreach (DataGridViewRow row in _gridDiffs.Rows)
                if (row.Tag == _currentRow)
                    row.Cells["Modified"].Value = "✎ Yes";

            MessageBox.Show("SQL edit saved for this row.", "Edit Applied",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void ResetEdit_Click(object? sender, EventArgs e)
        {
            if (_currentRow == null) return;
            _currentRow.EditedSql = "";
            _txtSqlEditor.Text = _currentRow.GeneratedSql;
            HighlightSql(_txtSqlEditor);
            _lblSqlLabel.Text = $"SQL for PK={_currentRow.PrimaryKey}  [{_currentRow.DiffType}]";

            foreach (DataGridViewRow row in _gridDiffs.Rows)
                if (row.Tag == _currentRow)
                    row.Cells["Modified"].Value = "";
        }

        private void UpdateSummaryBar()
        {
            int total = _diffs.SelectMany(d => d.Rows).Count();
            int selected = _diffs.SelectMany(d => d.Rows).Count(r => r.IsSelected);
            _lblSummary.Text = $"  {total} total changes across {_diffs.Count(d => d.HasChanges)} tables    |    {selected} selected for sync";
        }

        private void Confirm_Click(object? sender, EventArgs e)
        {
            ConfirmedRows = _diffs.SelectMany(d => d.Rows).Where(r => r.IsSelected).ToList();
            if (!ConfirmedRows.Any())
            {
                MessageBox.Show("No rows selected for sync.", "Nothing to Sync",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            var res = MessageBox.Show(
                $"You are about to apply {ConfirmedRows.Count} SQL statements to the target database.\n\nThis cannot be undone. Continue?",
                "Final Confirmation",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (res == DialogResult.Yes)
            {
                Confirmed = true;
                DialogResult = DialogResult.OK;
                Close();
            }
        }
    }
}
