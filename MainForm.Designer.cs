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
            this.rootPrefix = new System.Windows.Forms.TextBox();
            this.label13 = new System.Windows.Forms.Label();
            this.extIP = new System.Windows.Forms.Label();
            this.intIP = new System.Windows.Forms.Label();
            this.openPort = new System.Windows.Forms.CheckBox();
            this.extIPlabel = new System.Windows.Forms.Label();
            this.intIPlabel = new System.Windows.Forms.Label();
            this.serverName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.serverPort = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.serverButton = new System.Windows.Forms.Button();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.saveLog = new System.Windows.Forms.CheckBox();
            this.label11 = new System.Windows.Forms.Label();
            this.langCombo = new System.Windows.Forms.ComboBox();
            this.label8 = new System.Windows.Forms.Label();
            this.convertorFolder = new System.Windows.Forms.Button();
            this.convertorPath = new System.Windows.Forms.TextBox();
            this.closeToTray = new System.Windows.Forms.CheckBox();
            this.startMinimized = new System.Windows.Forms.CheckBox();
            this.startWithWindows = new System.Windows.Forms.CheckBox();
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
            this.tabControl1.ItemSize = new System.Drawing.Size(91, 30);
            this.tabControl1.Location = new System.Drawing.Point(-3, -1);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.Size = new System.Drawing.Size(475, 332);
            this.tabControl1.TabIndex = 8;
            // 
            // tabPage1
            // 
            this.tabPage1.BackColor = System.Drawing.SystemColors.Control;
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
            this.tabPage1.Size = new System.Drawing.Size(467, 294);
            this.tabPage1.TabIndex = 0;
            this.tabPage1.Text = "Scanner settings";
            // 
            // duplicates
            // 
            this.duplicates.AutoSize = true;
            this.duplicates.Location = new System.Drawing.Point(122, 198);
            this.duplicates.MinimumSize = new System.Drawing.Size(50, 0);
            this.duplicates.Name = "duplicates";
            this.duplicates.Size = new System.Drawing.Size(50, 13);
            this.duplicates.TabIndex = 29;
            this.duplicates.Text = "0";
            // 
            // label16
            // 
            this.label16.AutoSize = true;
            this.label16.Location = new System.Drawing.Point(14, 198);
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
            this.status.AutoSize = true;
            this.status.Location = new System.Drawing.Point(359, 196);
            this.status.MinimumSize = new System.Drawing.Size(50, 0);
            this.status.Name = "status";
            this.status.Size = new System.Drawing.Size(58, 13);
            this.status.TabIndex = 26;
            this.status.Text = "STOPPED";
            // 
            // label14
            // 
            this.label14.AutoSize = true;
            this.label14.Location = new System.Drawing.Point(252, 196);
            this.label14.Name = "label14";
            this.label14.Size = new System.Drawing.Size(40, 13);
            this.label14.TabIndex = 25;
            this.label14.Text = "Status:";
            // 
            // rate
            // 
            this.rate.AutoSize = true;
            this.rate.Location = new System.Drawing.Point(359, 166);
            this.rate.MinimumSize = new System.Drawing.Size(50, 0);
            this.rate.Name = "rate";
            this.rate.Size = new System.Drawing.Size(66, 13);
            this.rate.TabIndex = 24;
            this.rate.Text = "0 books/min";
            // 
            // label12
            // 
            this.label12.AutoSize = true;
            this.label12.Location = new System.Drawing.Point(252, 166);
            this.label12.Name = "label12";
            this.label12.Size = new System.Drawing.Size(33, 13);
            this.label12.TabIndex = 23;
            this.label12.Text = "Rate:";
            // 
            // elapsedTime
            // 
            this.elapsedTime.AutoSize = true;
            this.elapsedTime.Location = new System.Drawing.Point(359, 136);
            this.elapsedTime.MinimumSize = new System.Drawing.Size(50, 0);
            this.elapsedTime.Name = "elapsedTime";
            this.elapsedTime.Size = new System.Drawing.Size(50, 13);
            this.elapsedTime.TabIndex = 22;
            this.elapsedTime.Text = "00:00:00";
            // 
            // label10
            // 
            this.label10.AutoSize = true;
            this.label10.Location = new System.Drawing.Point(252, 136);
            this.label10.Name = "label10";
            this.label10.Size = new System.Drawing.Size(70, 13);
            this.label10.TabIndex = 21;
            this.label10.Text = "Elapsed time:";
            // 
            // startTime
            // 
            this.startTime.AutoSize = true;
            this.startTime.Location = new System.Drawing.Point(359, 106);
            this.startTime.MinimumSize = new System.Drawing.Size(50, 0);
            this.startTime.Name = "startTime";
            this.startTime.Size = new System.Drawing.Size(50, 13);
            this.startTime.TabIndex = 20;
            this.startTime.Text = "00:00:00";
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(252, 106);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(54, 13);
            this.label6.TabIndex = 19;
            this.label6.Text = "Start time:";
            // 
            // booksProcessed
            // 
            this.booksProcessed.AutoSize = true;
            this.booksProcessed.Location = new System.Drawing.Point(122, 228);
            this.booksProcessed.MinimumSize = new System.Drawing.Size(50, 0);
            this.booksProcessed.Name = "booksProcessed";
            this.booksProcessed.Size = new System.Drawing.Size(50, 13);
            this.booksProcessed.TabIndex = 18;
            this.booksProcessed.Text = "0";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(14, 228);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(92, 13);
            this.label5.TabIndex = 17;
            this.label5.Text = "Books processed:";
            // 
            // invalidBooks
            // 
            this.invalidBooks.AutoSize = true;
            this.invalidBooks.Location = new System.Drawing.Point(122, 138);
            this.invalidBooks.MinimumSize = new System.Drawing.Size(50, 0);
            this.invalidBooks.Name = "invalidBooks";
            this.invalidBooks.Size = new System.Drawing.Size(50, 13);
            this.invalidBooks.TabIndex = 16;
            this.invalidBooks.Text = "0";
            // 
            // label9
            // 
            this.label9.AutoSize = true;
            this.label9.Location = new System.Drawing.Point(14, 138);
            this.label9.Name = "label9";
            this.label9.Size = new System.Drawing.Size(73, 13);
            this.label9.TabIndex = 15;
            this.label9.Text = "Invalid books:";
            // 
            // skippedBooks
            // 
            this.skippedBooks.AutoSize = true;
            this.skippedBooks.Location = new System.Drawing.Point(122, 168);
            this.skippedBooks.MinimumSize = new System.Drawing.Size(50, 0);
            this.skippedBooks.Name = "skippedBooks";
            this.skippedBooks.Size = new System.Drawing.Size(50, 13);
            this.skippedBooks.TabIndex = 14;
            this.skippedBooks.Text = "0";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(14, 168);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(81, 13);
            this.label7.TabIndex = 13;
            this.label7.Text = "Skipped books:";
            // 
            // booksFound
            // 
            this.booksFound.AutoSize = true;
            this.booksFound.Location = new System.Drawing.Point(122, 108);
            this.booksFound.MinimumSize = new System.Drawing.Size(50, 0);
            this.booksFound.Name = "booksFound";
            this.booksFound.Size = new System.Drawing.Size(79, 13);
            this.booksFound.TabIndex = 12;
            this.booksFound.Text = "fb2: 0   epub: 0";
            // 
            // booksInDB
            // 
            this.booksInDB.AutoSize = true;
            this.booksInDB.Location = new System.Drawing.Point(122, 72);
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
            this.folderButton.Location = new System.Drawing.Point(430, 30);
            this.folderButton.Name = "folderButton";
            this.folderButton.Size = new System.Drawing.Size(29, 23);
            this.folderButton.TabIndex = 10;
            this.folderButton.UseVisualStyleBackColor = true;
            this.folderButton.Click += new System.EventHandler(this.folderButton_Click);
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(14, 73);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(101, 13);
            this.label2.TabIndex = 9;
            this.label2.Text = "Books in database: ";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(14, 108);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(70, 13);
            this.label1.TabIndex = 8;
            this.label1.Text = "Books found:";
            // 
            // scannerButton
            // 
            this.scannerButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.scannerButton.Location = new System.Drawing.Point(255, 246);
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
            this.libraryPath.Size = new System.Drawing.Size(409, 20);
            this.libraryPath.TabIndex = 6;
            this.libraryPath.Text = "P:\\My eBooks";
            this.libraryPath.TextChanged += new System.EventHandler(this.libraryPath_TextChanged);
            // 
            // tabPage2
            // 
            this.tabPage2.BackColor = System.Drawing.SystemColors.Control;
            this.tabPage2.Controls.Add(this.rootPrefix);
            this.tabPage2.Controls.Add(this.label13);
            this.tabPage2.Controls.Add(this.extIP);
            this.tabPage2.Controls.Add(this.intIP);
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
            this.tabPage2.Size = new System.Drawing.Size(467, 294);
            this.tabPage2.TabIndex = 1;
            this.tabPage2.Text = "OPDS server settings";
            // 
            // rootPrefix
            // 
            this.rootPrefix.Location = new System.Drawing.Point(22, 90);
            this.rootPrefix.Name = "rootPrefix";
            this.rootPrefix.Size = new System.Drawing.Size(152, 20);
            this.rootPrefix.TabIndex = 19;
            this.rootPrefix.TextChanged += new System.EventHandler(this.rootPrefix_TextChanged);
            // 
            // label13
            // 
            this.label13.AutoSize = true;
            this.label13.Location = new System.Drawing.Point(19, 70);
            this.label13.Name = "label13";
            this.label13.Size = new System.Drawing.Size(127, 13);
            this.label13.TabIndex = 18;
            this.label13.Text = "OPDS root catalog prefix:";
            // 
            // extIP
            // 
            this.extIP.AutoSize = true;
            this.extIP.Location = new System.Drawing.Point(172, 168);
            this.extIP.Name = "extIP";
            this.extIP.Size = new System.Drawing.Size(40, 13);
            this.extIP.TabIndex = 17;
            this.extIP.Text = "- - - - - -";
            // 
            // intIP
            // 
            this.intIP.AutoSize = true;
            this.intIP.Location = new System.Drawing.Point(172, 136);
            this.intIP.Name = "intIP";
            this.intIP.Size = new System.Drawing.Size(40, 13);
            this.intIP.TabIndex = 16;
            this.intIP.Text = "- - - - - -";
            // 
            // openPort
            // 
            this.openPort.AutoSize = true;
            this.openPort.Enabled = false;
            this.openPort.Location = new System.Drawing.Point(284, 90);
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
            this.extIPlabel.Location = new System.Drawing.Point(19, 168);
            this.extIPlabel.Name = "extIPlabel";
            this.extIPlabel.Size = new System.Drawing.Size(95, 13);
            this.extIPlabel.TabIndex = 14;
            this.extIPlabel.Text = "External OPDS url:";
            // 
            // intIPlabel
            // 
            this.intIPlabel.AutoSize = true;
            this.intIPlabel.Location = new System.Drawing.Point(19, 136);
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
            this.serverPort.TextChanged += new System.EventHandler(this.serverPort_TextChanged);
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
            this.serverButton.Location = new System.Drawing.Point(255, 246);
            this.serverButton.Name = "serverButton";
            this.serverButton.Size = new System.Drawing.Size(205, 40);
            this.serverButton.TabIndex = 8;
            this.serverButton.Text = "Start server";
            this.serverButton.Click += new System.EventHandler(this.serverButton_Click);
            // 
            // tabPage3
            // 
            this.tabPage3.BackColor = System.Drawing.SystemColors.Control;
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
            this.tabPage3.Size = new System.Drawing.Size(467, 294);
            this.tabPage3.TabIndex = 2;
            this.tabPage3.Text = "Miscellaneous";
            // 
            // saveLog
            // 
            this.saveLog.AutoSize = true;
            this.saveLog.Location = new System.Drawing.Point(11, 169);
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
            this.label11.Location = new System.Drawing.Point(261, 76);
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
            this.langCombo.Location = new System.Drawing.Point(264, 101);
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
            this.convertorFolder.Location = new System.Drawing.Point(424, 26);
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
            this.convertorPath.Size = new System.Drawing.Size(409, 20);
            this.convertorPath.TabIndex = 28;
            // 
            // closeToTray
            // 
            this.closeToTray.AutoSize = true;
            this.closeToTray.Location = new System.Drawing.Point(11, 137);
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
            this.startMinimized.Location = new System.Drawing.Point(11, 105);
            this.startMinimized.Name = "startMinimized";
            this.startMinimized.Size = new System.Drawing.Size(96, 17);
            this.startMinimized.TabIndex = 1;
            this.startMinimized.Text = "Start minimized";
            this.startMinimized.UseVisualStyleBackColor = true;
            // 
            // startWithWindows
            // 
            this.startWithWindows.AutoSize = true;
            this.startWithWindows.Location = new System.Drawing.Point(11, 72);
            this.startWithWindows.Name = "startWithWindows";
            this.startWithWindows.Size = new System.Drawing.Size(117, 17);
            this.startWithWindows.TabIndex = 0;
            this.startWithWindows.Text = "Start with Windows";
            this.startWithWindows.UseVisualStyleBackColor = true;
            this.startWithWindows.CheckedChanged += new System.EventHandler(this.startWithWindows_CheckedChanged);
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
            this.ClientSize = new System.Drawing.Size(468, 327);
            this.Controls.Add(this.tabControl1);
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Icon = ((System.Drawing.Icon)(resources.GetObject("$this.Icon")));
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "TinyOPDS server";
            this.Activated += new System.EventHandler(this.MainForm_Activated);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.FormClosed += new System.Windows.Forms.FormClosedEventHandler(this.MainForm_FormClosed);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.Resize += new System.EventHandler(this.MainForm_Resize);
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage1.PerformLayout();
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.tabPage3.ResumeLayout(false);
            this.tabPage3.PerformLayout();
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
        private System.Windows.Forms.Label intIP;
        private System.Windows.Forms.Label extIP;
        private System.Windows.Forms.CheckBox saveLog;
        private System.Windows.Forms.Label duplicates;
        private System.Windows.Forms.Label label16;
        private System.Windows.Forms.TextBox rootPrefix;
        private System.Windows.Forms.Label label13;
    }
}

