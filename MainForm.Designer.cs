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
            this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
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
            this.extLink = new System.Windows.Forms.LinkLabel();
            this.intLink = new System.Windows.Forms.LinkLabel();
            this.useUPnP = new System.Windows.Forms.CheckBox();
            this.rootPrefix = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.openPort = new System.Windows.Forms.CheckBox();
            this.extIPlabel = new System.Windows.Forms.Label();
            this.intIPlabel = new System.Windows.Forms.Label();
            this.serverName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.serverPort = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.serverButton = new System.Windows.Forms.Button();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.converterLinkLabel = new System.Windows.Forms.LinkLabel();
            this.saveLog = new System.Windows.Forms.CheckBox();
            this.label11 = new System.Windows.Forms.Label();
            this.langCombo = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.convertorFolder = new System.Windows.Forms.Button();
            this.convertorPath = new System.Windows.Forms.TextBox();
            this.closeToTray = new System.Windows.Forms.CheckBox();
            this.startMinimized = new System.Windows.Forms.CheckBox();
            this.startWithWindows = new System.Windows.Forms.CheckBox();
            this.tabPage4 = new System.Windows.Forms.TabPage();
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
            this.notifyIcon1 = new System.Windows.Forms.NotifyIcon(this.components);
            this.contextMenuStrip1 = new System.Windows.Forms.ContextMenuStrip(this.components);
            this.windowMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.serverMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.toolStripMenuItem1 = new System.Windows.Forms.ToolStripSeparator();
            this.exitMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.tabPage4.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.contextMenuStrip1.SuspendLayout();
            this.SuspendLayout();
            // 
            // tabControl1
            // 
            this.tabControl1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.ItemSize = new System.Drawing.Size(91, 30);
            this.tabControl1.Location = new System.Drawing.Point(-3, -1);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(482, 309);
            this.tabControl1.TabIndex = 8;
            // 
            // tabPage1
            // 
            this.tabPage1.BackColor = System.Drawing.SystemColors.Control;
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
            this.tabPage1.Location = new System.Drawing.Point(4, 34);
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage1.Size = new System.Drawing.Size(474, 271);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Scanner settings";
            // 
            // useWatcher
            // 
            this.useWatcher.AutoSize = true;
            this.useWatcher.Location = new System.Drawing.Point(329, 34);
            this.useWatcher.Name = "useWatcher";
            this.useWatcher.Size = new System.Drawing.Size(135, 17);
            this.useWatcher.TabIndex = 30;
            this.useWatcher.Text = "Monitor library changes";
            this.useWatcher.UseVisualStyleBackColor = true;
            this.useWatcher.CheckedChanged += new System.EventHandler(this.useWatcher_CheckedChanged);
            // 
            // duplicates
            // 
            this.duplicates.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.duplicates.AutoSize = true;
            this.duplicates.Location = new System.Drawing.Point(123, 189);
            this.duplicates.MinimumSize = new System.Drawing.Size(50, 0);
            this.duplicates.Name = "duplicates";
            this.duplicates.Size = new System.Drawing.Size(50, 13);
            this.duplicates.TabIndex = 29;
            this.duplicates.Text = "0";
            // 
            // label16
            // 
            this.label16.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(15, 189);
            this.label16.Name = "label16";
            this.label16.Size = new System.Drawing.Size(60, 13);
            this.label16.TabIndex = 28;
            this.label16.Text = "Duplicates:";
            // 
            // label15
            // 
            this.label15.AutoSize = true;
            this.label15.Location = new System.Drawing.Point(14, 16);
            this.label15.Name = "label15";
            this.label15.Size = new System.Drawing.Size(105, 13);
            this.label15.TabIndex = 27;
            this.label15.Text = "Path to books folder:";
            // 
            // status
            // 
            this.status.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.status.AutoSize = true;
            this.status.Location = new System.Drawing.Point(360, 187);
            this.status.MinimumSize = new System.Drawing.Size(50, 0);
            this.status.Name = "status";
            this.status.Size = new System.Drawing.Size(58, 13);
            this.status.TabIndex = 26;
            this.status.Text = "STOPPED";
            // 
            // label14
            // 
            this.label14.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(253, 187);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(40, 13);
            this.label14.TabIndex = 25;
            this.label14.Text = "Status:";
            // 
            // rate
            // 
            this.rate.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.rate.AutoSize = true;
            this.rate.Location = new System.Drawing.Point(360, 157);
            this.rate.MinimumSize = new System.Drawing.Size(50, 0);
            this.rate.Name = "rate";
            this.rate.Size = new System.Drawing.Size(66, 13);
            this.rate.TabIndex = 24;
            this.rate.Text = "0 books/min";
            // 
            // label12
            // 
            this.label12.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(253, 157);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(33, 13);
            this.label12.TabIndex = 23;
            this.label12.Text = "Rate:";
            // 
            // elapsedTime
            // 
            this.elapsedTime.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.elapsedTime.AutoSize = true;
            this.elapsedTime.Location = new System.Drawing.Point(360, 127);
            this.elapsedTime.MinimumSize = new System.Drawing.Size(50, 0);
            this.elapsedTime.Name = "elapsedTime";
            this.elapsedTime.Size = new System.Drawing.Size(50, 13);
            this.elapsedTime.TabIndex = 22;
            this.elapsedTime.Text = "00:00:00";
            // 
            // label10
            // 
            this.label10.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(253, 127);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(70, 13);
            this.label10.TabIndex = 21;
            this.label10.Text = "Elapsed time:";
            // 
            // startTime
            // 
            this.startTime.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.startTime.AutoSize = true;
            this.startTime.Location = new System.Drawing.Point(360, 97);
            this.startTime.MinimumSize = new System.Drawing.Size(50, 0);
            this.startTime.Name = "startTime";
            this.startTime.Size = new System.Drawing.Size(50, 13);
            this.startTime.TabIndex = 20;
            this.startTime.Text = "00:00:00";
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(253, 97);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(54, 13);
            this.label6.TabIndex = 19;
            this.label6.Text = "Start time:";
            // 
            // booksProcessed
            // 
            this.booksProcessed.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.booksProcessed.AutoSize = true;
            this.booksProcessed.Location = new System.Drawing.Point(123, 219);
            this.booksProcessed.MinimumSize = new System.Drawing.Size(50, 0);
            this.booksProcessed.Name = "booksProcessed";
            this.booksProcessed.Size = new System.Drawing.Size(50, 13);
            this.booksProcessed.TabIndex = 18;
            this.booksProcessed.Text = "0";
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(15, 219);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(92, 13);
            this.label5.TabIndex = 17;
            this.label5.Text = "Books processed:";
            // 
            // invalidBooks
            // 
            this.invalidBooks.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.invalidBooks.AutoSize = true;
            this.invalidBooks.Location = new System.Drawing.Point(123, 129);
            this.invalidBooks.MinimumSize = new System.Drawing.Size(50, 0);
            this.invalidBooks.Name = "invalidBooks";
            this.invalidBooks.Size = new System.Drawing.Size(50, 13);
            this.invalidBooks.TabIndex = 16;
            this.invalidBooks.Text = "0";
            // 
            // label9
            // 
            this.label9.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(15, 129);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(73, 13);
            this.label9.TabIndex = 15;
            this.label9.Text = "Invalid books:";
            // 
            // skippedBooks
            // 
            this.skippedBooks.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.skippedBooks.AutoSize = true;
            this.skippedBooks.Location = new System.Drawing.Point(123, 159);
            this.skippedBooks.MinimumSize = new System.Drawing.Size(50, 0);
            this.skippedBooks.Name = "skippedBooks";
            this.skippedBooks.Size = new System.Drawing.Size(50, 13);
            this.skippedBooks.TabIndex = 14;
            this.skippedBooks.Text = "0";
            // 
            // label7
            // 
            this.label7.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(15, 159);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(81, 13);
            this.label7.TabIndex = 13;
            this.label7.Text = "Skipped books:";
            // 
            // booksFound
            // 
            this.booksFound.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.booksFound.AutoSize = true;
            this.booksFound.Location = new System.Drawing.Point(123, 99);
            this.booksFound.MinimumSize = new System.Drawing.Size(50, 0);
            this.booksFound.Name = "booksFound";
            this.booksFound.Size = new System.Drawing.Size(79, 13);
            this.booksFound.TabIndex = 12;
            this.booksFound.Text = "fb2: 0   epub: 0";
            // 
            // booksInDB
            // 
            this.booksInDB.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.booksInDB.AutoSize = true;
            this.booksInDB.Location = new System.Drawing.Point(123, 63);
            this.booksInDB.MinimumSize = new System.Drawing.Size(50, 0);
            this.booksInDB.Name = "booksInDB";
            this.booksInDB.Size = new System.Drawing.Size(127, 13);
            this.booksInDB.TabIndex = 11;
            this.booksInDB.Text = "0         fb2:  0       epub: 0";
            // 
            // folderButton
            // 
            this.folderButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.folderButton.Image = ((System.Drawing.Image)(resources.GetObject("folderButton.Image")));
            this.folderButton.Location = new System.Drawing.Point(288, 30);
            this.folderButton.Name = "folderButton";
            this.folderButton.Size = new System.Drawing.Size(29, 23);
            this.folderButton.TabIndex = 10;
            this.folderButton.UseVisualStyleBackColor = true;
            this.folderButton.Click += new System.EventHandler(this.folderButton_Click);
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(15, 64);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(101, 13);
            this.label2.TabIndex = 9;
            this.label2.Text = "Books in database: ";
            // 
            // label1
            // 
            this.label1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(15, 99);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(70, 13);
            this.label1.TabIndex = 8;
            this.label1.Text = "Books found:";
            // 
            // scannerButton
            // 
            this.scannerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.scannerButton.Location = new System.Drawing.Point(262, 223);
            this.scannerButton.Name = "scannerButton";
            this.scannerButton.Size = new System.Drawing.Size(205, 40);
            this.scannerButton.TabIndex = 7;
            this.scannerButton.Text = "Start scanning";
            this.scannerButton.UseVisualStyleBackColor = true;
            this.scannerButton.Click += new System.EventHandler(this.scannerButton_Click);
            // 
            // libraryPath
            // 
            this.libraryPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.libraryPath.Location = new System.Drawing.Point(17, 32);
            this.libraryPath.Name = "libraryPath";
            this.libraryPath.ReadOnly = true;
            this.libraryPath.Size = new System.Drawing.Size(269, 20);
            this.libraryPath.TabIndex = 6;
            this.libraryPath.Text = "P:\\My eBooks";
            this.libraryPath.TextChanged += new System.EventHandler(this.libraryPath_TextChanged);
            // 
            // tabPage2
            // 
            this.tabPage2.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage2.Controls.Add(this.extLink);
            this.tabPage2.Controls.Add(this.intLink);
            this.tabPage2.Controls.Add(this.useUPnP);
            this.tabPage2.Controls.Add(this.rootPrefix);
            this.tabPage2.Controls.Add(this.label13);
            this.tabPage2.Controls.Add(this.openPort);
            this.tabPage2.Controls.Add(this.extIPlabel);
            this.tabPage2.Controls.Add(this.intIPlabel);
            this.tabPage2.Controls.Add(this.serverName);
            this.tabPage2.Controls.Add(this.label4);
            this.tabPage2.Controls.Add(this.serverPort);
            this.tabPage2.Controls.Add(this.label3);
            this.tabPage2.Controls.Add(this.serverButton);
            this.tabPage2.Location = new System.Drawing.Point(4, 34);
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage2.Size = new System.Drawing.Size(474, 271);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "OPDS server settings";
            // 
            // extLink
            // 
            this.extLink.AutoSize = true;
            this.extLink.Location = new System.Drawing.Point(172, 160);
            this.extLink.Name = "extLink";
            this.extLink.Size = new System.Drawing.Size(40, 13);
            this.extLink.TabIndex = 37;
            this.extLink.TabStop = true;
            this.extLink.Text = "- - - - - -";
            this.extLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            // 
            // intLink
            // 
            this.intLink.AutoSize = true;
            this.intLink.Location = new System.Drawing.Point(172, 134);
            this.intLink.Name = "intLink";
            this.intLink.Size = new System.Drawing.Size(40, 13);
            this.intLink.TabIndex = 36;
            this.intLink.TabStop = true;
            this.intLink.Text = "- - - - - -";
            this.intLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            // 
            // useUPnP
            // 
            this.useUPnP.AutoSize = true;
            this.useUPnP.Location = new System.Drawing.Point(284, 65);
            this.useUPnP.Name = "useUPnP";
            this.useUPnP.Size = new System.Drawing.Size(76, 17);
            this.useUPnP.TabIndex = 35;
            this.useUPnP.Text = "Use UPnP";
            this.useUPnP.UseVisualStyleBackColor = true;
            this.useUPnP.CheckedChanged += new System.EventHandler(this.useUPnP_CheckedChanged);
            // 
            // rootPrefix
            // 
            this.rootPrefix.Location = new System.Drawing.Point(22, 88);
            this.rootPrefix.Name = "rootPrefix";
            this.rootPrefix.Size = new System.Drawing.Size(190, 20);
            this.rootPrefix.TabIndex = 19;
            this.rootPrefix.TextChanged += new System.EventHandler(this.rootPrefix_TextChanged);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(19, 68);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(127, 13);
            this.label13.TabIndex = 18;
            this.label13.Text = "OPDS root catalog prefix:";
            // 
            // openPort
            // 
            this.openPort.AutoSize = true;
            this.openPort.Enabled = false;
            this.openPort.Location = new System.Drawing.Point(284, 91);
            this.openPort.Name = "openPort";
            this.openPort.Size = new System.Drawing.Size(161, 17);
            this.openPort.TabIndex = 15;
            this.openPort.Text = "Forward port on UPnP router";
            this.openPort.UseVisualStyleBackColor = true;
            this.openPort.CheckedChanged += new System.EventHandler(this.openPort_CheckedChanged);
            // 
            // extIPlabel
            // 
            this.extIPlabel.AutoSize = true;
            this.extIPlabel.Location = new System.Drawing.Point(19, 160);
            this.extIPlabel.Name = "extIPlabel";
            this.extIPlabel.Size = new System.Drawing.Size(95, 13);
            this.extIPlabel.TabIndex = 14;
            this.extIPlabel.Text = "External OPDS url:";
            // 
            // intIPlabel
            // 
            this.intIPlabel.AutoSize = true;
            this.intIPlabel.Location = new System.Drawing.Point(19, 134);
            this.intIPlabel.Name = "intIPlabel";
            this.intIPlabel.Size = new System.Drawing.Size(83, 13);
            this.intIPlabel.TabIndex = 13;
            this.intIPlabel.Text = "Local OPDS url:";
            // 
            // serverName
            // 
            this.serverName.Location = new System.Drawing.Point(20, 35);
            this.serverName.Name = "serverName";
            this.serverName.Size = new System.Drawing.Size(348, 20);
            this.serverName.TabIndex = 12;
            this.serverName.Text = "Мой домашний OPDS сервер";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(17, 16);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(70, 13);
            this.label4.TabIndex = 11;
            this.label4.Text = "Server name:";
            // 
            // serverPort
            // 
            this.serverPort.Location = new System.Drawing.Point(386, 35);
            this.serverPort.Name = "serverPort";
            this.serverPort.Size = new System.Drawing.Size(59, 20);
            this.serverPort.TabIndex = 10;
            this.serverPort.Text = "8080";
            this.serverPort.Validated += new System.EventHandler(this.serverPort_Validated);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(383, 16);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(62, 13);
            this.label3.TabIndex = 9;
            this.label3.Text = "Server port:";
            // 
            // serverButton
            // 
            this.serverButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.serverButton.Location = new System.Drawing.Point(262, 223);
            this.serverButton.Name = "serverButton";
            this.serverButton.Size = new System.Drawing.Size(205, 40);
            this.serverButton.TabIndex = 8;
            this.serverButton.Text = "Start server";
            this.serverButton.Click += new System.EventHandler(this.serverButton_Click);
            // 
            // tabPage3
            // 
            this.tabPage3.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage3.Controls.Add(this.converterLinkLabel);
            this.tabPage3.Controls.Add(this.saveLog);
            this.tabPage3.Controls.Add(this.label11);
            this.tabPage3.Controls.Add(this.langCombo);
            this.tabPage3.Controls.Add(this.label8);
            this.tabPage3.Controls.Add(this.convertorFolder);
            this.tabPage3.Controls.Add(this.convertorPath);
            this.tabPage3.Controls.Add(this.closeToTray);
            this.tabPage3.Controls.Add(this.startMinimized);
            this.tabPage3.Controls.Add(this.startWithWindows);
            this.tabPage3.Location = new System.Drawing.Point(4, 34);
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage3.Size = new System.Drawing.Size(474, 271);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Miscellaneous";
            // 
            // converterLinkLabel
            // 
            this.converterLinkLabel.AutoSize = true;
            this.converterLinkLabel.Location = new System.Drawing.Point(12, 55);
            this.converterLinkLabel.Name = "converterLinkLabel";
            this.converterLinkLabel.Size = new System.Drawing.Size(268, 13);
            this.converterLinkLabel.TabIndex = 34;
            this.converterLinkLabel.TabStop = true;
            this.converterLinkLabel.Text = "Click here to download latest version of ePub converter";
            this.converterLinkLabel.Visible = false;
            this.converterLinkLabel.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel3_LinkClicked);
            // 
            // saveLog
            // 
            this.saveLog.AutoSize = true;
            this.saveLog.Location = new System.Drawing.Point(11, 187);
            this.saveLog.Name = "saveLog";
            this.saveLog.Size = new System.Drawing.Size(96, 17);
            this.saveLog.TabIndex = 33;
            this.saveLog.Text = "Save log to file";
            this.saveLog.UseVisualStyleBackColor = true;
            this.saveLog.CheckedChanged += new System.EventHandler(this.saveLog_CheckedChanged);
            // 
            // label11
            // 
            this.label11.AutoSize = true;
            this.label11.Location = new System.Drawing.Point(261, 94);
            this.label11.Name = "label11";
            this.label11.Size = new System.Drawing.Size(130, 13);
            this.label11.TabIndex = 32;
            this.label11.Text = "GUI and OPDS language:";
            // 
            // langCombo
            // 
            this.langCombo.DisplayMember = "Value";
            this.langCombo.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.langCombo.FormattingEnabled = true;
            this.langCombo.Location = new System.Drawing.Point(264, 119);
            this.langCombo.Name = "langCombo";
            this.langCombo.Size = new System.Drawing.Size(127, 21);
            this.langCombo.TabIndex = 31;
            this.langCombo.ValueMember = "Key";
            this.langCombo.SelectedValueChanged += new System.EventHandler(this.langCombo_SelectedValueChanged);
            // 
            // label8
            // 
            this.label8.AutoSize = true;
            this.label8.Location = new System.Drawing.Point(8, 12);
            this.label8.Name = "label8";
            this.label8.Size = new System.Drawing.Size(138, 13);
            this.label8.TabIndex = 30;
            this.label8.Text = "Path to the ePub converter:";
            // 
            // convertorFolder
            // 
            this.convertorFolder.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.convertorFolder.Image = ((System.Drawing.Image)(resources.GetObject("convertorFolder.Image")));
            this.convertorFolder.Location = new System.Drawing.Point(431, 26);
            this.convertorFolder.Name = "convertorFolder";
            this.convertorFolder.Size = new System.Drawing.Size(29, 23);
            this.convertorFolder.TabIndex = 29;
            this.convertorFolder.UseVisualStyleBackColor = true;
            // 
            // convertorPath
            // 
            this.convertorPath.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.convertorPath.Location = new System.Drawing.Point(11, 28);
            this.convertorPath.Name = "convertorPath";
            this.convertorPath.Size = new System.Drawing.Size(416, 20);
            this.convertorPath.TabIndex = 28;
            // 
            // closeToTray
            // 
            this.closeToTray.AutoSize = true;
            this.closeToTray.Location = new System.Drawing.Point(11, 155);
            this.closeToTray.Name = "closeToTray";
            this.closeToTray.Size = new System.Drawing.Size(138, 17);
            this.closeToTray.TabIndex = 2;
            this.closeToTray.Text = "Close or minimize to tray";
            this.closeToTray.UseVisualStyleBackColor = true;
            this.closeToTray.CheckedChanged += new System.EventHandler(this.closeToTray_CheckedChanged);
            // 
            // startMinimized
            // 
            this.startMinimized.AutoSize = true;
            this.startMinimized.Location = new System.Drawing.Point(11, 123);
            this.startMinimized.Name = "startMinimized";
            this.startMinimized.Size = new System.Drawing.Size(96, 17);
            this.startMinimized.TabIndex = 1;
            this.startMinimized.Text = "Start minimized";
            this.startMinimized.UseVisualStyleBackColor = true;
            // 
            // startWithWindows
            // 
            this.startWithWindows.AutoSize = true;
            this.startWithWindows.Location = new System.Drawing.Point(11, 90);
            this.startWithWindows.Name = "startWithWindows";
            this.startWithWindows.Size = new System.Drawing.Size(117, 17);
            this.startWithWindows.TabIndex = 0;
            this.startWithWindows.Text = "Start with Windows";
            this.startWithWindows.UseVisualStyleBackColor = true;
            this.startWithWindows.CheckedChanged += new System.EventHandler(this.startWithWindows_CheckedChanged);
            // 
            // tabPage4
            // 
            this.tabPage4.BackColor = System.Drawing.SystemColors.Control;
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
            this.tabPage4.Location = new System.Drawing.Point(4, 34);
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.Padding = new System.Windows.Forms.Padding(3);
            this.tabPage4.Size = new System.Drawing.Size(474, 271);
            this.tabPage4.TabIndex = 3;
            this.tabPage4.Text = "About program";
            // 
            // linkLabel5
            // 
            this.linkLabel5.AutoSize = true;
            this.linkLabel5.Location = new System.Drawing.Point(195, 198);
            this.linkLabel5.Name = "linkLabel5";
            this.linkLabel5.Size = new System.Drawing.Size(97, 13);
            this.linkLabel5.TabIndex = 12;
            this.linkLabel5.TabStop = true;
            this.linkLabel5.Text = "ePubReader library";
            this.linkLabel5.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel3_LinkClicked);
            // 
            // linkLabel4
            // 
            this.linkLabel4.AutoSize = true;
            this.linkLabel4.Location = new System.Drawing.Point(195, 221);
            this.linkLabel4.Name = "linkLabel4";
            this.linkLabel4.Size = new System.Drawing.Size(86, 13);
            this.linkLabel4.TabIndex = 11;
            this.linkLabel4.TabStop = true;
            this.linkLabel4.Text = "DotNetZip library";
            this.linkLabel4.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel3_LinkClicked);
            // 
            // linkLabel3
            // 
            this.linkLabel3.AutoSize = true;
            this.linkLabel3.Location = new System.Drawing.Point(193, 176);
            this.linkLabel3.Name = "linkLabel3";
            this.linkLabel3.Size = new System.Drawing.Size(267, 13);
            this.linkLabel3.TabIndex = 10;
            this.linkLabel3.TabStop = true;
            this.linkLabel3.Text = "Lord KiRon, author of fb2librarynet library and converter";
            this.linkLabel3.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel3_LinkClicked);
            // 
            // label20
            // 
            this.label20.Location = new System.Drawing.Point(9, 176);
            this.label20.Name = "label20";
            this.label20.Size = new System.Drawing.Size(161, 13);
            this.label20.TabIndex = 9;
            this.label20.Text = "Special thanks:";
            this.label20.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // label19
            // 
            this.label19.AutoSize = true;
            this.label19.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.label19.Location = new System.Drawing.Point(192, 89);
            this.label19.Name = "label19";
            this.label19.Size = new System.Drawing.Size(220, 20);
            this.label19.TabIndex = 8;
            this.label19.Text = "Copyright © 2013, SeNSSoFT";
            // 
            // linkLabel2
            // 
            this.linkLabel2.AutoSize = true;
            this.linkLabel2.Location = new System.Drawing.Point(193, 153);
            this.linkLabel2.Name = "linkLabel2";
            this.linkLabel2.Size = new System.Drawing.Size(184, 13);
            this.linkLabel2.TabIndex = 7;
            this.linkLabel2.TabStop = true;
            this.linkLabel2.Text = "http://tinyopds.codeplex.com/license";
            this.linkLabel2.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            // 
            // label18
            // 
            this.label18.Location = new System.Drawing.Point(11, 153);
            this.label18.Name = "label18";
            this.label18.Size = new System.Drawing.Size(159, 13);
            this.label18.TabIndex = 6;
            this.label18.Text = "Project license:";
            this.label18.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // linkLabel1
            // 
            this.linkLabel1.AutoSize = true;
            this.linkLabel1.Location = new System.Drawing.Point(193, 130);
            this.linkLabel1.Name = "linkLabel1";
            this.linkLabel1.Size = new System.Drawing.Size(151, 13);
            this.linkLabel1.TabIndex = 5;
            this.linkLabel1.TabStop = true;
            this.linkLabel1.Text = "http://tinyopds.codeplex.com/";
            this.linkLabel1.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.linkLabel_LinkClicked);
            // 
            // label17
            // 
            this.label17.Location = new System.Drawing.Point(8, 130);
            this.label17.Name = "label17";
            this.label17.Size = new System.Drawing.Size(162, 13);
            this.label17.TabIndex = 4;
            this.label17.Text = "Project home page:";
            this.label17.TextAlign = System.Drawing.ContentAlignment.TopRight;
            // 
            // appVersion
            // 
            this.appVersion.AutoSize = true;
            this.appVersion.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.appVersion.Location = new System.Drawing.Point(259, 58);
            this.appVersion.Name = "appVersion";
            this.appVersion.Size = new System.Drawing.Size(85, 20);
            this.appVersion.TabIndex = 3;
            this.appVersion.Text = "version 1.0";
            // 
            // appName
            // 
            this.appName.AutoSize = true;
            this.appName.Font = new System.Drawing.Font("Microsoft Sans Serif", 20F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(204)));
            this.appName.Location = new System.Drawing.Point(190, 14);
            this.appName.Name = "appName";
            this.appName.Size = new System.Drawing.Size(226, 31);
            this.appName.TabIndex = 2;
            this.appName.Text = "TinyOPDS server";
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.Location = new System.Drawing.Point(8, 9);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(103, 103);
            this.pictureBox1.TabIndex = 1;
            this.pictureBox1.TabStop = false;
            // 
            // donateButton
            // 
            this.donateButton.Image = global::TinyOPDS.Properties.Resources.donate;
            this.donateButton.Location = new System.Drawing.Point(9, 204);
            this.donateButton.Name = "donateButton";
            this.donateButton.Size = new System.Drawing.Size(157, 56);
            this.donateButton.TabIndex = 0;
            this.donateButton.UseVisualStyleBackColor = true;
            this.donateButton.Click += new System.EventHandler(this.donateButton_Click);
            // 
            // notifyIcon1
            // 
            this.notifyIcon1.ContextMenuStrip = this.contextMenuStrip1;
            this.notifyIcon1.Icon = ((System.Drawing.Icon)(resources.GetObject("notifyIcon1.Icon")));
            this.notifyIcon1.Text = "TinyOPDS";
            this.notifyIcon1.MouseClick += new System.Windows.Forms.MouseEventHandler(this.notifyIcon1_MouseClick);
            // 
            // contextMenuStrip1
            // 
            this.contextMenuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.windowMenuItem,
            this.serverMenuItem,
            this.toolStripMenuItem1,
            this.exitMenuItem});
            this.contextMenuStrip1.Name = "contextMenuStrip1";
            this.contextMenuStrip1.Size = new System.Drawing.Size(145, 76);
            // 
            // windowMenuItem
            // 
            this.windowMenuItem.Name = "windowMenuItem";
            this.windowMenuItem.Size = new System.Drawing.Size(144, 22);
            this.windowMenuItem.Text = "Hide window";
            this.windowMenuItem.Click += new System.EventHandler(this.windowMenuItem_Click);
            // 
            // serverMenuItem
            // 
            this.serverMenuItem.Name = "serverMenuItem";
            this.serverMenuItem.Size = new System.Drawing.Size(144, 22);
            this.serverMenuItem.Text = "Stop server";
            this.serverMenuItem.Click += new System.EventHandler(this.serverButton_Click);
            // 
            // toolStripMenuItem1
            // 
            this.toolStripMenuItem1.Name = "toolStripMenuItem1";
            this.toolStripMenuItem1.Size = new System.Drawing.Size(141, 6);
            // 
            // exitMenuItem
            // 
            this.exitMenuItem.Name = "exitMenuItem";
            this.exitMenuItem.Size = new System.Drawing.Size(144, 22);
            this.exitMenuItem.Text = "Exit";
            this.exitMenuItem.Click += new System.EventHandler(this.exitMenuItem_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(475, 304);
            this.Controls.Add(this.tabControl1);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "TinyOPDS server";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
            this.tabPage4.ResumeLayout(false);
            this.tabPage4.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.contextMenuStrip1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog1;
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
        private System.Windows.Forms.NotifyIcon notifyIcon1;
        private System.Windows.Forms.ContextMenuStrip contextMenuStrip1;
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
    }
}

