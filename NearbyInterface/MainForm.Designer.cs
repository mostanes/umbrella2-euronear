using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Umbrella2.Pipeline.ViaNearby
{
	public partial class MainForm : Form
	{
		private MenuStrip menuStrip1;
		private ToolStripMenuItem selectFoldersToolStripMenuItem;
		private ToolStripMenuItem inputToolStripMenuItem;
		private ToolStripMenuItem outputToolStripMenuItem;
		private ToolStripMenuItem changeSettingsToolStripMenuItem;
		private Panel panel1;
		private Button button1;
		private Label label5;
		private Label label4;
		private Label label3;
		private Label label2;
		private TextBox textBox3;
		private TextBox textBox2;
		private TextBox textBox1;
		private Label label1;
		private System.IO.FileSystemWatcher fileSystemWatcher1;
		private FolderBrowserDialog folderBrowserDialog1;
		private RichTextBox richTextBox1;

		private void InitializeComponent()
		{
			this.menuStrip1 = new System.Windows.Forms.MenuStrip();
			this.selectFoldersToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.inputToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.outputToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.changeSettingsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
			this.panel1 = new System.Windows.Forms.Panel();
			this.button1 = new System.Windows.Forms.Button();
			this.label5 = new System.Windows.Forms.Label();
			this.label4 = new System.Windows.Forms.Label();
			this.label3 = new System.Windows.Forms.Label();
			this.label2 = new System.Windows.Forms.Label();
			this.textBox3 = new System.Windows.Forms.TextBox();
			this.textBox2 = new System.Windows.Forms.TextBox();
			this.textBox1 = new System.Windows.Forms.TextBox();
			this.label1 = new System.Windows.Forms.Label();
			this.richTextBox1 = new System.Windows.Forms.RichTextBox();
			this.fileSystemWatcher1 = new System.IO.FileSystemWatcher();
			this.folderBrowserDialog1 = new System.Windows.Forms.FolderBrowserDialog();
			this.menuStrip1.SuspendLayout();
			this.panel1.SuspendLayout();
			((System.ComponentModel.ISupportInitialize) (this.fileSystemWatcher1)).BeginInit();
			this.SuspendLayout();
			// 
			// menuStrip1
			// 
			this.menuStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
			this.selectFoldersToolStripMenuItem,
			this.changeSettingsToolStripMenuItem});
			this.menuStrip1.Location = new System.Drawing.Point(0, 0);
			this.menuStrip1.Name = "menuStrip1";
			this.menuStrip1.Size = new System.Drawing.Size(434, 24);
			this.menuStrip1.TabIndex = 0;
			this.menuStrip1.Text = "menuStrip1";
			// 
			// selectFoldersToolStripMenuItem
			// 
			this.selectFoldersToolStripMenuItem.DropDownItems.AddRange(new System.Windows.Forms.ToolStripItem[] {
			this.inputToolStripMenuItem,
			this.outputToolStripMenuItem});
			this.selectFoldersToolStripMenuItem.Name = "selectFoldersToolStripMenuItem";
			this.selectFoldersToolStripMenuItem.Size = new System.Drawing.Size(84, 20);
			this.selectFoldersToolStripMenuItem.Text = "Select folders";
			// 
			// inputToolStripMenuItem
			// 
			this.inputToolStripMenuItem.Name = "inputToolStripMenuItem";
			this.inputToolStripMenuItem.Size = new System.Drawing.Size(108, 22);
			this.inputToolStripMenuItem.Text = "Input";
			// 
			// outputToolStripMenuItem
			// 
			this.outputToolStripMenuItem.Name = "outputToolStripMenuItem";
			this.outputToolStripMenuItem.Size = new System.Drawing.Size(108, 22);
			this.outputToolStripMenuItem.Text = "Output";
			// 
			// changeSettingsToolStripMenuItem
			// 
			this.changeSettingsToolStripMenuItem.Name = "changeSettingsToolStripMenuItem";
			this.changeSettingsToolStripMenuItem.Size = new System.Drawing.Size(97, 20);
			this.changeSettingsToolStripMenuItem.Text = "Change settings";
			this.changeSettingsToolStripMenuItem.Click += new System.EventHandler(this.changeSettingsToolStripMenuItem_Click);
			// 
			// panel1
			// 
			this.panel1.Controls.Add(this.button1);
			this.panel1.Controls.Add(this.label5);
			this.panel1.Controls.Add(this.label4);
			this.panel1.Controls.Add(this.label3);
			this.panel1.Controls.Add(this.label2);
			this.panel1.Controls.Add(this.textBox3);
			this.panel1.Controls.Add(this.textBox2);
			this.panel1.Controls.Add(this.textBox1);
			this.panel1.Controls.Add(this.label1);
			this.panel1.Dock = System.Windows.Forms.DockStyle.Top;
			this.panel1.Location = new System.Drawing.Point(0, 24);
			this.panel1.Name = "panel1";
			this.panel1.Size = new System.Drawing.Size(434, 93);
			this.panel1.TabIndex = 1;
			// 
			// button1
			// 
			this.button1.Location = new System.Drawing.Point(288, 56);
			this.button1.Name = "button1";
			this.button1.Size = new System.Drawing.Size(134, 23);
			this.button1.TabIndex = 8;
			this.button1.Text = "Run Pipeline";
			this.button1.UseVisualStyleBackColor = true;
			this.button1.Click += new System.EventHandler(this.button1_Click);
			// 
			// label5
			// 
			this.label5.AutoSize = true;
			this.label5.Location = new System.Drawing.Point(12, 61);
			this.label5.Name = "label5";
			this.label5.Size = new System.Drawing.Size(68, 13);
			this.label5.TabIndex = 7;
			this.label5.Text = "Output folder";
			// 
			// label4
			// 
			this.label4.AutoSize = true;
			this.label4.Location = new System.Drawing.Point(12, 34);
			this.label4.Name = "label4";
			this.label4.Size = new System.Drawing.Size(60, 13);
			this.label4.TabIndex = 6;
			this.label4.Text = "Input folder";
			// 
			// label3
			// 
			this.label3.AutoSize = true;
			this.label3.Location = new System.Drawing.Point(288, 20);
			this.label3.Name = "label3";
			this.label3.Size = new System.Drawing.Size(70, 13);
			this.label3.TabIndex = 5;
			this.label3.Text = "Field number:";
			// 
			// label2
			// 
			this.label2.AutoSize = true;
			this.label2.Location = new System.Drawing.Point(288, 7);
			this.label2.Name = "label2";
			this.label2.Size = new System.Drawing.Size(35, 13);
			this.label2.TabIndex = 4;
			this.label2.Text = "Night:";
			// 
			// textBox3
			// 
			this.textBox3.Location = new System.Drawing.Point(86, 58);
			this.textBox3.Name = "textBox3";
			this.textBox3.Size = new System.Drawing.Size(196, 20);
			this.textBox3.TabIndex = 3;
			// 
			// textBox2
			// 
			this.textBox2.Location = new System.Drawing.Point(86, 31);
			this.textBox2.Name = "textBox2";
			this.textBox2.Size = new System.Drawing.Size(196, 20);
			this.textBox2.TabIndex = 2;
			// 
			// textBox1
			// 
			this.textBox1.Location = new System.Drawing.Point(86, 4);
			this.textBox1.Name = "textBox1";
			this.textBox1.Size = new System.Drawing.Size(100, 20);
			this.textBox1.TabIndex = 1;
			this.textBox1.Validating += new System.ComponentModel.CancelEventHandler(this.textBox1_Validating);
			// 
			// label1
			// 
			this.label1.AutoSize = true;
			this.label1.Location = new System.Drawing.Point(12, 7);
			this.label1.Name = "label1";
			this.label1.Size = new System.Drawing.Size(60, 13);
			this.label1.TabIndex = 0;
			this.label1.Text = "Field Name";
			// 
			// richTextBox1
			// 
			this.richTextBox1.Dock = System.Windows.Forms.DockStyle.Fill;
			this.richTextBox1.Font = new System.Drawing.Font("Courier New", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte) (0)));
			this.richTextBox1.Location = new System.Drawing.Point(0, 117);
			this.richTextBox1.Name = "richTextBox1";
			this.richTextBox1.ReadOnly = true;
			this.richTextBox1.Size = new System.Drawing.Size(434, 291);
			this.richTextBox1.TabIndex = 2;
			this.richTextBox1.Text = "";
			// 
			// fileSystemWatcher1
			// 
			this.fileSystemWatcher1.EnableRaisingEvents = true;
			this.fileSystemWatcher1.SynchronizingObject = this;
			// 
			// MainForm
			// 
			this.ClientSize = new System.Drawing.Size(434, 408);
			this.Controls.Add(this.richTextBox1);
			this.Controls.Add(this.panel1);
			this.Controls.Add(this.menuStrip1);
			this.MainMenuStrip = this.menuStrip1;
			this.Name = "MainForm";
			this.Text = "Umbrella2 NEARBY Input";
			this.Load += new System.EventHandler(this.MainForm_Load);
			this.menuStrip1.ResumeLayout(false);
			this.menuStrip1.PerformLayout();
			this.panel1.ResumeLayout(false);
			this.panel1.PerformLayout();
			((System.ComponentModel.ISupportInitialize) (this.fileSystemWatcher1)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}
	}
}
