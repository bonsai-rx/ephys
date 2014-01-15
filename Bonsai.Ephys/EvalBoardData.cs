using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenCV.Net;

namespace Bonsai.Ephys
{
    public class EvalBoardData
    {
        public EvalBoardData(Mat dataFrame, Mat auxFrame)
        {
            DataFrame = dataFrame;
            AuxFrame = auxFrame;
        }

        public Mat DataFrame { get; private set; }

        public Mat AuxFrame { get; private set; }
    }
}
