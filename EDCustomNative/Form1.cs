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
            public string Name { get; set; }
            public ChromiumWebBrowser Browser { get; set; }
            public Bitmap Thumbnail { get; set; }
            public Panel SidebarButtonPanel { get; set; }
            public PictureBox SidebarPictureBox { get; set; }
        }

        private List<ProfileInstance> instances = new List<ProfileInstance>();
        private ProfileInstance activeInstance;
        private ProfileInstance previewTarget;
        private ProfileInstance contextMenuTarget;

        private Panel panelSidebar;
        private FlowLayoutPanel panelButtonList;
        private Panel panelGameContainer;
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
        private int totalPlayTimeSeconds = 0;
        private int todayPlayTimeSeconds = 0;
        private string lastPlayedDate = "";

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

            var renameItem = new ToolStripMenuItem("Rename Profile");
            renameItem.Click += RenameItem_Click;
            renameItem.ForeColor = Color.White;

            var removeItem = new ToolStripMenuItem("Remove Profile");
            removeItem.Click += RemoveItem_Click;

            removeItem.ForeColor = Color.FromArgb(220, 53, 69);
            removeItem.Image = GetTrashCanIcon();

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
                Width = 160,
                BackColor = Color.FromArgb(30, 30, 30)
            };
            this.Controls.Add(panelSidebar);

            panelButtonList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                AutoScroll = true,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(5)
            };
            panelSidebar.Controls.Add(panelButtonList);

            panelGameContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };
            this.Controls.Add(panelGameContainer);
            panelGameContainer.BringToFront();

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
                Location = new Point(50, 150),
                Width = 500,
                Height = 300,
                BackColor = Color.FromArgb(30, 30, 30),
                Padding = new Padding(20)
            };
            panelDashboard.Controls.Add(panelStats);

            Label lblStatsHeader = new Label
            {
                Text = "STATISTICS & TELEMETRY",
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

            panelDashboard.Visible = true;
            panelDashboard.BringToFront();
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
            List<string> profilesToLoad = new List<string>();

            if (File.Exists(profilesListPath))
            {
                profilesToLoad = File.ReadAllLines(profilesListPath)
                                     .Where(line => !string.IsNullOrWhiteSpace(line))
                                     .ToList();
            }
            else
            {
                string[] directories = Directory.GetDirectories(appDataPath);
                foreach (var dir in directories)
                {
                    profilesToLoad.Add(Path.GetFileName(dir));
                }
                File.WriteAllLines(profilesListPath, profilesToLoad);
            }

            try
            {
                string[] physicalDirs = Directory.GetDirectories(appDataPath);
                foreach (var dir in physicalDirs)
                {
                    string folderName = Path.GetFileName(dir);
                    if (!profilesToLoad.Contains(folderName))
                    {
                        Directory.Delete(dir, true);
                    }
                }
            }
            catch { }

            foreach (string profileName in profilesToLoad)
            {
                Directory.CreateDirectory(Path.Combine(appDataPath, profileName));
                CreateInstancePlaceholder(profileName);
            }

            BuildAddAccountButton();
            panelButtonList.Controls.Add(panelAddAccount);

            ShowDashboard();
        }

        private void SaveProfilesList()
        {
            try
            {
                var names = instances.Select(i => i.Name).ToList();
                File.WriteAllLines(profilesListPath, names);
            }
            catch { }
        }

        private void CreateAndLaunchNewProfile()
        {
            int count = instances.Count + 1;
            string newName = $"Account {count}";
            string newPath = Path.Combine(appDataPath, newName);

            while (Directory.Exists(newPath))
            {
                count++;
                newName = $"Account {count}";
                newPath = Path.Combine(appDataPath, newName);
            }

            Directory.CreateDirectory(newPath);

            panelButtonList.Controls.Remove(panelAddAccount);

            var newInstance = CreateInstancePlaceholder(newName);

            panelButtonList.Controls.Add(panelAddAccount);

            SaveProfilesList();

            SwitchToInstance(newInstance);
        }

        private void LoadBrowserInstance(ProfileInstance target)
        {
            if (target.Browser != null) return;

            var requestContextSettings = new RequestContextSettings
            {
                CachePath = Path.Combine(appDataPath, target.Name),
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

        private ProfileInstance CreateInstancePlaceholder(string profileName)
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
                Text = profileName.ToUpper(),
                ForeColor = Color.White,
                Font = new Font("Arial", 8, FontStyle.Bold),
                Dock = DockStyle.Top,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                BackColor = Color.FromArgb(60, 60, 60)
            };
            btnPanel.Controls.Add(btnLabel);

            PictureBox picThumb = new PictureBox
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black,
                SizeMode = PictureBoxSizeMode.Zoom,
                Image = CreatePlaceholderImage(profileName)
            };
            btnPanel.Controls.Add(picThumb);
            picThumb.BringToFront();

            ProfileInstance instance = new ProfileInstance
            {
                Name = profileName,
                Browser = null,
                SidebarButtonPanel = btnPanel,
                SidebarPictureBox = picThumb
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

            CaptureInstanceThumbnail(activeInstance);

            if (target.Browser == null)
            {
                LoadBrowserInstance(target);
            }

            panelDashboard.Visible = false;

            foreach (var inst in instances)
            {
                if (inst == target)
                {
                    inst.Browser.Dock = DockStyle.Fill;
                    inst.Browser.Visible = true;
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

            activeInstance = target;
            this.Text = $"Epicest Dueler";
        }

        private void CaptureInstanceThumbnail(ProfileInstance target)
        {
            if (target == null || target.Browser == null || !target.Browser.IsBrowserInitialized)
                return;

            try
            {
                var control = target.Browser;
                if (control.Width <= 0 || control.Height <= 0 || !control.Visible) return;

                Bitmap bmp = new Bitmap(control.Width, control.Height);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    if (control.IsHandleCreated && !control.IsDisposed)
                    {
                        Point screenPoint = control.PointToScreen(Point.Empty);
                        g.CopyFromScreen(screenPoint.X, screenPoint.Y, 0, 0, control.Size);
                    }
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
            catch { }
        }

        private void ThumbnailUpdateTimer_Tick(object sender, EventArgs e)
        {
            if (activeInstance != null && activeInstance.Browser != null)
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
            }

            if (previewTarget != null) return;
            CaptureInstanceThumbnail(activeInstance);
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

        private void RenameItem_Click(object sender, EventArgs e)
        {
            if (contextMenuTarget == null) return;

            string oldName = contextMenuTarget.Name;
            string newName = ShowRenameDialog(oldName);

            if (string.IsNullOrEmpty(newName) || newName == oldName) return;

            foreach (char c in Path.GetInvalidFileNameChars())
            {
                newName = newName.Replace(c.ToString(), "");
            }

            string oldPath = Path.Combine(appDataPath, oldName);
            string newPath = Path.Combine(appDataPath, newName);

            if (Directory.Exists(newPath))
            {
                MessageBox.Show("A profile with that name already exists.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                HidePreview();

                var targetBrowser = contextMenuTarget.Browser;

                if (targetBrowser != null)
                {
                    if (panelGameContainer.Controls.Contains(targetBrowser))
                        panelGameContainer.Controls.Remove(targetBrowser);

                    targetBrowser.Dispose();
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();

                Directory.Move(oldPath, newPath);

                contextMenuTarget.Browser = null;
                contextMenuTarget.Name = newName;
                contextMenuTarget.SidebarButtonPanel.Controls.OfType<Label>().First().Text = newName.ToUpper();
                contextMenuTarget.SidebarPictureBox.Image = CreatePlaceholderImage(newName);

                SaveProfilesList();

                SwitchToInstance(contextMenuTarget);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Could not rename profile: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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

                    string pathToDelete = Path.Combine(appDataPath, selected);
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
            if (target == activeInstance || target == previewTarget || target.Browser == null) return;

            HidePreview();

            previewTarget = target;

            target.Browser.Dock = DockStyle.Fill;
            target.Browser.Visible = true;
            target.Browser.BringToFront();

            hoverTimer.Start();
        }

        private void HidePreview()
        {
            if (previewTarget != null)
            {
                CaptureInstanceThumbnail(previewTarget);

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