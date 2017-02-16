using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using System.IO.Ports;
using System.Configuration;
using System.ComponentModel;
using Be.Windows.Forms;

namespace SFPzilla {

 
    struct CommunicationResponse {
        public bool success;
        public String data;

        public CommunicationResponse(bool success, String data) {
            this.success = success;
            this.data = data;
        }

        public String filteredData {
            get {               
                return this.data.Replace("OK\r\n", "").Replace("FAIL\r\n", "");
            }
        }
    }


    struct WriterArgs {
        public HexBox control;
        public String device;

        public WriterArgs(HexBox control, String device) {
            this.control = control;
            this.device = device;
        }
    }



    class SerialInterface {
        private ApplicationForm _form = null;

        private SerialPort _port;

        private Object _mutex = new Object();

        private bool _echoDisabled = false;
        private bool _awaitingResponse = false;
        private int _timeoutCounter = 0;
        public CommunicationResponse _response;
        

        // Interface
        public SerialInterface(ApplicationForm form) {
            _port = new SerialPort();
            _port.BaudRate = 115200;
            _port.ReadTimeout = Properties.Settings.Default.CommunicationTimeout;
            _port.WriteTimeout = Properties.Settings.Default.CommunicationTimeout;
            _port.DtrEnable = true;

            _port.DataReceived += new SerialDataReceivedEventHandler(P_DataReceived);
        
            _form = form;

            _form.dataWriteWorker.DoWork += Worker_DoWrite;
            _form.dataWriteWorker.ProgressChanged += Worker_ProgressChanged;
            _form.dataWriteWorker.RunWorkerCompleted += Worker_Completed;
            _form.dataWriteWorker.WorkerReportsProgress = true;
        }

        public void UpdatePortName(String port) {
            ClosePort();
            _port.PortName = port;
            OpenPort();
                
            _form.toolStripStatusLabel1.Text = "Connected on port " + port + ".";
        }

        public void ClosePort() {
            if (IsPortOpen()) {
                try {
                    _port.Close();
                    _echoDisabled = false;
                } catch (Exception e) {
                    MessageBox.Show("Unable to close serial port (" + e.Message + ").", "Connection failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public void OpenPort() {
            if (!IsPortOpen()) {
                try {
                    _port.Open();
                    _echoDisabled = false;
                } catch (Exception e) {
                    MessageBox.Show("Unable to connect to device (" + e.Message + ").", "Connection failed", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        public bool IsPortOpen() {
            return _port.IsOpen;
        }

        private void VerifyPortStatus() {
            if (!IsPortOpen()) {
                throw new Exception("Device not connected. Select a device from the drop-down menu.");
            }
        }


        // Commands.
        public void ExecuteConsoleCommand(String cmd) {
            if (cmd.Equals("echo off") || cmd.Equals("echo on")) {
                _form.richTextBox1.AppendText("> " + cmd + "\n");
                _form.richTextBox1.AppendText("> Command not available\n");
                return;
            }

            _form.richTextBox1.AppendText("> " + cmd + "\n");

            try {
                ExecuteCommand(cmd);

                if (_response.success) {
                    _form.richTextBox1.AppendText(cmd.Equals("ping") ? "pong\n" : _response.filteredData);
                } else {
                    _form.richTextBox1.AppendText("> Error\n");
                }
            } catch (Exception e) {
                _form.richTextBox1.AppendText("> Error\n");
                ShowCommandErrorMessage(e.Message);
            }
        }

        public void Command_PowerOff() {
            try {
                ExecuteCommand("power off");
            } catch (Exception e) {
                ShowCommandErrorMessage(e.Message);
            }
        }

        public void Command_PowerOn() {
            try {
                ExecuteCommand("power on");
            } catch (Exception e) {
                ShowCommandErrorMessage(e.Message);
            }
        }

        public void Command_ReadA0(bool updateHexViewer = true) {
            try {
                ExecuteCommand("device " + Properties.Settings.Default.DeviceAddressA0);
                ExecuteCommand("offset 00");
                ExecuteCommand("read 00");
                if (updateHexViewer)
                    _form.hexViewerA0.ByteProvider = new DynamicByteProvider(_form.StringToByteArray(_response.filteredData));
            } catch (Exception e) {
                ShowCommandErrorMessage("Unable to read data from A0.\n\nDetails:\n" + e.Message);
            }
        }

        public void Command_ReadA2(bool updateHexViewer = true) {
            try {
                ExecuteCommand("device " + Properties.Settings.Default.DeviceAddressA2);
                ExecuteCommand("offset 00");
                ExecuteCommand("read 00");
                if (updateHexViewer)
                    _form.hexViewerA2.ByteProvider = new DynamicByteProvider(_form.StringToByteArray(_response.filteredData));
            } catch (Exception e) {
                ShowCommandErrorMessage("Unable to read data from A2.\n\nDetails:\n" + e.Message);
            }
        }

        public void Command_ReadB0(bool updateHexViewer = true) {
            try {
                ExecuteCommand("device " + Properties.Settings.Default.DeviceAddressB0);
                ExecuteCommand("offset 00");
                ExecuteCommand("read 00");
                if (updateHexViewer)
                    _form.hexViewerB0.ByteProvider = new DynamicByteProvider(_form.StringToByteArray(_response.filteredData));
            } catch (Exception e) {
                ShowCommandErrorMessage("Unable to read data from B0.\n\nDetails:\n" + e.Message);
            }
        }

        public void Command_ReadB2(bool updateHexViewer = true) {
            try {
                ExecuteCommand("device " + Properties.Settings.Default.DeviceAddressB2);
                ExecuteCommand("offset 00");
                ExecuteCommand("read 00");
                if (updateHexViewer)
                    _form.hexViewerB2.ByteProvider = new DynamicByteProvider(_form.StringToByteArray(_response.filteredData));
            } catch (Exception e) {
                ShowCommandErrorMessage("Unable to read data from B2.\n\nDetails:\n" + e.Message);
            }
        }

        public void Command_WriteA0() {
            WriteToDevice(_form.hexViewerA0, Properties.Settings.Default.DeviceAddressA0);
        }

        public void Command_WriteA2() {
            WriteToDevice(_form.hexViewerA2, Properties.Settings.Default.DeviceAddressA2);
        }

        public void Command_WriteB0() {
            WriteToDevice(_form.hexViewerB0, Properties.Settings.Default.DeviceAddressB0);
        }

        public void Command_WriteB2() {
            WriteToDevice(_form.hexViewerB2, Properties.Settings.Default.DeviceAddressB2);
        }

        private void WriteToDevice(HexBox control, String device) {
            if (control.ByteProvider.Length > 256) {
                ShowCommandErrorMessage("Max data size is 256 bytes.");
                return;
            }

            _form.Enabled = false;
            _form.toolStripProgressBar1.Value = 0;
            _form.dataWriteWorker.RunWorkerAsync(new WriterArgs(control, device));
        }

        private void Worker_DoWrite(object sender, DoWorkEventArgs e) {
            try {
                String device = ((WriterArgs) e.Argument).device;
                HexBox control = ((WriterArgs)e.Argument).control;
                ExecuteCommand("device " + device);

                List<byte> writeBuffer = new List<byte>();

                byte byteCount = 0;
                byte mem_offset = 0;

                ExecuteCommand("offset 00");

                for (int i = 0; i < control.ByteProvider.Length; i++) {
                    writeBuffer.Add(control.ByteProvider.ReadByte(i));
                    byteCount++;
                    if (byteCount >= 16) {
                        mem_offset += byteCount;
                        byteCount = 0;
                        ExecuteCommand("write " + _form.ByteArrayToString(writeBuffer.ToArray()));
                        ExecuteCommand("offset " + _form.ByteArrayToString(new byte[] { mem_offset }));
                        writeBuffer.Clear();

                        _form.dataWriteWorker.ReportProgress((int)Math.Round(((float)i / (float)control.ByteProvider.Length) * 100));
                    }
                }

                _form.dataWriteWorker.ReportProgress(100);
                MessageBox.Show("Wrote " + control.ByteProvider.Length + " bytes of data.", "Write successful", MessageBoxButtons.OK, MessageBoxIcon.Information);

            } catch (Exception ex) {
                ShowCommandErrorMessage("Error writing data.\n\nDetails:\n" + ex.Message);
            }
        }

        private void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            _form.toolStripProgressBar1.Value = Math.Min(e.ProgressPercentage + 2, 100);
            if (_form.toolStripProgressBar1.Value < 100)
                _form.toolStripProgressBar1.Value -= 1;

            if (e.ProgressPercentage == 100) {
                _form.Enabled = true;
            }
        }

        private void Worker_Completed(object sender, RunWorkerCompletedEventArgs e) {
            _form.Enabled = true;
            _form.toolStripProgressBar1.Value = 0;
        }


        private void ExecuteCommand(String cmd) {
            VerifyPortStatus();

            WriteCommand(cmd);

            _awaitingResponse = true;

            // Block main thread until a response is received.
            _response = new CommunicationResponse(false, "");
            _timeoutCounter = 0;
            while (_awaitingResponse && _timeoutCounter < Properties.Settings.Default.CommunicationTimeout) {
                Thread.Sleep(50);
                _timeoutCounter += 50;
            }

            if (_timeoutCounter >= Properties.Settings.Default.CommunicationTimeout) {
                throw new Exception("Operation timed out.\nCommand: " + cmd);
            }

            if (!_response.success) {
                throw new Exception("Error executing command:\n" + cmd);
            }
        }

        private void WriteCommand(String cmd) {
            cmd = cmd.Trim() + '\r';
            byte[] data = Encoding.ASCII.GetBytes(cmd);
            _port.Write(data, 0, data.Length);
            _port.DiscardInBuffer();
        }

        private void ShowCommandErrorMessage(String msg) {
            MessageBox.Show(msg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void P_DataReceived(object sender, SerialDataReceivedEventArgs e) {
            lock (_mutex) {
                if (!_echoDisabled) {
                    _echoDisabled = true;
                    WriteCommand("echo off");
                }

                if (_awaitingResponse) {
                    _response.data += _port.ReadExisting();

                    if (_response.data.IndexOf("OK\r\n") >= 0) {
                        _response.success = true;
                        _awaitingResponse = false;
                    } else if (_response.data.IndexOf("FAIL\r\n") >= 0) {
                        _response.success = false;
                        _awaitingResponse = false;
                    }
                }
            }
        }
    }
}
