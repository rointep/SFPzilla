using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SFPzilla {
    public partial class SplashScreenForm : Form {
        public SplashScreenForm() {
            InitializeComponent();

            timer1.Interval = 16;
            timer1.Start();
        }

        private void timer1_Tick(object sender, EventArgs e) {
            progressBar1.Value = Math.Min(progressBar1.Value + 2, 100);
            if (progressBar1.Value < 100)
                progressBar1.Value -= 1;

            if (progressBar1.Value >= 100) {
                timer1.Stop();
                this.Close();
            }
        }
    }
}
