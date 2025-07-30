using OpenCV.Net;
using Rhythm.Net;

namespace Bonsai.Ephys
{
    /// <summary>
    /// Represents a data structure storing data samples from a Rhythm FPGA interface
    /// controlling up to eight RHD2000 chips.
    /// </summary>
    /// <remarks>
    /// New instances of the <see cref="Rhd2000DataFrame"/> class are initialized from
    /// a stored RHD2000 data block and the current buffer capacity.
    /// </remarks>
    /// <param name="dataBlock">The stored data samples in one acquisition block.</param>
    /// <param name="bufferCapacity">The percentage capacity of the internal sample buffer.</param>
    public class Rhd2000DataFrame(Rhd2000DataBlock dataBlock, double bufferCapacity)
    {
        static Mat GetTimestampData(uint[] data)
        {
            return Mat.FromArray(data, 1, data.Length, Depth.S32, 1);
        }

        static Mat GetTtlData(int[] data)
        {
            var output = new Mat(1, data.Length, Depth.U8, 1);
            using (var header = Mat.CreateMatHeader(data))
            {
                CV.Convert(header, output);
            }

            return output;
        }

        static Mat GetAdcData(int[,] data)
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

        static Mat GetStreamData(int[][,] data)
        {
            if (data.Length == 0) return null;
            var numChannels = data[0].GetLength(0);
            var numSamples = data[0].GetLength(1);

            var output = new Mat(data.Length * numChannels, numSamples, Depth.U16, 1);
            for (int i = 0; i < data.Length; i++)
            {
                using var header = Mat.CreateMatHeader(data[i]);
                using var subRect = output.GetSubRect(new Rect(0, i * numChannels, numSamples, numChannels));
                CV.Convert(header, subRect);
            }

            return output;
        }

        static Mat GetAuxiliaryData(int[][,] data)
        {
            const int AuxDataChannels = 4;
            const int OutputChannels = AuxDataChannels - 1;
            if (data.Length == 0) return null;
            var numSamples = data[0].GetLength(1) / AuxDataChannels;
            var auxData = new short[OutputChannels * numSamples];
            for (int i = 0; i < auxData.Length; i++)
            {
                auxData[i] = (short)data[0][1, i % numSamples * AuxDataChannels + i / numSamples + 1];
            }

            return Mat.FromArray(auxData, OutputChannels, numSamples, Depth.U16, 1);
        }

        /// <summary>
        /// Gets the array of 32-bit sample timestamps.
        /// </summary>
        public Mat Timestamp { get; } = GetTimestampData(dataBlock.Timestamp);

        /// <summary>
        /// Gets the multi-channel electrode amplifier voltage data.
        /// </summary>
        public Mat AmplifierData { get; } = GetStreamData(dataBlock.AmplifierData);

        /// <summary>
        /// Gets the multi-channel auxiliary ADC input data.
        /// </summary>
        /// <remarks>
        /// These inputs are often used to interface with the 3-axis accelerometer included
        /// in some headstages.
        /// </remarks>
        public Mat AuxiliaryData { get; } = GetAuxiliaryData(dataBlock.AuxiliaryData);

        /// <summary>
        /// Gets the multi-channel board ADC input voltage data.
        /// </summary>
        public Mat BoardAdcData { get; } = GetAdcData(dataBlock.BoardAdcData);

        /// <summary>
        /// Gets the state of the 16 digital TTL input lines on the FPGA.
        /// </summary>
        public Mat TtlIn { get; } = GetTtlData(dataBlock.TtlIn);

        /// <summary>
        /// Gets the state of the 16 digital TTL output lines on the FPGA.
        /// </summary>
        public Mat TtlOut { get; } = GetTtlData(dataBlock.TtlOut);

        /// <summary>
        /// Gets the percentage capacity of the internal sample buffer.
        /// </summary>
        /// <remarks>
        /// When this value exceeds 100, there will be a buffer overrun and acquisition
        /// will be aborted.
        /// </remarks>
        public double BufferCapacity { get; } = bufferCapacity;
    }
}
