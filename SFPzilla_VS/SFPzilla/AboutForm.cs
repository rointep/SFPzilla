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
    public partial class AboutForm : Form {
        public AboutForm() {
            InitializeComponent();
        }

        private void AboutForm_Load(object sender, EventArgs e) {
            string version = Application.ProductVersion;
            label3.Text = String.Format("Version {0}", version);
        }

        private void button1_Click(object sender, EventArgs e) {
            this.Close();
        }
    }
}
