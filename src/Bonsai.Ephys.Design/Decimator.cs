using OpenCV.Net;
using System;

namespace Bonsai.Ephys.Design
{
    internal sealed class Decimator : IDisposable
    {
        int carry;
        int outputIndex;
        int inputIndex;
        readonly Mat buffer;
        readonly Mat carryBuffer;
        readonly Mat conversionBuffer;
        readonly int downsampleFactor;
        readonly Depth inputDepth;
        readonly ReduceOperation reduceOp;

        public Decimator(Mat input, int length, int factor, ReduceOperation reduceOperation)
        {
            if (length < 1)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (factor < 1)
                throw new ArgumentOutOfRangeException(nameof(factor));

            outputIndex = 0;
            downsampleFactor = factor;
            inputDepth = input.Depth;
            carry = downsampleFactor;
            carryBuffer = new Mat(input.Rows, 1, Depth.F32, input.Channels);
            buffer = new Mat(input.Rows, length, Depth.F32, input.Channels);
            buffer.Set(Scalar.All(double.NaN));
            reduceOp = reduceOperation;
            if (inputDepth != Depth.F32)
                conversionBuffer = new Mat(input.Size, Depth.F32, input.Channels);
        }

        public Mat Buffer => buffer;

        public int DownsampleFactor => downsampleFactor;

        public Depth InputDepth => inputDepth;

        public void Process(Mat input)
        {
            if (conversionBuffer is not null)
            {
                CV.Convert(input, conversionBuffer);
                input = conversionBuffer;
            }

            while (inputIndex < input.Cols)
            {
                var inputSamples = Math.Min(input.Cols - inputIndex, carry);
                var inputRect = new Rect(inputIndex, 0, inputSamples, input.Rows);

                using var inputBuffer = input.GetSubRect(inputRect);
                using var outputBuffer = buffer.GetCol(outputIndex);
                if (carry < downsampleFactor)
                {
                    CV.Reduce(inputBuffer, carryBuffer, 1, reduceOp);
                    switch (reduceOp)
                    {
                        case ReduceOperation.Sum:
                            CV.Add(outputBuffer, carryBuffer, outputBuffer);
                            break;
                        case ReduceOperation.Max:
                            CV.Max(outputBuffer, carryBuffer, outputBuffer);
                            break;
                        case ReduceOperation.Min:
                            CV.Min(outputBuffer, carryBuffer, outputBuffer);
                            break;
                    }
                }
                else CV.Reduce(inputBuffer, outputBuffer, 1, reduceOp);

                inputIndex += inputRect.Width;
                carry -= inputSamples;
                if (carry <= 0)
                {
                    outputIndex = (outputIndex + 1) % buffer.Cols;
                    carry = downsampleFactor;
                }

                outputIndex = outputIndex % buffer.Cols;
            }

            inputIndex -= input.Cols;
        }

        public void Dispose()
        {
            buffer.Dispose();
            carryBuffer.Dispose();
            conversionBuffer?.Dispose();
        }
    }
}
