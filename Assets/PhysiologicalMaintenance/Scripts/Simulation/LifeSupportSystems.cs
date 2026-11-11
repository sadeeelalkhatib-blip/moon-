using UnityEngine;

namespace PhysiologicalMaintenance
{
    public sealed class OxygenSystem
    {
        public float Percent { get; private set; }

        public OxygenSystem(float initialPercent) => Percent = Mathf.Clamp(initialPercent, 0f, 100f);
        public void Reset(float initialPercent) => Percent = Mathf.Clamp(initialPercent, 0f, 100f);

        public void Advance(float deltaMinutes, float exerciseDemandPerMinute, float idleUsePerMinute,
            bool regulatorEnabled, float regenerationPerMinute, float powerFactor)
        {
            float use = idleUsePerMinute + Mathf.Max(0f, exerciseDemandPerMinute);
            float supply = regulatorEnabled ? regenerationPerMinute * Mathf.Clamp01(powerFactor) : 0f;
            Percent = Mathf.Clamp(Percent + (supply - use) * deltaMinutes, 0f, 100f);
        }
    }

    public sealed class TemperatureSystem
    {
        public float Celsius { get; private set; }

        public TemperatureSystem(float initialCelsius) => Celsius = initialCelsius;
        public void Reset(float initialCelsius) => Celsius = initialCelsius;

        public void Advance(float deltaMinutes, float exerciseHeatPerMinute, float passiveDriftPerMinute,
            bool coolingEnabled, float coolingPerMinute, float powerFactor)
        {
            float cooling = coolingEnabled ? coolingPerMinute * Mathf.Clamp01(powerFactor) : 0f;
            Celsius += (Mathf.Max(0f, exerciseHeatPerMinute) + passiveDriftPerMinute - cooling) * deltaMinutes;
        }
    }

    public sealed class WasteManagementSystem
    {
        public float Stored { get; private set; }
        public float Capacity { get; }

        public WasteManagementSystem(float initialStored, float capacity)
        {
            Capacity = Mathf.Max(1f, capacity);
            Stored = Mathf.Clamp(initialStored, 0f, Capacity);
        }

        public void Reset(float initialStored) => Stored = Mathf.Clamp(initialStored, 0f, Capacity);

        public void Advance(float deltaMinutes, float exerciseGenerationPerMinute, float idleGenerationPerMinute,
            bool processorEnabled, float processingPerMinute, float powerFactor)
        {
            float generation = idleGenerationPerMinute + Mathf.Max(0f, exerciseGenerationPerMinute);
            float processing = processorEnabled ? processingPerMinute * Mathf.Clamp01(powerFactor) : 0f;
            Stored = Mathf.Clamp(Stored + (generation - processing) * deltaMinutes, 0f, Capacity);
        }
    }

    public sealed class PowerSystem
    {
        public float Stored { get; private set; }
        public float Capacity { get; }
        public float LastDemandPerMinute { get; private set; }

        public PowerSystem(float initialStored, float capacity = 100f)
        {
            Capacity = Mathf.Max(1f, capacity);
            Stored = Mathf.Clamp(initialStored, 0f, Capacity);
        }

        public float GetAvailableFactor(float deltaMinutes, float demandPerMinute, float solarSupplyPerMinute)
        {
            if (demandPerMinute <= 0f)
                return 1f;

            float availablePerMinute = deltaMinutes > 0f ? Stored / deltaMinutes : 0f;
            availablePerMinute += Mathf.Max(0f, solarSupplyPerMinute);
            return Mathf.Clamp01(availablePerMinute / demandPerMinute);
        }

        public void Advance(float deltaMinutes, float demandPerMinute, float solarSupplyPerMinute, float deliveredFactor)
        {
            LastDemandPerMinute = Mathf.Max(0f, demandPerMinute);
            float delivered = LastDemandPerMinute * Mathf.Clamp01(deliveredFactor);
            Stored = Mathf.Clamp(Stored + (Mathf.Max(0f, solarSupplyPerMinute) - delivered) * deltaMinutes, 0f, Capacity);
        }

        public void Reset(float initialStored)
        {
            Stored = Mathf.Clamp(initialStored, 0f, Capacity);
            LastDemandPerMinute = 0f;
        }
    }
}
