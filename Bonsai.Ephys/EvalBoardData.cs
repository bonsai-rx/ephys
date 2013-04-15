using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenCV.Net;

namespace Bonsai.Ephys
{
    public class EvalBoardData
    {
        public EvalBoardData(CvMat dataFrame, CvMat auxFrame)
        {
            DataFrame = dataFrame;
            AuxFrame = auxFrame;
        }

        public CvMat DataFrame { get; private set; }

        public CvMat AuxFrame { get; private set; }
    }
}
