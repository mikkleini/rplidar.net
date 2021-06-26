using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO.Ports;
using RPLidar;
using Microsoft.Extensions.Logging;
using NLog.Config;
using NLog.Windows.Forms;
using NLog.Extensions.Logging;
using LogLevel = NLog.LogLevel;

namespace Demo
{
    /// <summary>
    /// Main form
    /// </summary>
    public partial class MainForm : Form
    {
        private ILogger logger;
        private readonly Lidar lidar = new Lidar();
        private delegate void UpdateScanDelegate(Scan scan);
        private CancellationTokenSource cancellationSource;
        private Task lidarTask;

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

            // Setup other things
            comboIsFlipped.SelectedIndex = 0;
            textAngleOffset.Text = "0";
        }

        /// <summary>
        /// Form loading
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void MainForm_Load(object sender, EventArgs e)
        {
            // Setup form textbox as logging target
            // Solution from:
            // https://github.com/NLog/NLog/issues/133#issuecomment-136164391

            RichTextBoxTarget loggerTextBoxTarget = new RichTextBoxTarget
            {
                Name = "LogBox",
                FormName = Name,
                ControlName = logBox.Name,
                Layout = "${date:format=HH\\:mm\\:ss.fff} [${logger}] ${message} ${exception:format=Message}",
                AutoScroll = true
            };

            LoggingConfiguration logConfig = new LoggingConfiguration();
            logConfig.AddTarget(loggerTextBoxTarget.Name, loggerTextBoxTarget);
            logConfig.AddRule(LogLevel.Debug, LogLevel.Fatal, loggerTextBoxTarget);

            // Create MS logger
            using (ILoggerFactory loggerFactory = LoggerFactory.Create(builder => builder.AddNLog(logConfig)))
            {
                logger = loggerFactory.CreateLogger("Demo");
                lidar.Logger = loggerFactory.CreateLogger("Lidar");
            }
        }

        /// <summary>
        /// Start scan button press
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonStart_Click(object sender, EventArgs e)
        {
            // Parse mode
            if (!Enum.TryParse(comboMode.SelectedItem.ToString(), out ScanMode mode))
            {
                logger.LogError($"Invalid scan mode: {comboMode.SelectedItem.ToString()}");
                return;
            }

            // Decide port
            if (comboPort.SelectedIndex < 0)
            {
                logger.LogError($"No port selected");
                return;
            }
            lidar.PortName = (string)comboPort.SelectedItem;

            // Set timeout high enough to allow slow speed scanning
            lidar.ReceiveTimeout = 3000;

            // Flipped ?
            lidar.IsFlipped = comboIsFlipped.SelectedIndex == 1;

            // Try to parse angle offset
            if (float.TryParse(textAngleOffset.Text, out float angleOffset))
            {
                lidar.AngleOffset = angleOffset;
            }
            else
            {
                logger.LogWarning("Invalid angle offset, using zero.");
                lidar.AngleOffset = 0.0f;
            }

            // Try to open port
            if (lidar.Open())
            {
                // Allow stopping
                comboPort.Enabled = false;
                comboMode.Enabled = false;
                buttonStart.Enabled = false;
                buttonStop.Enabled = true;

                // Start scan task
                cancellationSource = new CancellationTokenSource();
                lidarTask = Task.Run(() => Scan(mode, cancellationSource.Token));
            }
        }

        /// <summary>
        /// Stop scan button press
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonStop_Click(object sender, EventArgs e)
        {
            // Cancel scanning
            if (cancellationSource != null)
            {
                cancellationSource.Cancel();
                cancellationSource.Dispose();
            }
            if (lidarTask != null)
            {
                lidarTask.GetAwaiter().GetResult();
            }

            // Close port
            lidar.Close();

            // Reset status texts
            labelSPC.Text = "-";
            labelPPS.Text = "-";

            // Allow starting again
            comboPort.Enabled = true;
            comboMode.Enabled = true;
            buttonStart.Enabled = true;
            buttonStop.Enabled = false;
        }

        /// <summary>
        /// Scan task
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        private void Scan(ScanMode mode, CancellationToken cancellationToken)
        {
            // Main loop
            while (!cancellationToken.IsCancellationRequested)
            {
                // Try to start lidar
                if (!StartLidar(mode))
                {
                    // Reset and try to start again in a while to avoid high CPU load if something breaks
                    lidar.Reset();
                    Thread.Sleep(1000);
                    continue;
                }

                // Run lidar
                if (!RunLidar(cancellationToken))
                {
                    // Reset and try to start again
                    lidar.Reset();
                    continue;
                }
            }

            // Stop lidar
            StopLidar();
        }

        /// <summary>
        /// Start lidar (at least try)
        /// </summary>
        /// <param name="mode">Scan mode</param>
        /// <returns>true if succeeded, false if not</returns>
        private bool StartLidar(ScanMode mode)
        {
            // Get health
            HealthInfo health = lidar.GetHealth();
            if (health == null)
            {
                return false;
            }

            // Good health ?
            if (health.Status != HealthStatus.Good)
            {
                logger.LogWarning($"Health {health.Status}, error code {health.ErrorCode}.");
                return false;
            }

            // Good health
            logger.LogInformation($"Health good.");

            // Get configuration
            Configuration config = lidar.GetConfiguration();
            if (config == null)
            {
                return false;
            }

            // Show configuration
            logger.LogInformation("Configuration:");
            foreach (KeyValuePair<ushort, ScanModeConfiguration> modeConfig in config.Modes)
            {
                logger.LogInformation($"0x{modeConfig.Key:X4} - {modeConfig.Value}"
                    + (config.Typical == modeConfig.Key ? " (typical)" : string.Empty));
            }

            // Start motor
            lidar.ControlMotorDtr(false);

            // Start scanning
            if (!lidar.StartScan(mode))
            {
                return false;
            }

            // Report
            logger.LogInformation("Scanning started.");

            return true;
        }

        /// <summary>
        /// Stop lidar
        /// </summary>
        private void StopLidar()
        {
            // Stop scanning
            lidar.StopScan();
            lidar.ControlMotorDtr(true);

            // Report
            logger.LogInformation("Scanning stopped");
        }

        /// <summary>
        /// Run lidar task
        /// </summary>
        /// <param name="cancellationToken"></param>
        private bool RunLidar(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                // Try to get scan
                Scan scan = lidar.GetScan(cancellationToken);
                if (scan == null)
                {
                    // It was either cancellation or error
                    return cancellationToken.IsCancellationRequested;
                }

                // Display it
                BeginInvoke(new UpdateScanDelegate(UpdateScan), new object[] { scan });
            }

            // Normal exit
            return true;
        }

        /// <summary>
        /// Update scan
        /// </summary>
        /// <param name="scan">Scan object</param>
        private void UpdateScan(Scan scan)
        {
            // Draw scan image
            Bitmap bmp = new Bitmap(pictureBox.Width, pictureBox.Height);
            DrawScan(bmp, scan);
            pictureBox.Image = bmp;

            // Show stats
            labelSPC.Text = scan.ScanRate.ToString("f2");
            labelPPS.Text = scan.Measurements.Count.ToString();
        }

        /// <summary>
        /// Draw scan image
        /// </summary>
        /// <param name="img">Image to draw</param>
        /// <param name="scan">Scan object</param>
        private void DrawScan(Image img, Scan scan)
        {
            Graphics gfx = Graphics.FromImage(img);
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
