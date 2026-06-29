using CefSharp;
using CefSharp.WinForms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EDCustomNative
{
    public partial class Form1 : Form
    {
        private class ProfileInstance
        {
            public string FolderName { get; set; }
            public string Name { get; set; }
            public ChromiumWebBrowser Browser { get; set; }
            public Bitmap Thumbnail { get; set; }
            public Panel SidebarButtonPanel { get; set; }
            public PictureBox SidebarPictureBox { get; set; }
            public CheckBox GridCheckBox { get; set; }
            public int PlayTimeSeconds { get; set; }
        }

        private List<ProfileInstance> instances = new List<ProfileInstance>();
        private ProfileInstance activeInstance;
        private ProfileInstance previewTarget;
        private ProfileInstance contextMenuTarget;

        private Panel panelSidebar;
        private Panel panelSidebarHeader;
        private Button btnSingleMode;
        private Button btnGridMode;
        private FlowLayoutPanel panelButtonList;
        private Panel panelGameContainer;
        private TableLayoutPanel gridContainer;
        private PictureBox previewOverlay;
        private Panel panelAddAccount;
        private ContextMenuStrip profileContextMenu;
        private string appDataPath;
        private string profilesListPath;
        private Timer hoverTimer;
        private Timer thumbnailUpdateTimer;
        private Panel panelDashboard;
        private Label lblTotalTime;
        private Label lblTodayTime;
        private Label lblActiveSessions;
        private FlowLayoutPanel panelProfileStatsList;

        private int totalPlayTimeSeconds = 0;
        private int todayPlayTimeSeconds = 0;
        private string lastPlayedDate = "";
        private bool isGridMode = false;

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        public static void EnableNativeDarkMode(IntPtr handle)
        {
            int trueValue = 1;
            if (DwmSetWindowAttribute(handle, 20, ref trueValue, sizeof(int)) != 0)
            {
                DwmSetWindowAttribute(handle, 19, ref trueValue, sizeof(int));
            }
        }

        public Form1()
        {
            InitializeComponent();
            this.Width = 1200;
            this.Height = 700;
            this.Text = "Epicest Dueler";
            this.BackColor = Color.FromArgb(20, 20, 20);

            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_icon.ico");
                if (File.Exists(iconPath)) this.Icon = new Icon(iconPath);
            }
            catch { }

            EnableNativeDarkMode(this.Handle);

            appDataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EDCustomNative");
            if (!Directory.Exists(appDataPath))
            {
                Directory.CreateDirectory(appDataPath);
            }

            profilesListPath = Path.Combine(appDataPath, "profiles.txt");

            hoverTimer = new Timer();
            hoverTimer.Interval = 100;
            hoverTimer.Tick += HoverTimer_Tick;

            thumbnailUpdateTimer = new Timer();
            thumbnailUpdateTimer.Interval = 2000;
            thumbnailUpdateTimer.Tick += ThumbnailUpdateTimer_Tick;
            thumbnailUpdateTimer.Start();

            LoadStats();
            InitializeGlobalCEF();
            BuildContextMenu();
            BuildBaseLayout();
            LoadAllProfilesOnStartup();
        }

        private void InitializeGlobalCEF()
        {
            if (!Cef.IsInitialized)
            {
                CefSettings settings = new CefSettings();
                string flashPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "pepflashplayer.dll");

                settings.CefCommandLineArgs.Add("enable-system-flash", "1");
                settings.CefCommandLineArgs.Add("ppapi-flash-path", flashPath);
                settings.CefCommandLineArgs.Add("ppapi-flash-version", "32.0.0.371");

                settings.CefCommandLineArgs.Add("ppapi-flash-bypass-user-gesture", "1");
                settings.CefCommandLineArgs.Add("always-authorize-plugins", "1");
                settings.CefCommandLineArgs.Add("allow-outdated-plugins", "1");
                settings.CefCommandLineArgs.Add("plugin-policy", "allow");

                settings.CefCommandLineArgs.Add("disable-web-security", "1");
                settings.CefCommandLineArgs.Add("allow-running-insecure-content", "1");

                settings.CefCommandLineArgs.Add("disable-backgrounding-occluded-windows", "1");
                settings.CefCommandLineArgs.Add("disable-renderer-backgrounding", "1");
                settings.CefCommandLineArgs.Add("disable-features", "CalculateWindowOcclusion");

                Cef.Initialize(settings);
            }
        }

        private Image GetTrashCanIcon()
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (Pen pen = new Pen(Color.FromArgb(220, 53, 69), 1.5f))
                {
                    g.DrawLine(pen, 3, 3, 13, 3);
                    g.DrawLine(pen, 6, 3, 6, 1);
                    g.DrawLine(pen, 10, 3, 10, 1);
                    g.DrawLine(pen, 6, 1, 10, 1);
                    g.DrawLine(pen, 4, 4, 5, 14);
                    g.DrawLine(pen, 12, 4, 11, 14);
                    g.DrawLine(pen, 5, 14, 11, 14);
                    g.DrawLine(pen, 7, 5, 7, 12);
                    g.DrawLine(pen, 9, 5, 9, 12);
                }
            }
            return bmp;
        }

        private void BuildContextMenu()
        {
            profileContextMenu = new ContextMenuStrip();

            var refreshItem = new ToolStripMenuItem("Refresh");
            refreshItem.Click += RefreshItem_Click;
            refreshItem.ForeColor = Color.White;

            var closeSessionItem = new ToolStripMenuItem("Close Session");
            closeSessionItem.Click += CloseSessionItem_Click;
            closeSessionItem.ForeColor = Color.White;

            var renameItem = new ToolStripMenuItem("Rename Profile");
            renameItem.Click += RenameItem_Click;
            renameItem.ForeColor = Color.White;

            var removeItem = new ToolStripMenuItem("Remove Profile");
            removeItem.Click += RemoveItem_Click;
            removeItem.ForeColor = Color.FromArgb(220, 53, 69);
            removeItem.Image = GetTrashCanIcon();

            profileContextMenu.Items.Add(refreshItem);
            profileContextMenu.Items.Add(closeSessionItem);
            profileContextMenu.Items.Add(renameItem);
            profileContextMenu.Items.Add(removeItem);

            profileContextMenu.BackColor = Color.FromArgb(35, 35, 35);
            profileContextMenu.ForeColor = Color.White;
            profileContextMenu.RenderMode = ToolStripRenderMode.System;
        }

        private void BuildBaseLayout()
        {
            panelSidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 145,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            this.Controls.Add(panelSidebar);

            panelSidebarHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 45,
                BackColor = Color.FromArgb(25, 25, 25),
                Padding = new Padding(5)
            };
            panelSidebar.Controls.Add(panelSidebarHeader);

            btnSingleMode = new Button
            {
                Width = 62,
                Height = 35,
                Location = new Point(5, 5),
                BackColor = Color.FromArgb(40, 40, 40),
                FlatStyle = FlatStyle.Flat
            };
            btnSingleMode.FlatAppearance.BorderSize = 0;
            btnSingleMode.Click += (s, e) => {
                isGridMode = false;
                SyncLayout();
                btnSingleMode.Invalidate();
                btnGridMode.Invalidate();
            };
            btnSingleMode.Paint += (s, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Color blockColor = !isGridMode ? Color.FromArgb(0, 120, 215) : Color.FromArgb(120, 120, 120);
                using (Brush brush = new SolidBrush(blockColor))
                {
                    e.Graphics.FillRectangle(brush, (btnSingleMode.Width - 18) / 2, (btnSingleMode.Height - 14) / 2, 18, 14);
                }
            };
            panelSidebarHeader.Controls.Add(btnSingleMode);

            btnGridMode = new Button
            {
                Width = 62,
                Height = 35,
                Location = new Point(78, 5),
                BackColor = Color.FromArgb(40, 40, 40),
                FlatStyle = FlatStyle.Flat
            };
            btnGridMode.FlatAppearance.BorderSize = 0;
            btnGridMode.Click += (s, e) => {
                isGridMode = true;
                SyncLayout();
                btnSingleMode.Invalidate();
                btnGridMode.Invalidate();
            };
            btnGridMode.Paint += (s, e) => {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                Color blockColor = isGridMode ? Color.FromArgb(0, 120, 215) : Color.FromArgb(120, 120, 120);
                using (Brush brush = new SolidBrush(blockColor))
                {
                    int xStart = (btnGridMode.Width - 18) / 2;
                    int yStart = (btnGridMode.Height - 14) / 2;
                    e.Graphics.FillRectangle(brush, xStart, yStart, 8, 6);
                    e.Graphics.FillRectangle(brush, xStart + 10, yStart, 8, 6);
                    e.Graphics.FillRectangle(brush, xStart, yStart + 8, 8, 6);
                    e.Graphics.FillRectangle(brush, xStart + 10, yStart + 8, 8, 6);
                }
            };
            panelSidebarHeader.Controls.Add(btnGridMode);

            panelButtonList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(5, 5, 5, 5)
            };
            panelSidebar.Controls.Add(panelButtonList);
            panelButtonList.BringToFront();

            panelGameContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            this.Controls.Add(panelGameContainer);
            panelGameContainer.BringToFront();

            gridContainer = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                Visible = false
            };
            panelGameContainer.Controls.Add(gridContainer);

            previewOverlay = new PictureBox
            {
                SizeMode = PictureBoxSizeMode.Zoom,
                BackColor = Color.FromArgb(10, 10, 10),
                Visible = false,
                BorderStyle = BorderStyle.FixedSingle,
                Enabled = false
            };
            panelGameContainer.Controls.Add(previewOverlay);

            BuildDashboardPanel();
        }

        private void BuildDashboardPanel()
        {
            panelDashboard = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(20, 20, 20)
            };
            panelGameContainer.Controls.Add(panelDashboard);

            Panel panelStats = new Panel
            {
                Location = new Point(40, 100),
                Width = 450,
                Height = 350,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(20)
            };
            panelDashboard.Controls.Add(panelStats);

            Label lblStatsHeader = new Label
            {
                Text = "GLOBAL STATISTICS",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(20, 20),
                Width = 400,
                Height = 25
            };
            panelStats.Controls.Add(lblStatsHeader);

            lblTotalTime = new Label
            {
                Text = "Total Time Played: --",
                ForeColor = Color.LightGreen,
                Font = new Font("Segoe UI", 11),
                Location = new Point(20, 70),
                Width = 400,
                Height = 25
            };
            panelStats.Controls.Add(lblTotalTime);

            lblTodayTime = new Label
            {
                Text = "Played Today: --",
                ForeColor = Color.LightGreen,
                Font = new Font("Segoe UI", 11),
                Location = new Point(20, 110),
                Width = 400,
                Height = 25
            };
            panelStats.Controls.Add(lblTodayTime);

            lblActiveSessions = new Label
            {
                Text = "Active Game Processes: --",
                ForeColor = Color.Gold,
                Font = new Font("Segoe UI", 11),
                Location = new Point(20, 150),
                Width = 400,
                Height = 25
            };
            panelStats.Controls.Add(lblActiveSessions);

            Panel panelProfileStatsContainer = new Panel
            {
                Location = new Point(510, 100),
                Width = 500,
                Height = 350,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(20)
            };
            panelDashboard.Controls.Add(panelProfileStatsContainer);

            Label lblProfileStatsHeader = new Label
            {
                Text = "PLAYTIME BY ACCOUNT",
                ForeColor = Color.White,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                Location = new Point(20, 20),
                Width = 400,
                Height = 25
            };
            panelProfileStatsContainer.Controls.Add(lblProfileStatsHeader);

            panelProfileStatsList = new FlowLayoutPanel
            {
                Location = new Point(20, 60),
                Width = 460,
                Height = 270,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false
            };
            panelProfileStatsContainer.Controls.Add(panelProfileStatsList);
        }

        private void ShowDashboard()
        {
            foreach (var inst in instances)
            {
                if (inst.Browser != null)
                {
                    inst.Browser.Visible = false;
                }
                inst.SidebarButtonPanel.BackColor = Color.FromArgb(45, 45, 45);
                inst.SidebarButtonPanel.Controls.OfType<Label>().First().BackColor = Color.FromArgb(60, 60, 60);
            }

            activeInstance = null;
            this.Text = "Epicest Dueler";

            lblTotalTime.Text = $"Total Time Played:  {FormatTime(totalPlayTimeSeconds)}";
            lblTodayTime.Text = $"Played Today:  {FormatTime(todayPlayTimeSeconds)}";
            lblActiveSessions.Text = $"Active Game Processes:  {instances.Count(i => i.Browser != null)}";

            UpdateDashboardProfileStats();

            panelDashboard.Visible = true;
            panelDashboard.BringToFront();
        }

        private void UpdateDashboardProfileStats()
        {
            panelProfileStatsList.Controls.Clear();
            foreach (var inst in instances)
            {
                Panel row = new Panel { Width = 430, Height = 30, Margin = new Padding(0, 2, 0, 2) };

                Button btnFolder = new Button
                {
                    Size = new Size(24, 24),
                    Location = new Point(0, 3),
                    FlatStyle = FlatStyle.Flat,
                    BackColor = Color.FromArgb(45, 45, 45)
                };
                btnFolder.FlatAppearance.BorderSize = 0;
                btnFolder.Paint += (s, e) =>
                {
                    e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                    using (Pen pen = new Pen(Color.FromArgb(230, 180, 80), 1.5f))
                    using (Brush brush = new SolidBrush(Color.FromArgb(230, 180, 80)))
                    {
                        e.Graphics.DrawRectangle(pen, 3, 7, 17, 11);
                        Point[] points = {
                            new Point(3, 7),
                            new Point(3, 4),
                            new Point(8, 4),
                            new Point(10, 7)
                        };
                        e.Graphics.FillPolygon(brush, points);
                    }
                };

                string targetPath = Path.Combine(appDataPath, inst.FolderName);
                btnFolder.Click += (s, e) =>
                {
                    try
                    {
                        if (Directory.Exists(targetPath))
                        {
                            System.Diagnostics.Process.Start("explorer.exe", $"\"{targetPath}\"");
                        }
                    }
                    catch { }
                };
                row.Controls.Add(btnFolder);

                Label nameLabel = new Label { Text = inst.Name, ForeColor = Color.White, Font = new Font("Segoe UI", 10, FontStyle.Bold), Location = new Point(30, 5), Width = 180 };
                Label timeLabel = new Label { Text = FormatTime(inst.PlayTimeSeconds), ForeColor = Color.LightGreen, Font = new Font("Segoe UI", 10), Location = new Point(220, 5), Width = 190, TextAlign = ContentAlignment.TopRight };

                row.Controls.Add(nameLabel);
                row.Controls.Add(timeLabel);
                panelProfileStatsList.Controls.Add(row);
            }
        }

        private string FormatTime(int totalSeconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(totalSeconds);
            if (t.TotalHours >= 1)
            {
                return $"{(int)t.TotalHours} hours {t.Minutes} minutes";
            }
            return $"{t.Minutes} minutes {t.Seconds} seconds";
        }

        private void BuildAddAccountButton()
        {
            panelAddAccount = new Panel
            {
                Width = 135,
                Height = 100,
                BackColor = Color.FromArgb(45, 45, 45),
                Margin = new Padding(0, 5, 0, 5)
            };

            Label btnLabel = new Label
            {
                Text = "ADD ACCOUNT",
                ForeColor = Color.White,
                Font = new Font("Arial", 8, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(60, 60, 60)
            };
            panelAddAccount.Controls.Add(btnLabel);

            PictureBox picAdd = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            panelAddAccount.Controls.Add(picAdd);
            picAdd.BringToFront();

            picAdd.Paint += (s, e) =>
            {
                using (Pen pen = new Pen(Color.FromArgb(0, 120, 215), 4))
                {
                    int midX = picAdd.Width / 2;
                    int midY = picAdd.Height / 2;
                    int length = 12;

                    e.Graphics.DrawLine(pen, midX - length, midY, midX + length, midY);
                    e.Graphics.DrawLine(pen, midX, midY - length, midX, midY + length);
                }
            };

            Action addAction = () => CreateAndLaunchNewProfile();
            btnLabel.Click += (s, e) => addAction();
            picAdd.Click += (s, e) => addAction();
            panelAddAccount.Click += (s, e) => addAction();
        }

        private void LoadAllProfilesOnStartup()
        {
            List<string> rawLines = new List<string>();

            if (File.Exists(profilesListPath))
            {
                rawLines = File.ReadAllLines(profilesListPath)
                                     .Where(line => !string.IsNullOrWhiteSpace(line))
                                     .ToList();
            }

            foreach (string line in rawLines)
            {
                string folderName;
                string displayName;
                int playTime = 0;

                if (line.Contains("|"))
                {
                    string[] parts = line.Split('|');
                    folderName = parts[0];
                    displayName = parts.Length > 1 ? parts[1] : parts[0];
                    if (parts.Length > 2)
                    {
                        int.TryParse(parts[2], out playTime);
                    }
                }
                else
                {
                    folderName = line;
                    displayName = line;
                }

                Directory.CreateDirectory(Path.Combine(appDataPath, folderName));
                CreateInstancePlaceholder(folderName, displayName, playTime);
            }

            BuildAddAccountButton();
            panelButtonList.Controls.Add(panelAddAccount);

            ShowDashboard();
        }

        private void SaveProfilesList()
        {
            try
            {
                var lines = instances.Select(i => $"{i.FolderName}|{i.Name}|{i.PlayTimeSeconds}").ToList();
                File.WriteAllLines(profilesListPath, lines);
            }
            catch { }
        }

        private void CreateAndLaunchNewProfile()
        {
            int count = instances.Count + 1;
            string baseFolder = $"Profile_{DateTime.Now.Ticks}";
            string displayName = $"Account {count}";

            while (Directory.Exists(Path.Combine(appDataPath, baseFolder)))
            {
                baseFolder = $"Profile_{DateTime.Now.Ticks}";
            }

            Directory.CreateDirectory(Path.Combine(appDataPath, baseFolder));

            panelButtonList.Controls.Remove(panelAddAccount);

            var newInstance = CreateInstancePlaceholder(baseFolder, displayName, 0);

            panelButtonList.Controls.Add(panelAddAccount);

            SaveProfilesList();

            SwitchToInstance(newInstance);
        }

        private void LoadBrowserInstance(ProfileInstance target)
        {
            if (target.Browser != null) return;

            var requestContextSettings = new RequestContextSettings
            {
                CachePath = Path.Combine(appDataPath, target.FolderName),
                PersistSessionCookies = true
            };
            var requestContext = new RequestContext(requestContextSettings);

            var browserInstance = new ChromiumWebBrowser("https://epicduelstage.artix.com/omegaloader14.swf", requestContext);
            browserInstance.Dock = DockStyle.Fill;
            browserInstance.Visible = false;
            panelGameContainer.Controls.Add(browserInstance);

            browserInstance.IsBrowserInitializedChanged += (s, e) =>
            {
                if (browserInstance.IsBrowserInitialized)
                {
                    try
                    {
                        var context = browserInstance.GetBrowser().GetHost().RequestContext;
                        string error;
                        context.SetPreference("profile.default_content_setting_values.plugins", 1, out error);
                    }
                    catch { }
                }
            };

            target.Browser = browserInstance;
        }

        private Image CreatePlaceholderImage(string name)
        {
            Bitmap bmp = new Bitmap(135, 80);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.FromArgb(25, 25, 25));

                using (Pen borderPen = new Pen(Color.FromArgb(45, 45, 45), 1))
                {
                    g.DrawRectangle(borderPen, 0, 0, bmp.Width - 1, bmp.Height - 1);
                }

                string displayText = name.Length > 12 ? name.Substring(0, 11) + "..." : name;
                using (Font font = new Font("Segoe UI", 9, FontStyle.Bold))
                using (Brush brush = new SolidBrush(Color.FromArgb(110, 110, 110)))
                {
                    SizeF size = g.MeasureString(displayText, font);
                    g.DrawString(displayText, font, brush, (bmp.Width - size.Width) / 2, (bmp.Height - size.Height) / 2);
                }
            }
            return bmp;
        }

        private ProfileInstance CreateInstancePlaceholder(string folderName, string displayName, int playTime)
        {
            Panel btnPanel = new Panel
            {
                Width = 135,
                Height = 100,
                BackColor = Color.FromArgb(45, 45, 45),
                Margin = new Padding(0, 5, 0, 5)
            };

            Label btnLabel = new Label
            {
                Text = displayName.ToUpper(),
                ForeColor = Color.White,
                Font = new Font("Arial", 8, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(60, 60, 60)
            };
            btnPanel.Controls.Add(btnLabel);

            CheckBox chkGrid = new CheckBox
            {
                Size = new Size(16, 16),
                Location = new Point(115, 2),
                BackColor = Color.FromArgb(60, 60, 60),
                FlatStyle = FlatStyle.Flat
            };
            chkGrid.FlatAppearance.BorderSize = 0;
            chkGrid.CheckedChanged += (s, e) => {
                if (isGridMode) SyncLayout();
            };
            btnPanel.Controls.Add(chkGrid);
            chkGrid.BringToFront();

            PictureBox picThumb = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = CreatePlaceholderImage(displayName)
            };
            btnPanel.Controls.Add(picThumb);
            picThumb.BringToFront();

            ProfileInstance instance = new ProfileInstance
            {
                FolderName = folderName,
                Name = displayName,
                PlayTimeSeconds = playTime,
                Browser = null,
                SidebarButtonPanel = btnPanel,
                SidebarPictureBox = picThumb,
                GridCheckBox = chkGrid
            };

            btnLabel.Click += (s, e) => SwitchToInstance(instance);
            picThumb.Click += (s, e) => SwitchToInstance(instance);

            picThumb.MouseEnter += (s, e) => ShowPreview(instance);
            picThumb.MouseLeave += (s, e) => HidePreview();
            btnLabel.MouseEnter += (s, e) => ShowPreview(instance);
            btnLabel.MouseLeave += (s, e) => HidePreview();

            MouseEventHandler rightClickDetector = (s, e) =>
            {
                if (e.Button == MouseButtons.Right)
                {
                    contextMenuTarget = instance;
                    profileContextMenu.Show(Cursor.Position);
                }
            };
            btnPanel.MouseDown += rightClickDetector;
            btnLabel.MouseDown += rightClickDetector;
            picThumb.MouseDown += rightClickDetector;

            panelButtonList.Controls.Add(btnPanel);
            instances.Add(instance);

            return instance;
        }

        private void SwitchToInstance(ProfileInstance target)
        {
            if (target == null) return;

            if (previewTarget == target)
            {
                previewTarget = null;
                hoverTimer.Stop();
            }
            else
            {
                HidePreview();
            }

            if (activeInstance != null && !isGridMode)
            {
                CaptureInstanceThumbnail(activeInstance);
            }

            isGridMode = false;

            if (activeInstance == target)
            {
                activeInstance = null;
            }
            else
            {
                activeInstance = target;
            }

            btnSingleMode.Invalidate();
            btnGridMode.Invalidate();

            SyncLayout();
        }

        private void SyncLayout()
        {
            if (isGridMode)
            {
                var checkedInstances = instances.Where(i => i.GridCheckBox.Checked).ToList();

                if (checkedInstances.Count == 0)
                {
                    gridContainer.Visible = false;
                    gridContainer.Controls.Clear();
                    ShowDashboard();
                    return;
                }

                panelDashboard.Visible = false;

                int count = checkedInstances.Count;
                int cols = (int)Math.Ceiling(Math.Sqrt(count));
                int rows = (int)Math.Ceiling((double)count / cols);

                gridContainer.Controls.Clear();
                gridContainer.ColumnStyles.Clear();
                gridContainer.RowStyles.Clear();
                gridContainer.ColumnCount = cols;
                gridContainer.RowCount = rows;

                for (int i = 0; i < cols; i++)
                    gridContainer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / cols));
                for (int i = 0; i < rows; i++)
                    gridContainer.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / rows));

                for (int i = 0; i < count; i++)
                {
                    var inst = checkedInstances[i];
                    if (inst.Browser == null)
                    {
                        LoadBrowserInstance(inst);
                    }

                    inst.Browser.Visible = true;
                    inst.Browser.Dock = DockStyle.Fill;
                    gridContainer.Controls.Add(inst.Browser, i % cols, i / cols);
                }

                gridContainer.Visible = true;
                gridContainer.BringToFront();

                foreach (var inst in instances)
                {
                    if (!checkedInstances.Contains(inst) && inst.Browser != null)
                    {
                        inst.Browser.Visible = false;
                    }
                    inst.SidebarButtonPanel.BackColor = Color.FromArgb(45, 45, 45);
                    inst.SidebarButtonPanel.Controls.OfType<Label>().First().BackColor = Color.FromArgb(60, 60, 60);
                }
            }
            else
            {
                gridContainer.Visible = false;
                gridContainer.Controls.Clear();

                if (activeInstance != null)
                {
                    panelDashboard.Visible = false;
                    if (activeInstance.Browser == null)
                    {
                        LoadBrowserInstance(activeInstance);
                    }

                    foreach (var inst in instances)
                    {
                        if (inst == activeInstance)
                        {
                            inst.Browser.Dock = DockStyle.Fill;
                            inst.Browser.Visible = true;
                            panelGameContainer.Controls.Add(inst.Browser);
                            inst.Browser.BringToFront();
                            inst.SidebarButtonPanel.BackColor = Color.FromArgb(0, 120, 215);
                            inst.SidebarButtonPanel.Controls.OfType<Label>().First().BackColor = Color.FromArgb(0, 150, 255);
                        }
                        else
                        {
                            if (inst.Browser != null)
                                inst.Browser.Visible = false;

                            inst.SidebarButtonPanel.BackColor = Color.FromArgb(45, 45, 45);
                            inst.SidebarButtonPanel.Controls.OfType<Label>().First().BackColor = Color.FromArgb(60, 60, 60);
                        }
                    }
                    this.Text = "Epicest Dueler";
                }
                else
                {
                    ShowDashboard();
                }
            }
        }

        private void CaptureInstanceThumbnail(ProfileInstance target)
        {
            if (target == null || target.Browser == null || !target.Browser.IsBrowserInitialized)
                return;

            try
            {
                var control = target.Browser;
                if (!control.Visible || control.Width <= 150 || control.Height <= 150) return;

                if (control.IsHandleCreated && !control.IsDisposed)
                {
                    Point screenPoint = control.PointToScreen(Point.Empty);
                    Rectangle screenBounds = Screen.FromControl(control).Bounds;

                    if (!screenBounds.Contains(screenPoint) ||
                        screenPoint.X + control.Width > screenBounds.Right ||
                        screenPoint.Y + control.Height > screenBounds.Bottom)
                    {
                        return;
                    }

                    Bitmap bmp = new Bitmap(control.Width, control.Height);
                    using (Graphics g = Graphics.FromImage(bmp))
                    {
                        g.CopyFromScreen(screenPoint.X, screenPoint.Y, 0, 0, control.Size);
                    }

                    Action updateAction = () =>
                    {
                        target.Thumbnail?.Dispose();
                        target.Thumbnail = bmp;
                        target.SidebarPictureBox.Image = bmp;
                    };

                    if (this.InvokeRequired)
                    {
                        this.BeginInvoke(updateAction);
                    }
                    else
                    {
                        updateAction();
                    }
                }
            }
            catch { }
        }

        private void ThumbnailUpdateTimer_Tick(object sender, EventArgs e)
        {
            bool timeAdded = false;

            if (isGridMode)
            {
                var runningGridInstances = instances.Where(i => i.GridCheckBox.Checked && i.Browser != null).ToList();
                foreach (var inst in runningGridInstances)
                {
                    inst.PlayTimeSeconds += 2;
                    timeAdded = true;
                    CaptureInstanceThumbnail(inst);
                }
            }
            else if (activeInstance != null && activeInstance.Browser != null)
            {
                activeInstance.PlayTimeSeconds += 2;
                timeAdded = true;
                CaptureInstanceThumbnail(activeInstance);
            }

            if (timeAdded)
            {
                totalPlayTimeSeconds += 2;

                string todayStr = DateTime.Now.ToString("yyyy-MM-dd");
                if (lastPlayedDate != todayStr)
                {
                    lastPlayedDate = todayStr;
                    todayPlayTimeSeconds = 0;
                }
                todayPlayTimeSeconds += 2;

                SaveStats();
                SaveProfilesList();

                if (panelDashboard.Visible)
                {
                    UpdateDashboardProfileStats();
                }
            }
        }

        private void LoadStats()
        {
            string statsPath = Path.Combine(appDataPath, "stats.txt");
            if (File.Exists(statsPath))
            {
                try
                {
                    string[] lines = File.ReadAllLines(statsPath);
                    if (lines.Length >= 3)
                    {
                        totalPlayTimeSeconds = int.Parse(lines[0]);
                        lastPlayedDate = lines[1];
                        todayPlayTimeSeconds = int.Parse(lines[2]);

                        if (lastPlayedDate != DateTime.Now.ToString("yyyy-MM-dd"))
                        {
                            todayPlayTimeSeconds = 0;
                            lastPlayedDate = DateTime.Now.ToString("yyyy-MM-dd");
                        }
                    }
                }
                catch { }
            }
            else
            {
                lastPlayedDate = DateTime.Now.ToString("yyyy-MM-dd");
            }
        }

        private void SaveStats()
        {
            try
            {
                string statsPath = Path.Combine(appDataPath, "stats.txt");
                string[] lines = new string[]
                {
                    totalPlayTimeSeconds.ToString(),
                    lastPlayedDate,
                    todayPlayTimeSeconds.ToString()
                };
                File.WriteAllLines(statsPath, lines);
            }
            catch { }
        }

        private void RefreshItem_Click(object sender, EventArgs e)
        {
            if (contextMenuTarget != null && contextMenuTarget.Browser != null)
            {
                contextMenuTarget.Browser.Reload();
            }
        }

        private void CloseSessionItem_Click(Object sender, EventArgs e)
        {
            if (contextMenuTarget != null)
            {
                CloseProfileSession(contextMenuTarget);
            }
        }

        private void CloseProfileSession(ProfileInstance target)
        {
            if (target == null) return;

            if (target.Browser != null)
            {
                try
                {
                    if (panelGameContainer.Controls.Contains(target.Browser))
                    {
                        panelGameContainer.Controls.Remove(target.Browser);
                    }
                    target.Browser.Dispose();
                }
                catch { }

                target.Browser = null;
            }

            if (target.Thumbnail != null)
            {
                target.Thumbnail.Dispose();
                target.Thumbnail = null;
            }
            target.SidebarPictureBox.Image = CreatePlaceholderImage(target.Name);

            if (target.GridCheckBox != null)
            {
                target.GridCheckBox.Checked = false;
            }

            if (activeInstance == target)
            {
                activeInstance = null;
            }

            SyncLayout();
            if (panelDashboard.Visible)
            {
                UpdateDashboardProfileStats();
            }
        }

        private void RenameItem_Click(object sender, EventArgs e)
        {
            if (contextMenuTarget == null) return;

            string oldName = contextMenuTarget.Name;
            string newName = ShowRenameDialog(oldName);

            if (string.IsNullOrEmpty(newName) || newName == oldName) return;

            contextMenuTarget.Name = newName;
            contextMenuTarget.SidebarButtonPanel.Controls.OfType<Label>().First().Text = newName.ToUpper();
            contextMenuTarget.SidebarPictureBox.Image = CreatePlaceholderImage(newName);

            SaveProfilesList();

            if (activeInstance == contextMenuTarget)
            {
                this.Text = "Epicest Dueler";
            }
        }

        private string ShowRenameDialog(string currentName)
        {
            using (Form prompt = new Form())
            {
                prompt.Width = 300;
                prompt.Height = 150;
                prompt.Text = "Rename Profile";
                prompt.BackColor = Color.FromArgb(25, 25, 25);
                prompt.ForeColor = Color.White;
                prompt.FormBorderStyle = FormBorderStyle.FixedDialog;
                prompt.StartPosition = FormStartPosition.CenterParent;
                prompt.MaximizeBox = false;
                prompt.MinimizeBox = false;
                EnableNativeDarkMode(prompt.Handle);

                Label textLabel = new Label { Left = 15, Top = 15, Text = "Enter new profile name:", Width = 250, Height = 20, ForeColor = Color.Cyan, Font = new Font("Arial", 9, FontStyle.Bold) };
                TextBox textBox = new TextBox { Left = 15, Top = 40, Width = 250, Text = currentName, BackColor = Color.FromArgb(40, 40, 40), ForeColor = Color.White, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Arial", 10) };
                Button confirmation = new Button { Text = "RENAME", Left = 165, Width = 100, Top = 75, Height = 30, DialogResult = DialogResult.OK, BackColor = Color.FromArgb(0, 120, 215), ForeColor = Color.White, FlatStyle = FlatStyle.Flat };
                confirmation.FlatAppearance.BorderSize = 0;

                prompt.Controls.Add(textBox);
                prompt.Controls.Add(confirmation);
                prompt.Controls.Add(textLabel);
                prompt.AcceptButton = confirmation;

                return prompt.ShowDialog() == DialogResult.OK ? textBox.Text.Trim() : null;
            }
        }

        private void RemoveItem_Click(object sender, EventArgs e)
        {
            if (contextMenuTarget == null) return;

            string selected = contextMenuTarget.Name;
            string targetFolder = contextMenuTarget.FolderName;

            DialogResult confirm = MessageBox.Show(
                $"Are you sure you want to permanently delete the profile '{selected}'?\nThis will clear its saved logins and cookies.",
                "Confirm Deletion",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (confirm == DialogResult.Yes)
            {
                try
                {
                    HidePreview();

                    var targetBrowser = contextMenuTarget.Browser;
                    var targetPanel = contextMenuTarget.SidebarButtonPanel;

                    if (contextMenuTarget == activeInstance)
                    {
                        var nextInstance = instances.FirstOrDefault(i => i != contextMenuTarget);
                        if (nextInstance != null)
                        {
                            SwitchToInstance(nextInstance);
                        }
                        else
                        {
                            activeInstance = null;
                            ShowDashboard();
                        }
                    }

                    if (targetBrowser != null)
                    {
                        if (panelGameContainer.Controls.Contains(targetBrowser))
                            panelGameContainer.Controls.Remove(targetBrowser);

                        targetBrowser.Dispose();
                    }

                    if (panelButtonList.Controls.Contains(targetPanel))
                        panelButtonList.Controls.Remove(targetPanel);

                    targetPanel.Dispose();
                    contextMenuTarget.Thumbnail?.Dispose();

                    instances.Remove(contextMenuTarget);

                    SaveProfilesList();

                    if (isGridMode) SyncLayout();

                    string pathToDelete = Path.Combine(appDataPath, targetFolder);
                    Task.Run(() =>
                    {
                        try
                        {
                            System.Threading.Thread.Sleep(1000);
                            if (Directory.Exists(pathToDelete))
                            {
                                Directory.Delete(pathToDelete, true);
                            }
                        }
                        catch { }
                    });

                    contextMenuTarget = null;
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Could not remove profile: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void HoverTimer_Tick(object sender, EventArgs e)
        {
            Point clientMousePos = panelSidebar.PointToClient(Cursor.Position);
            if (!panelSidebar.ClientRectangle.Contains(clientMousePos))
            {
                HidePreview();
            }
        }

        private void ShowPreview(ProfileInstance target)
        {
            if (isGridMode || target == activeInstance || target == previewTarget || target.Browser == null) return;

            HidePreview();

            previewTarget = target;

            target.Browser.Dock = DockStyle.Fill;
            target.Browser.Visible = true;
            target.Browser.BringToFront();

            hoverTimer.Start();
        }

        private void HidePreview()
        {
            if (isGridMode) return;

            if (previewTarget != null)
            {
                previewTarget.Browser.Visible = false;
                previewTarget = null;
            }

            if (activeInstance != null)
            {
                activeInstance.Browser.Visible = true;
                activeInstance.Browser.BringToFront();
            }
            else
            {
                panelDashboard.Visible = true;
                panelDashboard.BringToFront();
            }

            hoverTimer.Stop();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            hoverTimer?.Dispose();
            thumbnailUpdateTimer?.Dispose();
            Cef.Shutdown();
            base.OnFormClosing(e);
        }
    }
}