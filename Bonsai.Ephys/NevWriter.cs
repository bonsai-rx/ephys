using Bonsai.Dsp;
using Bonsai.IO;
using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Ephys
{
    [Description("Sinks individual spike waveforms of the input sequence to a binary NEV file.")]
    public class NevWriter : FileSink<SpikeWaveformCollection, BinaryWriter>
    {
        const uint BasicHeaderSize = 336u;
        const uint ExtendedHeaderSize = 32u;
        static readonly double[] DefaultThreshold = new[] { 0.0 };
        static readonly byte[] LabelHeaderReservedBytes = new byte[6];

        public NevWriter()
        {
            Depth = Depth.S16;
        }

        [Description("The total number of amplifier channels in the input signal.")]
        public int ChannelCount { get; set; }

        [TypeConverter(typeof(UnidimensionalArrayConverter))]
        [Description("The per-channel threshold for detecting individual spikes.")]
        public double[] Threshold { get; set; }

        [Description("The bit depth of individual spike waveforms.")]
        public Depth Depth { get; set; }

        [Description("The per-channel sampling frequency (Hz) of the input signal.")]
        public int SamplingFrequency { get; set; }

        [Description("An optional comment string that will be embedded into the NEV basic header.")]
        public string Comments { get; set; }

        protected override BinaryWriter CreateWriter(string fileName, SpikeWaveformCollection input)
        {
            var channels = ChannelCount;
            var spikeWidth = input.BufferSize.Width;
            var bytesPerWaveform = BytesPerWaveform(Depth);
            var thresholdValues = Threshold ?? DefaultThreshold;
            var stream = new FileStream(fileName, Overwrite ? FileMode.Create : FileMode.CreateNew);
            var writer = new BinaryWriter(stream, Encoding.ASCII);
            WriteBasicHeader(writer, (uint)channels);
            for (int i = 0; i < channels; i++)
            {
                var id = i + 1;
                var label = "chan" + id;
                var threshold = thresholdValues.Length > 1 ? thresholdValues[i] : thresholdValues[0];
                WriteNeuralEventWaveformHeader(writer, id, threshold, bytesPerWaveform, spikeWidth);
                WriteNeuralEventLabelHeader(writer, id, label);
            }
            return writer;
        }

        int BytesPerWaveform(Depth depth)
        {
            switch (depth)
            {
                case Depth.F32: return 4;
                case Depth.F64: return 8;
                case Depth.S16: return 2;
                case Depth.S32: return 4;
                case Depth.S8: return 1;
                case Depth.U16: return 2;
                case Depth.U8: return 1;
                case Depth.UserType: return 1;
                default: return 1;
            }
        }

        void WriteBasicHeader(BinaryWriter writer, uint channels)
        {
            // File type ID
            writer.Write(new[] { 'N', 'E', 'U', 'R', 'A', 'L', 'E', 'V' });
            // File spec (2.3)
            writer.Write((byte)0x02);
            writer.Write((byte)0x03);
            // Additional flags (bit 0 cleared)
            writer.Write((ushort)0);
            // Bytes in headers
            var extendedHeaderCount = channels * 2;
            writer.Write(BasicHeaderSize + extendedHeaderCount * ExtendedHeaderSize);
            // Bytes in data packets
            writer.Write(0);
            // Time resolution of timestamps
            writer.Write((uint)SamplingFrequency);
            // Time resolution of samples
            writer.Write((uint)SamplingFrequency);
            // Time origin
            var time = HighResolutionScheduler.Now;
            writer.Write((ushort)time.Year);
            writer.Write((ushort)time.Month);
            writer.Write((ushort)time.DayOfWeek);
            writer.Write((ushort)time.Day);
            writer.Write((ushort)time.Hour);
            writer.Write((ushort)time.Minute);
            writer.Write((ushort)time.Second);
            writer.Write((ushort)time.Millisecond);
            // Application to create file
            var version = Assembly.GetExecutingAssembly().GetName().Version;
            var applicationId = string.Format("Bonsai {0}.{1}", version.Major, version.Minor);
            var appIdBytes = new char[32];
            for (int i = 0; i < applicationId.Length; i++)
            {
                appIdBytes[i] = applicationId[i];
            }
            writer.Write(appIdBytes);
            // Comment field
            var comments = Comments;
            var commentField = new char[256];
            if (!string.IsNullOrEmpty(comments))
            {
                var commentSize = Math.Min(comments.Length, commentField.Length - 1);
                for (int i = 0; i < commentSize; i++)
                {
                    commentField[i] = comments[i];
                }
            }
            writer.Write(commentField);
            // Number of extended headers
            writer.Write(extendedHeaderCount);
        }

        void WriteNeuralEventWaveformHeader(BinaryWriter writer, int electrodeId, double threshold, int bytesPerWaveform, int spikeWidth)
        {
            // Packet ID
            writer.Write(new[] { 'N', 'E', 'U', 'E', 'V', 'W', 'A', 'V' });
            // Electrode ID
            writer.Write((ushort)electrodeId);
            // Physical connector (assuming 32-channels per connector)
            writer.Write((byte)(electrodeId / 32));
            // Connector pin (assuming 32-channels per connector)
            writer.Write((byte)(electrodeId % 32));
            // Digitization factor (195 nV per LSB in Rhythm)
            writer.Write((ushort)195);
            // Energy threshold
            writer.Write((ushort)0);
            // High threshold
            writer.Write((short)(threshold >= 0 ? threshold : short.MaxValue));
            // Low threshold
            writer.Write((short)(threshold < 0 ? threshold : short.MinValue));
            // Number of sorted units
            writer.Write((byte)0);
            // Bytes per waveform
            writer.Write((byte)bytesPerWaveform);
            // Spike width
            writer.Write((ushort)spikeWidth);
            // Reserved bytes
            writer.Write(0L);
        }

        void WriteNeuralEventLabelHeader(BinaryWriter writer, int electrodeId, string label)
        {
            // Packet ID
            writer.Write(new[] { 'N', 'E', 'U', 'E', 'V', 'L', 'B', 'L' });
            // Electrode ID
            writer.Write((ushort)electrodeId);
            // Label
            var labelField = new char[16];
            var labelLength = Math.Min(label.Length, labelField.Length - 1);
            for (int i = 0; i < labelLength; i++)
            {
                labelField[i] = label[i];
            }
            writer.Write(labelField);
            // Reserved bytes
            writer.Write(LabelHeaderReservedBytes);
        }

        void WriteSpikeEventDataPacket(BinaryWriter writer, SpikeWaveform spike, byte[] data)
        {
            // Timestamp
            writer.Write((uint)spike.SampleIndex);
            // Packet ID
            writer.Write((ushort)(spike.ChannelIndex + 1));
            // Unit classification number (unclassified)
            writer.Write((byte)0);
            // Reserved
            writer.Write((byte)0);
            // Waveform
            writer.Write(data);
        }

        protected override void Write(BinaryWriter writer, SpikeWaveformCollection input)
        {
            if (input.Count > 0)
            {
                var waveform = input[0].Waveform;
                var data = new byte[waveform.Step];
                using (var dataHeader = Mat.CreateMatHeader(data, waveform.Rows, waveform.Cols, waveform.Depth, waveform.Channels))
                {
                    foreach (var spike in input)
                    {
                        CV.Copy(spike.Waveform, dataHeader);
                        WriteSpikeEventDataPacket(writer, spike, data);
                    }
                }
            }
        }
    }
}
