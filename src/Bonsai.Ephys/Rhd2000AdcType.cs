namespace Bonsai.Ephys
{
    /// <summary>
    /// Specifies the available ADC types in a RHD2000 USB interface board.
    /// </summary>
    public enum Rhd2000AdcType
    {
        /// <summary>
        /// Bipolar electrode voltage signals sampled in steps of 0.195 microvolts.
        /// </summary>
        Electrode,

        /// <summary>
        /// Auxiliary analog input pins to the on-chip ADC, in the 0.10V-2.45V range.
        /// </summary>
        AuxiliaryInput,

        /// <summary>
        /// Supply voltage sensor used to measure local chip power supply, in the 0.2V-4.9V range.
        /// </summary>
        SupplyVoltage,

        /// <summary>
        /// RHD2000 temperature sensor channel, 0.01ºC per step.
        /// </summary>
        Temperature,

        /// <summary>
        /// RHD2000 USB interface board analog inputs, sampled in the 0V-3.3V range.
        /// </summary>
        BoardAdc
    }
}
