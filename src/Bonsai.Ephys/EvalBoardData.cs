using OpenCV.Net;

namespace Bonsai.Ephys
{
    /// <summary>
    /// Represents a data object containing buffered amplifier voltage and auxiliary
    /// TTL input data sampled from an <see cref="IntanEvalBoard"/>.
    /// </summary>
    /// <param name="dataFrame">The buffered electrode amplifier voltage data.</param>
    /// <param name="auxFrame">The buffered auxiliary TTL input data.</param>
    public class EvalBoardData(Mat dataFrame, Mat auxFrame)
    {
        /// <summary>
        /// Gets the buffered electrode amplifier voltage data.
        /// </summary>
        public Mat DataFrame { get; } = dataFrame;

        /// <summary>
        /// Gets the buffered auxiliary TTL input data.
        /// </summary>
        public Mat AuxFrame { get; } = auxFrame;
    }
}
