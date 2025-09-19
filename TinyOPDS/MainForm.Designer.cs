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
                if (watcher != null) watcher.Dispose();
                if (upnpController != null) upnpController.Dispose();
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
            this.extWebLink = new System.Windows.Forms.LinkLabel();
            this.intWebLink = new System.Windows.Forms.LinkLabel();
            this.label34 = new System.Windows.Forms.Label();
            this.label35 = new System.Windows.Forms.Label();
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
            this.openPort = new System.Windows.Forms.CheckBox();
            this.serverPort = new System.Windows.Forms.TextBox();
            this.rootPrefix = new System.Windows.Forms.TextBox();
            this.serverName = new System.Windows.Forms.TextBox();
            this.tabPage6 = new System.Windows.Forms.TabPage();
            this.filterByLanguage = new System.Windows.Forms.CheckBox();
            this.clearDownloadsButton = new System.Windows.Forms.Button();
            this.label40 = new System.Windows.Forms.Label();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.radioButton2 = new System.Windows.Forms.RadioButton();
            this.radioButton1 = new System.Windows.Forms.RadioButton();
            this.newBooksPeriodCombo = new System.Windows.Forms.ComboBox();
            this.label39 = new System.Windows.Forms.Label();
            this.sortOrderCombo = new System.Windows.Forms.ComboBox();
            this.label38 = new System.Windows.Forms.Label();
            this.label37 = new System.Windows.Forms.Label();
            this.label36 = new System.Windows.Forms.Label();
            this.itemsPerWeb = new System.Windows.Forms.NumericUpDown();
            this.itemsPerOPDS = new System.Windows.Forms.NumericUpDown();
            this.tabPage7 = new System.Windows.Forms.TabPage();
            this.treeViewOPDS = new System.Windows.Forms.TreeView();
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
            this.oneInstance = new System.Windows.Forms.CheckBox();
            this.viewLogFile = new System.Windows.Forms.Button();
            this.label32 = new System.Windows.Forms.Label();
            this.updateCombo = new System.Windows.Forms.ComboBox();
            this.label22 = new System.Windows.Forms.Label();
            this.logVerbosity = new System.Windows.Forms.ComboBox();
            this.label11 = new System.Windows.Forms.Label();
            this.langCombo = new System.Windows.Forms.ComboBox();
            this.saveLog = new System.Windows.Forms.CheckBox();
            this.closeToTray = new System.Windows.Forms.CheckBox();
            this.startMinimized = new System.Windows.Forms.CheckBox();
            this.startWithWindows = new System.Windows.Forms.CheckBox();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.pictureBox2 = new System.Windows.Forms.PictureBox();
            this.linkLabel6 = new System.Windows.Forms.LinkLabel();
            this.label20 = new System.Windows.Forms.Label();
            this.label19 = new System.Windows.Forms.Label();
            this.linkLabel2 = new System.Windows.Forms.LinkLabel();
            this.label18 = new System.Windows.Forms.Label();
            this.linkLabel1 = new System.Windows.Forms.LinkLabel();
            this.label17 = new System.Windows.Forms.Label();
            this.appVersion = new System.Windows.Forms.Label();
            this.appName = new System.Windows.Forms.Label();
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.contextMenuStrip = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.windowMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.serverMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage6.SuspendLayout();
            this.groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.itemsPerWeb)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.itemsPerOPDS)).BeginInit();
            this.tabPage7.SuspendLayout();
            this.tabPage5.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.wrongAttemptsCount)).BeginInit();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).BeginInit();
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
            this.tabControl1.Controls.Add(this.tabPage7);
            this.tabControl1.Controls.Add(this.tabPage5);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.ItemSize = new System.Drawing.Size(91, 30);
            this.tabControl1.Location = new System.Drawing.Point(-8, -2);
            this.tabControl1.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.Padding = new System.Drawing.Point(16, 3);
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(1287, 856);
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
            this.tabPage1.Size = new System.Drawing.Size(1267, 806);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Scanner";
            // 
            // databaseFileName
            // 
            this.databaseFileName.Location = new System.Drawing.Point(333, 158);
            this.databaseFileName.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.databaseFileName.Name = "databaseFileName";
            this.databaseFileName.ReadOnly = true;
            this.databaseFileName.Size = new System.Drawing.Size(881, 38);
            this.databaseFileName.TabIndex = 32;
            // 
            // label21
            // 
            this.label21.AutoSize = true;
            this.label21.Location = new System.Drawing.Point(40, 161);
            this.label21.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label21.Name = "label21";
            this.label21.Size = new System.Drawing.Size(274, 32);
            this.label21.TabIndex = 31;
            this.label21.Text = "Database file name: ";
            // 
            // useWatcher
            // 
            this.useWatcher.AutoSize = true;
            this.useWatcher.Checked = global::TinyOPDS.Properties.Settings.Default.WatchLibrary;
            this.useWatcher.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "WatchLibrary", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.useWatcher.Location = new System.Drawing.Point(841, 86);
            this.useWatcher.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.useWatcher.Name = "useWatcher";
            this.useWatcher.Size = new System.Drawing.Size(262, 36);
            this.useWatcher.TabIndex = 30;
            this.useWatcher.Text = "Monitor changes";
            this.useWatcher.UseVisualStyleBackColor = true;
            this.useWatcher.CheckedChanged += new System.EventHandler(this.UseWatcher_CheckedChanged);
            // 
            // duplicates
            // 
            this.duplicates.AutoSize = true;
            this.duplicates.Location = new System.Drawing.Point(328, 528);
            this.duplicates.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.duplicates.MinimumSize = new System.Drawing.Size(133, 0);
            this.duplicates.Name = "duplicates";
            this.duplicates.Size = new System.Drawing.Size(133, 32);
            this.duplicates.TabIndex = 29;
            this.duplicates.Text = "0";
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(40, 528);
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
            this.status.AutoSize = true;
            this.status.Location = new System.Drawing.Point(994, 528);
            this.status.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.status.MinimumSize = new System.Drawing.Size(133, 0);
            this.status.Name = "status";
            this.status.Size = new System.Drawing.Size(149, 32);
            this.status.TabIndex = 26;
            this.status.Text = "STOPPED";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(702, 528);
            this.label14.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(103, 32);
            this.label14.TabIndex = 25;
            this.label14.Text = "Status:";
            // 
            // rate
            // 
            this.rate.AutoSize = true;
            this.rate.Location = new System.Drawing.Point(994, 456);
            this.rate.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.rate.MinimumSize = new System.Drawing.Size(133, 0);
            this.rate.Name = "rate";
            this.rate.Size = new System.Drawing.Size(167, 32);
            this.rate.TabIndex = 24;
            this.rate.Text = "0 books/min";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(702, 456);
            this.label12.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(143, 32);
            this.label12.TabIndex = 23;
            this.label12.Text = "Scan rate:";
            // 
            // elapsedTime
            // 
            this.elapsedTime.AutoSize = true;
            this.elapsedTime.Location = new System.Drawing.Point(994, 384);
            this.elapsedTime.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.elapsedTime.MinimumSize = new System.Drawing.Size(133, 0);
            this.elapsedTime.Name = "elapsedTime";
            this.elapsedTime.Size = new System.Drawing.Size(133, 32);
            this.elapsedTime.TabIndex = 22;
            this.elapsedTime.Text = "00:00:00";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(702, 384);
            this.label10.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(187, 32);
            this.label10.TabIndex = 21;
            this.label10.Text = "Elapsed time:";
            // 
            // startTime
            // 
            this.startTime.AutoSize = true;
            this.startTime.Location = new System.Drawing.Point(994, 312);
            this.startTime.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.startTime.MinimumSize = new System.Drawing.Size(133, 0);
            this.startTime.Name = "startTime";
            this.startTime.Size = new System.Drawing.Size(133, 32);
            this.startTime.TabIndex = 20;
            this.startTime.Text = "00:00:00";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(702, 312);
            this.label6.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(143, 32);
            this.label6.TabIndex = 19;
            this.label6.Text = "Start time:";
            // 
            // booksProcessed
            // 
            this.booksProcessed.AutoSize = true;
            this.booksProcessed.Location = new System.Drawing.Point(328, 600);
            this.booksProcessed.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.booksProcessed.MinimumSize = new System.Drawing.Size(133, 0);
            this.booksProcessed.Name = "booksProcessed";
            this.booksProcessed.Size = new System.Drawing.Size(133, 32);
            this.booksProcessed.TabIndex = 18;
            this.booksProcessed.Text = "0";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(40, 600);
            this.label5.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(239, 32);
            this.label5.TabIndex = 17;
            this.label5.Text = "Books processed:";
            // 
            // invalidBooks
            // 
            this.invalidBooks.AutoSize = true;
            this.invalidBooks.Location = new System.Drawing.Point(328, 384);
            this.invalidBooks.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.invalidBooks.MinimumSize = new System.Drawing.Size(133, 0);
            this.invalidBooks.Name = "invalidBooks";
            this.invalidBooks.Size = new System.Drawing.Size(133, 32);
            this.invalidBooks.TabIndex = 16;
            this.invalidBooks.Text = "0";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(40, 384);
            this.label9.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(188, 32);
            this.label9.TabIndex = 15;
            this.label9.Text = "Invalid books:";
            // 
            // skippedBooks
            // 
            this.skippedBooks.AutoSize = true;
            this.skippedBooks.Location = new System.Drawing.Point(328, 456);
            this.skippedBooks.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.skippedBooks.MinimumSize = new System.Drawing.Size(133, 0);
            this.skippedBooks.Name = "skippedBooks";
            this.skippedBooks.Size = new System.Drawing.Size(133, 32);
            this.skippedBooks.TabIndex = 14;
            this.skippedBooks.Text = "0";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(40, 456);
            this.label7.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(209, 32);
            this.label7.TabIndex = 13;
            this.label7.Text = "Skipped books:";
            // 
            // booksFound
            // 
            this.booksFound.AutoSize = true;
            this.booksFound.Location = new System.Drawing.Point(328, 312);
            this.booksFound.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.booksFound.MinimumSize = new System.Drawing.Size(340, 0);
            this.booksFound.Name = "booksFound";
            this.booksFound.Size = new System.Drawing.Size(340, 32);
            this.booksFound.TabIndex = 12;
            this.booksFound.Text = "fb2: 0       epub: 0";
            // 
            // booksInDB
            // 
            this.booksInDB.AutoSize = true;
            this.booksInDB.Location = new System.Drawing.Point(327, 233);
            this.booksInDB.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.booksInDB.MinimumSize = new System.Drawing.Size(500, 0);
            this.booksInDB.Name = "booksInDB";
            this.booksInDB.Size = new System.Drawing.Size(500, 32);
            this.booksInDB.TabIndex = 11;
            this.booksInDB.Text = "0         fb2:  0       epub: 0";
            // 
            // folderButton
            // 
            this.folderButton.Image = ((System.Drawing.Image)(resources.GetObject("folderButton.Image")));
            this.folderButton.Location = new System.Drawing.Point(760, 83);
            this.folderButton.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.folderButton.Name = "folderButton";
            this.folderButton.Size = new System.Drawing.Size(50, 41);
            this.folderButton.TabIndex = 10;
            this.folderButton.UseVisualStyleBackColor = true;
            this.folderButton.Click += new System.EventHandler(this.FolderButton_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(40, 233);
            this.label2.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(263, 32);
            this.label2.TabIndex = 9;
            this.label2.Text = "Books in database: ";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(40, 312);
            this.label1.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(180, 32);
            this.label1.TabIndex = 8;
            this.label1.Text = "Books found:";
            // 
            // scannerButton
            // 
            this.scannerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.scannerButton.Location = new System.Drawing.Point(710, 680);
            this.scannerButton.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.scannerButton.Name = "scannerButton";
            this.scannerButton.Size = new System.Drawing.Size(500, 90);
            this.scannerButton.TabIndex = 7;
            this.scannerButton.Text = "Start scanning";
            this.scannerButton.UseVisualStyleBackColor = true;
            this.scannerButton.Click += new System.EventHandler(this.ScannerButton_Click);
            // 
            // libraryPath
            // 
            this.libraryPath.Location = new System.Drawing.Point(45, 83);
            this.libraryPath.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.libraryPath.Name = "libraryPath";
            this.libraryPath.Size = new System.Drawing.Size(712, 38);
            this.libraryPath.TabIndex = 6;
            this.libraryPath.TextChanged += new System.EventHandler(this.LibraryPath_TextChanged);
            this.libraryPath.Validated += new System.EventHandler(this.LibraryPath_Validated);
            // 
            // tabPage2
            // 
            this.tabPage2.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage2.Controls.Add(this.extWebLink);
            this.tabPage2.Controls.Add(this.intWebLink);
            this.tabPage2.Controls.Add(this.label34);
            this.tabPage2.Controls.Add(this.label35);
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
            this.tabPage2.Controls.Add(this.openPort);
            this.tabPage2.Controls.Add(this.serverPort);
            this.tabPage2.Controls.Add(this.rootPrefix);
            this.tabPage2.Controls.Add(this.serverName);
            this.tabPage2.Location = new System.Drawing.Point(10, 40);
            this.tabPage2.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage2.Size = new System.Drawing.Size(1267, 806);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "Server";
            // 
            // extWebLink
            // 
            this.extWebLink.Location = new System.Drawing.Point(697, 393);
            this.extWebLink.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.extWebLink.Name = "extWebLink";
            this.extWebLink.Size = new System.Drawing.Size(541, 31);
            this.extWebLink.TabIndex = 53;
            this.extWebLink.TabStop = true;
            this.extWebLink.Text = "- - - - - -";
            this.extWebLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel_LinkClicked);
            // 
            // intWebLink
            // 
            this.intWebLink.Location = new System.Drawing.Point(697, 286);
            this.intWebLink.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.intWebLink.Name = "intWebLink";
            this.intWebLink.Size = new System.Drawing.Size(541, 31);
            this.intWebLink.TabIndex = 52;
            this.intWebLink.TabStop = true;
            this.intWebLink.Text = "- - - - - -";
            this.intWebLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel_LinkClicked);
            // 
            // label34
            // 
            this.label34.AutoSize = true;
            this.label34.Location = new System.Drawing.Point(697, 357);
            this.label34.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label34.Name = "label34";
            this.label34.Size = new System.Drawing.Size(249, 32);
            this.label34.TabIndex = 51;
            this.label34.Text = "External web URL:";
            // 
            // label35
            // 
            this.label35.AutoSize = true;
            this.label35.Location = new System.Drawing.Point(697, 251);
            this.label35.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label35.Name = "label35";
            this.label35.Size = new System.Drawing.Size(213, 32);
            this.label35.TabIndex = 50;
            this.label35.Text = "Local web URL:";
            // 
            // interfaceCombo
            // 
            this.interfaceCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.interfaceCombo.FormattingEnabled = true;
            this.interfaceCombo.Location = new System.Drawing.Point(703, 86);
            this.interfaceCombo.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.interfaceCombo.Name = "interfaceCombo";
            this.interfaceCombo.Size = new System.Drawing.Size(391, 39);
            this.interfaceCombo.TabIndex = 47;
            this.interfaceCombo.SelectedIndexChanged += new System.EventHandler(this.InterfaceCombo_SelectedIndexChanged);
            // 
            // label29
            // 
            this.label29.AutoSize = true;
            this.label29.Location = new System.Drawing.Point(697, 38);
            this.label29.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label29.Name = "label29";
            this.label29.Size = new System.Drawing.Size(242, 32);
            this.label29.TabIndex = 46;
            this.label29.Text = "Network interface:";
            // 
            // statUniqueClients
            // 
            this.statUniqueClients.AutoSize = true;
            this.statUniqueClients.Location = new System.Drawing.Point(376, 471);
            this.statUniqueClients.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.statUniqueClients.Name = "statUniqueClients";
            this.statUniqueClients.Size = new System.Drawing.Size(30, 32);
            this.statUniqueClients.TabIndex = 45;
            this.statUniqueClients.Text = "0";
            // 
            // label26
            // 
            this.label26.AutoSize = true;
            this.label26.Location = new System.Drawing.Point(37, 471);
            this.label26.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label26.Name = "label26";
            this.label26.Size = new System.Drawing.Size(202, 32);
            this.label26.TabIndex = 44;
            this.label26.Text = "Unique clients:";
            // 
            // statImages
            // 
            this.statImages.AutoSize = true;
            this.statImages.Location = new System.Drawing.Point(1040, 550);
            this.statImages.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.statImages.Name = "statImages";
            this.statImages.Size = new System.Drawing.Size(30, 32);
            this.statImages.TabIndex = 43;
            this.statImages.Text = "0";
            // 
            // label27
            // 
            this.label27.AutoSize = true;
            this.label27.Location = new System.Drawing.Point(704, 550);
            this.label27.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label27.Name = "label27";
            this.label27.Size = new System.Drawing.Size(175, 32);
            this.label27.TabIndex = 42;
            this.label27.Text = "Images sent:";
            // 
            // statBooks
            // 
            this.statBooks.AutoSize = true;
            this.statBooks.Location = new System.Drawing.Point(376, 550);
            this.statBooks.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.statBooks.Name = "statBooks";
            this.statBooks.Size = new System.Drawing.Size(30, 32);
            this.statBooks.TabIndex = 41;
            this.statBooks.Text = "0";
            // 
            // label25
            // 
            this.label25.AutoSize = true;
            this.label25.Location = new System.Drawing.Point(37, 550);
            this.label25.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label25.Name = "label25";
            this.label25.Size = new System.Drawing.Size(162, 32);
            this.label25.TabIndex = 40;
            this.label25.Text = "Books sent:";
            // 
            // statRequests
            // 
            this.statRequests.AutoSize = true;
            this.statRequests.Location = new System.Drawing.Point(1040, 471);
            this.statRequests.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.statRequests.Name = "statRequests";
            this.statRequests.Size = new System.Drawing.Size(30, 32);
            this.statRequests.TabIndex = 39;
            this.statRequests.Text = "0";
            // 
            // label23
            // 
            this.label23.AutoSize = true;
            this.label23.Location = new System.Drawing.Point(697, 471);
            this.label23.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label23.Name = "label23";
            this.label23.Size = new System.Drawing.Size(202, 32);
            this.label23.TabIndex = 38;
            this.label23.Text = "Total requests:";
            // 
            // extLink
            // 
            this.extLink.Location = new System.Drawing.Point(37, 393);
            this.extLink.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.extLink.Name = "extLink";
            this.extLink.Size = new System.Drawing.Size(571, 31);
            this.extLink.TabIndex = 37;
            this.extLink.TabStop = true;
            this.extLink.Text = "- - - - - -";
            this.extLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel_LinkClicked);
            // 
            // intLink
            // 
            this.intLink.Location = new System.Drawing.Point(44, 286);
            this.intLink.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.intLink.Name = "intLink";
            this.intLink.Size = new System.Drawing.Size(571, 31);
            this.intLink.TabIndex = 36;
            this.intLink.TabStop = true;
            this.intLink.Text = "- - - - - -";
            this.intLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel_LinkClicked);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(44, 172);
            this.label13.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(158, 32);
            this.label13.TabIndex = 18;
            this.label13.Text = "OPDS root:";
            // 
            // extIPlabel
            // 
            this.extIPlabel.AutoSize = true;
            this.extIPlabel.Location = new System.Drawing.Point(37, 357);
            this.extIPlabel.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.extIPlabel.Name = "extIPlabel";
            this.extIPlabel.Size = new System.Drawing.Size(277, 32);
            this.extIPlabel.TabIndex = 14;
            this.extIPlabel.Text = "External OPDS URL:";
            // 
            // intIPlabel
            // 
            this.intIPlabel.AutoSize = true;
            this.intIPlabel.Location = new System.Drawing.Point(37, 251);
            this.intIPlabel.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.intIPlabel.Name = "intIPlabel";
            this.intIPlabel.Size = new System.Drawing.Size(241, 32);
            this.intIPlabel.TabIndex = 13;
            this.intIPlabel.Text = "Local OPDS URL:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(37, 38);
            this.label4.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(183, 32);
            this.label4.TabIndex = 11;
            this.label4.Text = "Server name:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(1104, 38);
            this.label3.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(74, 32);
            this.label3.TabIndex = 9;
            this.label3.Text = "Port:";
            // 
            // serverButton
            // 
            this.serverButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.serverButton.Location = new System.Drawing.Point(710, 680);
            this.serverButton.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.serverButton.Name = "serverButton";
            this.serverButton.Size = new System.Drawing.Size(500, 90);
            this.serverButton.TabIndex = 8;
            this.serverButton.Text = "Start server";
            this.serverButton.Click += new System.EventHandler(this.ServerButton_Click);
            // 
            // openPort
            // 
            this.openPort.AutoSize = true;
            this.openPort.Checked = global::TinyOPDS.Properties.Settings.Default.OpenNATPort;
            this.openPort.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "OpenNATPort", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.openPort.Enabled = false;
            this.openPort.Location = new System.Drawing.Point(703, 166);
            this.openPort.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.openPort.Name = "openPort";
            this.openPort.Size = new System.Drawing.Size(331, 36);
            this.openPort.TabIndex = 15;
            this.openPort.Text = "Forward port on router";
            this.openPort.UseVisualStyleBackColor = true;
            this.openPort.CheckedChanged += new System.EventHandler(this.OpenPort_CheckedChanged);
            // 
            // serverPort
            // 
            this.serverPort.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::TinyOPDS.Properties.Settings.Default, "ServerPort", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.serverPort.Location = new System.Drawing.Point(1110, 86);
            this.serverPort.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.serverPort.Name = "serverPort";
            this.serverPort.Size = new System.Drawing.Size(100, 38);
            this.serverPort.TabIndex = 10;
            this.serverPort.Text = global::TinyOPDS.Properties.Settings.Default.ServerPort;
            this.serverPort.Validated += new System.EventHandler(this.ServerPort_Validated);
            // 
            // rootPrefix
            // 
            this.rootPrefix.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::TinyOPDS.Properties.Settings.Default, "RootPrefix", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.rootPrefix.Location = new System.Drawing.Point(382, 169);
            this.rootPrefix.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.rootPrefix.Name = "rootPrefix";
            this.rootPrefix.Size = new System.Drawing.Size(184, 38);
            this.rootPrefix.TabIndex = 19;
            this.rootPrefix.Text = global::TinyOPDS.Properties.Settings.Default.RootPrefix;
            this.rootPrefix.TextChanged += new System.EventHandler(this.RootPrefix_TextChanged);
            // 
            // serverName
            // 
            this.serverName.DataBindings.Add(new System.Windows.Forms.Binding("Text", global::TinyOPDS.Properties.Settings.Default, "ServerName", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.serverName.Location = new System.Drawing.Point(43, 87);
            this.serverName.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.serverName.Name = "serverName";
            this.serverName.Size = new System.Drawing.Size(523, 38);
            this.serverName.TabIndex = 12;
            this.serverName.Text = global::TinyOPDS.Properties.Settings.Default.ServerName;
            this.serverName.TextChanged += new System.EventHandler(this.ServerName_TextChanged);
            // 
            // tabPage6
            // 
            this.tabPage6.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage6.Controls.Add(this.filterByLanguage);
            this.tabPage6.Controls.Add(this.clearDownloadsButton);
            this.tabPage6.Controls.Add(this.label40);
            this.tabPage6.Controls.Add(this.comboBox1);
            this.tabPage6.Controls.Add(this.groupBox1);
            this.tabPage6.Controls.Add(this.newBooksPeriodCombo);
            this.tabPage6.Controls.Add(this.label39);
            this.tabPage6.Controls.Add(this.sortOrderCombo);
            this.tabPage6.Controls.Add(this.label38);
            this.tabPage6.Controls.Add(this.label37);
            this.tabPage6.Controls.Add(this.label36);
            this.tabPage6.Controls.Add(this.itemsPerWeb);
            this.tabPage6.Controls.Add(this.itemsPerOPDS);
            this.tabPage6.Location = new System.Drawing.Point(10, 40);
            this.tabPage6.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage6.Name = "tabPage6";
            this.tabPage6.Size = new System.Drawing.Size(1267, 806);
            this.tabPage6.TabIndex = 5;
            this.tabPage6.Text = "OPDS catalog";
            // 
            // filterByLanguage
            // 
            this.filterByLanguage.AutoSize = true;
            this.filterByLanguage.Location = new System.Drawing.Point(711, 566);
            this.filterByLanguage.Name = "filterByLanguage";
            this.filterByLanguage.Size = new System.Drawing.Size(464, 36);
            this.filterByLanguage.TabIndex = 12;
            this.filterByLanguage.Text = "Books in interface language only";
            this.filterByLanguage.UseVisualStyleBackColor = true;
            this.filterByLanguage.CheckedChanged += new System.EventHandler(this.filterByLanguage_CheckedChanged);
            // 
            // clearDownloadsButton
            // 
            this.clearDownloadsButton.Location = new System.Drawing.Point(67, 566);
            this.clearDownloadsButton.Name = "clearDownloadsButton";
            this.clearDownloadsButton.Size = new System.Drawing.Size(463, 69);
            this.clearDownloadsButton.TabIndex = 11;
            this.clearDownloadsButton.Text = "Clear download history";
            this.clearDownloadsButton.UseVisualStyleBackColor = true;
            this.clearDownloadsButton.Click += new System.EventHandler(this.ClearDownloadsButton_Click);
            // 
            // label40
            // 
            this.label40.AutoSize = true;
            this.label40.Location = new System.Drawing.Point(705, 382);
            this.label40.Name = "label40";
            this.label40.Size = new System.Drawing.Size(264, 32);
            this.label40.TabIndex = 10;
            this.label40.Text = "Memory cache size:";
            // 
            // comboBox1
            // 
            this.comboBox1.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Items.AddRange(new object[] {
            "64 MB",
            "128 MB",
            "256 MB",
            "384 MB",
            "512 MB",
            "768 MB",
            "1024 MB"});
            this.comboBox1.Location = new System.Drawing.Point(711, 442);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(331, 39);
            this.comboBox1.TabIndex = 9;
            this.comboBox1.SelectedIndexChanged += new System.EventHandler(this.ComboBox1_SelectedIndexChanged);
            // 
            // groupBox1
            // 
            this.groupBox1.Controls.Add(this.radioButton2);
            this.groupBox1.Controls.Add(this.radioButton1);
            this.groupBox1.Location = new System.Drawing.Point(67, 382);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(463, 123);
            this.groupBox1.TabIndex = 8;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "Cache images";
            // 
            // radioButton2
            // 
            this.radioButton2.AutoSize = true;
            this.radioButton2.Location = new System.Drawing.Point(290, 58);
            this.radioButton2.Name = "radioButton2";
            this.radioButton2.Size = new System.Drawing.Size(141, 36);
            this.radioButton2.TabIndex = 1;
            this.radioButton2.Text = "on disk";
            this.radioButton2.UseVisualStyleBackColor = true;
            this.radioButton2.CheckedChanged += new System.EventHandler(this.CacheType_CheckedChanged);
            // 
            // radioButton1
            // 
            this.radioButton1.AutoSize = true;
            this.radioButton1.Checked = true;
            this.radioButton1.Location = new System.Drawing.Point(41, 58);
            this.radioButton1.Name = "radioButton1";
            this.radioButton1.Size = new System.Drawing.Size(182, 36);
            this.radioButton1.TabIndex = 0;
            this.radioButton1.TabStop = true;
            this.radioButton1.Text = "in memory";
            this.radioButton1.UseVisualStyleBackColor = true;
            this.radioButton1.CheckedChanged += new System.EventHandler(this.CacheType_CheckedChanged);
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
            this.newBooksPeriodCombo.Location = new System.Drawing.Point(717, 270);
            this.newBooksPeriodCombo.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.newBooksPeriodCombo.Name = "newBooksPeriodCombo";
            this.newBooksPeriodCombo.Size = new System.Drawing.Size(325, 39);
            this.newBooksPeriodCombo.TabIndex = 7;
            this.newBooksPeriodCombo.SelectedIndexChanged += new System.EventHandler(this.NewBooksPeriodCombo_SelectedIndexChanged);
            // 
            // label39
            // 
            this.label39.AutoSize = true;
            this.label39.Location = new System.Drawing.Point(705, 218);
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
            this.sortOrderCombo.Location = new System.Drawing.Point(67, 270);
            this.sortOrderCombo.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.sortOrderCombo.Name = "sortOrderCombo";
            this.sortOrderCombo.Size = new System.Drawing.Size(463, 39);
            this.sortOrderCombo.TabIndex = 5;
            this.sortOrderCombo.SelectedIndexChanged += new System.EventHandler(this.SortOrderCombo_SelectedIndexChanged);
            // 
            // label38
            // 
            this.label38.AutoSize = true;
            this.label38.Location = new System.Drawing.Point(59, 218);
            this.label38.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label38.Name = "label38";
            this.label38.Size = new System.Drawing.Size(217, 32);
            this.label38.TabIndex = 4;
            this.label38.Text = "Items sort order:";
            // 
            // label37
            // 
            this.label37.AutoSize = true;
            this.label37.Location = new System.Drawing.Point(705, 60);
            this.label37.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label37.Name = "label37";
            this.label37.Size = new System.Drawing.Size(268, 32);
            this.label37.TabIndex = 2;
            this.label37.Text = "Items per web page:";
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
            // itemsPerWeb
            // 
            this.itemsPerWeb.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::TinyOPDS.Properties.Settings.Default, "ItemsPerWebPage", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.itemsPerWeb.Location = new System.Drawing.Point(713, 114);
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
            this.itemsPerWeb.ValueChanged += new System.EventHandler(this.ItemsPerWeb_ValueChanged);
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
            this.itemsPerOPDS.ValueChanged += new System.EventHandler(this.ItemsPerOPDS_ValueChanged);
            // 
            // tabPage7
            // 
            this.tabPage7.Controls.Add(this.treeViewOPDS);
            this.tabPage7.Location = new System.Drawing.Point(10, 40);
            this.tabPage7.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabPage7.Name = "tabPage7";
            this.tabPage7.Padding = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.tabPage7.Size = new System.Drawing.Size(1267, 806);
            this.tabPage7.TabIndex = 6;
            this.tabPage7.Text = "OPDS routes";
            this.tabPage7.UseVisualStyleBackColor = true;
            // 
            // treeViewOPDS
            // 
            this.treeViewOPDS.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.treeViewOPDS.CheckBoxes = true;
            this.treeViewOPDS.Font = new System.Drawing.Font("Microsoft Sans Serif", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(254)));
            this.treeViewOPDS.Location = new System.Drawing.Point(24, 24);
            this.treeViewOPDS.Margin = new System.Windows.Forms.Padding(3, 2, 3, 2);
            this.treeViewOPDS.Name = "treeViewOPDS";
            this.treeViewOPDS.Size = new System.Drawing.Size(1213, 746);
            this.treeViewOPDS.TabIndex = 2;
            this.treeViewOPDS.AfterCheck += new System.Windows.Forms.TreeViewEventHandler(this.TreeViewOPDS_AfterCheck);
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
            this.tabPage5.Size = new System.Drawing.Size(1267, 806);
            this.tabPage5.TabIndex = 4;
            this.tabPage5.Text = "Auth";
            // 
            // statBannedClients
            // 
            this.statBannedClients.AutoSize = true;
            this.statBannedClients.Location = new System.Drawing.Point(1144, 584);
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
            this.label31.Location = new System.Drawing.Point(891, 584);
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
            this.statWrongLogins.Location = new System.Drawing.Point(744, 584);
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
            this.label30.Location = new System.Drawing.Point(493, 584);
            this.label30.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label30.Name = "label30";
            this.label30.Size = new System.Drawing.Size(184, 32);
            this.label30.TabIndex = 42;
            this.label30.Text = "Failed logins:";
            // 
            // statGoodLogins
            // 
            this.statGoodLogins.AutoSize = true;
            this.statGoodLogins.Location = new System.Drawing.Point(341, 584);
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
            this.label28.Location = new System.Drawing.Point(61, 584);
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
            this.dataGridView1.Size = new System.Drawing.Size(1136, 320);
            this.dataGridView1.TabIndex = 1;
            this.dataGridView1.CellFormatting += new System.Windows.Forms.DataGridViewCellFormattingEventHandler(this.DataGridView1_CellFormatting);
            // 
            // wrongAttemptsCount
            // 
            this.wrongAttemptsCount.DataBindings.Add(new System.Windows.Forms.Binding("Value", global::TinyOPDS.Properties.Settings.Default, "WrongAttemptsCount", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.wrongAttemptsCount.Location = new System.Drawing.Point(755, 114);
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
            this.wrongAttemptsCount.ValueChanged += new System.EventHandler(this.WrongAttemptsCount_ValueChanged);
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
            this.banClients.CheckedChanged += new System.EventHandler(this.BanClients_CheckedChanged);
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
            this.rememberClients.CheckedChanged += new System.EventHandler(this.RememberClients_CheckedChanged);
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
            this.useHTTPAuth.CheckedChanged += new System.EventHandler(this.UseHTTPAuth_CheckedChanged);
            // 
            // tabPage3
            // 
            this.tabPage3.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage3.Controls.Add(this.oneInstance);
            this.tabPage3.Controls.Add(this.viewLogFile);
            this.tabPage3.Controls.Add(this.label32);
            this.tabPage3.Controls.Add(this.updateCombo);
            this.tabPage3.Controls.Add(this.label22);
            this.tabPage3.Controls.Add(this.logVerbosity);
            this.tabPage3.Controls.Add(this.label11);
            this.tabPage3.Controls.Add(this.langCombo);
            this.tabPage3.Controls.Add(this.saveLog);
            this.tabPage3.Controls.Add(this.closeToTray);
            this.tabPage3.Controls.Add(this.startMinimized);
            this.tabPage3.Controls.Add(this.startWithWindows);
            this.tabPage3.Location = new System.Drawing.Point(10, 40);
            this.tabPage3.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage3.Size = new System.Drawing.Size(1267, 806);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Misc";
            // 
            // oneInstance
            // 
            this.oneInstance.AutoSize = true;
            this.oneInstance.Location = new System.Drawing.Point(691, 168);
            this.oneInstance.Name = "oneInstance";
            this.oneInstance.Size = new System.Drawing.Size(280, 36);
            this.oneInstance.TabIndex = 40;
            this.oneInstance.Text = "Only one instance";
            this.oneInstance.UseVisualStyleBackColor = true;
            this.oneInstance.CheckedChanged += new System.EventHandler(this.OneInstance_CheckedChanged);
            // 
            // viewLogFile
            // 
            this.viewLogFile.Location = new System.Drawing.Point(691, 562);
            this.viewLogFile.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.viewLogFile.Name = "viewLogFile";
            this.viewLogFile.Size = new System.Drawing.Size(471, 55);
            this.viewLogFile.TabIndex = 39;
            this.viewLogFile.Text = "View log file";
            this.viewLogFile.UseVisualStyleBackColor = true;
            this.viewLogFile.Click += new System.EventHandler(this.ViewLogFile_Click);
            // 
            // label32
            // 
            this.label32.AutoSize = true;
            this.label32.Location = new System.Drawing.Point(685, 267);
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
            this.updateCombo.Location = new System.Drawing.Point(691, 315);
            this.updateCombo.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.updateCombo.Name = "updateCombo";
            this.updateCombo.Size = new System.Drawing.Size(471, 39);
            this.updateCombo.TabIndex = 37;
            this.updateCombo.SelectedIndexChanged += new System.EventHandler(this.UpdateCombo_SelectedIndexChanged);
            // 
            // label22
            // 
            this.label22.AutoSize = true;
            this.label22.Location = new System.Drawing.Point(35, 510);
            this.label22.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.label22.Name = "label22";
            this.label22.Size = new System.Drawing.Size(250, 32);
            this.label22.TabIndex = 36;
            this.label22.Text = "Log verbosity level";
            // 
            // logVerbosity
            // 
            this.logVerbosity.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.logVerbosity.Enabled = false;
            this.logVerbosity.FormattingEnabled = true;
            this.logVerbosity.ItemHeight = 31;
            this.logVerbosity.Items.AddRange(new object[] {
            "Info, warnings and errors",
            "Warnings and errors",
            "Errors only"});
            this.logVerbosity.Location = new System.Drawing.Point(41, 565);
            this.logVerbosity.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.logVerbosity.Name = "logVerbosity";
            this.logVerbosity.Size = new System.Drawing.Size(528, 39);
            this.logVerbosity.TabIndex = 35;
            this.logVerbosity.SelectedIndexChanged += new System.EventHandler(this.LogVerbosity_SelectedIndexChanged);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(35, 267);
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
            this.langCombo.Location = new System.Drawing.Point(41, 315);
            this.langCombo.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.langCombo.Name = "langCombo";
            this.langCombo.Size = new System.Drawing.Size(528, 39);
            this.langCombo.TabIndex = 31;
            this.langCombo.ValueMember = "Key";
            this.langCombo.SelectedValueChanged += new System.EventHandler(this.LangCombo_SelectedValueChanged);
            // 
            // saveLog
            // 
            this.saveLog.AutoSize = true;
            this.saveLog.Checked = global::TinyOPDS.Properties.Settings.Default.SaveLogToDisk;
            this.saveLog.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "SaveLogToDisk", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.saveLog.Location = new System.Drawing.Point(41, 410);
            this.saveLog.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.saveLog.Name = "saveLog";
            this.saveLog.Size = new System.Drawing.Size(239, 36);
            this.saveLog.TabIndex = 33;
            this.saveLog.Text = "Save log to file";
            this.saveLog.UseVisualStyleBackColor = true;
            this.saveLog.CheckedChanged += new System.EventHandler(this.SaveLog_CheckedChanged);
            // 
            // closeToTray
            // 
            this.closeToTray.AutoSize = true;
            this.closeToTray.Checked = global::TinyOPDS.Properties.Settings.Default.CloseToTray;
            this.closeToTray.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "CloseToTray", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.closeToTray.Location = new System.Drawing.Point(41, 168);
            this.closeToTray.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.closeToTray.Name = "closeToTray";
            this.closeToTray.Size = new System.Drawing.Size(362, 36);
            this.closeToTray.TabIndex = 2;
            this.closeToTray.Text = "Close or minimize to tray";
            this.closeToTray.UseVisualStyleBackColor = true;
            this.closeToTray.CheckedChanged += new System.EventHandler(this.CloseToTray_CheckedChanged);
            // 
            // startMinimized
            // 
            this.startMinimized.AutoSize = true;
            this.startMinimized.Checked = global::TinyOPDS.Properties.Settings.Default.StartMinimized;
            this.startMinimized.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "StartMinimized", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.startMinimized.Location = new System.Drawing.Point(691, 62);
            this.startMinimized.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.startMinimized.Name = "startMinimized";
            this.startMinimized.Size = new System.Drawing.Size(248, 36);
            this.startMinimized.TabIndex = 1;
            this.startMinimized.Text = "Start minimized";
            this.startMinimized.UseVisualStyleBackColor = true;
            this.startMinimized.CheckedChanged += new System.EventHandler(this.StartMinimized_CheckedChanged);
            // 
            // startWithWindows
            // 
            this.startWithWindows.AutoSize = true;
            this.startWithWindows.Checked = global::TinyOPDS.Properties.Settings.Default.StartWithWindows;
            this.startWithWindows.DataBindings.Add(new System.Windows.Forms.Binding("Checked", global::TinyOPDS.Properties.Settings.Default, "StartWithWindows", true, System.Windows.Forms.DataSourceUpdateMode.OnPropertyChanged));
            this.startWithWindows.Location = new System.Drawing.Point(41, 62);
            this.startWithWindows.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.startWithWindows.Name = "startWithWindows";
            this.startWithWindows.Size = new System.Drawing.Size(292, 36);
            this.startWithWindows.TabIndex = 0;
            this.startWithWindows.Text = "Start with Windows";
            this.startWithWindows.UseVisualStyleBackColor = true;
            this.startWithWindows.CheckedChanged += new System.EventHandler(this.StartWithWindows_CheckedChanged);
            // 
            // tabPage4
            // 
            this.tabPage4.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage4.Controls.Add(this.pictureBox2);
            this.tabPage4.Controls.Add(this.linkLabel6);
            this.tabPage4.Controls.Add(this.label20);
            this.tabPage4.Controls.Add(this.label19);
            this.tabPage4.Controls.Add(this.linkLabel2);
            this.tabPage4.Controls.Add(this.label18);
            this.tabPage4.Controls.Add(this.linkLabel1);
            this.tabPage4.Controls.Add(this.label17);
            this.tabPage4.Controls.Add(this.appVersion);
            this.tabPage4.Controls.Add(this.appName);
            this.tabPage4.Controls.Add(this.pictureBox1);
            this.tabPage4.Location = new System.Drawing.Point(10, 40);
            this.tabPage4.Margin = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(8, 7, 8, 7);
            this.tabPage4.Size = new System.Drawing.Size(1267, 806);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "About";
            // 
            // pictureBox2
            // 
            this.pictureBox2.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox2.Image")));
            this.pictureBox2.Location = new System.Drawing.Point(35, 592);
            this.pictureBox2.Name = "pictureBox2";
            this.pictureBox2.Size = new System.Drawing.Size(261, 132);
            this.pictureBox2.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox2.TabIndex = 14;
            this.pictureBox2.TabStop = false;
            this.pictureBox2.Click += new System.EventHandler(this.DonateButton_Click);
            // 
            // linkLabel6
            // 
            this.linkLabel6.AutoSize = true;
            this.linkLabel6.Location = new System.Drawing.Point(515, 449);
            this.linkLabel6.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.linkLabel6.Name = "linkLabel6";
            this.linkLabel6.Size = new System.Drawing.Size(363, 32);
            this.linkLabel6.TabIndex = 13;
            this.linkLabel6.TabStop = true;
            this.linkLabel6.Text = "Author of SQLite FTS5 NET";
            this.linkLabel6.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel3_LinkClicked);
            // 
            // label20
            // 
            this.label20.Location = new System.Drawing.Point(32, 450);
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
            this.linkLabel2.Location = new System.Drawing.Point(515, 396);
            this.linkLabel2.Margin = new System.Windows.Forms.Padding(8, 0, 8, 0);
            this.linkLabel2.Name = "linkLabel2";
            this.linkLabel2.Size = new System.Drawing.Size(286, 32);
            this.linkLabel2.TabIndex = 7;
            this.linkLabel2.TabStop = true;
            this.linkLabel2.Text = "https://mit-license.org";
            this.linkLabel2.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel_LinkClicked);
            // 
            // label18
            // 
            this.label18.Location = new System.Drawing.Point(37, 396);
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
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.LinkLabel_LinkClicked);
            // 
            // label17
            // 
            this.label17.Location = new System.Drawing.Point(29, 344);
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
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBox1.TabIndex = 1;
            this.pictureBox1.TabStop = false;
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
            this.windowMenuItem.Click += new System.EventHandler(this.WindowMenuItem_Click);
            // 
            // serverMenuItem
            // 
            this.serverMenuItem.Name = "serverMenuItem";
            this.serverMenuItem.Size = new System.Drawing.Size(270, 48);
            this.serverMenuItem.Text = "Stop server";
            this.serverMenuItem.Click += new System.EventHandler(this.ServerButton_Click);
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
            this.exitMenuItem.Click += new System.EventHandler(this.ExitMenuItem_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(16F, 31F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1268, 842);
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
            this.groupBox1.ResumeLayout(false);
            this.groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.itemsPerWeb)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.itemsPerOPDS)).EndInit();
            this.tabPage7.ResumeLayout(false);
            this.tabPage5.ResumeLayout(false);
            this.tabPage5.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.wrongAttemptsCount)).EndInit();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.tabPage4.ResumeLayout(false);
            this.tabPage4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox2)).EndInit();
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
        private System.Windows.Forms.LinkLabel extWebLink;
        private System.Windows.Forms.LinkLabel intWebLink;
        private System.Windows.Forms.Label label34;
        private System.Windows.Forms.Label label35;
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
        private System.Windows.Forms.TabPage tabPage7;
        private System.Windows.Forms.TreeView treeViewOPDS;
        private System.Windows.Forms.Label label40;
        private System.Windows.Forms.ComboBox comboBox1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.RadioButton radioButton2;
        private System.Windows.Forms.RadioButton radioButton1;
        private System.Windows.Forms.CheckBox oneInstance;
        private System.Windows.Forms.PictureBox pictureBox2;
        private System.Windows.Forms.Button clearDownloadsButton;
        private System.Windows.Forms.CheckBox filterByLanguage;
    }
}

