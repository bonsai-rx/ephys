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
        readonly int downsampleFactor;
        readonly ReduceOperation reduceOp;

        public Decimator(Mat input, int length, int factor, ReduceOperation reduceOperation)
        {
            if (length < 1)
                throw new ArgumentOutOfRangeException(nameof(length));

            if (factor < 1)
                throw new ArgumentOutOfRangeException(nameof(factor));

            outputIndex = 0;
            downsampleFactor = factor;
            carry = downsampleFactor;
            carryBuffer = new Mat(input.Rows, 1, input.Depth, input.Channels);
            buffer = new Mat(input.Rows, length, input.Depth, input.Channels);
            buffer.Set(Scalar.All(double.NaN));
            reduceOp = reduceOperation;
        }

        public Mat Buffer => buffer;

        public int DownsampleFactor => downsampleFactor;

        public void Process(Mat input)
        {
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
        }
    }
}
