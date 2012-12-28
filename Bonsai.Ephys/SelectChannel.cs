using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenCV.Net;

namespace Bonsai.Ephys
{
    public class SelectChannel : Transform<CvMat, CvMat>
    {
        public int Channel { get; set; }

        public override CvMat Process(CvMat input)
        {
            return input.GetSubRect(new CvRect(0, Channel, input.Cols, 1));
        }
    }
}
