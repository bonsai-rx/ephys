using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Globalization;

namespace Bonsai.Ephys.Design
{
    public partial class IntanEvalBoardEditorForm : Form
    {
        public IntanEvalBoardEditorForm()
        {
            InitializeComponent();
        }

        public IntanEvalBoard Source { get; set; }

        private void impedanceTestButton_Click(object sender, EventArgs e)
        {
            int i, channel;
            double[] zFrame = new double[1250];
            double[] zMeasured = new double[16];
            float[,] dataFrame = new float[16, 750];
            UInt16[] auxFrame = new UInt16[750];
            double meanI, meanQ, amplitude;

            using (var connection = Source.Load())
            {
                var usbSource = Source.UsbSource;

                // Enable electrode impedance test mode
                usbSource.ZCheckOn();

                Thread.Sleep(10);

                // Set analog MUX to channel 0
                usbSource.ChannelReset();

                // Wait 10 msec
                Thread.Sleep(10);

                // Start streaming data (all samples will be from channel 0)
                usbSource.Start();

                // Clear plots
                //Rectangle rectBounds = new Rectangle(XPlotOffset, 0, 751, 800);
                //myBuffer.Graphics.FillRectangle(SystemBrushes.Control, rectBounds);

                for (channel = 0; channel < zMeasured.Length; channel++)
                {
                    // Wait for enough data from this channel
                    IntanUsbData plotData;
                    for (i = 0; i < 2; i++)  // read two "dummy" frames to ignore any transients
                    {
                        plotData = usbSource.ReadUsbData();
                    }

                    plotData = usbSource.ReadUsbData();             // now read two real frames to get a total of 1250 data points:
                    plotData.CopyToArray(dataFrame, auxFrame);  // 50 msec = 3 complete cycles of 60 Hz, 50 complete cycles of 1 kHz
                    for (i = 0; i < 750; i++)
                    {
                        zFrame[i] = (double)dataFrame[0, i];
                    }

                    plotData = usbSource.ReadUsbData();
                    plotData.CopyToArray(dataFrame, auxFrame);
                    for (i = 0; i < 250; i++)
                    {
                        zFrame[i + 750] = (double)dataFrame[0, i];
                    }

                    // Go ahead and move on to next channel
                    usbSource.ChannelStep();

                    // Calculate amplitude of 1 kHz component in signal
                    meanI = 0.0;
                    meanQ = 0.0;
                    for (i = 0; i < 1250; i++)
                    {
                        meanI += zFrame[i] * Math.Cos(2.0 * Math.PI * 1000.0 * (double)i * 0.00004);    // 0.00004 = 1/25000 = ADC sample rate
                        meanQ += zFrame[i] * Math.Sin(2.0 * Math.PI * 1000.0 * (double)i * 0.00004);
                    }
                    meanI = meanI / 1250.0;
                    meanQ = meanQ / 1250.0;

                    amplitude = 2.0 * Math.Sqrt(meanI * meanI + meanQ * meanQ);

                    zMeasured[channel] = (amplitude / 0.001) / 1000.0;   // Test current is +/-1 nA; voltage is expressed in uV.
                    // Divide by 1000 to put impedance in units of kOhm.

                    zMeasured[channel] *= 1.06;     // empirical fudge factor
                }

                // Turn off impedance check mode, and stop data transfer
                usbSource.ZCheckOff();
                usbSource.Stop();

                // Display results on screen
                string zText;
                Font objFont = new System.Drawing.Font("Arial", 12, FontStyle.Bold);
                for (channel = 0; channel < zMeasured.Length; channel++)
                {
                    var amplifier = new ListViewItem(channel.ToString(CultureInfo.InvariantCulture));
                    if (zMeasured[channel] < 4.0)
                    {
                        zText = "< 4 k" + '\u03A9';
                        var impedance = amplifier.SubItems.Add(zText);
                        impedance.ForeColor = Color.Black;
                        //myBuffer.Graphics.DrawString(zText, objFont, System.Drawing.Brushes.Black, 70, 50 + (channel * 40));
                    }
                    else if (zMeasured[channel] > 4000.0)
                    {
                        zText = "> 4 M" + '\u03A9';
                        var impedance = amplifier.SubItems.Add(zText);
                        impedance.ForeColor = Color.Red;
                        //myBuffer.Graphics.DrawString(zText, objFont, System.Drawing.Brushes.Red, 70, 50 + (channel * 40));
                    }
                    else if (zMeasured[channel] < 1000.0)
                    {
                        zText = zMeasured[channel].ToString("F00") + " k" + '\u03A9';
                        var impedance = amplifier.SubItems.Add(zText);
                        impedance.ForeColor = Color.RoyalBlue;
                        //myBuffer.Graphics.DrawString(zText, objFont, System.Drawing.Brushes.RoyalBlue, 70, 50 + (channel * 40));
                    }
                    else
                    {
                        double zMOhm = zMeasured[channel] / 1000.0;
                        zText = zMOhm.ToString("F02") + " M" + '\u03A9';
                        var impedance = amplifier.SubItems.Add(zText);
                        impedance.ForeColor = Color.RoyalBlue;
                        //myBuffer.Graphics.DrawString(zText, objFont, System.Drawing.Brushes.RoyalBlue, 70, 50 + (channel * 40));
                    }

                    amplifier.UseItemStyleForSubItems = false;
                    impedanceListView.Items.Add(amplifier);
                }
            }
        }
    }
}
