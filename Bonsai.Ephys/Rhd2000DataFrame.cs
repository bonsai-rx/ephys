using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenCV.Net;
using Rhythm.Net;

namespace Bonsai.Ephys
{
    public class Rhd2000DataFrame
    {
        public Rhd2000DataFrame(Rhd2000DataBlock dataBlock, double bufferCapacity)
        {
            Timestamp = dataBlock.Timestamp;
            AmplifierData = GetStreamData(dataBlock.AmplifierData, 0.195, -6389.76);
            AuxiliaryData = GetStreamData(dataBlock.AuxiliaryData, 0.0000374, 0);
            BoardAdcData = Mat.FromArray(dataBlock.BoardAdcData);
            TtlIn = Mat.FromArray(dataBlock.TtlIn);
            TtlOut = Mat.FromArray(dataBlock.TtlOut);
            BufferCapacity = bufferCapacity;
        }

        Mat GetStreamData(int[][,] data, double scale, double shift)
        {
            var numChannels = data[0].GetLength(0);
            var numSamples = data[0].GetLength(1);

            var output = new Mat(data.Length * numChannels, numSamples, Depth.F32, 1);
            for (int i = 0; i < data.Length; i++)
            {
                using (var header = Mat.CreateMatHeader(data[i]))
                using (var subRect = output.GetSubRect(new Rect(0, i * numChannels, numSamples, numChannels)))
                {
                    CV.ConvertScale(header, subRect, scale, shift);
                }
            }

            return output;
        }

        public uint[] Timestamp { get; private set; }

        public Mat AmplifierData { get; private set; }

        public Mat AuxiliaryData { get; private set; }

        public Mat BoardAdcData { get; private set; }

        public Mat TtlIn { get; private set; }

        public Mat TtlOut { get; private set; }

        public double BufferCapacity { get; private set; }
    }
}
