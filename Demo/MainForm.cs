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
        private float sps = 0.0f; // sps = Scans per second

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
                buttonStart.Enabled = true;
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
            textLog.AppendText($"{DateTime.Now.ToString("HH:MM:ss.fff")} [{severity}] {message}" + Environment.NewLine);
            textLog.ScrollToCaret();
        }

        /// <summary>
        /// Start scan button press
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonStart_Click(object sender, EventArgs e)
        {
            if (!StartScan())
            {
                // Close if something failed
                lidar.Close();
            }
        }

        /// <summary>
        /// Start scanning (at least try)
        /// </summary>
        /// <returns></returns>
        private bool StartScan()
        {
            // Any port selected ?
            if (comboPort.SelectedIndex < 0) return false;

            // Try to open port
            lidar.PortName = (string)comboPort.SelectedItem;
            if (!lidar.Open()) return false;

            // Check health
            if (!lidar.GetHealth(out HealthStatus health, out ushort errorCode)) return false;
            labelHealth.Text = health.ToString();
            if (health != HealthStatus.Good)
            {
                WriteLog($"Health {health}, error code {errorCode}", Severity.Warning);
            }

            // Health not good ?
            if (health != HealthStatus.Good)
            {
                if (!lidar.Reset()) return false;
                WriteLog("Reset done, try open again", Severity.Info);
                return false;
            }

            // Get configuration
            if (!lidar.GetConfiguration(out Configuration config)) return false;
            WriteLog("Configuration:", Severity.Info);
            foreach (KeyValuePair<ushort, ScanModeConfiguration> modeConfig in config.Modes)
            {
                WriteLog($"0x{modeConfig.Key:X4} - {modeConfig.Value}"
                    + (config.Typical == modeConfig.Key ? " (typical)" : string.Empty), Severity.Info);
            }

            // Start motor and scan
            lidar.ControlMotorDtr(true);
            if (!Enum.TryParse<ScanMode>(comboMode.SelectedItem.ToString(), out ScanMode mode)) return false;
            if (!lidar.StartScan(mode)) return false;
            sps = 0.0f;
            
            // Scan started, now poll for results
            timerScan.Enabled = true;

            // Can't re-open, but can close
            comboPort.Enabled = false;
            comboMode.Enabled = false;
            buttonStart.Enabled = false;
            buttonStop.Enabled = true;

            // Report
            WriteLog("Scanning started", Severity.Info);
            return true;
        }

        /// <summary>
        /// Stop scan button press
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonStop_Click(object sender, EventArgs e)
        {
            // Stop scanning
            timerScan.Enabled = false;
            lidar.ControlMotorDtr(false);
            lidar.StopScan();
            lidar.Close();

            // Update status texts
            labelHealth.Text = "-";
            labelSPC.Text = "-";
            labelPPS.Text = "-";

            // Allow opening again
            comboPort.Enabled = true;
            comboMode.Enabled = true;
            buttonStart.Enabled = true;
            buttonStop.Enabled = false;

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
            // Do scan
            if (!lidar.GetScan(out Scan scan))
            {
                // Error, should restart
                return;
            }

            // Got full scan ?
            if (scan != null)
            {
                // Draw scan
                Bitmap bmp = new Bitmap(pictureBox.Width, pictureBox.Height);
                DrawScan(bmp, scan);
                pictureBox.Image = bmp;

                // Calculate scan per second with low pass filtering
                float fScan = 1000.0f / Math.Max(1, scan.Duration);
                if (sps <= float.Epsilon)
                {
                    sps = fScan;
                }
                else
                {
                    sps = (sps + fScan) / 2.0f;
                }

                // Show SpS
                labelSPC.Text = sps.ToString("f2");
                labelPPS.Text = scan.Measurements.Count.ToString();
            }
        }

        /// <summary>
        /// Draw scan
        /// </summary>
        /// <param name="img"></param>
        /// <param name="scan"></param>
        private void DrawScan(Image img, Scan scan)
        {
            Graphics gfx =  Graphics.FromImage(img);
            Point center = new Point(img.Width / 2, img.Height / 2);
            float scale = (Math.Min(img.Height, img.Width) / 2) / (float)trackDisplayRange.Value;
            int pointSize = 2;

            // Clear back and draw grid
            gfx.FillRectangle(SystemBrushes.Window, 0, 0, img.Width, img.Height);
            gfx.DrawLine(Pens.LightGreen, 0, center.Y, img.Width, center.Y);
            gfx.DrawLine(Pens.LightGreen, center.X, 0, center.X, img.Height);

            // Draw ranges
            int rangeStep = Math.Max(1, ((int)(img.Width / scale) / 10));
            for (int range = rangeStep; range <= 50; range += rangeStep)
            {
                float sr = range * scale;
                gfx.DrawEllipse(Pens.LightGreen, center.X - sr, center.Y - sr, sr * 2, sr * 2);
                gfx.DrawString(range.ToString() + "m", SystemFonts.DialogFont, Brushes.LightGreen, center.X + sr + 5, center.Y + 5);
            }

            // Draw measurement points
            foreach (Measurement measurement in scan.Measurements)
            {
                // Skip zero-distance (failed) measurements
                if (measurement.Distance <= float.Epsilon) continue;

                Point p = new Point
                {
                    X = (int)(measurement.Distance * scale * (float)Math.Cos(Math.PI / 180.0f * measurement.Angle)) + center.X,
                    Y = (int)(measurement.Distance * scale * (float)Math.Sin(Math.PI / 180.0f * measurement.Angle)) + center.Y
                };

                gfx.FillEllipse(Brushes.Black, p.X - pointSize / 2, p.Y - pointSize / 2, pointSize, pointSize);
            }
        }
    }
}
