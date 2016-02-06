using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Ephys
{
    [Description("Rescales ADC values sampled from Rhd2000 data blocks into SI voltage units.")]
    public class AdcScale : Transform<Mat, Mat>
    {
        [Description("The type of the ADC from which the input samples were taken.")]
        public AdcType AdcType { get; set; }

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
                        throw new InvalidOperationException("Invalid adc type.");
                }

                return output;
            });
        }
    }
}
