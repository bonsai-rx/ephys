/*
 * Intan Amplifier Demo for use with RHA2000-EVAL Board and RHA2000 Series Amplifier Chips
 * Copyright (c) 2010-2011 Intan Technologies, LLC  http://www.intantech.com
 * 
 * Modifications to integrate the Bonsai.Ephys package
 * Copyright (c) 2012 Gonçalo C. Lopes
 * 
 * This software is provided 'as-is', without any express or implied 
 * warranty.  In no event will the authors be held liable for any damages 
 * arising from the use of this software. 
 * 
 * Permission is granted to anyone to use this software for any applications that use
 * Intan Technologies integrated circuits, and to alter it and redistribute it freely,
 * subject to the following restrictions: 
 * 
 * 1. The application must require the use of Intan Technologies integrated circuits.
 *
 * 2. The origin of this software must not be misrepresented; you must not 
 *    claim that you wrote the original software. If you use this software 
 *    in a product, an acknowledgment in the product documentation is required.
 * 
 * 3. Altered source versions must be plainly marked as such, and must not be 
 *    misrepresented as being the original software.
 * 
 * 4. This notice may not be removed or altered from any source distribution.
 * 
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.IO;
using System.Media;
using FTD2XX_NET;


namespace Bonsai.Ephys
{
    /// <summary>
    /// This class provides access to and control of an RHA2000-EVAL board connected
    /// to a USB port.
    /// </summary>
    public class IntanUsbSource
    {
        // private variables

        private FTDI myFtdiDeviceA;
        private float[,] dataRaw = new float[16, 750];
        private UInt16[] auxFrame = new UInt16[750];
        private float[,] dataFrame = new float[16, 750];
        private float[,] dataNotch = new float[16, 750];
        private float[] dataState = new float[16];

        private float[] dataDelay1 = new float[16];
        private float[] dataDelay2 = new float[16];
        private float[] notchDelay1 = new float[16];
        private float[] notchDelay2 = new float[16];

        private byte[] readDataBufferA = new byte[36000];  // USB data read buffer
        private byte[] resyncDataBufferA = new byte[36000];  // probably only need 48 bytes maximum, but let's be safe
        private bool dataJustStarted = true;
        private int resyncCount = 1;
        private bool running = false;

        private bool enableHPF;
        private double fHPF;
        private bool enableNotch;
        private double fNotch;


        // properties

        /// <summary>
        /// Is software high-pass filter enabled?
        /// </summary>
        public bool EnableHPF
        {
            get { return enableHPF; }
            set { enableHPF = value; }
        }

        /// <summary>
        /// Cutoff frequency of software high-pass filter
        /// </summary>
        public double FHPF
        {
            get { return fHPF; }
            set { fHPF = value; }
        }

        /// <summary>
        /// Is software notch filter enabled?
        /// </summary>
        public bool EnableNotch
        {
            get { return enableNotch; }
            set { enableNotch = value; }
        }

        /// <summary>
        /// Frequency of software notch filter
        /// </summary>
        public double FNotch
        {
            get { return fNotch; }
            set { fNotch = value; }
        }

        // public methods

        /// <summary>
        /// Attempt to open RHA2000-EVAL board connected to USB port.
        /// </summary>
        /// <param name="firmwareID1">Board ID number (1 of 3)</param>
        /// <param name="firmwareID2">Board ID number (2 of 3)</param>
        /// <param name="firmwareID3">Board ID number (3 of 3)</param>
        public void Open(ref int firmwareID1, ref int firmwareID2, ref int firmwareID3)
        {
            // Open FTDI USB device.
            UInt32 ftdiDeviceCount = 0;
            FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;

            // Create new instance of the FTDI device class.
            myFtdiDeviceA = new FTDI();

            // Determine the number of FTDI devices connected to the machine.
            ftStatus = myFtdiDeviceA.GetNumberOfDevices(ref ftdiDeviceCount);

            // Check status.
            if (!(ftStatus == FTDI.FT_STATUS.FT_OK))
            {
                IntanUsbException e = new IntanUsbException("USB Setup Error: Failed to get number devices");
                throw e;
            }

            // If no devices available, return.
            if (ftdiDeviceCount == 0)
            {
                IntanUsbException e = new IntanUsbException("No valid USB devices detected");
                throw e;
            }

            // Allocate storage for device info list.
            FTDI.FT_DEVICE_INFO_NODE[] ftdiDeviceList = new FTDI.FT_DEVICE_INFO_NODE[ftdiDeviceCount];

            // Populate our device list.
            ftStatus = myFtdiDeviceA.GetDeviceList(ftdiDeviceList);
            // There could be a status error here, but we're not checking for it...


            // The Intan Technologies RHA2000-EVAL board uses an FTDI FT2232H chip to provide a USB
            // interface to a PC.  Detailed information on this chip may be found at:
            //
            //   http://www.ftdichip.com/Products/ICs/FT2232H.htm
            //
            // The FT2232H supports two independent FIFO channels.  The channel used by the RHA2000-EVAL
            // board is factory-configured with the name "Intan I/O board 1.0 A".  (FTDI provides software
            // routines to open device by its name.)

            ftStatus = myFtdiDeviceA.OpenByDescription("Intan I/O Board 1.0 A");
            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                IntanUsbException e = new IntanUsbException("Intan USB device A not found");
                throw e;
            }
                       
            // Set read timeout to 5 seconds, write timeout to infinite
            ftStatus = myFtdiDeviceA.SetTimeouts(5000, 0);
            // There could be status error here, but we're not checking for it...

            this.Stop();

            // Purge receive buffer

            myFtdiDeviceA.Purge(FTDI.FT_PURGE.FT_PURGE_RX);

            // Check board ID and version number
            //
            // The RHA2000-EVAL board is controlled by sending one-byte ASCII command characters over
            // the USB interface.  The 'I' character commands the board to return a 3-byte ID/version
            // number.

            UInt32 numBytesWritten = 0;
            Byte[] myChars = { 73 };   // 'I' = request board ID and version number

            ftStatus = myFtdiDeviceA.Write(myChars, 1, ref numBytesWritten);

            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                IntanUsbException e = new IntanUsbException("Could not write to Intan USB device");
                throw e;
            }

            const UInt32 numBytesToRead = 3;

            // Wait until the desired number of bytes have been received.
            UInt32 numBytesAvailable = 0;

            while (numBytesAvailable < numBytesToRead)
            {
                ftStatus = myFtdiDeviceA.GetRxBytesAvailable(ref numBytesAvailable);

                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    IntanUsbException e = new IntanUsbException("Failed to get number of USB bytes available to read");
                    throw e;
                }
            }

            // Now that we have the amount of data we want available, read it.

            UInt32 numBytesRead = 0;

            ftStatus = myFtdiDeviceA.Read(readDataBufferA, numBytesToRead, ref numBytesRead);

            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                IntanUsbException e = new IntanUsbException("USB read error");
                throw e;
            }

            firmwareID1 = Convert.ToInt32(readDataBufferA[0]);
            firmwareID2 = Convert.ToInt32(readDataBufferA[1]);
            firmwareID3 = Convert.ToInt32(readDataBufferA[2]);

            Debug.WriteLine("Board ID: " + readDataBufferA[0] + " " + (Convert.ToInt32(readDataBufferA[1])).ToString() + " " + (Convert.ToInt32(readDataBufferA[2])).ToString());

            this.ZCheckOff();
            this.SettleOff();

            // Purge receive buffer.
            myFtdiDeviceA.Purge(FTDI.FT_PURGE.FT_PURGE_RX);

            dataJustStarted = true;

        }

        /// <summary>
        /// Command RHA2000-EVAL board to start streaming amplifier data to PC.
        /// </summary>
        public void Start()
        {
            FTDI.FT_STATUS ftStatus;

            // Purge receive buffer
            myFtdiDeviceA.Purge(FTDI.FT_PURGE.FT_PURGE_RX);

            // The RHA2000-EVAL board is controlled by sending one-byte ASCII command characters over
            // the USB interface.  The 'S' character commands the board to start streaming amplifier
            // data to the PC.

            UInt32 numBytesWritten = 0;
            Byte[] myChars = { 83 };   // 'S' = start data transfer

            ftStatus = myFtdiDeviceA.Write(myChars, 1, ref numBytesWritten);

            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                IntanUsbException e = new IntanUsbException("Could not write to Intan USB device");
                throw e;
            }

            // Resync to channel 0.
            this.ResyncA();

            running = true;
            dataJustStarted = true;
        }

        /// <summary>
        /// Command RHA2000-EVAL board to stop streaming amplifier data to PC.
        /// </summary>
        public void Stop()
        {
            // The RHA2000-EVAL board is controlled by sending one-byte ASCII command characters over
            // the USB interface.  The 's' character commands the board to stop streaming amplifier
            // data to the PC.

            UInt32 numBytesWritten = 0;
            Byte[] myChars = { 115 };  // 's' = stop data transfer
            FTDI.FT_STATUS ftStatus;

            ftStatus = myFtdiDeviceA.Write(myChars, 1, ref numBytesWritten);

            // Purge receive buffer.
            myFtdiDeviceA.Purge(FTDI.FT_PURGE.FT_PURGE_RX);

            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                IntanUsbException e = new IntanUsbException("Could not write to Intan USB device");
                throw e;
            }

            running = false;
        }

        /// <summary>
        /// Close USB connection to RHA2000-EVAL board.
        /// </summary>
        public void Close()
        {
            myFtdiDeviceA.Close();
        }

        /// <summary>
        /// Check to see if there is at least 30 msec worth of data in the USB read buffer.
        /// </summary>
        /// <param name="plotQueue">Queue used for plotting data to screen.</param>
        /// <param name="saveQueue">Queue used for saving data to disk.</param>
        public IntanUsbData CheckForUsbData()
        {
            // Note: Users must call CheckForUsbData periodically during time-consuming operations (like updating graphics)
            // to make sure the USB read buffer doesn't overflow!

            FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;
            const UInt32 numBytesToRead = 36000; // 36000 / (3 bytes/sample x 16 channels x 25 kS/s) = 30 msec of data
            // It seems that the FTDI read buffer is slightly less than 64000 bytes
            // in size, so we must read from the buffer before it overflows.  However,
            // if we read a small number of bytes at a time, data transfer is slow.
            // Empirically, waiting for 36000 bytes seems to work well.

            // Wait until at least 30 msec of amplifier data have been received.
            //ftStatus = myFtdiDeviceA.GetRxBytesAvailable(ref numBytesAvailableA);

            //if (ftStatus != FTDI.FT_STATUS.FT_OK)
            //{
            //    UsbException e = new UsbException("Failed to get number of USB bytes available to read");
            //    throw e;
            //}
            //if (numBytesAvailableA >= numBytesToRead) haveEnoughData = true;

            // Now that we have the amount of data we want available, read it.

            UInt32 numBytesReadA = 0;

            ftStatus = myFtdiDeviceA.Read(readDataBufferA, numBytesToRead, ref numBytesReadA);
            if (numBytesReadA != numBytesToRead)
            {
                throw new IntanUsbException("Data out of sync!!");
            }

            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                IntanUsbException e = new IntanUsbException("USB read error");
                throw e;
            }

            // The following section of code reads and parses a block of data from the RHA2000-EVAL board.
            // A complete sequence of single A/D samples from all 16 amplifiers uses 16 x 3 = 48 bytes in
            // the following three-byte format (MSB is on the left; LSB is on the right):
            //
            // Byte 1:    1   ADC6   ADC5   ADC4   ADC3   ADC2  ADC1   ADC0
            // Byte 2:    1   ADC13  ADC12  ADC11  ADC10  ADC9  ADC8   ADC7
            // Byte 3:    0     0     CH3    CH2    CH1    CH0  ADC15  ADC14
            //
            // ADC0 through ADC 15 comprise the 16-bit A/D converter sample from a particular amplifier
            // channel.  (ADC15 is the MDB; ADC0 is the LSB.)  The A/D full-scale range is 2.5V.  The "zero"
            // level of RHA2000 amplifiers is around 1.225V, although this can vary from one channel to another
            // due to built-in offset voltages.  The use of a software high-pass filter is recommended to
            // remove these offsets (see the RHA2000 series datasheet for more information).  With the RHA2000
            // gain of 200 taken into account, each A/D step corresponds to an electrode-referred voltage of
            // (2.5V/200)/(2^16) = 0.19073 microvolts.
            //
            // CH0 through CH3 encode information that varies with the channel number.  The following table
            // shows the values for these bits depending on the channel:
            //
            // Amplifier Channel     CH3    CH2    CH1    CH0
            //        0               0      0      0      0
            //        1               0      0      0     AUX1
            //        2               0      0      0     AUX2
            //        3               0      0      0     AUX3
            //        4               0      0      0     AUX4
            //        5               0      0      0     AUX5
            //        6               0      0      0     AUX6
            //        7               X      X      X      X
            //        8               X      X      X      X
            //        9               X      X      X      X
            //       10               X      X      X      X
            //       11               X      X      X      X
            //       12               X      X      X      X
            //       13               X      X      X      X
            //       14               X      X      X      X
            //       15               1      1      1      1
            //
            // AUX1 through AUX6 are bits corresponding to the Port J3 auxiliary TTL inputs shown in the
            // RHA2000-EVAL datasheet.  Any bits listed as 'X' are not specified and should not be used.
            // Note that channels 0 and 15 are unambiguously marked (0000 and 1111, respectively).  It is
            // recommended that interface software first watch for a byte that begins with '001111xx'.
            // This must correspond to byte 3 of channel 15.  The next 48 bytes will comprise a complete
            // 16-channel data frame starting with channel 0 and proceeding through channel 15.
            //
            // Each amplifier channel is sampled at 25 kS/s, which gives a data rate of 25,000 x 16 x 3 =
            // 1.2 MByte/s.
            //
            // In our experience, the FTDI USB interface chip on the RHA2000-EVAL occasionally drops bytes
            // (though this seems to depend on the exact USB interface card used in the PC), requiring any
            // interface software to frequently look for a '001111xx' byte at the end of each 48-byte data
            // frame to ensure synchronization is maintained.  When sync is lost, the software must search
            // for this byte again.

            // Are we out of sync due to a dropped byte over the USB link?  If so, resync.
            if ((readDataBufferA[35999] & 0xfc) != 0x3c)  // look for '001111xx' byte
            {
                // for (int j = 0; j < 36000; j++)
                // {
                //     if ((readDataBufferA[j] & 0xfc) == 0x3c)
                //     {
                //         Debug.WriteLine("readDataBufferA[" + j + "] = 0x3C.");
                //         break;
                //     }
                // }

                int i = 0;
                int index = 0;

                int numExtraBytes = this.ResyncA();
                Debug.WriteLine("Resync " + numExtraBytes + " bytes.");

                int indexError = 47 + 48;
                while ((readDataBufferA[indexError] & 0xfc) == 0x3c)
                {
                    indexError += 48;
                }
                Debug.WriteLine("indexError = " + indexError);

                // Duplicate previous sample to 'patch' missing or erroneous data
                for (i = indexError - 47; i < indexError + 1; i++)
                {
                    readDataBufferA[i] = readDataBufferA[i - 48];
                }

                for (i = indexError - 47 + 48; i < 36000 - numExtraBytes; i++)
                {
                    readDataBufferA[i] = readDataBufferA[i + numExtraBytes];
                }
                for (i = 36000 - numExtraBytes; i < 36000; i++)
                {
                    readDataBufferA[i] = resyncDataBufferA[index++];
                }
            }

            // Parse data (see comments above for description of 3-byte data packet)

            int indexA = 0;
            byte byte1, byte2, byte3;

            for (int i = 0; i < 750; i++)
            {
                auxFrame[i] = 0;
                for (int channel = 0; channel < 16; channel++)
                {
                    byte1 = readDataBufferA[indexA++];
                    byte2 = readDataBufferA[indexA++];
                    byte3 = readDataBufferA[indexA++];

                    if (channel == 1)
                        auxFrame[i] += (UInt16)(1 * (int)((byte3 & 0x04) >> 2));
                    else if (channel == 2)
                        auxFrame[i] += (UInt16)(2 * (int)((byte3 & 0x04) >> 2));
                    else if (channel == 3)
                        auxFrame[i] += (UInt16)(4 * (int)((byte3 & 0x04) >> 2));
                    else if (channel == 4)
                        auxFrame[i] += (UInt16)(8 * (int)((byte3 & 0x04) >> 2));
                    else if (channel == 5)
                        auxFrame[i] += (UInt16)(16 * (int)((byte3 & 0x04) >> 2));
                    else if (channel == 6)
                        auxFrame[i] += (UInt16)(32 * (int)((byte3 & 0x04) >> 2));

                    dataRaw[channel, i] = 0.1907F * (16384.0F * Convert.ToSingle(byte3 & 0x03) +
                        128.0F * Convert.ToSingle(byte2 & 0x7f) + Convert.ToSingle(byte1 & 0x7f)) - 6175.0F;    // 6175 = 1.235 V offset divided by 200, expressed in microvolts
                }
            }

            if (dataJustStarted)
            {
                for (int channel = 0; channel < 16; channel++)
                {
                    dataState[channel] = dataRaw[channel, 0];
                    dataDelay1[channel] = dataRaw[channel, 0];
                    dataDelay2[channel] = dataRaw[channel, 0];
                    notchDelay1[channel] = dataRaw[channel, 0];
                    notchDelay2[channel] = dataRaw[channel, 0];
                }
                dataJustStarted = false;
            }

            // Apply software first-order high-pass filter
            // (See RHA2000 series datasheet for desciption of this algorithm.)

            const double FSample = 25000.0; // ADC sample rate, in Hz

            float filtA = (float)(Math.Exp(-2.0 * Math.PI * fHPF / FSample));  // A
            float filtB = 1.0F - filtA;                                        // B = 1 - A

            for (int channel = 0; channel < 16; channel++)
            {
                dataState[channel] = filtA * dataState[channel] + filtB * dataRaw[channel, 0];
                if (enableHPF)
                    dataFrame[channel, 0] = dataRaw[channel, 0] - dataState[channel];
                else
                    dataFrame[channel, 0] = dataRaw[channel, 0];

                for (int i = 1; i < 750; i++)
                {
                    dataState[channel] = filtA * dataState[channel] + filtB * dataRaw[channel, i];
                    if (enableHPF)
                        dataFrame[channel, i] = dataRaw[channel, i] - dataState[channel];
                    else
                        dataFrame[channel, i] = dataRaw[channel, i];
                }
            }


            // Apply software notch filter
            // (See RHA2000 series datasheet for desciption of this algorithm.)

            const double NotchBW = 10.0;  // notch filter bandwidth, in Hz
            // Note: Reducing the notch filter bandwidth will create a more frequency-selective filter, but this
            // can lead to a long settling time for the filter.  Selecting a bandwdith of 10 Hz (e.g., filtering
            // out frequencies between 55 Hz and 65 Hz for the 60 Hz notch filter setting) implements a fast-
            // settling filter.

            float d = (float)Math.Exp(-1.0 * Math.PI * NotchBW / FSample);
            float b = (1.0F + d * d) * (float)Math.Cos(2.0 * Math.PI * fNotch / FSample);
            float a = 0.5F * (1.0F + d * d);

            float b0 = a;
            float b1 = a * (-2.0F) * (float)Math.Cos(2.0 * Math.PI * fNotch / FSample);
            float b2 = a;
            float a1 = -b;
            float a2 = d * d;

            for (int channel = 0; channel < 16; channel++)
            {
                dataNotch[channel, 0] = b2 * dataDelay2[channel] + b1 * dataDelay1[channel] + b0 * dataFrame[channel, 0] - a2 * notchDelay2[channel] - a1 * notchDelay1[channel];
                dataNotch[channel, 1] = b2 * dataDelay1[channel] + b1 * dataFrame[channel, 0] + b0 * dataFrame[channel, 1] - a2 * notchDelay1[channel] - a1 * dataNotch[channel, 0];
                for (int i = 2; i < 750; i++)
                {
                    dataNotch[channel, i] = b2 * dataFrame[channel, i - 2] + b1 * dataFrame[channel, i - 1] + b0 * dataFrame[channel, i] - a2 * dataNotch[channel, i - 2] - a1 * dataNotch[channel, i - 1];
                }
            }

            for (int channel = 0; channel < 16; channel++)
            {
                dataDelay2[channel] = dataFrame[channel, 748];
                dataDelay1[channel] = dataFrame[channel, 749];
                notchDelay2[channel] = dataNotch[channel, 748];
                notchDelay1[channel] = dataNotch[channel, 749];
            }


            if (enableNotch) return new IntanUsbData(dataNotch, auxFrame);
            else return new IntanUsbData(dataFrame, auxFrame);
        }

        /// <summary>
        /// Return a random number from a Gaussian distribution with variance = 1.
        /// </summary>
        /// <param name="rand">Pseudo-random number generator object.</param>
        /// <returns>Random number picked from Gaussian distribution.</returns>
        private static float gaussian(Random rand)
        {
            double r = 0.0;
            const double Sqrt3 = 1.73205080757;
            const int N = 8;   // making N larger increases accuracy at the expense of speed

            for (int i = 0; i < N; i++)
            {
                r += (Sqrt3 * 2.0 * rand.NextDouble()) - 1.0;
            }

            r /= Math.Sqrt(N);

            return ((float)r);
        }

        /// <summary>
        /// Search for the '001111xx' byte that designates the end of a 16-channel data packet.
        /// </summary>
        /// <returns>Number of bytes we skipped trying to get back in sync.</returns>
        public int ResyncA()
        {
            bool inSync = false;

            FTDI.FT_STATUS ftStatus = FTDI.FT_STATUS.FT_OK;

            byte[] resyncBuffer = new byte[1];

            Debug.WriteLine("Resync #" + resyncCount.ToString());
            resyncCount++;

            int resyncBytes = 0;

            while (inSync == false)
            {
                // Wait until the desired number of bytes have been received.
                UInt32 numBytesAvailable = 0;

                while (numBytesAvailable < 1)
                {
                    ftStatus = myFtdiDeviceA.GetRxBytesAvailable(ref numBytesAvailable);

                    if (ftStatus != FTDI.FT_STATUS.FT_OK)
                    {
                        IntanUsbException e = new IntanUsbException("Failed to get number of USB bytes available to read");
                        throw e;
                    }
                }

                // Now that we have the amount of data we want available, read it.
                UInt32 numBytesRead = 0;

                ftStatus = myFtdiDeviceA.Read(resyncBuffer, 1, ref numBytesRead);

                if (ftStatus != FTDI.FT_STATUS.FT_OK)
                {
                    IntanUsbException e = new IntanUsbException("USB read error");
                    throw e;
                }

                resyncDataBufferA[resyncBytes] = resyncBuffer[0];
                resyncBytes++;

                // Are we back in sync?
                if ((resyncBuffer[0] & 0xfc) == 0x3c)
                {
                    inSync = true;
                }
            }

            return resyncBytes;
        }

        /// <summary>
        /// Command RHA2000-EVAL board to enable amplifier fast settle.
        /// </summary>
        public void SettleOn()
        {
            // The RHA2000-EVAL board is controlled by sending one-byte ASCII command characters over
            // the USB interface.  The 'F' character commands the board to enable amplfier fast settle.

            UInt32 numBytesWritten = 0;
            Byte[] myChars = { 70, 115 };  // 'F' = fast settle on

            if (running)
            {
                myChars[1] = 83;
            }

            FTDI.FT_STATUS ftStatus = myFtdiDeviceA.Write(myChars, 2, ref numBytesWritten);

            // Purge receive buffer.
            myFtdiDeviceA.Purge(FTDI.FT_PURGE.FT_PURGE_RX);

            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                IntanUsbException e = new IntanUsbException("Could not write to Intan USB device");
                throw e;
            }
        }

        /// <summary>
        /// Command RHA2000-EVAL board to disable amplifier fast settle.
        /// </summary>
        public void SettleOff()
        {
            // The RHA2000-EVAL board is controlled by sending one-byte ASCII command characters over
            // the USB interface.  The 'f' character commands the board to disable amplfier fast settle.

            UInt32 numBytesWritten = 0;
            Byte[] myChars = { 102, 115 };  // 'f' = fast settle off

            if (running)
            {
                myChars[1] = 83;
            }

            FTDI.FT_STATUS ftStatus = myFtdiDeviceA.Write(myChars, 2, ref numBytesWritten);

            // Purge receive buffer.
            myFtdiDeviceA.Purge(FTDI.FT_PURGE.FT_PURGE_RX);

            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                IntanUsbException e = new IntanUsbException("Could not write to Intan USB device");
                throw e;
            }
        }

        /// <summary>
        /// Command RHA2000-EVAL board to enable impedance check mode.
        /// </summary>
        public void ZCheckOn()
        {
            // The RHA2000-EVAL board is controlled by sending one-byte ASCII command characters over
            // the USB interface.  The 'Z' character commands the board to enable impedance check mode.

            UInt32 numBytesWritten = 0;
            Byte[] myChars = { 90, 115 };  // 'Z' = impedance check on

            if (running)
            {
                myChars[1] = 83;
            }

            FTDI.FT_STATUS ftStatus = myFtdiDeviceA.Write(myChars, 2, ref numBytesWritten);

            // Purge receive buffer.
            myFtdiDeviceA.Purge(FTDI.FT_PURGE.FT_PURGE_RX);

            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                IntanUsbException e = new IntanUsbException("Could not write to Intan USB device");
                throw e;
            }
        }

        /// <summary>
        /// Command RHA2000-EVAL board to disable impedance check mode.
        /// </summary>
        public void ZCheckOff()
        {
            // The RHA2000-EVAL board is controlled by sending one-byte ASCII command characters over
            // the USB interface.  The 'Z' character commands the board to disable impedance check mode.

            UInt32 numBytesWritten = 0;
            Byte[] myChars = { 122, 115 };  // 'z' = impedance check off

            if (running)
            {
                myChars[1] = 83;
            }

            FTDI.FT_STATUS ftStatus = myFtdiDeviceA.Write(myChars, 2, ref numBytesWritten);

            // Purge receive buffer.
            myFtdiDeviceA.Purge(FTDI.FT_PURGE.FT_PURGE_RX);

            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                IntanUsbException e = new IntanUsbException("Could not write to Intan USB device");
                throw e;
            }
        }

        /// <summary>
        /// Command RHA2000-EVAL board to manually reset the analog MUX to amplifier channel 0.
        /// This is only used for electrode impedance checking.
        /// </summary>
        public void ChannelReset()
        {
            // The RHA2000-EVAL board is controlled by sending one-byte ASCII command characters over
            // the USB interface.  The 'R' character commands the board to manually reset the analog MUX
            // to amplifier channel 0.  This command is only used for electrode impedance checking.

            UInt32 numBytesWritten = 0;
            Byte[] myChars = { 82, 115 };  // 'R' = reset to channel 0 (impedance check mode)

            if (running)
            {
                myChars[1] = 83;
            }

            FTDI.FT_STATUS ftStatus = myFtdiDeviceA.Write(myChars, 2, ref numBytesWritten);

            // Purge receive buffer.
            myFtdiDeviceA.Purge(FTDI.FT_PURGE.FT_PURGE_RX);

            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                IntanUsbException e = new IntanUsbException("Could not write to Intan USB device");
                throw e;
            }
        }

        /// <summary>
        /// Command RHA2000-EVAL board to manually step the analog MUX to the next amplifier channel.
        /// This is only used for electrode impedance checking.
        /// </summary>
        public void ChannelStep()
        {
            // The RHA2000-EVAL board is controlled by sending one-byte ASCII command characters over
            // the USB interface.  The 'N' character commands the board to manually step the analog MUX
            // to the next amplifier channel.  This command is only used for electrode impedance checking.

            UInt32 numBytesWritten = 0;
            Byte[] myChars = { 78, 115 };  // 'N' = step to next channel (impedance check mode)

            if (running)
            {
                myChars[1] = 83;
            }

            FTDI.FT_STATUS ftStatus = myFtdiDeviceA.Write(myChars, 2, ref numBytesWritten);

            // Purge receive buffer.
            myFtdiDeviceA.Purge(FTDI.FT_PURGE.FT_PURGE_RX);

            if (ftStatus != FTDI.FT_STATUS.FT_OK)
            {
                IntanUsbException e = new IntanUsbException("Could not write to Intan USB device");
                throw e;
            }
        }

        /// <summary>
        /// USBSource constructor.
        /// </summary>
        public IntanUsbSource()
        {
            enableHPF = true;
            fHPF = 1.0;
            enableNotch = false;
            fNotch = 60.0;
        }
    }

    public class IntanUsbException : System.Exception
    {
        public IntanUsbException(string message) :
            base(message) // pass the message up to the base class
        {
        }
    }

}
