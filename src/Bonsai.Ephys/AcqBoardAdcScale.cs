using OpenCV.Net;
using System;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;

namespace Bonsai.Ephys
{
    /// <summary>
    /// Represents an operator that rescales ADC values sampled from the Open Ephys
    /// Acquisition Board into SI voltage units.
    /// </summary>
    [Description("Rescales ADC values sampled from the Open Ephys Acquisition Board into SI voltage units.")]
    public class AcqBoardAdcScale : Transform<Mat, Mat>
    {
        /// <summary>
        /// Gets or sets the type of the ADC from which the input samples were taken.
        /// </summary>
        [Description("The type of the ADC from which the input samples were taken.")]
        public Rhd2000AdcType AdcType { get; set; }

        /// <summary>
        /// Rescales every sampled ADC value in an observable sequence into SI voltage units.
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
                    case Rhd2000AdcType.Electrode:
                        CV.ConvertScale(input, output, 0.195, -6389.76);
                        break;
                    case Rhd2000AdcType.AuxiliaryInput:
                        CV.ConvertScale(input, output, 0.0000374, 0);
                        break;
                    case Rhd2000AdcType.SupplyVoltage:
                        CV.ConvertScale(input, output, 0.0000748, 0);
                        break;
                    case Rhd2000AdcType.Temperature:
                        CV.ConvertScale(input, output, 1 / 100.0, 0);
                        break;
                    case Rhd2000AdcType.BoardAdc:
                        CV.ConvertScale(input, output, 0.00015258789f, -5 - 0.4096);
                        break;
                    default:
                        throw new InvalidOperationException("Invalid ADC type.");
                }

                return output;
            });
        }
    }
}
