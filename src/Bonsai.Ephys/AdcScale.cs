using OpenCV.Net;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;

namespace Bonsai.Ephys
{
    /// <summary>
    /// Represents an operator that rescales ADC values sampled from RHD2000 data blocks
    /// into SI voltage units.
    /// </summary>
    [Description("Rescales ADC values sampled from RHD2000 data blocks into SI voltage units.")]
    public class AdcScale : Transform<Mat, Mat>
    {
        /// <summary>
        /// Gets or sets the type of the ADC from which the input samples were taken.
        /// </summary>
        [Description("The type of the ADC from which the input samples were taken.")]
        public AdcType AdcType { get; set; }

        /// <summary>
        /// Rescales every RHD2000 ADC value in an observable sequence into SI voltage units.
        /// </summary>
        /// <param name="source">A sequence of multi-channel ADC values.</param>
        /// <returns>
        /// A sequence of multi-channel array values, where each element of the array
        /// has been rescaled to SI voltage units.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// The observable sequence will emit an exception if an invalid or unsupported
        /// ADC type is specified.
        /// </exception>
        public override IObservable<Mat> Process(IObservable<Mat> source)
        {
            return source.Select(input =>
            {
                var output = new Mat(input.Size, Depth.F32, input.Channels);
                switch (AdcType)
                {
                    case AdcType.Electrode:
                        CV.ConvertScale(input, output, 0.195, -6389.76);
                        break;
                    case AdcType.AuxiliaryInput:
                        CV.ConvertScale(input, output, 0.0000374, 0);
                        break;
                    case AdcType.SupplyVoltage:
                        CV.ConvertScale(input, output, 0.0000748, 0);
                        break;
                    case AdcType.Temperature:
                        CV.ConvertScale(input, output, 1 / 100.0, 0);
                        break;
                    case AdcType.BoardAdc:
                        CV.ConvertScale(input, output, 0.000050354, 0);
                        break;
                    default:
                        throw new InvalidOperationException("Invalid ADC type.");
                }

                return output;
            });
        }
    }
}
