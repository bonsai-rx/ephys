using OpenCV.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Bonsai.Ephys
{
    public class TtlState : Transform<Mat, Mat>
    {
        public override IObservable<Mat> Process(IObservable<Mat> source)
        {
            return source.Select(input =>
            {
                var output = new Mat(8, input.Cols, Depth.U8, 1);
                for (int i = 0; i < output.Rows; i++)
                {
                    using (var row = output.GetRow(i))
                    {
                        CV.AndS(input, Scalar.Real(1 << i), row);
                    }
                }
                return output;
            });
        }
    }
}
