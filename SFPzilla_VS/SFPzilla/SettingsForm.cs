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
    public partial class SettingsForm : Form {
        public SettingsForm() {
            InitializeComponent();
        }

        private void SettingsForm_Load(object sender, EventArgs e) {
            LoadSettings();
        }


        private void LoadSettings() {
            textBoxA0.Text = Properties.Settings.Default.DeviceAddressA0;
            textBoxA2.Text = Properties.Settings.Default.DeviceAddressA2;
            textBoxB0.Text = Properties.Settings.Default.DeviceAddressB0;
            textBoxB2.Text = Properties.Settings.Default.DeviceAddressB2;
        }

        private void SaveSettings() {
            Properties.Settings.Default.DeviceAddressA0 = textBoxA0.Text;
            Properties.Settings.Default.DeviceAddressA2 = textBoxA2.Text;
            Properties.Settings.Default.DeviceAddressB0 = textBoxB0.Text;
            Properties.Settings.Default.DeviceAddressB2 = textBoxB2.Text;
            Properties.Settings.Default.Save();
        }

        private void button1_Click(object sender, EventArgs e) {
            SaveSettings();
            this.Close();
        }

        private void button2_Click(object sender, EventArgs e) {
            this.Close();
        }
    }
}
