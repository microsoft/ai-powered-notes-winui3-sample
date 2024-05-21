using SharpDX.DXGI;
using System.Collections.Generic;
using System.Diagnostics;

namespace Notes
{
    internal partial class Utils
    {
        public static List<Adapter1> GetAdapters()
        {
            var factory1 = new Factory1();
            var adapters = new List<Adapter1>();
            for (int i = 0; i < factory1.GetAdapterCount1(); i++)
            {
                adapters.Add(factory1.GetAdapter1(i));
            }

            return adapters;
        }

        public static int GetBestDeviceId()
        {
            int deviceId = 0;
            Adapter1? selectedAdapter = null;
            List<Adapter1> list = GetAdapters();
            for (int i = 0; i < list.Count; i++)
            {
                Adapter1? adapter = list[i];
                Debug.WriteLine($"Adapter {i}:");
                Debug.WriteLine($"\tDescription: {adapter.Description1.Description}");
                Debug.WriteLine($"\tDedicatedVideoMemory: {(long)adapter.Description1.DedicatedVideoMemory / 1000000000}GB");
                Debug.WriteLine($"\tSharedSystemMemory: {(long)adapter.Description1.SharedSystemMemory / 1000000000}GB");
                if (selectedAdapter == null || (long)adapter.Description1.DedicatedVideoMemory > (long)selectedAdapter.Description1.DedicatedVideoMemory)
                {
                    selectedAdapter = adapter;
                    deviceId = i;
                }
            }

            return deviceId;
        }
    }
}
