namespace AndAppUtil {
    internal static class Program {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            InitTasktrayIcon();
            InitTimer();
            Application.Run();
        }

        static void InitTasktrayIcon() {
            var notifyIcon = new System.Windows.Forms.NotifyIcon();
            notifyIcon.Icon = Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location);
            notifyIcon.Visible = true;

            // コンテキストメニュー
            var contextMenuStrip = new ContextMenuStrip();
            var toolStripMenuItem = new ToolStripMenuItem();
            toolStripMenuItem.Text = "終了";
            toolStripMenuItem.Click += (sender, e) => {
                notifyIcon.Visible = false;
                Application.Exit();
            };
            contextMenuStrip.Items.Add(toolStripMenuItem);
            notifyIcon.ContextMenuStrip = contextMenuStrip;
        }

        static void InitTimer() {
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 2000;
            timer.Tick += (sender, e) => {
                (new WindowChecker()).Check();
            };
            timer.Start();

            (new WindowChecker()).Check();
        }
    }
}