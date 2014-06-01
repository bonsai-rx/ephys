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
            AmplifierData = GetStreamData(dataBlock.AmplifierData);
            AuxiliaryData = GetStreamData(dataBlock.AuxiliaryData);
            BoardAdcData = GetAdcData(dataBlock.BoardAdcData);
            TtlIn = GetTtlData(dataBlock.TtlIn);
            TtlOut = GetTtlData(dataBlock.TtlOut);
            BufferCapacity = bufferCapacity;
        }

        Mat GetTtlData(int[] data)
        {
            var output = new Mat(1, data.Length, Depth.U8, 1);
            using (var header = Mat.CreateMatHeader(data))
            {
                CV.Convert(header, output);
            }

            return output;
        }

        Mat GetAdcData(int[,] data)
        {
            var numChannels = data.GetLength(0);
            var numSamples = data.GetLength(1);

            var output = new Mat(numChannels, numSamples, Depth.U16, 1);
            using (var header = Mat.CreateMatHeader(data))
            {
                CV.Convert(header, output);
            }

            return output;
        }

        Mat GetStreamData(int[][,] data)
        {
            if (data.Length == 0) return null;
            var numChannels = data[0].GetLength(0);
            var numSamples = data[0].GetLength(1);

            var output = new Mat(data.Length * numChannels, numSamples, Depth.U16, 1);
            for (int i = 0; i < data.Length; i++)
            {
                using (var header = Mat.CreateMatHeader(data[i]))
                using (var subRect = output.GetSubRect(new Rect(0, i * numChannels, numSamples, numChannels)))
                {
                    CV.Convert(header, subRect);
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
