﻿using System.Threading.Tasks;
using BattleNetPrefill.Utils.Debug.Models;
using NUnit.Framework;
using Spectre.Console.Testing;

namespace BattleNetPrefill.Test.DownloadTests.Activision
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class CodModernWarfare2
    {
        private ComparisonResult _results;

        [OneTimeSetUp]
        public async Task Setup()
        {
            // Run the download process only once
            var debugConfig = new DebugConfig { UseCdnDebugMode = true, CompareAgainstRealRequests = true };
            _results = await TactProductHandler.ProcessProductAsync(TactProduct.CodMW2, new TestConsole(), debugConfig: debugConfig);
        }

        [Test]
        public void Misses()
        {
            Assert.AreEqual(0, _results.MissCount);
        }

        [Test]
        public void MissedBandwidth()
        {
            Assert.AreEqual(0, _results.MissedBandwidth.Bytes);
        }

        [Test]
        public void WastedBandwidth()
        {
            Assert.AreEqual(0, _results.WastedBandwidth.Bytes);
        }
    }
}
