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

            // Fill scan modes list
            comboMode.Items.AddRange(Enum.GetNames(typeof(ScanMode)));
            comboMode.SelectedIndex = 0;

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
            textLog.AppendText($"{DateTime.Now.ToLocalTime()} [{severity}] {message}" + Environment.NewLine);
            textLog.ScrollToCaret();
        }

        /// <summary>
        /// (Re)open lidar port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonOpen_Click(object sender, EventArgs e)
        {
            // Any port selected ?
            if (comboPort.SelectedIndex < 0) return;

            // Try to open port
            lidar.PortName = (string)comboPort.SelectedItem;
            if (!lidar.Open()) return;

            // Check health
            if (!lidar.GetHealth(out HealthStatus health, out ushort errorCode)) return;
            labelHealth.Text = health.ToString();

            // Health not good ?
            if (health != HealthStatus.Good)
            {
                if (!lidar.Reset()) return;
                WriteLog("Reset done, try open again", Severity.Info);
                return;
            }

            // Start motor and scan
            lidar.ControlMotorDtr(true);
            if (!lidar.StartScan(ScanMode.Legacy)) return;
            
            // Scan started, now poll for results
            timerScan.Enabled = true;            

            // Can't re-open, but can close
            buttonOpen.Enabled = false;
            buttonClose.Enabled = true;

            // Report
            WriteLog("Scanning started", Severity.Info);
        }

        /// <summary>
        /// Close lidar port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonClose_Click(object sender, EventArgs e)
        {
            // Stop scanning
            timerScan.Enabled = false;
            lidar.ControlMotorDtr(false);
            lidar.StopScan();
            lidar.Close();

            // Update status texts
            labelHealth.Text = "-";
            labelSPC.Text = "-";

            // Allow opening again
            buttonOpen.Enabled = true;
            buttonClose.Enabled = false;

            // Report
            WriteLog("Scanning stopped", Severity.Info);
        }

        /// <summary>
        /// Scan timer tick
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimerScan_Tick(object sender, EventArgs e)
        {
            if (lidar.GetScan(out Scan scan))
            {
                if (scan != null)
                {
                    WriteLog("Got scan", Severity.Info);

                    labelSPC.Text = (1000.0f / (float)scan.Duration).ToString("f2");
                }
            }
        }
    }
}
