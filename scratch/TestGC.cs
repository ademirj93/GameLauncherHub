using System.Diagnostics;

Console.WriteLine("Process PID: " + Environment.ProcessId);
Console.WriteLine("GC Total Memory: " + (GC.GetTotalMemory(false) / 1024 / 1024) + " MB");
var info = GC.GetGCMemoryInfo();
Console.WriteLine("HeapSizeBytes: " + (info.HeapSizeBytes / 1024 / 1024) + " MB");
Console.WriteLine("FragmentedBytes: " + (info.FragmentedBytes / 1024 / 1024) + " MB");
Console.WriteLine("TotalCommittedBytes: " + (info.TotalCommittedBytes / 1024 / 1024) + " MB");
Console.WriteLine("Generation: Gen0=" + GC.CollectionCount(0) + " Gen1=" + GC.CollectionCount(1) + " Gen2=" + GC.CollectionCount(2));

