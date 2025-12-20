using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using NetShaper.Abstractions;
using NetShaper.Rules;
using NetShaper.Rules.Rules;

namespace NetShaper.Tests.Benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            // Set High Priority and Affinity to Core 1 (strictly for stable benchmarking)
            Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.High;
            try 
            {
                Process.GetCurrentProcess().ProcessorAffinity = (IntPtr)(1 << 1); 
            }
            catch { /* Ignore if affinity fails (e.g. single core) */ }

            Console.WriteLine("NetShaper.Rules Benchmark & Integration Test");
            Console.WriteLine("============================================");
            
            TestShortCircuit();
            TestModify();
            TestStress(0);
            TestStress(32);
            TestHotSwap();
        }
        
        static void TestShortCircuit()
        {
            Console.WriteLine("\n[Integration] Testing Short-Circuit (DropRule)...");
            
            var mockCapture = new MockPacketCapture();
            var pipeline = new RulePipeline();
            var ruleCapture = new RulePacketCapture(mockCapture, pipeline);
            
            // Rules: DropRule followed by a "Spy" rule that counts executions
            int spyCount = 0;
            RuleFunc spyRule = (ReadOnlySpan<byte> p, ref PacketMetadata m, ref RuleState s, ref ActionResult r) => 
            {
                spyCount++;
                return ActionMask.None;
            };
            
            // Setup Drop Rule
            var dropRule = DropRule.Create();
            var dropState = DropRule.CreateState();
            
            var rules = new[] { dropRule, spyRule };
            var states = new[] { dropState, default(RuleState) };
            
            // Activate
            pipeline.Swap(Ruleset.Create(rules, states, RuleCapability.HasDropRules));
            
            // Send packet
            byte[] buf = new byte[64];
            var meta = new PacketMetadata();
            ruleCapture.Send(buf, ref meta);
            
            // Checks
            Console.WriteLine($"Spy executed: {spyCount} (Expected: 0)");
            Console.WriteLine($"Inner Send called: {mockCapture.SendCount} (Expected: 0)");
            
            if (spyCount == 0 && mockCapture.SendCount == 0)
                Console.WriteLine("PASSED");
            else
                Console.WriteLine("FAILED");
        }
        
        static void TestModify()
        {
            Console.WriteLine("\n[Integration] Testing Modify (Truncate/Corrupt)...");
            
            var mockCapture = new MockPacketCapture();
            var pipeline = new RulePipeline();
            var ruleCapture = new RulePacketCapture(mockCapture, pipeline);
            
            // Setup Corrupt Rule
            var tamperRule = TamperRule.Create();
            var tamperState = TamperRule.CreateState(ModifyFlags.Corrupt);
            
            var rules = new[] { tamperRule };
            var states = new[] { tamperState };
            
            pipeline.Swap(Ruleset.Create(rules, states, RuleCapability.HasModifyRules));
            
            // Send packet (needs enough size > 50 for corrupt)
            byte[] buf = new byte[64];
            for (int i = 0; i < buf.Length; i++) buf[i] = (byte)i; // Fill pattern
            byte originalVal = buf[50];
            
            var meta = new PacketMetadata();
            ruleCapture.Send(buf, ref meta);
            
            // Checks
            var lastPacket = mockCapture.LastPacket;
            bool passed = true;
            
            if (lastPacket == null || lastPacket.Length < 50)
            {
                 Console.WriteLine("Packet not received or too short.");
                 passed = false;
            }
            else
            {
                // Verify ONLY byte 50 changed
                for (int i = 0; i < buf.Length; i++)
                {
                    if (i == 50)
                    {
                        if (lastPacket[i] == originalVal)
                        {
                            Console.WriteLine($"Byte[50] NOT modified. Got {lastPacket[i]:X2}");
                            passed = false;
                        }
                    }
                    else
                    {
                        if (lastPacket[i] != buf[i])
                        {
                            Console.WriteLine($"Corruption at Byte[{i}]. Expected {buf[i]:X2}, Got {lastPacket[i]:X2}");
                            passed = false;
                            break;
                        }
                    }
                }
                
                if (passed)
                {
                     Console.WriteLine($"Byte[50]: 0x{lastPacket[50]:X2} (Expected: 0x{originalVal ^ 0xFF:X2})");
                }
            }
            
            if (passed)
                Console.WriteLine("PASSED");
            else
                Console.WriteLine("FAILED");
        }
        
        static void TestStress(int ruleCount)
        {
            Console.WriteLine($"\n[Stress] Testing overhead with {ruleCount} rules...");
            
            var mockCapture = new MockPacketCapture();
            var pipeline = new RulePipeline();
            var ruleCapture = new RulePacketCapture(mockCapture, pipeline);
            
            if (ruleCount > 0)
            {
                var rules = new RuleFunc[ruleCount];
                var states = new RuleState[ruleCount];
                for(int i=0; i<ruleCount; i++)
                {
                    rules[i] = (ReadOnlySpan<byte> p, ref PacketMetadata m, ref RuleState s, ref ActionResult r) => ActionMask.None;
                    states[i] = default;
                }
                // IMPORTANT: Use HasModifyRules to force pipeline loop entry (avoid early exit)
                pipeline.Swap(Ruleset.Create(rules, states, RuleCapability.HasModifyRules));
            }
            else
            {
                pipeline.Swap(Ruleset.Create(Array.Empty<RuleFunc>(), Array.Empty<RuleState>(), RuleCapability.None));
            }
            
            byte[] buf = new byte[1500];
            var meta = new PacketMetadata();
            
            // Warmup
            for(int i=0; i<1000; i++) ruleCapture.Send(buf, ref meta);
            
            long count = 10_000_000;
            var sw = Stopwatch.StartNew();
            
            for(long i=0; i<count; i++)
            {
                ruleCapture.Send(buf, ref meta);
            }
            
            sw.Stop();
            double elapsedSec = sw.Elapsed.TotalSeconds;
            double pps = count / elapsedSec;
            double nsPerPacket = (elapsedSec * 1e9) / count;
            
            Console.WriteLine($"Packets: {count:N0}");
            Console.WriteLine($"Time: {elapsedSec:F3} s");
            Console.WriteLine($"PPS: {pps:N0}");
            Console.WriteLine($"Latency: {nsPerPacket:F1} ns/packet");
        }
        
        static void TestHotSwap()
        {
            Console.WriteLine("\n[Stress] Testing Hot Swap under load...");
            
            var mockCapture = new MockPacketCapture();
            var pipeline = new RulePipeline();
            var ruleCapture = new RulePacketCapture(mockCapture, pipeline);
            
            bool running = true;
            long packets = 0;
            
            // Loader thread
            var t = new Thread(() => 
            {
                byte[] buf = new byte[64];
                var meta = new PacketMetadata();
                // Ensure affinity for loader thread too
                try {
                Thread.BeginThreadAffinity();
                } catch {} 

                while(Volatile.Read(ref running))
                {
                    ruleCapture.Send(buf, ref meta);
                    packets++;
                }
                try {
                Thread.EndThreadAffinity();
                } catch {}
            });
            t.Start();
            
            // Swap loop
            var sw = Stopwatch.StartNew();
            int swaps = 0;
            while(sw.ElapsedMilliseconds < 2000)
            {
                // Swap between 0 and 10 rules
                int count = (swaps % 2 == 0) ? 0 : 10;
                var rules = new RuleFunc[count];
                var states = new RuleState[count];
                
                // Use ThrottleRule for realism logic
                for(int i=0; i<count; i++)
                {
                    rules[i] = ThrottleRule.Create();
                    states[i] = ThrottleRule.CreateState(100000);
                }
                
                pipeline.Swap(Ruleset.Create(rules, states, count > 0 ? RuleCapability.HasDropRules : RuleCapability.None));
                swaps++;
                Thread.SpinWait(5000); // Replacing Sleep(1) with busy wait for tighter loop
            }
            
            running = false;
            t.Join();
            
            Console.WriteLine($"Swaps: {swaps}");
            Console.WriteLine($"Total Packets: {packets:N0}");
            Console.WriteLine("PASSED (No Crash)");
        }
    }
    
    class MockPacketCapture : IPacketCapture
    {
        public int SendCount;
        public byte[]? LastPacket;
        
        public CaptureResult Open(string filter) => CaptureResult.Success;
        public CaptureResult Receive(Span<byte> buffer, out uint length, ref PacketMetadata metadata) { length = 0; return CaptureResult.ElementNotFound; }
        public CaptureResult ReceiveBatch(Span<byte> buffer, Span<PacketMetadata> metadataArray, out uint batchLength, out int packetCount) { batchLength = 0; packetCount = 0; return CaptureResult.ElementNotFound; }
        
        public CaptureResult Send(ReadOnlySpan<byte> buffer, ref PacketMetadata metadata)
        {
            SendCount++;
            if (LastPacket == null || LastPacket.Length != buffer.Length)
                LastPacket = new byte[buffer.Length];
            buffer.CopyTo(LastPacket);
            return CaptureResult.Success;
        }
        
        public void CalculateChecksums(Span<byte> buffer, uint length, ref PacketMetadata metadata) { }
        public void Shutdown() { }
        public void Dispose() { }
    }
}
