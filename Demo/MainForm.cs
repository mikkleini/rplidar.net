using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
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

            lidar.OnLog += Lidar_OnLog;
        }

        /// <summary>
        /// Lidar log event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Lidar_OnLog(object sender, LogEventArgs e)
        {
            labelStatus.Text = $"[{e.Severity}] {e.Message}";
        }

        /// <summary>
        /// (Re)open lidar port
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ButtonOpen_Click(object sender, EventArgs e)
        {
            lidar.Close();

            lidar.PortName = textPortName.Text;

            if (lidar.Open())
            {
                lidar.StartLegacyScan();
            }
        }

        private void ButtonClose_Click(object sender, EventArgs e)
        {
            lidar.Close();
        }
    }
}
