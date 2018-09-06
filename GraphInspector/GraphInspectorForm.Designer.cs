namespace GraphInspector
{
	public partial class GraphInspectorForm<TVertex, TEdge>
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
			this.SuspendLayout();
			// 
			// GraphInspectorForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(284, 262);
			this.Name = "GraphInspectorForm";
			this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
			this.Text = "Graph Inspector";
			this.Paint += new System.Windows.Forms.PaintEventHandler(this.GraphInspectorForm_Paint);
			this.MouseClick += new System.Windows.Forms.MouseEventHandler(this.GraphInspectorForm_MouseClick);
			this.Resize += new System.EventHandler(this.GraphInspectorForm_Resize);
			this.ResumeLayout(false);

		}

		#endregion
	}
}

