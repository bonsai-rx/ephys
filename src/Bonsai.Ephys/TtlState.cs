using OpenCV.Net;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;

namespace Bonsai.Ephys
{
    /// <summary>
    /// Represents an operator that demultiplexes TTL digital state into
    /// independent channels.
    /// </summary>
    [Description("Demultiplexes TTL digital state into independent channels.")]
    public class TtlState : Transform<Mat, Mat>
    {
        /// <summary>
        /// Demultiplexes TTL digital state arrays in an observable sequence into
        /// a multi-channel array where the state of each input pin is represented
        /// in an independent channel.
        /// </summary>
        /// <param name="source">A sequence of TTL digital state arrays to demultiplex.</param>
        /// <returns>
        /// A sequence of multi-channel array values, where the state of each input pin
        /// is represented in an independent channel.
        /// </returns>
        public override IObservable<Mat> Process(IObservable<Mat> source)
        {
            return source.Select(input =>
            {
                var output = new Mat(8, input.Cols, Depth.U8, 1);
                for (int i = 0; i < output.Rows; i++)
                {
                    using var row = output.GetRow(i);
                    CV.AndS(input, Scalar.Real(1 << i), row);
                }
                return output;
            });
        }
    }
}
