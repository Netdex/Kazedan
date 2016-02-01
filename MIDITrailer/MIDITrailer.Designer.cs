namespace MIDITrailer
{
    partial class MIDITrailer
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
            this.timer = new System.Windows.Forms.Timer(this.components);
            this.eventTimer = new System.Windows.Forms.Timer(this.components);
            this.paintTimer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // timer
            // 
            this.timer.Enabled = true;
            this.timer.Interval = 9;
            this.timer.Tick += new System.EventHandler(this.timer_Tick);
            // 
            // eventTimer
            // 
            this.eventTimer.Enabled = true;
            this.eventTimer.Interval = 5;
            this.eventTimer.Tick += new System.EventHandler(this.eventTimer_Tick);
            // 
            // paintTimer
            // 
            this.paintTimer.Enabled = true;
            this.paintTimer.Interval = 20;
            this.paintTimer.Tick += new System.EventHandler(this.paintTimer_Tick);
            // 
            // MIDITrailer
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(282, 253);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MIDITrailer";
            this.Text = "MIDITrailer";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MIDITrailer_FormClosing);
            this.Load += new System.EventHandler(this.MIDITrailer_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Timer timer;
        private System.Windows.Forms.Timer eventTimer;
        private System.Windows.Forms.Timer paintTimer;
    }
}

