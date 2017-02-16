using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using System.IO;
using System.Management;
using Be.Windows.Forms;


namespace SFPzilla {

    public partial class ApplicationForm : Form {

        struct COMDevice {
            public String port;
            public String fullName;

            public COMDevice(String port, String fullName) {
                this.port = port;
                this.fullName = fullName;
            }
        }

        private List<COMDevice> _COMDevices = new List<COMDevice>();
        private List<String> scriptCommands = null;

        private SerialInterface _interface;


        // UI - General.
        public ApplicationForm() {
            InitializeComponent();
            DeviceMonitor.RegisterUsbDeviceNotification(this.Handle);
        }       

        private void Form1_Load(object sender, EventArgs e) {
            _interface = new SerialInterface(this);

            hexViewerA0.ByteProvider = new DynamicByteProvider(new byte[0]);
            hexViewerA2.ByteProvider = new DynamicByteProvider(new byte[0]);
            hexViewerB0.ByteProvider = new DynamicByteProvider(new byte[0]);
            hexViewerB2.ByteProvider = new DynamicByteProvider(new byte[0]);

            PopulateComPorts();

            toolStripProgressBar1.AutoSize = false;
            ResizeProgressBar();

            SplashScreenForm splashScreen = new SplashScreenForm();
            splashScreen.Show();
            splashScreen.FormClosed += SplashScreenFinished;

            this.Text += String.Format(" - Version {0}", Application.ProductVersion);
        }

        protected override void WndProc(ref Message m) {
            base.WndProc(ref m);
            if (m.Msg == DeviceMonitor.WmDevicechange) {
                switch ((int)m.WParam) {
                    case DeviceMonitor.DbtDeviceremovecomplete:
                        DeviceRemoved();
                        break;
                    case DeviceMonitor.DbtDevicearrival:
                        DeviceAdded();
                        break;
                }
            }
        }

        private void SplashScreenFinished(object sender, EventArgs e) {
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e) {
            AboutForm aboutForm = new AboutForm();
            aboutForm.ShowDialog();
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e) {
            SettingsForm settingsForm = new SettingsForm();
            settingsForm.ShowDialog();
        }

        private void ApplicationForm_Resize(object sender, EventArgs e) { ResizeProgressBar(); }
        private void ResizeProgressBar() { toolStripProgressBar1.Width = statusStrip1.Width - 4; }


        // UI - Scripts
        private void toolStripButton4_ButtonClick(object sender, EventArgs e) {
            if (scriptCommands == null) {
                LoadScript();
                RunScript();
            } else {
                RunScript();
            }
        }

        private void loadScriptToolStripMenuItem_Click(object sender, EventArgs e) {
            LoadScript();
        }

        private void LoadScript() {
            OpenFileDialog openDialog = new OpenFileDialog();

            openDialog.Filter = "All Files (*.*)|*.*";
            openDialog.Title = "Open Script File";
            openDialog.FilterIndex = 1;

            if (openDialog.ShowDialog() == DialogResult.OK) {
                scriptCommands = new List<String>(File.ReadAllLines(openDialog.FileName));
                toolStripMenuItem2.Text = "Run script [" + Path.GetFileName(openDialog.FileName) + "]";
                toolStripButton4.Text = toolStripMenuItem2.Text;
            }
        }
        
        private void RunScript() {
            if (scriptCommands == null)
                return;

            richTextBox1.AppendText("> Running script\n");
            foreach (String command in scriptCommands) {
                _interface.ExecuteConsoleCommand(command);
            }
        }


        // UI - File controls.
        private void newToolStripMenuItem_Click(object sender, EventArgs e) {
            // New.
            switch (tabControl1.SelectedIndex) {
                case 0:
                    hexViewerA0.ByteProvider = new DynamicByteProvider(new byte[0]);
                    break;
                case 1:
                    hexViewerA2.ByteProvider = new DynamicByteProvider(new byte[0]);
                    break;
                case 2:
                    hexViewerB0.ByteProvider = new DynamicByteProvider(new byte[0]);
                    break;
                case 3:
                    hexViewerB2.ByteProvider = new DynamicByteProvider(new byte[0]);
                    break;
            }
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e) {
            // Open.
            OpenFileDialog openDialog = new OpenFileDialog();

            openDialog.Filter = "All Files (*.*)|*.*";
            openDialog.FilterIndex = 1;

            if (openDialog.ShowDialog() == DialogResult.OK) {
                byte[] data = File.ReadAllBytes(openDialog.FileName);

                HexBox control;

                switch (tabControl1.SelectedIndex) {
                    default:
                    case 0:
                        control = hexViewerA0;
                        break;
                    case 1:
                        control = hexViewerA2;
                        break;
                    case 2:
                        control = hexViewerB0;
                        break;
                    case 3:
                        control = hexViewerB2;
                        break;
                }

                control.ByteProvider = new DynamicByteProvider(data);
            }
        }


        private void saveToolStripMenuItem_Click(object sender, EventArgs e) {
            // Save.
            SaveFileDialog saveDialog = new SaveFileDialog();
            
            List<byte> data = new List<byte>();
            HexBox control;

            switch (tabControl1.SelectedIndex) {
                default:
                case 0:
                    saveDialog.FileName = "map.a0";
                    saveDialog.Title = "Save As - A0 Data";
                    control = hexViewerA0;
                    break;
                case 1:
                    saveDialog.FileName = "map.a2";
                    saveDialog.Title = "Save As - A2 Data";
                    control = hexViewerA2;
                    break;
                case 2:
                    saveDialog.FileName = "map.b0";
                    saveDialog.Title = "Save As - B0 Data";
                    control = hexViewerB0;
                    break;
                case 3:
                    saveDialog.FileName = "map.b2";
                    saveDialog.Title = "Save As - B2 Data";
                    control = hexViewerB2;
                    break;
            }

            for (int i=0; i<control.ByteProvider.Length; i++) {
                data.Add(control.ByteProvider.ReadByte(i));
            }

            saveDialog.Filter = "All Files (*.*)|*.*";
            
            if (saveDialog.ShowDialog() == DialogResult.OK) {
                try {
                    File.WriteAllBytes(saveDialog.FileName, data.ToArray());
                } catch (Exception ex) {
                    MessageBox.Show(ex.Message, "Error saving file", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e) {
            // Exit.
            Application.Exit();
        }


        // UI - Device selection.
        private void toolStripComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (toolStripComboBox1.Items.Count > 0) {
                _interface.UpdatePortName(_COMDevices[toolStripComboBox1.SelectedIndex].port);
            } else {
                _interface.ClosePort();
            }
        }

        private void toolStripButton3_Click(object sender, EventArgs e) {
            PopulateComPorts();
        }


        // UI - Console.
        private void button1_Click(object sender, EventArgs e) {
            if (textBox2.Text.Length != 0) {
                _interface.ExecuteConsoleCommand(textBox2.Text);
                textBox2.Text = "";
            }
        }

        private void textBox2_KeyDown(object sender, KeyEventArgs e) {
            if (e.KeyData == Keys.Enter) {
                if (textBox2.Text.Length != 0) {
                    _interface.ExecuteConsoleCommand(textBox2.Text);
                    textBox2.Text = "";
                }
            }
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e) {
            richTextBox1.SelectionStart = richTextBox1.TextLength;
            richTextBox1.ScrollToCaret();
        }


        // UI - Power.
        private void onToolStripMenuItem_Click(object sender, EventArgs e) { _interface.Command_PowerOn(); }
        private void offToolStripMenuItem_Click(object sender, EventArgs e) { _interface.Command_PowerOff(); }


        // UI - Reading & writing data.
        private void toolStripButton5_Click(object sender, EventArgs e) {
            // Read.
            switch (tabControl1.SelectedIndex) {
                case 0:
                    _interface.Command_ReadA0();
                    break;
                case 1:
                    _interface.Command_ReadA2();
                    break;
                case 2:
                    _interface.Command_ReadB0();
                    break;
                case 3:
                    _interface.Command_ReadB2();
                    break;
            }
        }
        
        private void readAllToolStripMenuItem_Click(object sender, EventArgs e) {
            _interface.Command_ReadA0();
            _interface.Command_ReadA2();
            _interface.Command_ReadB0();
            _interface.Command_ReadB2();
        }

        private void toolStripButton6_Click(object sender, EventArgs e) {
            // Write.
            switch (tabControl1.SelectedIndex) {
                case 0:
                    _interface.Command_WriteA0();
                    break;
                case 1:
                    _interface.Command_WriteA2();
                    break;
                case 2:
                    _interface.Command_WriteB0();
                    break;
                case 3:
                    _interface.Command_WriteB2();
                    break;
            }          
        }


        private void toolStripButton8_Click(object sender, EventArgs e) {
            // Verify.
            HexBox control;
            switch (tabControl1.SelectedIndex) {
                default:
                case 0:
                    _interface.Command_ReadA0(false);
                    control = hexViewerA0;
                    break;
                case 1:
                    _interface.Command_ReadA2(false);
                    control = hexViewerA2;
                    break;
                case 2:
                    _interface.Command_ReadB0(false);
                    control = hexViewerB0;
                    break;
                case 3:
                    _interface.Command_ReadB2(false);
                    control = hexViewerB2;
                    break;
            }

            byte[] dataRemote = StringToByteArray(_interface._response.filteredData);
            List<byte> dataLocal = new List<byte>();
            for (int i = 0; i < control.ByteProvider.Length; i++) {
                dataLocal.Add(control.ByteProvider.ReadByte(i));
            }

            if (dataLocal.ToArray().SequenceEqual(dataRemote)) {
                MessageBox.Show("Verification successful.\n", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
            } else {
                MessageBox.Show("Verification failed.\nData on device does not match program data.", "Failure", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }


        // Utility.
        private void PopulateComPorts() {
            try {
                _COMDevices.Clear();

                toolStripComboBox1.Items.Clear();

                using (var searcher = new ManagementObjectSearcher
                ("SELECT * FROM WIN32_SerialPort")) {
                    string[] portnames = SerialPort.GetPortNames();
                    var ports = searcher.Get().Cast<ManagementBaseObject>().ToList();
                    var tList = (from n in portnames
                                 join p in ports on n equals p["DeviceID"].ToString()
                                 select n + " - " + p["Caption"]).ToList();

                    int idx = 0;
                    bool matched = false;
                    foreach (String s in tList) {
                        _COMDevices.Add(new COMDevice(s.Remove(s.IndexOf("-")).Trim(), s));
                        toolStripComboBox1.Items.Add(s);
                        if (s.IndexOf("Arduino Uno") > 0 && !matched) {
                            toolStripComboBox1.SelectedIndex = idx;
                            matched = true;
                        }
                        idx++;
                    }

                    if (_COMDevices.Count > 0) {
                        if (!matched)
                            toolStripComboBox1.SelectedIndex = 0;
                    } else {
                        toolStripStatusLabel1.Text = "Select a device to connect to.";
                    }
                }
            } catch (ManagementException e) {
                MessageBox.Show(e.Message);
            }
        }

        private void DeviceAdded() {
           // Do nothing for now.
        }

        private void DeviceRemoved() {
            if (toolStripComboBox1.SelectedIndex < 0)
                return;

            if (toolStripComboBox1.SelectedIndex >= 0 && _COMDevices[toolStripComboBox1.SelectedIndex].fullName.IndexOf("Arduino Uno") >= 0) {
                _interface.ClosePort();
                MessageBox.Show("Device disconnected.", "Connection lost", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                toolStripStatusLabel1.Text = "Select a device to connect to.";
                toolStripComboBox1.Items.RemoveAt(toolStripComboBox1.SelectedIndex);
                toolStripComboBox1.SelectedIndex = -1;
            }
        }


        public byte[] StringToByteArray(String s) {
            List<byte> bytes = new List<byte>();
            foreach (String token in s.Trim().Split(' ')) {
                bytes.Add(Convert.ToByte(token, 16));
            }
            return bytes.ToArray();
        }

        public String ByteArrayToString(byte[] b) {
            return BitConverter.ToString(b).Replace("-", " ").ToUpper();
        }

    }
}
