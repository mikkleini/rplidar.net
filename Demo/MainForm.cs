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
using RPLidar;

namespace Demo
{
    public partial class MainForm : Form
    {
        private readonly Lidar lidar = new Lidar();

        /// <summary>
        /// Constructor
        /// </summary>
        public MainForm()
        {
            InitializeComponent();

            // Fill port list and select first, if any ports available
            comboPort.Items.AddRange(SerialPort.GetPortNames().ToArray());
            if (comboPort.Items.Count > 0)
            {
                comboPort.SelectedIndex = 0;
                buttonOpen.Enabled = true;
            }
            else
            {
                comboPort.Items.Add("No port");
            }

            // Listen for lidar log events
            lidar.OnLog += Lidar_OnLog;
        }

        /// <summary>
        /// Lidar log event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Lidar_OnLog(object sender, LogEventArgs e)
        {
            WriteLog(e.Message, e.Severity);
        }

        /// <summary>
        /// Add message to log window
        /// </summary>
        /// <param name="text"></param>
        /// <param name="severity"></param>
        private void WriteLog(string message, Severity severity)
        {
            textLog.AppendText($"[{severity}] {message}" + Environment.NewLine);
            textLog.ScrollToCaret();
        }

        /// <summary>
        /// (Re)open lidar port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonOpen_Click(object sender, EventArgs e)
        {
            if (comboPort.SelectedIndex >= 0)
            {
                lidar.PortName = (string)comboPort.SelectedItem;

                if (lidar.Open())
                {
                    buttonOpen.Enabled = false;
                    buttonClose.Enabled = true;

                    lidar.StartScan(ScanMode.Legacy);
                }
            }
        }

        /// <summary>
        /// Close lidar port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonClose_Click(object sender, EventArgs e)
        {
            if (lidar.Close())
            {
                buttonOpen.Enabled = true;
                buttonClose.Enabled = false;
            }
        }
    }
}
