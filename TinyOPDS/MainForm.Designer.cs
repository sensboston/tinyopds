namespace TinyOPDS
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
                if (_watcher != null) _watcher.Dispose();
                if (_upnpController != null) _upnpController.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(MainForm));
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.databaseFileName = new System.Windows.Forms.TextBox();
            this.label21 = new System.Windows.Forms.Label();
            this.useWatcher = new System.Windows.Forms.CheckBox();
            this.duplicates = new System.Windows.Forms.Label();
            this.label16 = new System.Windows.Forms.Label();
            this.label15 = new System.Windows.Forms.Label();
            this.status = new System.Windows.Forms.Label();
            this.label14 = new System.Windows.Forms.Label();
            this.rate = new System.Windows.Forms.Label();
            this.label12 = new System.Windows.Forms.Label();
            this.elapsedTime = new System.Windows.Forms.Label();
            this.label10 = new System.Windows.Forms.Label();
            this.startTime = new System.Windows.Forms.Label();
            this.label6 = new System.Windows.Forms.Label();
            this.booksProcessed = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.invalidBooks = new System.Windows.Forms.Label();
            this.label9 = new System.Windows.Forms.Label();
            this.skippedBooks = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.booksFound = new System.Windows.Forms.Label();
            this.booksInDB = new System.Windows.Forms.Label();
            this.folderButton = new System.Windows.Forms.Button();
            this.label2 = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.scannerButton = new System.Windows.Forms.Button();
            this.libraryPath = new System.Windows.Forms.TextBox();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.useAbsoluteUri = new System.Windows.Forms.CheckBox();
            this.extWebLink = new System.Windows.Forms.LinkLabel();
            this.intWebLink = new System.Windows.Forms.LinkLabel();
            this.label34 = new System.Windows.Forms.Label();
            this.label35 = new System.Windows.Forms.Label();
            this.label33 = new System.Windows.Forms.Label();
            this.interfaceCombo = new System.Windows.Forms.ComboBox();
            this.label29 = new System.Windows.Forms.Label();
            this.statUniqueClients = new System.Windows.Forms.Label();
            this.label26 = new System.Windows.Forms.Label();
            this.statImages = new System.Windows.Forms.Label();
            this.label27 = new System.Windows.Forms.Label();
            this.statBooks = new System.Windows.Forms.Label();
            this.label25 = new System.Windows.Forms.Label();
            this.statRequests = new System.Windows.Forms.Label();
            this.label23 = new System.Windows.Forms.Label();
            this.extLink = new System.Windows.Forms.LinkLabel();
            this.intLink = new System.Windows.Forms.LinkLabel();
            this.label13 = new System.Windows.Forms.Label();
            this.extIPlabel = new System.Windows.Forms.Label();
            this.intIPlabel = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.serverButton = new System.Windows.Forms.Button();
            this.webPrefix = new System.Windows.Forms.TextBox();
            this.openPort = new System.Windows.Forms.CheckBox();
            this.serverPort = new System.Windows.Forms.TextBox();
            this.useUPnP = new System.Windows.Forms.CheckBox();
            this.rootPrefix = new System.Windows.Forms.TextBox();
            this.serverName = new System.Windows.Forms.TextBox();
            this.tabPage6 = new System.Windows.Forms.TabPage();
            this.checkBox2 = new System.Windows.Forms.CheckBox();
            this.newBooksPeriodCombo = new System.Windows.Forms.ComboBox();
            this.label39 = new System.Windows.Forms.Label();
            this.sortOrderCombo = new System.Windows.Forms.ComboBox();
            this.label38 = new System.Windows.Forms.Label();
            this.itemsPerWeb = new System.Windows.Forms.NumericUpDown();
            this.label37 = new System.Windows.Forms.Label();
            this.itemsPerOPDS = new System.Windows.Forms.NumericUpDown();
            this.label36 = new System.Windows.Forms.Label();
            this.tabPage5 = new System.Windows.Forms.TabPage();
            this.statBannedClients = new System.Windows.Forms.Label();
            this.label31 = new System.Windows.Forms.Label();
            this.label24 = new System.Windows.Forms.Label();
            this.statWrongLogins = new System.Windows.Forms.Label();
            this.label30 = new System.Windows.Forms.Label();
            this.statGoodLogins = new System.Windows.Forms.Label();
            this.label28 = new System.Windows.Forms.Label();
            this.dataGridView1 = new System.Windows.Forms.DataGridView();
            this.wrongAttemptsCount = new System.Windows.Forms.NumericUpDown();
            this.banClients = new System.Windows.Forms.CheckBox();
            this.rememberClients = new System.Windows.Forms.CheckBox();
            this.useHTTPAuth = new System.Windows.Forms.CheckBox();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.viewLogFile = new System.Windows.Forms.Button();
            this.label32 = new System.Windows.Forms.Label();
            this.updateCombo = new System.Windows.Forms.ComboBox();
            this.label22 = new System.Windows.Forms.Label();
            this.logVerbosity = new System.Windows.Forms.ComboBox();
            this.converterLinkLabel = new System.Windows.Forms.LinkLabel();
            this.label11 = new System.Windows.Forms.Label();
            this.langCombo = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.convertorFolder = new System.Windows.Forms.Button();
            this.convertorPath = new System.Windows.Forms.TextBox();
            this.saveLog = new System.Windows.Forms.CheckBox();
            this.closeToTray = new System.Windows.Forms.CheckBox();
            this.startMinimized = new System.Windows.Forms.CheckBox();
            this.startWithWindows = new System.Windows.Forms.CheckBox();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.linkLabel6 = new System.Windows.Forms.LinkLabel();
            this.linkLabel5 = new System.Windows.Forms.LinkLabel();
            this.linkLabel4 = new System.Windows.Forms.LinkLabel();
            this.linkLabel3 = new System.Windows.Forms.LinkLabel();
            this.label20 = new System.Windows.Forms.Label();
            this.label19 = new System.Windows.Forms.Label();
            this.linkLabel2 = new System.Windows.Forms.LinkLabel();
            this.label18 = new System.Windows.Forms.Label();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.label17 = new System.Windows.Forms.Label();
            this.appVersion = new System.Windows.Forms.Label();
            this.appName = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.donateButton = new System.Windows.Forms.Button();
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.windowMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.serverMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage6.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.itemsPerWeb)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.itemsPerOPDS)).BeginInit();
            this.tabPage5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.wrongAttemptsCount)).BeginInit();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.contextMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage6);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.ItemSize = new System.Drawing.Size(91, 30);
            this.tabControl1.Location = new System.Drawing.Point(-8, -2);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1283, 825);
            this.tabControl1.TabIndex = 8;
            // 
            // tabPage1
            // 
            this.tabPage1.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage1.Controls.Add(this.databaseFileName);
            this.tabPage1.Controls.Add(this.label21);
            this.tabPage1.Controls.Add(this.useWatcher);
            this.tabPage1.Controls.Add(this.duplicates);
            this.tabPage1.Controls.Add(this.label16);
            this.tabPage1.Controls.Add(this.label15);
            this.tabPage1.Controls.Add(this.status);
            this.tabPage1.Controls.Add(this.label14);
            this.tabPage1.Controls.Add(this.rate);
            this.tabPage1.Controls.Add(this.label12);
            this.tabPage1.Controls.Add(this.elapsedTime);
            this.tabPage1.Controls.Add(this.label10);
            this.tabPage1.Controls.Add(this.startTime);
            this.tabPage1.Controls.Add(this.label6);
            this.tabPage1.Controls.Add(this.booksProcessed);
            this.tabPage1.Controls.Add(this.label5);
            this.tabPage1.Controls.Add(this.invalidBooks);
            this.tabPage1.Controls.Add(this.label9);
            this.tabPage1.Controls.Add(this.skippedBooks);
            this.tabPage1.Controls.Add(this.label7);
            this.tabPage1.Controls.Add(this.booksFound);
            this.tabPage1.Controls.Add(this.booksInDB);
            this.tabPage1.Controls.Add(this.folderButton);
            this.tabPage1.Controls.Add(this.label2);
            this.tabPage1.Controls.Add(this.label1);
            this.tabPage1.Controls.Add(this.scannerButton);
            this.tabPage1.Controls.Add(this.libraryPath);
            this.tabPage1.Location = new System.Drawing.Point(10, 40);
            this.tabPage1.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage1.Size = new System.Drawing.Size(1263, 775);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Scanner";
            // 
            // databaseFileName
            // 
            this.databaseFileName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.databaseFileName.Location = new System.Drawing.Point(333, 157);
            this.databaseFileName.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.databaseFileName.Name = "databaseFileName";
            this.databaseFileName.ReadOnly = true;
            this.databaseFileName.Size = new System.Drawing.Size(889, 38);
            this.databaseFileName.TabIndex = 32;
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(40, 167);
            this.label21.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(274, 32);
            this.label21.TabIndex = 31;
            this.label21.Text = "Database file name: ";
            // 
            // useWatcher
            // 
            this.useWatcher.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.useWatcher.AutoSize = true;
            this.useWatcher.Checked = global::TinyOPDS.Properties.Settings.Default.WatchLibrary;
            this.useWatcher.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "WatchLibrary", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.useWatcher.Location = new System.Drawing.Point(869, 81);
            this.useWatcher.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.useWatcher.Name = "useWatcher";
            this.useWatcher.Size = new System.Drawing.Size(347, 36);
            this.useWatcher.TabIndex = 30;
            this.useWatcher.Text = "Monitor library changes";
            this.useWatcher.UseVisualStyleBackColor = true;
            this.useWatcher.CheckedChanged += new System.EventHandler(this.useWatcher_CheckedChanged);
            // 
            // duplicates
            // 
            this.duplicates.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.duplicates.AutoSize = true;
            this.duplicates.Location = new System.Drawing.Point(325, 544);
            this.duplicates.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.duplicates.MinimumSize = new System.Drawing.Size(133, 0);
            this.duplicates.Name = "duplicates";
            this.duplicates.Size = new System.Drawing.Size(133, 32);
            this.duplicates.TabIndex = 29;
            this.duplicates.Text = "0";
            // 
            // label16
            // 
            this.label16.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(40, 544);
            this.label16.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(156, 32);
            this.label16.TabIndex = 28;
            this.label16.Text = "Duplicates:";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(37, 38);
            this.label15.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(274, 32);
            this.label15.TabIndex = 27;
            this.label15.Text = "Path to books folder:";
            // 
            // status
            // 
            this.status.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.status.AutoSize = true;
            this.status.Location = new System.Drawing.Point(960, 544);
            this.status.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.status.MinimumSize = new System.Drawing.Size(133, 0);
            this.status.Name = "status";
            this.status.Size = new System.Drawing.Size(149, 32);
            this.status.TabIndex = 26;
            this.status.Text = "STOPPED";
            // 
            // label14
            // 
            this.label14.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(675, 544);
            this.label14.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(103, 32);
            this.label14.TabIndex = 25;
            this.label14.Text = "Status:";
            // 
            // rate
            // 
            this.rate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.rate.AutoSize = true;
            this.rate.Location = new System.Drawing.Point(960, 482);
            this.rate.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.rate.MinimumSize = new System.Drawing.Size(133, 0);
            this.rate.Name = "rate";
            this.rate.Size = new System.Drawing.Size(167, 32);
            this.rate.TabIndex = 24;
            this.rate.Text = "0 books/min";
            // 
            // label12
            // 
            this.label12.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(675, 482);
            this.label12.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(82, 32);
            this.label12.TabIndex = 23;
            this.label12.Text = "Rate:";
            // 
            // elapsedTime
            // 
            this.elapsedTime.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.elapsedTime.AutoSize = true;
            this.elapsedTime.Location = new System.Drawing.Point(960, 420);
            this.elapsedTime.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.elapsedTime.MinimumSize = new System.Drawing.Size(133, 0);
            this.elapsedTime.Name = "elapsedTime";
            this.elapsedTime.Size = new System.Drawing.Size(133, 32);
            this.elapsedTime.TabIndex = 22;
            this.elapsedTime.Text = "00:00:00";
            // 
            // label10
            // 
            this.label10.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(675, 420);
            this.label10.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(187, 32);
            this.label10.TabIndex = 21;
            this.label10.Text = "Elapsed time:";
            // 
            // startTime
            // 
            this.startTime.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.startTime.AutoSize = true;
            this.startTime.Location = new System.Drawing.Point(960, 358);
            this.startTime.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.startTime.MinimumSize = new System.Drawing.Size(133, 0);
            this.startTime.Name = "startTime";
            this.startTime.Size = new System.Drawing.Size(133, 32);
            this.startTime.TabIndex = 20;
            this.startTime.Text = "00:00:00";
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(675, 358);
            this.label6.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(143, 32);
            this.label6.TabIndex = 19;
            this.label6.Text = "Start time:";
            // 
            // booksProcessed
            // 
            this.booksProcessed.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.booksProcessed.AutoSize = true;
            this.booksProcessed.Location = new System.Drawing.Point(328, 606);
            this.booksProcessed.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.booksProcessed.MinimumSize = new System.Drawing.Size(133, 0);
            this.booksProcessed.Name = "booksProcessed";
            this.booksProcessed.Size = new System.Drawing.Size(133, 32);
            this.booksProcessed.TabIndex = 18;
            this.booksProcessed.Text = "0";
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(40, 606);
            this.label5.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(239, 32);
            this.label5.TabIndex = 17;
            this.label5.Text = "Books processed:";
            // 
            // invalidBooks
            // 
            this.invalidBooks.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.invalidBooks.AutoSize = true;
            this.invalidBooks.Location = new System.Drawing.Point(328, 420);
            this.invalidBooks.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.invalidBooks.MinimumSize = new System.Drawing.Size(133, 0);
            this.invalidBooks.Name = "invalidBooks";
            this.invalidBooks.Size = new System.Drawing.Size(133, 32);
            this.invalidBooks.TabIndex = 16;
            this.invalidBooks.Text = "0";
            // 
            // label9
            // 
            this.label9.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(40, 420);
            this.label9.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(188, 32);
            this.label9.TabIndex = 15;
            this.label9.Text = "Invalid books:";
            // 
            // skippedBooks
            // 
            this.skippedBooks.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.skippedBooks.AutoSize = true;
            this.skippedBooks.Location = new System.Drawing.Point(328, 482);
            this.skippedBooks.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.skippedBooks.MinimumSize = new System.Drawing.Size(133, 0);
            this.skippedBooks.Name = "skippedBooks";
            this.skippedBooks.Size = new System.Drawing.Size(133, 32);
            this.skippedBooks.TabIndex = 14;
            this.skippedBooks.Text = "0";
            // 
            // label7
            // 
            this.label7.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(40, 482);
            this.label7.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(209, 32);
            this.label7.TabIndex = 13;
            this.label7.Text = "Skipped books:";
            // 
            // booksFound
            // 
            this.booksFound.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.booksFound.AutoSize = true;
            this.booksFound.Location = new System.Drawing.Point(328, 358);
            this.booksFound.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.booksFound.MinimumSize = new System.Drawing.Size(133, 0);
            this.booksFound.Name = "booksFound";
            this.booksFound.Size = new System.Drawing.Size(201, 32);
            this.booksFound.TabIndex = 12;
            this.booksFound.Text = "fb2: 0   epub: 0";
            // 
            // booksInDB
            // 
            this.booksInDB.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.booksInDB.AutoSize = true;
            this.booksInDB.Location = new System.Drawing.Point(328, 265);
            this.booksInDB.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.booksInDB.MinimumSize = new System.Drawing.Size(133, 0);
            this.booksInDB.Name = "booksInDB";
            this.booksInDB.Size = new System.Drawing.Size(315, 32);
            this.booksInDB.TabIndex = 11;
            this.booksInDB.Text = "0         fb2:  0       epub: 0";
            // 
            // folderButton
            // 
            this.folderButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.folderButton.Image = global::TinyOPDS.Properties.Resources.folder;
            this.folderButton.Location = new System.Drawing.Point(749, 72);
            this.folderButton.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.folderButton.Name = "folderButton";
            this.folderButton.Size = new System.Drawing.Size(77, 55);
            this.folderButton.TabIndex = 10;
            this.folderButton.UseVisualStyleBackColor = true;
            this.folderButton.Click += new System.EventHandler(this.folderButton_Click);
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(40, 265);
            this.label2.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(263, 32);
            this.label2.TabIndex = 9;
            this.label2.Text = "Books in database: ";
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(40, 358);
            this.label1.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(180, 32);
            this.label1.TabIndex = 8;
            this.label1.Text = "Books found:";
            // 
            // scannerButton
            // 
            this.scannerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.scannerButton.Location = new System.Drawing.Point(680, 620);
            this.scannerButton.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.scannerButton.Name = "scannerButton";
            this.scannerButton.Size = new System.Drawing.Size(560, 95);
            this.scannerButton.TabIndex = 7;
            this.scannerButton.Text = "Start scanning";
            this.scannerButton.UseVisualStyleBackColor = true;
            this.scannerButton.Click += new System.EventHandler(this.scannerButton_Click);
            // 
            // libraryPath
            // 
            this.libraryPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.libraryPath.Location = new System.Drawing.Point(45, 76);
            this.libraryPath.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.libraryPath.Name = "libraryPath";
            this.libraryPath.Size = new System.Drawing.Size(684, 38);
            this.libraryPath.TabIndex = 6;
            this.libraryPath.TextChanged += new System.EventHandler(this.libraryPath_TextChanged);
            this.libraryPath.Validated += new System.EventHandler(this.libraryPath_Validated);
            // 
            // tabPage2
            // 
            this.tabPage2.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage2.Controls.Add(this.useAbsoluteUri);
            this.tabPage2.Controls.Add(this.extWebLink);
            this.tabPage2.Controls.Add(this.intWebLink);
            this.tabPage2.Controls.Add(this.label34);
            this.tabPage2.Controls.Add(this.label35);
            this.tabPage2.Controls.Add(this.label33);
            this.tabPage2.Controls.Add(this.interfaceCombo);
            this.tabPage2.Controls.Add(this.label29);
            this.tabPage2.Controls.Add(this.statUniqueClients);
            this.tabPage2.Controls.Add(this.label26);
            this.tabPage2.Controls.Add(this.statImages);
            this.tabPage2.Controls.Add(this.label27);
            this.tabPage2.Controls.Add(this.statBooks);
            this.tabPage2.Controls.Add(this.label25);
            this.tabPage2.Controls.Add(this.statRequests);
            this.tabPage2.Controls.Add(this.label23);
            this.tabPage2.Controls.Add(this.extLink);
            this.tabPage2.Controls.Add(this.intLink);
            this.tabPage2.Controls.Add(this.label13);
            this.tabPage2.Controls.Add(this.extIPlabel);
            this.tabPage2.Controls.Add(this.intIPlabel);
            this.tabPage2.Controls.Add(this.label4);
            this.tabPage2.Controls.Add(this.label3);
            this.tabPage2.Controls.Add(this.serverButton);
            this.tabPage2.Controls.Add(this.webPrefix);
            this.tabPage2.Controls.Add(this.openPort);
            this.tabPage2.Controls.Add(this.serverPort);
            this.tabPage2.Controls.Add(this.useUPnP);
            this.tabPage2.Controls.Add(this.rootPrefix);
            this.tabPage2.Controls.Add(this.serverName);
            this.tabPage2.Location = new System.Drawing.Point(10, 40);
            this.tabPage2.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage2.Size = new System.Drawing.Size(1263, 775);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Server";
            // 
            // useAbsoluteUri
            // 
            this.useAbsoluteUri.AutoSize = true;
            this.useAbsoluteUri.Checked = global::TinyOPDS.Properties.Settings.Default.UseAbsoluteUri;
            this.useAbsoluteUri.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "UseAbsoluteUri", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.useAbsoluteUri.Location = new System.Drawing.Point(907, 169);
            this.useAbsoluteUri.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.useAbsoluteUri.Name = "useAbsoluteUri";
            this.useAbsoluteUri.Size = new System.Drawing.Size(229, 36);
            this.useAbsoluteUri.TabIndex = 54;
            this.useAbsoluteUri.Text = "Absolute links";
            this.useAbsoluteUri.UseVisualStyleBackColor = true;
            // 
            // extWebLink
            // 
            this.extWebLink.Location = new System.Drawing.Point(672, 448);
            this.extWebLink.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.extWebLink.Name = "extWebLink";
            this.extWebLink.Size = new System.Drawing.Size(541, 31);
            this.extWebLink.TabIndex = 53;
            this.extWebLink.TabStop = true;
            this.extWebLink.Text = "- - - - - -";
            this.extWebLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            // 
            // intWebLink
            // 
            this.intWebLink.Location = new System.Drawing.Point(672, 353);
            this.intWebLink.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.intWebLink.Name = "intWebLink";
            this.intWebLink.Size = new System.Drawing.Size(541, 31);
            this.intWebLink.TabIndex = 52;
            this.intWebLink.TabStop = true;
            this.intWebLink.Text = "- - - - - -";
            this.intWebLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            // 
            // label34
            // 
            this.label34.AutoSize = true;
            this.label34.Location = new System.Drawing.Point(672, 413);
            this.label34.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label34.Name = "label34";
            this.label34.Size = new System.Drawing.Size(249, 32);
            this.label34.TabIndex = 51;
            this.label34.Text = "External web URL:";
            // 
            // label35
            // 
            this.label35.AutoSize = true;
            this.label35.Location = new System.Drawing.Point(672, 317);
            this.label35.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label35.Name = "label35";
            this.label35.Size = new System.Drawing.Size(213, 32);
            this.label35.TabIndex = 50;
            this.label35.Text = "Local web URL:";
            // 
            // label33
            // 
            this.label33.AutoSize = true;
            this.label33.Location = new System.Drawing.Point(672, 253);
            this.label33.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label33.Name = "label33";
            this.label33.Size = new System.Drawing.Size(136, 32);
            this.label33.TabIndex = 48;
            this.label33.Text = "Web root:";
            // 
            // interfaceCombo
            // 
            this.interfaceCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.interfaceCombo.FormattingEnabled = true;
            this.interfaceCombo.Location = new System.Drawing.Point(680, 83);
            this.interfaceCombo.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.interfaceCombo.Name = "interfaceCombo";
            this.interfaceCombo.Size = new System.Drawing.Size(316, 39);
            this.interfaceCombo.TabIndex = 47;
            this.interfaceCombo.SelectedIndexChanged += new System.EventHandler(this.interfaceCombo_SelectedIndexChanged);
            // 
            // label29
            // 
            this.label29.AutoSize = true;
            this.label29.Location = new System.Drawing.Point(672, 38);
            this.label29.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label29.Name = "label29";
            this.label29.Size = new System.Drawing.Size(242, 32);
            this.label29.TabIndex = 46;
            this.label29.Text = "Network interface:";
            // 
            // statUniqueClients
            // 
            this.statUniqueClients.AutoSize = true;
            this.statUniqueClients.Location = new System.Drawing.Point(395, 610);
            this.statUniqueClients.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.statUniqueClients.Name = "statUniqueClients";
            this.statUniqueClients.Size = new System.Drawing.Size(30, 32);
            this.statUniqueClients.TabIndex = 45;
            this.statUniqueClients.Text = "0";
            // 
            // label26
            // 
            this.label26.AutoSize = true;
            this.label26.Location = new System.Drawing.Point(51, 610);
            this.label26.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(202, 32);
            this.label26.TabIndex = 44;
            this.label26.Text = "Unique clients:";
            // 
            // statImages
            // 
            this.statImages.AutoSize = true;
            this.statImages.Location = new System.Drawing.Point(1187, 544);
            this.statImages.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.statImages.Name = "statImages";
            this.statImages.Size = new System.Drawing.Size(30, 32);
            this.statImages.TabIndex = 43;
            this.statImages.Text = "0";
            // 
            // label27
            // 
            this.label27.AutoSize = true;
            this.label27.Location = new System.Drawing.Point(939, 544);
            this.label27.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label27.Name = "label27";
            this.label27.Size = new System.Drawing.Size(175, 32);
            this.label27.TabIndex = 42;
            this.label27.Text = "Images sent:";
            // 
            // statBooks
            // 
            this.statBooks.AutoSize = true;
            this.statBooks.Location = new System.Drawing.Point(771, 544);
            this.statBooks.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.statBooks.Name = "statBooks";
            this.statBooks.Size = new System.Drawing.Size(30, 32);
            this.statBooks.TabIndex = 41;
            this.statBooks.Text = "0";
            // 
            // label25
            // 
            this.label25.AutoSize = true;
            this.label25.Location = new System.Drawing.Point(533, 544);
            this.label25.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(162, 32);
            this.label25.TabIndex = 40;
            this.label25.Text = "Books sent:";
            // 
            // statRequests
            // 
            this.statRequests.AutoSize = true;
            this.statRequests.Location = new System.Drawing.Point(395, 544);
            this.statRequests.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.statRequests.Name = "statRequests";
            this.statRequests.Size = new System.Drawing.Size(30, 32);
            this.statRequests.TabIndex = 39;
            this.statRequests.Text = "0";
            // 
            // label23
            // 
            this.label23.AutoSize = true;
            this.label23.Location = new System.Drawing.Point(51, 544);
            this.label23.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label23.Name = "label23";
            this.label23.Size = new System.Drawing.Size(202, 32);
            this.label23.TabIndex = 38;
            this.label23.Text = "Total requests:";
            // 
            // extLink
            // 
            this.extLink.Location = new System.Drawing.Point(51, 448);
            this.extLink.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.extLink.Name = "extLink";
            this.extLink.Size = new System.Drawing.Size(571, 31);
            this.extLink.TabIndex = 37;
            this.extLink.TabStop = true;
            this.extLink.Text = "- - - - - -";
            this.extLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            // 
            // intLink
            // 
            this.intLink.Location = new System.Drawing.Point(51, 353);
            this.intLink.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.intLink.Name = "intLink";
            this.intLink.Size = new System.Drawing.Size(571, 31);
            this.intLink.TabIndex = 36;
            this.intLink.TabStop = true;
            this.intLink.Text = "- - - - - -";
            this.intLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(53, 253);
            this.label13.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(158, 32);
            this.label13.TabIndex = 18;
            this.label13.Text = "OPDS root:";
            // 
            // extIPlabel
            // 
            this.extIPlabel.AutoSize = true;
            this.extIPlabel.Location = new System.Drawing.Point(51, 413);
            this.extIPlabel.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.extIPlabel.Name = "extIPlabel";
            this.extIPlabel.Size = new System.Drawing.Size(277, 32);
            this.extIPlabel.TabIndex = 14;
            this.extIPlabel.Text = "External OPDS URL:";
            // 
            // intIPlabel
            // 
            this.intIPlabel.AutoSize = true;
            this.intIPlabel.Location = new System.Drawing.Point(51, 317);
            this.intIPlabel.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.intIPlabel.Name = "intIPlabel";
            this.intIPlabel.Size = new System.Drawing.Size(241, 32);
            this.intIPlabel.TabIndex = 13;
            this.intIPlabel.Text = "Local OPDS URL:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(51, 38);
            this.label4.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(183, 32);
            this.label4.TabIndex = 11;
            this.label4.Text = "Server name:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(1080, 38);
            this.label3.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(74, 32);
            this.label3.TabIndex = 9;
            this.label3.Text = "Port:";
            // 
            // serverButton
            // 
            this.serverButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.serverButton.Location = new System.Drawing.Point(680, 620);
            this.serverButton.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.serverButton.Name = "serverButton";
            this.serverButton.Size = new System.Drawing.Size(560, 95);
            this.serverButton.TabIndex = 8;
            this.serverButton.Text = "Start server";
            this.serverButton.Click += new System.EventHandler(this.serverButton_Click);
            // 
            // webPrefix
            // 
            this.webPrefix.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::TinyOPDS.Properties.Settings.Default, "HttpPrefix", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.webPrefix.Location = new System.Drawing.Point(907, 246);
            this.webPrefix.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.webPrefix.Name = "webPrefix";
            this.webPrefix.Size = new System.Drawing.Size(308, 38);
            this.webPrefix.TabIndex = 49;
            this.webPrefix.Text = global::TinyOPDS.Properties.Settings.Default.HttpPrefix;
            this.webPrefix.TextChanged += new System.EventHandler(this.rootPrefix_TextChanged);
            // 
            // openPort
            // 
            this.openPort.AutoSize = true;
            this.openPort.Checked = global::TinyOPDS.Properties.Settings.Default.OpenNATPort;
            this.openPort.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "OpenNATPort", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.openPort.Enabled = false;
            this.openPort.Location = new System.Drawing.Point(427, 169);
            this.openPort.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.openPort.Name = "openPort";
            this.openPort.Size = new System.Drawing.Size(331, 36);
            this.openPort.TabIndex = 15;
            this.openPort.Text = "Forward port on router";
            this.openPort.UseVisualStyleBackColor = true;
            this.openPort.CheckedChanged += new System.EventHandler(this.openPort_CheckedChanged);
            // 
            // serverPort
            // 
            this.serverPort.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::TinyOPDS.Properties.Settings.Default, "ServerPort", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.serverPort.Location = new System.Drawing.Point(1088, 86);
            this.serverPort.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.serverPort.Name = "serverPort";
            this.serverPort.Size = new System.Drawing.Size(127, 38);
            this.serverPort.TabIndex = 10;
            this.serverPort.Text = global::TinyOPDS.Properties.Settings.Default.ServerPort;
            this.serverPort.Validated += new System.EventHandler(this.serverPort_Validated);
            // 
            // useUPnP
            // 
            this.useUPnP.AutoSize = true;
            this.useUPnP.Checked = global::TinyOPDS.Properties.Settings.Default.UseUPnP;
            this.useUPnP.CheckState = System.Windows.Forms.CheckState.Checked;
            this.useUPnP.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "UseUPnP", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.useUPnP.Location = new System.Drawing.Point(59, 169);
            this.useUPnP.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.useUPnP.Name = "useUPnP";
            this.useUPnP.Size = new System.Drawing.Size(183, 36);
            this.useUPnP.TabIndex = 35;
            this.useUPnP.Text = "Use UPnP";
            this.useUPnP.UseVisualStyleBackColor = true;
            this.useUPnP.CheckStateChanged += new System.EventHandler(this.useUPnP_CheckStateChanged);
            // 
            // rootPrefix
            // 
            this.rootPrefix.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::TinyOPDS.Properties.Settings.Default, "RootPrefix", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.rootPrefix.Location = new System.Drawing.Point(307, 246);
            this.rootPrefix.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.rootPrefix.Name = "rootPrefix";
            this.rootPrefix.Size = new System.Drawing.Size(308, 38);
            this.rootPrefix.TabIndex = 19;
            this.rootPrefix.Text = global::TinyOPDS.Properties.Settings.Default.RootPrefix;
            this.rootPrefix.TextChanged += new System.EventHandler(this.rootPrefix_TextChanged);
            // 
            // serverName
            // 
            this.serverName.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::TinyOPDS.Properties.Settings.Default, "ServerName", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.serverName.Location = new System.Drawing.Point(59, 83);
            this.serverName.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.serverName.Name = "serverName";
            this.serverName.Size = new System.Drawing.Size(556, 38);
            this.serverName.TabIndex = 12;
            this.serverName.Text = global::TinyOPDS.Properties.Settings.Default.ServerName;
            // 
            // tabPage6
            // 
            this.tabPage6.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage6.Controls.Add(this.checkBox2);
            this.tabPage6.Controls.Add(this.newBooksPeriodCombo);
            this.tabPage6.Controls.Add(this.label39);
            this.tabPage6.Controls.Add(this.sortOrderCombo);
            this.tabPage6.Controls.Add(this.label38);
            this.tabPage6.Controls.Add(this.itemsPerWeb);
            this.tabPage6.Controls.Add(this.label37);
            this.tabPage6.Controls.Add(this.itemsPerOPDS);
            this.tabPage6.Controls.Add(this.label36);
            this.tabPage6.Location = new System.Drawing.Point(10, 40);
            this.tabPage6.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage6.Name = "tabPage6";
            this.tabPage6.Size = new System.Drawing.Size(1263, 775);
            this.tabPage6.TabIndex = 5;
            this.tabPage6.Text = "OPDS catalog";
            // 
            // checkBox2
            // 
            this.checkBox2.AutoSize = true;
            this.checkBox2.Checked = global::TinyOPDS.Properties.Settings.Default.UseAuthorsAliases;
            this.checkBox2.CheckState = System.Windows.Forms.CheckState.Checked;
            this.checkBox2.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "UseAuthorsAliases", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.checkBox2.Location = new System.Drawing.Point(67, 386);
            this.checkBox2.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.checkBox2.Name = "checkBox2";
            this.checkBox2.Size = new System.Drawing.Size(301, 36);
            this.checkBox2.TabIndex = 9;
            this.checkBox2.Text = "Use authors aliases";
            this.checkBox2.UseVisualStyleBackColor = true;
            this.checkBox2.CheckedChanged += new System.EventHandler(this.checkBox_CheckedChanged);
            // 
            // newBooksPeriodCombo
            // 
            this.newBooksPeriodCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.newBooksPeriodCombo.FormattingEnabled = true;
            this.newBooksPeriodCombo.Items.AddRange(new object[] {
            "one week",
            "two weeks",
            "three weeks",
            "month",
            "month and half",
            "two month",
            "three month"});
            this.newBooksPeriodCombo.Location = new System.Drawing.Point(677, 274);
            this.newBooksPeriodCombo.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.newBooksPeriodCombo.Name = "newBooksPeriodCombo";
            this.newBooksPeriodCombo.Size = new System.Drawing.Size(463, 39);
            this.newBooksPeriodCombo.TabIndex = 7;
            this.newBooksPeriodCombo.SelectedIndexChanged += new System.EventHandler(this.newBooksPeriodCombo_SelectedIndexChanged);
            // 
            // label39
            // 
            this.label39.AutoSize = true;
            this.label39.Location = new System.Drawing.Point(669, 227);
            this.label39.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label39.Name = "label39";
            this.label39.Size = new System.Drawing.Size(349, 32);
            this.label39.TabIndex = 6;
            this.label39.Text = "\"New books\" check period:";
            // 
            // sortOrderCombo
            // 
            this.sortOrderCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.sortOrderCombo.FormattingEnabled = true;
            this.sortOrderCombo.Items.AddRange(new object[] {
            "Latin first",
            "Cyrillic first"});
            this.sortOrderCombo.Location = new System.Drawing.Point(67, 274);
            this.sortOrderCombo.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.sortOrderCombo.Name = "sortOrderCombo";
            this.sortOrderCombo.Size = new System.Drawing.Size(463, 39);
            this.sortOrderCombo.TabIndex = 5;
            this.sortOrderCombo.SelectedIndexChanged += new System.EventHandler(this.sortOrderCombo_SelectedIndexChanged);
            // 
            // label38
            // 
            this.label38.AutoSize = true;
            this.label38.Location = new System.Drawing.Point(59, 227);
            this.label38.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label38.Name = "label38";
            this.label38.Size = new System.Drawing.Size(217, 32);
            this.label38.TabIndex = 4;
            this.label38.Text = "Items sort order:";
            // 
            // itemsPerWeb
            // 
            this.itemsPerWeb.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::TinyOPDS.Properties.Settings.Default, "ItemsPerWebPage", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.itemsPerWeb.Location = new System.Drawing.Point(677, 114);
            this.itemsPerWeb.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.itemsPerWeb.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.itemsPerWeb.Minimum = new decimal(new int[] {
            30,
            0,
            0,
            0});
            this.itemsPerWeb.Name = "itemsPerWeb";
            this.itemsPerWeb.Size = new System.Drawing.Size(147, 38);
            this.itemsPerWeb.TabIndex = 3;
            this.itemsPerWeb.Value = global::TinyOPDS.Properties.Settings.Default.ItemsPerWebPage;
            // 
            // label37
            // 
            this.label37.AutoSize = true;
            this.label37.Location = new System.Drawing.Point(669, 60);
            this.label37.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label37.Name = "label37";
            this.label37.Size = new System.Drawing.Size(268, 32);
            this.label37.TabIndex = 2;
            this.label37.Text = "Items per web page:";
            // 
            // itemsPerOPDS
            // 
            this.itemsPerOPDS.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::TinyOPDS.Properties.Settings.Default, "ItemsPerOPDSPage", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.itemsPerOPDS.Location = new System.Drawing.Point(67, 114);
            this.itemsPerOPDS.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.itemsPerOPDS.Maximum = new decimal(new int[] {
            1000,
            0,
            0,
            0});
            this.itemsPerOPDS.Minimum = new decimal(new int[] {
            30,
            0,
            0,
            0});
            this.itemsPerOPDS.Name = "itemsPerOPDS";
            this.itemsPerOPDS.Size = new System.Drawing.Size(147, 38);
            this.itemsPerOPDS.TabIndex = 1;
            this.itemsPerOPDS.Value = global::TinyOPDS.Properties.Settings.Default.ItemsPerOPDSPage;
            // 
            // label36
            // 
            this.label36.AutoSize = true;
            this.label36.Location = new System.Drawing.Point(59, 62);
            this.label36.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label36.Name = "label36";
            this.label36.Size = new System.Drawing.Size(296, 32);
            this.label36.TabIndex = 0;
            this.label36.Text = "Items per OPDS page:";
            // 
            // tabPage5
            // 
            this.tabPage5.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage5.Controls.Add(this.statBannedClients);
            this.tabPage5.Controls.Add(this.label31);
            this.tabPage5.Controls.Add(this.label24);
            this.tabPage5.Controls.Add(this.statWrongLogins);
            this.tabPage5.Controls.Add(this.label30);
            this.tabPage5.Controls.Add(this.statGoodLogins);
            this.tabPage5.Controls.Add(this.label28);
            this.tabPage5.Controls.Add(this.dataGridView1);
            this.tabPage5.Controls.Add(this.wrongAttemptsCount);
            this.tabPage5.Controls.Add(this.banClients);
            this.tabPage5.Controls.Add(this.rememberClients);
            this.tabPage5.Controls.Add(this.useHTTPAuth);
            this.tabPage5.Location = new System.Drawing.Point(10, 40);
            this.tabPage5.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage5.Name = "tabPage5";
            this.tabPage5.Padding = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage5.Size = new System.Drawing.Size(1263, 775);
            this.tabPage5.TabIndex = 4;
            this.tabPage5.Text = "Authentication";
            // 
            // statBannedClients
            // 
            this.statBannedClients.AutoSize = true;
            this.statBannedClients.Location = new System.Drawing.Point(1144, 606);
            this.statBannedClients.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.statBannedClients.Name = "statBannedClients";
            this.statBannedClients.Size = new System.Drawing.Size(30, 32);
            this.statBannedClients.TabIndex = 48;
            this.statBannedClients.Text = "0";
            this.statBannedClients.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label31
            // 
            this.label31.AutoSize = true;
            this.label31.Location = new System.Drawing.Point(891, 606);
            this.label31.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label31.Name = "label31";
            this.label31.Size = new System.Drawing.Size(210, 32);
            this.label31.TabIndex = 47;
            this.label31.Text = "Banned clients:";
            // 
            // label24
            // 
            this.label24.AutoSize = true;
            this.label24.Location = new System.Drawing.Point(877, 122);
            this.label24.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label24.Name = "label24";
            this.label24.Size = new System.Drawing.Size(200, 32);
            this.label24.TabIndex = 46;
            this.label24.Text = "failed attempts";
            // 
            // statWrongLogins
            // 
            this.statWrongLogins.AutoSize = true;
            this.statWrongLogins.Location = new System.Drawing.Point(744, 606);
            this.statWrongLogins.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.statWrongLogins.Name = "statWrongLogins";
            this.statWrongLogins.Size = new System.Drawing.Size(30, 32);
            this.statWrongLogins.TabIndex = 43;
            this.statWrongLogins.Text = "0";
            this.statWrongLogins.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label30
            // 
            this.label30.AutoSize = true;
            this.label30.Location = new System.Drawing.Point(493, 606);
            this.label30.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label30.Name = "label30";
            this.label30.Size = new System.Drawing.Size(184, 32);
            this.label30.TabIndex = 42;
            this.label30.Text = "Failed logins:";
            // 
            // statGoodLogins
            // 
            this.statGoodLogins.AutoSize = true;
            this.statGoodLogins.Location = new System.Drawing.Point(341, 606);
            this.statGoodLogins.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.statGoodLogins.Name = "statGoodLogins";
            this.statGoodLogins.Size = new System.Drawing.Size(30, 32);
            this.statGoodLogins.TabIndex = 41;
            this.statGoodLogins.Text = "0";
            this.statGoodLogins.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label28
            // 
            this.label28.AutoSize = true;
            this.label28.Location = new System.Drawing.Point(61, 606);
            this.label28.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label28.Name = "label28";
            this.label28.Size = new System.Drawing.Size(243, 32);
            this.label28.TabIndex = 40;
            this.label28.Text = "Successful logins:";
            // 
            // dataGridView1
            // 
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.Location = new System.Drawing.Point(69, 198);
            this.dataGridView1.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.RowHeadersWidth = 102;
            this.dataGridView1.Size = new System.Drawing.Size(1136, 358);
            this.dataGridView1.TabIndex = 1;
            this.dataGridView1.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.dataGridView1_CellFormatting);
            // 
            // wrongAttemptsCount
            // 
            this.wrongAttemptsCount.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::TinyOPDS.Properties.Settings.Default, "WrongAttemptsCount", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.wrongAttemptsCount.Location = new System.Drawing.Point(755, 110);
            this.wrongAttemptsCount.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.wrongAttemptsCount.Minimum = new decimal(new int[] {
            3,
            0,
            0,
            0});
            this.wrongAttemptsCount.Name = "wrongAttemptsCount";
            this.wrongAttemptsCount.Size = new System.Drawing.Size(107, 38);
            this.wrongAttemptsCount.TabIndex = 45;
            this.wrongAttemptsCount.Value = global::TinyOPDS.Properties.Settings.Default.WrongAttemptsCount;
            // 
            // banClients
            // 
            this.banClients.AutoSize = true;
            this.banClients.Checked = global::TinyOPDS.Properties.Settings.Default.BanClients;
            this.banClients.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "BanClients", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.banClients.Location = new System.Drawing.Point(755, 55);
            this.banClients.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.banClients.Name = "banClients";
            this.banClients.Size = new System.Drawing.Size(256, 36);
            this.banClients.TabIndex = 44;
            this.banClients.Text = "Ban clients after";
            this.banClients.UseVisualStyleBackColor = true;
            this.banClients.CheckedChanged += new System.EventHandler(this.banClients_CheckedChanged);
            // 
            // rememberClients
            // 
            this.rememberClients.AutoSize = true;
            this.rememberClients.Checked = global::TinyOPDS.Properties.Settings.Default.RememberClients;
            this.rememberClients.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "RememberClients", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.rememberClients.Location = new System.Drawing.Point(69, 122);
            this.rememberClients.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.rememberClients.Name = "rememberClients";
            this.rememberClients.Size = new System.Drawing.Size(421, 36);
            this.rememberClients.TabIndex = 2;
            this.rememberClients.Text = "Remember authorized clients";
            this.rememberClients.UseVisualStyleBackColor = true;
            // 
            // useHTTPAuth
            // 
            this.useHTTPAuth.AutoSize = true;
            this.useHTTPAuth.Checked = global::TinyOPDS.Properties.Settings.Default.UseHTTPAuth;
            this.useHTTPAuth.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "UseHTTPAuth", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.useHTTPAuth.Location = new System.Drawing.Point(69, 55);
            this.useHTTPAuth.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.useHTTPAuth.Name = "useHTTPAuth";
            this.useHTTPAuth.Size = new System.Drawing.Size(443, 36);
            this.useHTTPAuth.TabIndex = 0;
            this.useHTTPAuth.Text = "Use HTTP basic authentication";
            this.useHTTPAuth.UseVisualStyleBackColor = true;
            this.useHTTPAuth.CheckedChanged += new System.EventHandler(this.useHTTPAuth_CheckedChanged);
            // 
            // tabPage3
            // 
            this.tabPage3.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage3.Controls.Add(this.viewLogFile);
            this.tabPage3.Controls.Add(this.label32);
            this.tabPage3.Controls.Add(this.updateCombo);
            this.tabPage3.Controls.Add(this.label22);
            this.tabPage3.Controls.Add(this.logVerbosity);
            this.tabPage3.Controls.Add(this.converterLinkLabel);
            this.tabPage3.Controls.Add(this.label11);
            this.tabPage3.Controls.Add(this.langCombo);
            this.tabPage3.Controls.Add(this.label8);
            this.tabPage3.Controls.Add(this.convertorFolder);
            this.tabPage3.Controls.Add(this.convertorPath);
            this.tabPage3.Controls.Add(this.saveLog);
            this.tabPage3.Controls.Add(this.closeToTray);
            this.tabPage3.Controls.Add(this.startMinimized);
            this.tabPage3.Controls.Add(this.startWithWindows);
            this.tabPage3.Location = new System.Drawing.Point(10, 40);
            this.tabPage3.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage3.Size = new System.Drawing.Size(1263, 775);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Miscellaneous";
            // 
            // viewLogFile
            // 
            this.viewLogFile.Location = new System.Drawing.Point(797, 520);
            this.viewLogFile.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.viewLogFile.Name = "viewLogFile";
            this.viewLogFile.Size = new System.Drawing.Size(347, 55);
            this.viewLogFile.TabIndex = 39;
            this.viewLogFile.Text = "View log file";
            this.viewLogFile.UseVisualStyleBackColor = true;
            this.viewLogFile.Click += new System.EventHandler(this.viewLogFile_Click);
            // 
            // label32
            // 
            this.label32.AutoSize = true;
            this.label32.Location = new System.Drawing.Point(795, 341);
            this.label32.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label32.Name = "label32";
            this.label32.Size = new System.Drawing.Size(237, 32);
            this.label32.TabIndex = 38;
            this.label32.Text = "Check for update:";
            // 
            // updateCombo
            // 
            this.updateCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.updateCombo.FormattingEnabled = true;
            this.updateCombo.Items.AddRange(new object[] {
            "Never",
            "Once a week",
            "Once a month"});
            this.updateCombo.Location = new System.Drawing.Point(797, 396);
            this.updateCombo.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.updateCombo.Name = "updateCombo";
            this.updateCombo.Size = new System.Drawing.Size(332, 39);
            this.updateCombo.TabIndex = 37;
            this.updateCombo.SelectedIndexChanged += new System.EventHandler(this.updateCombo_SelectedIndexChanged);
            // 
            // label22
            // 
            this.label22.AutoSize = true;
            this.label22.Location = new System.Drawing.Point(35, 465);
            this.label22.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(250, 32);
            this.label22.TabIndex = 36;
            this.label22.Text = "Log verbosity level";
            // 
            // logVerbosity
            // 
            this.logVerbosity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.logVerbosity.FormattingEnabled = true;
            this.logVerbosity.Items.AddRange(new object[] {
            "Info, warnings and errors",
            "Warnings and errors",
            "Errors only"});
            this.logVerbosity.Location = new System.Drawing.Point(37, 520);
            this.logVerbosity.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.logVerbosity.Name = "logVerbosity";
            this.logVerbosity.Size = new System.Drawing.Size(649, 39);
            this.logVerbosity.TabIndex = 35;
            this.logVerbosity.SelectedIndexChanged += new System.EventHandler(this.logVerbosity_SelectedIndexChanged);
            // 
            // converterLinkLabel
            // 
            this.converterLinkLabel.AutoSize = true;
            this.converterLinkLabel.Location = new System.Drawing.Point(32, 131);
            this.converterLinkLabel.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.converterLinkLabel.Name = "converterLinkLabel";
            this.converterLinkLabel.Size = new System.Drawing.Size(706, 32);
            this.converterLinkLabel.TabIndex = 34;
            this.converterLinkLabel.TabStop = true;
            this.converterLinkLabel.Text = "Click here to download latest version of ePub converter";
            this.converterLinkLabel.Visible = false;
            this.converterLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel3_LinkClicked);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(789, 196);
            this.label11.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(339, 32);
            this.label11.TabIndex = 32;
            this.label11.Text = "GUI and OPDS language:";
            // 
            // langCombo
            // 
            this.langCombo.DisplayMember = "Value";
            this.langCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.langCombo.FormattingEnabled = true;
            this.langCombo.Location = new System.Drawing.Point(797, 255);
            this.langCombo.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.langCombo.Name = "langCombo";
            this.langCombo.Size = new System.Drawing.Size(332, 39);
            this.langCombo.TabIndex = 31;
            this.langCombo.ValueMember = "Key";
            this.langCombo.SelectedValueChanged += new System.EventHandler(this.langCombo_SelectedValueChanged);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(21, 29);
            this.label8.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(358, 32);
            this.label8.TabIndex = 30;
            this.label8.Text = "Path to the ePub converter:";
            // 
            // convertorFolder
            // 
            this.convertorFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.convertorFolder.Image = global::TinyOPDS.Properties.Resources.folder;
            this.convertorFolder.Location = new System.Drawing.Point(1149, 62);
            this.convertorFolder.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.convertorFolder.Name = "convertorFolder";
            this.convertorFolder.Size = new System.Drawing.Size(77, 55);
            this.convertorFolder.TabIndex = 29;
            this.convertorFolder.UseVisualStyleBackColor = true;
            this.convertorFolder.Click += new System.EventHandler(this.folderButton_Click);
            // 
            // convertorPath
            // 
            this.convertorPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.convertorPath.Location = new System.Drawing.Point(29, 67);
            this.convertorPath.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.convertorPath.Name = "convertorPath";
            this.convertorPath.Size = new System.Drawing.Size(1095, 38);
            this.convertorPath.TabIndex = 28;
            this.convertorPath.Validated += new System.EventHandler(this.convertorPath_Validated);
            // 
            // saveLog
            // 
            this.saveLog.AutoSize = true;
            this.saveLog.Checked = global::TinyOPDS.Properties.Settings.Default.SaveLogToDisk;
            this.saveLog.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "SaveLogToDisk", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.saveLog.Location = new System.Drawing.Point(37, 396);
            this.saveLog.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.saveLog.Name = "saveLog";
            this.saveLog.Size = new System.Drawing.Size(239, 36);
            this.saveLog.TabIndex = 33;
            this.saveLog.Text = "Save log to file";
            this.saveLog.UseVisualStyleBackColor = true;
            this.saveLog.CheckedChanged += new System.EventHandler(this.saveLog_CheckedChanged);
            // 
            // closeToTray
            // 
            this.closeToTray.AutoSize = true;
            this.closeToTray.Checked = global::TinyOPDS.Properties.Settings.Default.CloseToTray;
            this.closeToTray.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "CloseToTray", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.closeToTray.Location = new System.Drawing.Point(37, 329);
            this.closeToTray.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.closeToTray.Name = "closeToTray";
            this.closeToTray.Size = new System.Drawing.Size(362, 36);
            this.closeToTray.TabIndex = 2;
            this.closeToTray.Text = "Close or minimize to tray";
            this.closeToTray.UseVisualStyleBackColor = true;
            this.closeToTray.CheckedChanged += new System.EventHandler(this.closeToTray_CheckedChanged);
            // 
            // startMinimized
            // 
            this.startMinimized.AutoSize = true;
            this.startMinimized.Checked = global::TinyOPDS.Properties.Settings.Default.StartMinimized;
            this.startMinimized.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "StartMinimized", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.startMinimized.Location = new System.Drawing.Point(37, 262);
            this.startMinimized.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.startMinimized.Name = "startMinimized";
            this.startMinimized.Size = new System.Drawing.Size(248, 36);
            this.startMinimized.TabIndex = 1;
            this.startMinimized.Text = "Start minimized";
            this.startMinimized.UseVisualStyleBackColor = true;
            // 
            // startWithWindows
            // 
            this.startWithWindows.AutoSize = true;
            this.startWithWindows.Checked = global::TinyOPDS.Properties.Settings.Default.StartWithWindows;
            this.startWithWindows.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "StartWithWindows", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.startWithWindows.Location = new System.Drawing.Point(37, 196);
            this.startWithWindows.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.startWithWindows.Name = "startWithWindows";
            this.startWithWindows.Size = new System.Drawing.Size(292, 36);
            this.startWithWindows.TabIndex = 0;
            this.startWithWindows.Text = "Start with Windows";
            this.startWithWindows.UseVisualStyleBackColor = true;
            this.startWithWindows.CheckedChanged += new System.EventHandler(this.startWithWindows_CheckedChanged);
            // 
            // tabPage4
            // 
            this.tabPage4.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage4.Controls.Add(this.linkLabel6);
            this.tabPage4.Controls.Add(this.linkLabel5);
            this.tabPage4.Controls.Add(this.linkLabel4);
            this.tabPage4.Controls.Add(this.linkLabel3);
            this.tabPage4.Controls.Add(this.label20);
            this.tabPage4.Controls.Add(this.label19);
            this.tabPage4.Controls.Add(this.linkLabel2);
            this.tabPage4.Controls.Add(this.label18);
            this.tabPage4.Controls.Add(this.linkLabel1);
            this.tabPage4.Controls.Add(this.label17);
            this.tabPage4.Controls.Add(this.appVersion);
            this.tabPage4.Controls.Add(this.appName);
            this.tabPage4.Controls.Add(this.pictureBox1);
            this.tabPage4.Controls.Add(this.donateButton);
            this.tabPage4.Location = new System.Drawing.Point(10, 40);
            this.tabPage4.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage4.Size = new System.Drawing.Size(1263, 775);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "About program";
            // 
            // linkLabel6
            // 
            this.linkLabel6.AutoSize = true;
            this.linkLabel6.Location = new System.Drawing.Point(520, 615);
            this.linkLabel6.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.linkLabel6.Name = "linkLabel6";
            this.linkLabel6.Size = new System.Drawing.Size(444, 32);
            this.linkLabel6.TabIndex = 13;
            this.linkLabel6.TabStop = true;
            this.linkLabel6.Text = "Gremlin2, author of Fb2Fix project";
            this.linkLabel6.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel3_LinkClicked);
            // 
            // linkLabel5
            // 
            this.linkLabel5.AutoSize = true;
            this.linkLabel5.Location = new System.Drawing.Point(520, 506);
            this.linkLabel5.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.linkLabel5.Name = "linkLabel5";
            this.linkLabel5.Size = new System.Drawing.Size(259, 32);
            this.linkLabel5.TabIndex = 12;
            this.linkLabel5.TabStop = true;
            this.linkLabel5.Text = "ePubReader library";
            this.linkLabel5.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel3_LinkClicked);
            // 
            // linkLabel4
            // 
            this.linkLabel4.AutoSize = true;
            this.linkLabel4.Location = new System.Drawing.Point(520, 560);
            this.linkLabel4.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.linkLabel4.Name = "linkLabel4";
            this.linkLabel4.Size = new System.Drawing.Size(227, 32);
            this.linkLabel4.TabIndex = 11;
            this.linkLabel4.TabStop = true;
            this.linkLabel4.Text = "DotNetZip library";
            this.linkLabel4.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel3_LinkClicked);
            // 
            // linkLabel3
            // 
            this.linkLabel3.AutoSize = true;
            this.linkLabel3.Location = new System.Drawing.Point(515, 453);
            this.linkLabel3.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.linkLabel3.Name = "linkLabel3";
            this.linkLabel3.Size = new System.Drawing.Size(713, 32);
            this.linkLabel3.TabIndex = 10;
            this.linkLabel3.TabStop = true;
            this.linkLabel3.Text = "Lord KiRon, author of fb2librarynet library and converter";
            this.linkLabel3.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel3_LinkClicked);
            // 
            // label20
            // 
            this.label20.Location = new System.Drawing.Point(24, 453);
            this.label20.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(429, 31);
            this.label20.TabIndex = 9;
            this.label20.Text = "Special thanks to:";
            this.label20.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label19.Location = new System.Drawing.Point(512, 212);
            this.label19.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(656, 46);
            this.label19.TabIndex = 8;
            this.label19.Text = "Copyright © 2013-2025, SeNSSoFT";
            // 
            // linkLabel2
            // 
            this.linkLabel2.AutoSize = true;
            this.linkLabel2.Location = new System.Drawing.Point(515, 398);
            this.linkLabel2.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.linkLabel2.Name = "linkLabel2";
            this.linkLabel2.Size = new System.Drawing.Size(294, 32);
            this.linkLabel2.TabIndex = 7;
            this.linkLabel2.TabStop = true;
            this.linkLabel2.Text = "https://mit-license.org/";
            this.linkLabel2.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            // 
            // label18
            // 
            this.label18.Location = new System.Drawing.Point(29, 398);
            this.label18.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(424, 31);
            this.label18.TabIndex = 6;
            this.label18.Text = "Project license:";
            this.label18.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.Location = new System.Drawing.Point(515, 343);
            this.linkLabel1.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(509, 32);
            this.linkLabel1.TabIndex = 5;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "https://github.com/sensboston/tinyopds";
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            // 
            // label17
            // 
            this.label17.Location = new System.Drawing.Point(21, 343);
            this.label17.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(432, 31);
            this.label17.TabIndex = 4;
            this.label17.Text = "Project home page:";
            this.label17.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // appVersion
            // 
            this.appVersion.AutoSize = true;
            this.appVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.appVersion.Location = new System.Drawing.Point(699, 138);
            this.appVersion.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.appVersion.Name = "appVersion";
            this.appVersion.Size = new System.Drawing.Size(214, 46);
            this.appVersion.TabIndex = 3;
            this.appVersion.Text = "version 3.0";
            // 
            // appName
            // 
            this.appName.AutoSize = true;
            this.appName.Font = new System.Drawing.Font("Microsoft Sans Serif", 20F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.appName.Location = new System.Drawing.Point(507, 33);
            this.appName.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.appName.Name = "appName";
            this.appName.Size = new System.Drawing.Size(552, 76);
            this.appName.TabIndex = 2;
            this.appName.Text = "TinyOPDS server";
            // 
            // pictureBox1
            // 
            this.pictureBox1.ErrorImage = null;
            this.pictureBox1.Image = global::TinyOPDS.Properties.Resources.TinyOPDS;
            this.pictureBox1.Location = new System.Drawing.Point(21, 21);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(275, 246);
            this.pictureBox1.TabIndex = 1;
            this.pictureBox1.TabStop = false;
            // 
            // donateButton
            // 
            this.donateButton.Image = global::TinyOPDS.Properties.Resources.donate;
            this.donateButton.Location = new System.Drawing.Point(24, 572);
            this.donateButton.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.donateButton.Name = "donateButton";
            this.donateButton.Size = new System.Drawing.Size(419, 134);
            this.donateButton.TabIndex = 0;
            this.donateButton.UseVisualStyleBackColor = true;
            this.donateButton.Click += new System.EventHandler(this.donateButton_Click);
            // 
            // contextMenuStrip
            // 
            this.contextMenuStrip.ImageScalingSize = new System.Drawing.Size(40, 40);
            this.contextMenuStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.windowMenuItem,
            this.serverMenuItem,
            this.toolStripMenuItem1,
            this.exitMenuItem});
            this.contextMenuStrip.Name = "contextMenuStrip1";
            this.contextMenuStrip.Size = new System.Drawing.Size(271, 154);
            // 
            // windowMenuItem
            // 
            this.windowMenuItem.Name = "windowMenuItem";
            this.windowMenuItem.Size = new System.Drawing.Size(270, 48);
            this.windowMenuItem.Text = "Hide window";
            this.windowMenuItem.Click += new System.EventHandler(this.windowMenuItem_Click);
            // 
            // serverMenuItem
            // 
            this.serverMenuItem.Name = "serverMenuItem";
            this.serverMenuItem.Size = new System.Drawing.Size(270, 48);
            this.serverMenuItem.Text = "Stop server";
            this.serverMenuItem.Click += new System.EventHandler(this.serverButton_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(267, 6);
            // 
            // exitMenuItem
            // 
            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.Size = new System.Drawing.Size(270, 48);
            this.exitMenuItem.Text = "Exit";
            this.exitMenuItem.Click += new System.EventHandler(this.exitMenuItem_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(16F, 31F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1264, 813);
            this.Controls.Add(this.tabControl1);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "TinyOPDS server";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.tabPage6.ResumeLayout(false);
            this.tabPage6.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.itemsPerWeb)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.itemsPerOPDS)).EndInit();
            this.tabPage5.ResumeLayout(false);
            this.tabPage5.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.wrongAttemptsCount)).EndInit();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.tabPage4.ResumeLayout(false);
            this.tabPage4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.contextMenuStrip.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.Button folderButton;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Button scannerButton;
        private System.Windows.Forms.TextBox libraryPath;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.TextBox serverPort;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Button serverButton;
        private System.Windows.Forms.TabPage tabPage3;
        private System.Windows.Forms.Label invalidBooks;
        private System.Windows.Forms.Label label9;
        private System.Windows.Forms.Label skippedBooks;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.Label booksFound;
        private System.Windows.Forms.Label booksInDB;
        private System.Windows.Forms.Label label15;
        private System.Windows.Forms.Label status;
        private System.Windows.Forms.Label label14;
        private System.Windows.Forms.Label rate;
        private System.Windows.Forms.Label label12;
        private System.Windows.Forms.Label elapsedTime;
        private System.Windows.Forms.Label label10;
        private System.Windows.Forms.Label startTime;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.Label booksProcessed;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox serverName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.CheckBox closeToTray;
        private System.Windows.Forms.CheckBox startMinimized;
        private System.Windows.Forms.CheckBox startWithWindows;
        private System.Windows.Forms.Label label8;
        private System.Windows.Forms.Button convertorFolder;
        private System.Windows.Forms.TextBox convertorPath;
        private System.Windows.Forms.Label label11;
        private System.Windows.Forms.ComboBox langCombo;
        private System.Windows.Forms.CheckBox openPort;
        private System.Windows.Forms.Label extIPlabel;
        private System.Windows.Forms.Label intIPlabel;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem windowMenuItem;
        private System.Windows.Forms.ToolStripMenuItem serverMenuItem;
        private System.Windows.Forms.ToolStripSeparator toolStripMenuItem1;
        private System.Windows.Forms.ToolStripMenuItem exitMenuItem;
        private System.Windows.Forms.CheckBox saveLog;
        private System.Windows.Forms.Label duplicates;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox rootPrefix;
        private System.Windows.Forms.Label label13;
        private System.Windows.Forms.CheckBox useWatcher;
        private System.Windows.Forms.TabPage tabPage4;
        private System.Windows.Forms.CheckBox useUPnP;
        private System.Windows.Forms.LinkLabel converterLinkLabel;
        private System.Windows.Forms.Button donateButton;
        private System.Windows.Forms.PictureBox pictureBox1;
        private System.Windows.Forms.Label label19;
        private System.Windows.Forms.LinkLabel linkLabel2;
        private System.Windows.Forms.Label label18;
        private System.Windows.Forms.LinkLabel linkLabel1;
        private System.Windows.Forms.Label label17;
        private System.Windows.Forms.Label appVersion;
        private System.Windows.Forms.Label appName;
        private System.Windows.Forms.Label label20;
        private System.Windows.Forms.LinkLabel intLink;
        private System.Windows.Forms.LinkLabel extLink;
        private System.Windows.Forms.LinkLabel linkLabel5;
        private System.Windows.Forms.LinkLabel linkLabel4;
        private System.Windows.Forms.LinkLabel linkLabel3;
        private System.Windows.Forms.TextBox databaseFileName;
        private System.Windows.Forms.Label label21;
        private System.Windows.Forms.TabPage tabPage5;
        private System.Windows.Forms.CheckBox useHTTPAuth;
        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.CheckBox rememberClients;
        private System.Windows.Forms.Label label22;
        private System.Windows.Forms.ComboBox logVerbosity;
        private System.Windows.Forms.Label statImages;
        private System.Windows.Forms.Label label27;
        private System.Windows.Forms.Label statBooks;
        private System.Windows.Forms.Label label25;
        private System.Windows.Forms.Label statRequests;
        private System.Windows.Forms.Label label23;
        private System.Windows.Forms.Label statUniqueClients;
        private System.Windows.Forms.Label label26;
        private System.Windows.Forms.Label statGoodLogins;
        private System.Windows.Forms.Label label28;
        private System.Windows.Forms.Label statWrongLogins;
        private System.Windows.Forms.Label label30;
        private System.Windows.Forms.Label label24;
        private System.Windows.Forms.NumericUpDown wrongAttemptsCount;
        private System.Windows.Forms.CheckBox banClients;
        private System.Windows.Forms.Label statBannedClients;
        private System.Windows.Forms.Label label31;
        private System.Windows.Forms.Label label32;
        private System.Windows.Forms.ComboBox updateCombo;
        private System.Windows.Forms.ComboBox interfaceCombo;
        private System.Windows.Forms.Label label29;
        private System.Windows.Forms.Label label33;
        private System.Windows.Forms.TextBox webPrefix;
        private System.Windows.Forms.LinkLabel extWebLink;
        private System.Windows.Forms.LinkLabel intWebLink;
        private System.Windows.Forms.Label label34;
        private System.Windows.Forms.Label label35;
        private System.Windows.Forms.CheckBox useAbsoluteUri;
        private System.Windows.Forms.Button viewLogFile;
        private System.Windows.Forms.TabPage tabPage6;
        private System.Windows.Forms.ComboBox newBooksPeriodCombo;
        private System.Windows.Forms.Label label39;
        private System.Windows.Forms.ComboBox sortOrderCombo;
        private System.Windows.Forms.Label label38;
        private System.Windows.Forms.NumericUpDown itemsPerWeb;
        private System.Windows.Forms.Label label37;
        private System.Windows.Forms.NumericUpDown itemsPerOPDS;
        private System.Windows.Forms.Label label36;
        private System.Windows.Forms.LinkLabel linkLabel6;
        private System.Windows.Forms.CheckBox checkBox2;
    }
}

