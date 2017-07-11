using System.Diagnostics;

namespace PrettyOnionNameFinderRole
{
    public static class PerfCounter
    {
        private const string counterCategory = "PrettyOnionNameFinder";

        internal static PerformanceCounter CounterStarted;
        internal static PerformanceCounter CounterValided;

        public static void Init()
        {
            /* <!> Need to be done by Powershell : current right are not enouth : <Runtime executionContext="elevated" /> don't seems to work now. */
            if (!PerformanceCounterCategory.Exists(counterCategory))
            {
                CounterCreationDataCollection counterCollection = new CounterCreationDataCollection
                {
                    new CounterCreationData()
                    {
                        CounterName = "Started",
                        CounterHelp = "PrettyOnionNameFinder Try new hostname",
                        CounterType = PerformanceCounterType.NumberOfItems32
                    },
                    new CounterCreationData()
                    {
                        CounterName = "Valided",
                        CounterHelp = "PrettyOnionNameFinder Value Found",
                        CounterType = PerformanceCounterType.NumberOfItems32
                    }
                };
                PerformanceCounterCategory.Create(
                  counterCategory,
                  "PrettyOnionNameFinder Category",
                  PerformanceCounterCategoryType.SingleInstance, counterCollection); // won't work ! not enouth right on azure cloud right now ! 
            }

            CounterStarted = new PerformanceCounter(counterCategory, "Started", string.Empty, false);
            CounterValided = new PerformanceCounter(counterCategory, "Valided", string.Empty, false);
            CounterStarted.RawValue = 0; // show that it s a new run by starting @0
            CounterValided.RawValue = 0; // show that it s a new run by starting @0
        }

    }
}
