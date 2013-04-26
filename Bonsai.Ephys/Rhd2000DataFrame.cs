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
            BoardAdcData = CvMat.FromArray(dataBlock.BoardAdcData);
            TtlIn = CvMat.FromArray(dataBlock.TtlIn);
            TtlOut = CvMat.FromArray(dataBlock.TtlOut);
            BufferCapacity = bufferCapacity;
        }

        CvMat GetStreamData(int[][,] data, double scale, double shift)
        {
            var numChannels = data[0].GetLength(0);
            var numSamples = data[0].GetLength(1);

            var output = new CvMat(data.Length * numChannels, numSamples, CvMatDepth.CV_32F, 1);
            for (int i = 0; i < data.Length; i++)
            {
                using (var header = CvMat.CreateMatHeader(data[i]))
                using (var subRect = output.GetSubRect(new CvRect(0, i * numChannels, numSamples, numChannels)))
                {
                    Core.cvConvertScale(header, subRect, scale, shift);
                }
            }

            return output;
        }

        public uint[] Timestamp { get; private set; }

        public CvMat AmplifierData { get; private set; }

        public CvMat AuxiliaryData { get; private set; }

        public CvMat BoardAdcData { get; private set; }

        public CvMat TtlIn { get; private set; }

        public CvMat TtlOut { get; private set; }

        public double BufferCapacity { get; private set; }
    }
}
