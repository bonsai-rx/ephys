namespace Bonsai.Ephys.Design
{
    partial class IntanEvalBoardEditorForm
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
            this.impedanceTestButton = new System.Windows.Forms.Button();
            this.impedanceListView = new System.Windows.Forms.ListView();
            this.Amplifier = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.Impedance = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tableLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // impedanceTestButton
            // 
            this.impedanceTestButton.Dock = System.Windows.Forms.DockStyle.Fill;
            this.impedanceTestButton.Location = new System.Drawing.Point(3, 311);
            this.impedanceTestButton.Name = "impedanceTestButton";
            this.impedanceTestButton.Size = new System.Drawing.Size(152, 23);
            this.impedanceTestButton.TabIndex = 0;
            this.impedanceTestButton.Text = "Electrode Impedance Test";
            this.impedanceTestButton.UseVisualStyleBackColor = true;
            this.impedanceTestButton.Click += new System.EventHandler(this.impedanceTestButton_Click);
            // 
            // impedanceListView
            // 
            this.impedanceListView.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.Amplifier,
            this.Impedance});
            this.impedanceListView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.impedanceListView.Location = new System.Drawing.Point(3, 3);
            this.impedanceListView.Name = "impedanceListView";
            this.impedanceListView.Size = new System.Drawing.Size(152, 302);
            this.impedanceListView.TabIndex = 1;
            this.impedanceListView.UseCompatibleStateImageBehavior = false;
            this.impedanceListView.View = System.Windows.Forms.View.Details;
            // 
            // Amplifier
            // 
            this.Amplifier.Text = "Amplifier";
            // 
            // Impedance
            // 
            this.Impedance.Text = "Impedance";
            this.Impedance.Width = 88;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 1;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.Controls.Add(this.impedanceListView, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.impedanceTestButton, 0, 1);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 100F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle());
            this.tableLayoutPanel1.Size = new System.Drawing.Size(158, 337);
            this.tableLayoutPanel1.TabIndex = 2;
            // 
            // IntanEvalBoardEditorForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(158, 337);
            this.Controls.Add(this.tableLayoutPanel1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "IntanEvalBoardEditorForm";
            this.Text = "Impedance Test";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button impedanceTestButton;
        private System.Windows.Forms.ListView impedanceListView;
        private System.Windows.Forms.ColumnHeader Amplifier;
        private System.Windows.Forms.ColumnHeader Impedance;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
    }
}