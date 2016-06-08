using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Kazedan.Construct;

namespace Kazedan.Graphics
{
    partial class KZControl : Form
    {
        private string Path = "";
        private readonly MIDISequencer Sequencer;
        public KZControl(MIDISequencer seq)
        {
            InitializeComponent();
            this.Sequencer = seq;
        }

        private void btnSelect_Click(object sender, EventArgs e)
        {
            var openFileDialog = new OpenFileDialog();
            var result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                Path = openFileDialog.FileName;
                lblFile.Text = Path;
            }
        }

        private void btnLoad_Click(object sender, EventArgs e)
        {
            Sequencer.Stop();
            Sequencer.Reset();
            Sequencer.Load(Path);
        }

        private void btnHelp_Click(object sender, EventArgs e)
        {
            MessageBox.Show("KAZEDAN KEY SHORTCUTS\n" +
                            "SPACE - Start/Stop\n" +
                            "D - Toggle Debug\n" +
                            "UP/DOWN - Change note TTL\n" +
                            "LEFT/RIGHT - Shift keyboard\n");
        }

        private void btnJump_Click(object sender, EventArgs e)
        {
            Sequencer.Jump((int)jumpTick.Value);
        }
    }
}
