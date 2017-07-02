using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Reactive.Linq;
using Rhythm.Net;
using System.Reactive.Disposables;
using System.ComponentModel;
using System.Drawing.Design;
using System.Threading;

namespace Bonsai.Ephys
{
    [Description("Produces a sequence of buffered samples acquired from any RHD2000 compatible USB interface board.")]
    public class Rhd2000EvalBoard : Source<Rhd2000DataFrame>
    {
        const string CableDelayCategory = "Delay Settings";
        const int ChipIdRhd2132 = 1;
        const int ChipIdRhd2216 = 2;
        const int ChipIdRhd2164 = 4;
        IObservable<Rhd2000DataFrame> source;
        Rhd2000Registers chipRegisters;
        Rhythm.Net.Rhd2000EvalBoard evalBoard;
        double cableLengthPortA;
        double cableLengthPortB;
        double cableLengthPortC;
        double cableLengthPortD;
        int samplesPerBlock;

        public Rhd2000EvalBoard()
        {
            BitFileName = "main.bit";
            SampleRate = AmplifierSampleRate.SampleRate20000Hz;
            LowerBandwidth = 0.1;
            UpperBandwidth = 7500.0;
            DspCutoffFrequency = 1.0;
            DspEnabled = true;

            source = Observable.Create<Rhd2000DataFrame>(observer =>
            {
                Load();
                var running = true;
                evalBoard.SetContinuousRunMode(true);
                evalBoard.Run();
                var thread = new Thread(() =>
                {
                    var blocksToRead = GetDataBlockReadCount(SampleRate);
                    var fifoCapacity = Rhythm.Net.Rhd2000EvalBoard.FifoCapacityInWords();
                    var ledSequence = LedSequence();

                    var queue = new Queue<Rhd2000DataBlock>();
                    ledSequence.MoveNext();
                    while (running)
                    {
                        if (evalBoard.ReadDataBlocks(blocksToRead, queue))
                        {
                            var wordsInFifo = evalBoard.NumWordsInFifo();
                            var bufferPercentageCapacity = 100.0 * wordsInFifo / fifoCapacity;
                            foreach (var dataBlock in queue)
                            {
                                observer.OnNext(new Rhd2000DataFrame(dataBlock, bufferPercentageCapacity));
                            }
                            queue.Clear();
                            ledSequence.MoveNext();
                        }
                    }

                    ledSequence.Dispose();
                });

                thread.Start();
                return () =>
                {
                    running = false;
                    if (thread != Thread.CurrentThread) thread.Join();
                    evalBoard.SetContinuousRunMode(false);
                    evalBoard.SetMaxTimeStep(0);
                    evalBoard.Flush();
                    evalBoard.Close();
                    evalBoard = null;
                    chipRegisters = null;
                };
            })
            .PublishReconnectable()
            .RefCount();
        }

        [FileNameFilter("BIT Files|*.bit")]
        [Editor("Bonsai.Design.OpenFileNameEditor, Bonsai.Design", typeof(UITypeEditor))]
        [Description("The name of the Rhythm bitfile used to configure the Xilinx FPGA.")]
        public string BitFileName { get; set; }

        [Description("The per-channel sampling rate.")]
        public AmplifierSampleRate SampleRate { get; set; }

        [Description("Specifies whether to fast settle amplifiers when reconfiguring the evaluation board.")]
        public bool FastSettle { get; set; }

        [Description("The lower bandwidth of the amplifier on-board DSP filter (Hz).")]
        public double LowerBandwidth { get; set; }

        [Description("The upper bandwidth of the amplifier on-board DSP filter (Hz).")]
        public double UpperBandwidth { get; set; }

        [Description("The cutoff frequency of the DSP offset removal filter (Hz).")]
        public double DspCutoffFrequency { get; set; }

        [Description("Specifies whether the DSP offset removal filter is enabled.")]
        public bool DspEnabled { get; set; }

        [Category(CableDelayCategory)]
        [Description("The optional delay for sampling the MISO line in port A, in integer clock steps.")]
        public int? CableDelayA { get; set; }

        [Category(CableDelayCategory)]
        [Description("The optional delay for sampling the MISO line in port B, in integer clock steps.")]
        public int? CableDelayB { get; set; }

        [Category(CableDelayCategory)]
        [Description("The optional delay for sampling the MISO line in port C, in integer clock steps.")]
        public int? CableDelayC { get; set; }

        [Category(CableDelayCategory)]
        [Description("The optional delay for sampling the MISO line in port D, in integer clock steps.")]
        public int? CableDelayD { get; set; }

        IEnumerator<int> LedSequence()
        {
            var ledArray = new[] { 1, 0, 0, 0, 0, 0, 0, 0 };
            var ledIndex = 0;
            evalBoard.SetLedDisplay(ledArray);
            yield return ledIndex;

            try
            {
                while (true)
                {
                    ledArray[ledIndex] = 0;
                    ledIndex = (ledIndex + 1) % ledArray.Length;
                    ledArray[ledIndex] = 1;
                    evalBoard.SetLedDisplay(ledArray);
                    yield return ledIndex;
                }
            }
            finally
            {
                ledArray[ledIndex] = 0;
                evalBoard.SetLedDisplay(ledArray);
            }
        }

        void ImpedanceMeasurement()
        {
            var actualImpedanceFreq = 0.0;

            var sampleRate = evalBoard.GetSampleRate();
            var chipRegisters = new Rhd2000Registers(sampleRate);
            var ledSequence = LedSequence();
            ledSequence.MoveNext();

            // Create a command list for the AuxCmd1 slot.
            var commandList = new List<int>();
            var sequenceLength = chipRegisters.CreateCommandListZcheckDac(commandList, actualImpedanceFreq, 128.0);
            evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd1, 1);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd1, 0, sequenceLength - 1);

            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd1, 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd1, 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd1, 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd1, 1);

            // Select number of periods to measure impedance over
            int numPeriods = (int)Math.Round(0.020 * actualImpedanceFreq); // Test each channel for at least 20 msec...
            if (numPeriods < 5) numPeriods = 5; // ...but always measure across no fewer than 5 complete periods

            double period = sampleRate / actualImpedanceFreq;
            int numBlocks = (int)Math.Ceiling((numPeriods + 2.0) * period / 60.0); // + 2 periods to give time to settle initially
            if (numBlocks < 2) numBlocks = 2; // need first block for command to switch channels to take effect.

            chipRegisters.SetDspCutoffFreq(DspCutoffFrequency);
            chipRegisters.SetLowerBandwidth(LowerBandwidth);
            chipRegisters.SetUpperBandwidth(UpperBandwidth);
            chipRegisters.EnableDsp(DspEnabled);
            chipRegisters.EnableZcheck(true);

            sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, false);
            // Upload version with no ADC calibration to AuxCmd3 RAM Bank 1.
            evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 3);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 3);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 3);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 3);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 3);

            evalBoard.SetContinuousRunMode(false);
            evalBoard.SetMaxTimeStep((uint)samplesPerBlock * (uint)numBlocks);

            // Create matrices of doubles of size (numStreams x 32 x 3) to store complex amplitudes
            // of all amplifier channels (32 on each data stream) at three different Cseries values.
            var numChannels = 32;
            var numStreams = evalBoard.GetNumEnabledDataStreams();
            var measuredMagnitude = new double[numStreams][,];
            var measuredPhase = new double[numStreams][,];
            for (int i = 0; i < numStreams; i++)
            {
                measuredMagnitude[i] = new double[numChannels, 3];
                measuredPhase[i] = new double[numChannels, 3];
            }

            // We execute three complete electrode impedance measurements: one each with
            // Cseries set to 0.1 pF, 1 pF, and 10 pF.  Then we select the best measurement
            // for each channel so that we achieve a wide impedance measurement range.
            Queue<Rhd2000DataBlock> dataQueue = new Queue<Rhd2000DataBlock>();
            for (int capRange = 0; capRange < 3; capRange++)
            {
                double cSeries;
                switch (capRange)
                {
                    case 0:
                        chipRegisters.SetZcheckScale(ZcheckCs.ZcheckCs100fF);
                        cSeries = 0.1e-12;
                        break;
                    case 1:
                        chipRegisters.SetZcheckScale(ZcheckCs.ZcheckCs1pF);
                        cSeries = 1.0e-12;
                        break;
                    default:
                        chipRegisters.SetZcheckScale(ZcheckCs.ZcheckCs10pF);
                        cSeries = 10.0e-12;
                        break;
                }

                // Check all 32 channels across all active data streams.
                for (int channel = 0; channel < numChannels; channel++)
                {
                    chipRegisters.SetZcheckChannel(channel);
                    sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, false);
                    // Upload version with no ADC calibration to AuxCmd3 RAM Bank 1.
                    evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 3);

                    // Start SPI interface and wait for command to complete.
                    evalBoard.Run();
                    while (evalBoard.IsRunning()) Thread.Sleep(0);
                    evalBoard.ReadDataBlocks(numBlocks, dataQueue);
                    MeasureComplexAmplitude(measuredMagnitude, measuredPhase, null, capRange, channel, numStreams, numBlocks, sampleRate, actualImpedanceFreq, numPeriods);

                    // Advance LED display
                    ledSequence.MoveNext();
                }
            }

            evalBoard.SetContinuousRunMode(false);
            evalBoard.SetMaxTimeStep(0);
            evalBoard.Flush();

            // Switch back to flatline
            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd1, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd1, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd1, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd1, 0);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd1, 0, 1);
            UpdateRegisterConfiguration();

            // Turn off LED.
            ledSequence.Dispose();
        }

        void MeasureComplexAmplitude(double[][,] measuredMagnitude, double[][,] measuredPhase, int[][,] amplifierData, int capIndex, int chipChannel, int numStreams, int numBlocks, double sampleRate, double frequency, int numPeriods)
        {
            var period = (int)Math.Round(sampleRate / frequency);
            var startIndex = 0;
            var endIndex = startIndex + numPeriods * period - 1;

            // Move the measurement window to the end of the waveform to ignore start-up transient.
            while (endIndex < samplesPerBlock * numBlocks - period)
            {
                startIndex += period;
                endIndex += period;
            }

            for (int stream = 0; stream < numStreams; stream++)
            {
                double iComponent, qComponent;
                // Measure real (iComponent) and imaginary (qComponent) amplitude of frequency component.
                AmplitudeOfFreqComponent(out iComponent, out qComponent, null, startIndex, endIndex, sampleRate, frequency);
                // Calculate magnitude and phase from real (I) and imaginary (Q) components.
                measuredMagnitude[stream][chipChannel, capIndex] = Math.Sqrt(iComponent * iComponent + qComponent * qComponent);
                measuredPhase[stream][chipChannel, capIndex] = Math.Atan2(qComponent, iComponent);
            }
        }

        void AmplitudeOfFreqComponent(out double realComponent, out double imagComponent, double[] data, int startIndex, int endIndex, double sampleRate, double frequency)
        {
            var length = endIndex - startIndex + 1;
            var k = 2 * Math.PI * frequency / sampleRate; // precalculate for speed

            // Perform correlation with sine and cosine waveforms.
            var meanI = 0.0;
            var meanQ = 0.0;
            for (int t = startIndex; t <= endIndex; ++t)
            {
                meanI += data[t] * Math.Cos(k * t);
                meanQ += data[t] * -1.0 * Math.Sin(k * t);
            }
            meanI /= length;
            meanQ /= length;

            realComponent = 2.0 * meanI;
            imagComponent = 2.0 * meanQ;
        }

        Rhd2000DataBlock RunSingleCommandSequence()
        {
            // Start SPI interface.
            evalBoard.Run();

            // Wait for the 60-sample run to complete.
            while (evalBoard.IsRunning()) Thread.Sleep(0);

            // Read the resulting single data block from the USB interface.
            var dataBlock = new Rhd2000DataBlock(evalBoard.GetNumEnabledDataStreams(), evalBoard.IsUsb3());
            evalBoard.ReadDataBlock(dataBlock);
            return dataBlock;
        }

        int ReadDeviceId(Rhd2000DataBlock dataBlock, int stream, out int register59Value)
        {
            // First, check ROM registers 32-36 to verify that they hold 'INTAN', and
            // the initial chip name ROM registers 24-26 that hold 'RHD'.
            // This is just used to verify that we are getting good data over the SPI
            // communication channel.
            var intanChipPresent = (
                (char)dataBlock.AuxiliaryData[stream][2, 32] == 'I' &&
                (char)dataBlock.AuxiliaryData[stream][2, 33] == 'N' &&
                (char)dataBlock.AuxiliaryData[stream][2, 34] == 'T' &&
                (char)dataBlock.AuxiliaryData[stream][2, 35] == 'A' &&
                (char)dataBlock.AuxiliaryData[stream][2, 36] == 'N' &&
                (char)dataBlock.AuxiliaryData[stream][2, 24] == 'R' &&
                (char)dataBlock.AuxiliaryData[stream][2, 25] == 'H' &&
                (char)dataBlock.AuxiliaryData[stream][2, 26] == 'D');

            // If the SPI communication is bad, return -1.  Otherwise, return the Intan
            // chip ID number stored in ROM regstier 63.
            if (!intanChipPresent)
            {
                register59Value = -1;
                return -1;
            }
            else
            {
                register59Value = dataBlock.AuxiliaryData[stream][2, 23]; // Register 59
                return dataBlock.AuxiliaryData[stream][2, 19]; // chip ID (Register 63)
            }
        }

        void ScanConnectedAmplifiers()
        {
            // Set sampling rate to highest value for maximum temporal resolution.
            ChangeSampleRate(AmplifierSampleRate.SampleRate30000Hz);

            // Enable all data streams, and set sources to cover one or two chips
            // on Ports A-D.
            evalBoard.SetDataSource(0, BoardDataSource.PortA1);
            evalBoard.SetDataSource(1, BoardDataSource.PortA2);
            evalBoard.SetDataSource(2, BoardDataSource.PortB1);
            evalBoard.SetDataSource(3, BoardDataSource.PortB2);
            evalBoard.SetDataSource(4, BoardDataSource.PortC1);
            evalBoard.SetDataSource(5, BoardDataSource.PortC2);
            evalBoard.SetDataSource(6, BoardDataSource.PortD1);
            evalBoard.SetDataSource(7, BoardDataSource.PortD2);

            var maxNumDataStreams = Rhythm.Net.Rhd2000EvalBoard.MaxNumDataStreams(evalBoard.IsUsb3());
            for (int i = 0; i < maxNumDataStreams; i++)
            {
                evalBoard.EnableDataStream(i, true);
            }

            // Select RAM Bank 0 for AuxCmd3
            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 0);

            // Since our longest command sequence is 60 commands, we run the SPI
            // interface for 60 samples (256 for usb3 power-of two needs).
            evalBoard.SetMaxTimeStep((uint)samplesPerBlock);
            evalBoard.SetContinuousRunMode(false);

            // Run SPI command sequence at all 16 possible FPGA MISO delay settings
            // to find optimum delay for each SPI interface cable.
            var maxNumChips = 8;
            var chipId = new int[maxNumChips];
            var optimumDelays = new int[maxNumChips];
            var secondDelays = new int[maxNumChips];
            var goodDelayCounts = new int[maxNumChips];
            for (int i = 0; i < optimumDelays.Length; i++)
            {
                optimumDelays[i] = -1;
                secondDelays[i] = -1;
                goodDelayCounts[i] = 0;
            }

            for (int delay = 0; delay < 16; delay++)
            {
                evalBoard.SetCableDelay(BoardPort.PortA, delay);
                evalBoard.SetCableDelay(BoardPort.PortB, delay);
                evalBoard.SetCableDelay(BoardPort.PortC, delay);
                evalBoard.SetCableDelay(BoardPort.PortD, delay);

                // Run SPI command sequence
                var dataBlock = RunSingleCommandSequence();

                // Read the Intan chip ID number from each RHD2000 chip found.
                // Record delay settings that yield good communication with the chip.
                for (int chipIdx = 0; chipIdx < chipId.Length; chipIdx++)
                {
                    int register59Value;
                    var id = ReadDeviceId(dataBlock, chipIdx, out register59Value);
                    if (id > 0)
                    {
                        chipId[chipIdx] = id;
                        goodDelayCounts[chipIdx]++;
                        switch (goodDelayCounts[chipIdx])
                        {
                            case 1: optimumDelays[chipIdx] = delay; break;
                            case 2: secondDelays[chipIdx] = delay; break;
                            case 3: optimumDelays[chipIdx] = secondDelays[chipIdx]; break;
                        }
                    }
                }
            }

            // Now that we know which RHD2000 amplifier chips are plugged into each SPI port,
            // add up the total number of amplifier channels on each port and calculate the number
            // of data streams necessary to convey this data over the USB interface.
            var numStreamsRequired = 0;
            var rhd2216ChipPresent = false;
            for (int chipIdx = 0; chipIdx < chipId.Length; ++chipIdx)
            {
                switch (chipId[chipIdx])
                {
                    case ChipIdRhd2216:
                        numStreamsRequired++;
                        rhd2216ChipPresent = true;
                        break;
                    case ChipIdRhd2132:
                        numStreamsRequired++;
                        break;
                    case ChipIdRhd2164:
                        numStreamsRequired += 2;
                        break;
                    default:
                        break;
                }
            }

            // If the user plugs in more chips than the USB interface can support, throw an exception
            if (numStreamsRequired > maxNumDataStreams)
            {
                var capacityExceededMessage = "Capacity of USB Interface Exceeded. This RHD2000 USB interface board can only support {0} amplifier channels.";
                if (rhd2216ChipPresent) capacityExceededMessage += " (Each RHD2216 chip counts as 32 channels for USB interface purposes.)";
                capacityExceededMessage = string.Format(capacityExceededMessage, maxNumDataStreams * 32);
                throw new InvalidOperationException(capacityExceededMessage);
            }

            // Reconfigure USB data streams in consecutive order to accommodate all connected chips.
            int activeStream = 0;
            for (int chipIdx = 0; chipIdx < chipId.Length; ++chipIdx)
            {
                if (chipId[chipIdx] > 0)
                {
                    evalBoard.EnableDataStream(activeStream, true);
                    evalBoard.SetDataSource(activeStream, (BoardDataSource)chipIdx);
                    if (chipId[chipIdx] == ChipIdRhd2164)
                    {
                        evalBoard.EnableDataStream(activeStream + 1, true);
                        evalBoard.SetDataSource(activeStream + 1, (BoardDataSource)(chipIdx + maxNumDataStreams));
                        activeStream += 2;
                    }
                    else activeStream++;
                }
                else optimumDelays[chipIdx] = 0;
            }

            // Now, disable data streams where we did not find chips present.
            for (; activeStream < maxNumDataStreams; activeStream++)
            {
                evalBoard.EnableDataStream(activeStream, false);
            }

            // Set cable delay settings that yield good communication with each
            // RHD2000 chip.
            var optimumDelayA = CableDelayA.GetValueOrDefault(Math.Max(optimumDelays[0], optimumDelays[1]));
            var optimumDelayB = CableDelayB.GetValueOrDefault(Math.Max(optimumDelays[2], optimumDelays[3]));
            var optimumDelayC = CableDelayC.GetValueOrDefault(Math.Max(optimumDelays[4], optimumDelays[5]));
            var optimumDelayD = CableDelayD.GetValueOrDefault(Math.Max(optimumDelays[6], optimumDelays[7]));
            evalBoard.SetCableDelay(BoardPort.PortA, optimumDelayA);
            evalBoard.SetCableDelay(BoardPort.PortB, optimumDelayB);
            evalBoard.SetCableDelay(BoardPort.PortC, optimumDelayC);
            evalBoard.SetCableDelay(BoardPort.PortD, optimumDelayD);
            cableLengthPortA = evalBoard.EstimateCableLengthMeters(optimumDelayA);
            cableLengthPortB = evalBoard.EstimateCableLengthMeters(optimumDelayB);
            cableLengthPortC = evalBoard.EstimateCableLengthMeters(optimumDelayC);
            cableLengthPortD = evalBoard.EstimateCableLengthMeters(optimumDelayD);

            // Return sample rate to original user-selected value.
            ChangeSampleRate(SampleRate);
        }

        void ChangeSampleRate(AmplifierSampleRate amplifierSampleRate)
        {
            evalBoard.SetSampleRate(amplifierSampleRate);

            // Now that we have set our sampling rate, we can set the MISO sampling delay
            // which is dependent on the sample rate.
            evalBoard.SetCableLengthMeters(BoardPort.PortA, cableLengthPortA);
            evalBoard.SetCableLengthMeters(BoardPort.PortB, cableLengthPortB);
            evalBoard.SetCableLengthMeters(BoardPort.PortC, cableLengthPortC);
            evalBoard.SetCableLengthMeters(BoardPort.PortD, cableLengthPortD);

            // Set up an RHD2000 register object using this sample rate to
            // optimize MUX-related register settings.
            var sampleRate = evalBoard.GetSampleRate();
            chipRegisters = new Rhd2000Registers(sampleRate);
            var commandList = new List<int>();

            // Create a command list for the AuxCmd1 slot.  This command sequence will create a 250 Hz,
            // zero-amplitude sine wave (i.e., a flatline).  We will change this when we want to perform
            // impedance testing.
            var sequenceLength = chipRegisters.CreateCommandListZcheckDac(commandList, 250, 0);
            evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd1, 0);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd1, 0, sequenceLength - 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd1, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd1, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd1, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd1, 0);

            // Next, we'll create a command list for the AuxCmd2 slot.  This command sequence
            // will sample the temperature sensor and other auxiliary ADC inputs.
            sequenceLength = chipRegisters.CreateCommandListTempSensor(commandList);
            evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd2, 0);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd2, 0, sequenceLength - 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd2, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd2, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd2, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd2, 0);

            // For the AuxCmd3 slot, we will create three command sequences.  All sequences
            // will configure and read back the RHD2000 chip registers, but one sequence will
            // also run ADC calibration.  Another sequence will enable amplifier 'fast settle'.

            // Before generating register configuration command sequences, set amplifier
            // bandwidth parameters.
            chipRegisters.SetDspCutoffFreq(DspCutoffFrequency);
            chipRegisters.SetLowerBandwidth(LowerBandwidth);
            chipRegisters.SetUpperBandwidth(UpperBandwidth);
            chipRegisters.EnableDsp(DspEnabled);

            // Upload version with ADC calibration to AuxCmd3 RAM Bank 0.
            sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, true);
            evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);

            // Upload version with no ADC calibration to AuxCmd3 RAM Bank 1.
            sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, false);
            evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 1);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);

            // Upload version with fast settle enabled to AuxCmd3 RAM Bank 2.
            chipRegisters.SetFastSettle(true);
            sequenceLength = chipRegisters.CreateCommandListRegisterConfig(commandList, false);
            evalBoard.UploadCommandList(commandList, AuxCmdSlot.AuxCmd3, 2);
            evalBoard.SelectAuxCommandLength(AuxCmdSlot.AuxCmd3, 0, sequenceLength - 1);
            chipRegisters.SetFastSettle(false);

            UpdateRegisterConfiguration();
        }

        void UpdateRegisterConfiguration()
        {
            var fastSettle = FastSettle;
            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, fastSettle ? 2 : 1);
        }

        void RunCalibration()
        {
            // Select RAM Bank 0 for AuxCmd3 initially, so the ADC is calibrated.
            evalBoard.SelectAuxCommandBank(BoardPort.PortA, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortB, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortC, AuxCmdSlot.AuxCmd3, 0);
            evalBoard.SelectAuxCommandBank(BoardPort.PortD, AuxCmdSlot.AuxCmd3, 0);

            // Since our longest command sequence is 60 commands, we run the SPI
            // interface for 60 samples (256 for usb3 power-of two needs).
            evalBoard.SetMaxTimeStep((uint)samplesPerBlock);
            evalBoard.SetContinuousRunMode(false);

            // Run ADC calibration command sequence
            RunSingleCommandSequence();

            // Now that ADC calibration has been performed, we switch to the command sequence
            // that does not execute ADC calibration.
            UpdateRegisterConfiguration();
        }

        void SetDacDefaultConfiguration()
        {
            for (int i = 0; i < 8; i++)
            {
                evalBoard.EnableDac(i, false);
                evalBoard.SelectDacDataStream(i, 0);
                evalBoard.SelectDacDataChannel(i, 0);
            }

            evalBoard.SetDacManual(32768);
            evalBoard.SetDacGain(0);
            evalBoard.SetAudioNoiseSuppress(0);
        }

        int GetDataBlockReadCount(AmplifierSampleRate sampleRate)
        {
            switch (sampleRate)
            {
                case AmplifierSampleRate.SampleRate1000Hz:
                case AmplifierSampleRate.SampleRate1250Hz:
                case AmplifierSampleRate.SampleRate1500Hz:
                case AmplifierSampleRate.SampleRate2000Hz:
                case AmplifierSampleRate.SampleRate2500Hz:
                    return 1;
                case AmplifierSampleRate.SampleRate3000Hz:
                case AmplifierSampleRate.SampleRate3333Hz:
                case AmplifierSampleRate.SampleRate4000Hz:
                    return 2;
                case AmplifierSampleRate.SampleRate5000Hz:
                case AmplifierSampleRate.SampleRate6250Hz:
                    return 3;
                case AmplifierSampleRate.SampleRate8000Hz:
                    return 4;
                case AmplifierSampleRate.SampleRate10000Hz:
                    return 6;
                case AmplifierSampleRate.SampleRate12500Hz:
                    return 7;
                case AmplifierSampleRate.SampleRate15000Hz:
                    return 8;
                case AmplifierSampleRate.SampleRate20000Hz:
                    return 12;
                case AmplifierSampleRate.SampleRate25000Hz:
                    return 14;
                case AmplifierSampleRate.SampleRate30000Hz:
                    return 16;
                default:
                    throw new ArgumentException("Invalid amplifier sample rate.", "sampleRate");
            }
        }

        private void Load()
        {
            evalBoard = new Rhythm.Net.Rhd2000EvalBoard();

            // Open Opal Kelly XEM6010 board.
            evalBoard.Open();
            samplesPerBlock = Rhd2000DataBlock.GetSamplesPerDataBlock(evalBoard.IsUsb3());

            try
            {
                // Load Rhythm FPGA configuration bitfile (provided by Intan Technologies).
                evalBoard.UploadFpgaBitfile(BitFileName);

                // Initialize interface board.
                evalBoard.Initialize();

                // Set sample rate and upload all auxiliary SPI command sequences.
                ChangeSampleRate(SampleRate);

                // Run ADC calibration
                RunCalibration();

                // Set default configuration for all eight DACs on interface board.
                SetDacDefaultConfiguration();

                // Find amplifier chips connected to interface board and compute their
                // optimal delay parameters.
                ScanConnectedAmplifiers();
            }
            catch
            {
                // Close interface board in case of configuration errors
                evalBoard.Close();
                throw;
            }
        }

        public override IObservable<Rhd2000DataFrame> Generate()
        {
            return source;
        }
    }
}
