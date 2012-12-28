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


namespace Bonsai.Ephys
{
    /// <summary>
    /// This class stores data frames containing amplifier data from all 16 channels,
    /// plus binary data from the Port J3 auxiliary TTL inputs. 
    /// </summary>
    public class IntanUsbData
    {
        // private variables
        private float[,] dataFrame = new float[16, 750];
        private UInt16[] auxFrame = new UInt16[750];

        // public methods

        public float[,] DataFrame
        {
            get { return dataFrame; }
        }

        public UInt16[] AuxFrame
        {
            get { return auxFrame; }
        }

        /// <summary>
        /// Write first element to debugging window.
        /// </summary>
        public void DisplayFirstElement()
        {
            Debug.WriteLine("First element = " + Convert.ToString(dataFrame[0, 0]));
        }

        /// <summary>
        /// Copy data frame to two arrays: one for amplifier data and another for
        /// auxiliary TTL input data.
        /// </summary>
        /// <param name="dataArray">Array for amplifier data.</param>
        /// <param name="auxArray">Array for auxiliary TTL data.</param>
        public void CopyToArray(float[,] dataArray, UInt16[] auxArray)
        {
            for (int channel = 0; channel < 16; channel++)
            {
                for (int i = 0; i < 750; i++)
                {
                    dataArray[channel, i] = dataFrame[channel, i];
                }
            }
            for (int i = 0; i < 750; i++)
            {
                auxArray[i] = auxFrame[i];
            }
        }

        /// <summary>
        /// USBData constructor.
        /// </summary>
        /// <param name="dataIn">Amplifier data.</param>
        /// <param name="auxIn">Auxiliary TTL data.</param>
        public IntanUsbData(float[,] dataIn, UInt16[] auxIn)
        {
            for (int channel = 0; channel < 16; channel++)
            {
                for (int i = 0; i < 750; i++)
                {
                    dataFrame[channel, i] = dataIn[channel, i];
                }
            }
            for (int i = 0; i < 750; i++)
            {
                auxFrame[i] = auxIn[i];
            }
        }
    }

}
