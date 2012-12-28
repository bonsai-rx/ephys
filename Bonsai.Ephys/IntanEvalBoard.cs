using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenCV.Net;
using System.Reactive.Linq;
using System.Threading;
using System.Runtime.InteropServices;

namespace Bonsai.Ephys
{
    public class IntanEvalBoard : Source<CvMat>
    {
        IntanUsbSource source;

        public override IDisposable Load()
        {
            int firmwareID1 = 0;
            int firmwareID2 = 0;
            int firmwareID3 = 0;
            source = new IntanUsbSource();
            source.Open(ref firmwareID1, ref firmwareID2, ref firmwareID3);
            return base.Load();
        }

        protected override void Unload()
        {
            source.Close();
            source = null;
            base.Unload();
        }

        protected override IObservable<CvMat> Generate()
        {
            return Observable.Create<CvMat>(observer =>
            {
                var running = true;
                source.Start();
                var thread = new Thread(() =>
                {
                    while (running)
                    {
                        var data = source.CheckForUsbData();
                        if (data != null)
                        {
                            var dataFrame = data.DataFrame;
                            var numChannels = dataFrame.GetLength(0);
                            var numSamples = dataFrame.GetLength(1);
                            var dataHandle = GCHandle.Alloc(dataFrame, GCHandleType.Pinned);
                            var dataHeader = new CvMat(numChannels, numSamples, CvMatDepth.CV_32F, 1, dataHandle.AddrOfPinnedObject());
                            var output = dataHeader.Clone();
                            dataHandle.Free();
                            observer.OnNext(output);

                        }
                    }
                });

                thread.Start();
                return () =>
                {
                    running = false;
                    if (thread != Thread.CurrentThread) thread.Join();
                    source.Stop();
                };
            });
        }
    }
}
