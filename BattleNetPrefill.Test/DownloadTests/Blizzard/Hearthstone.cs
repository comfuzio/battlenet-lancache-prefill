﻿using System.Threading.Tasks;
using BattleNetPrefill.Utils.Debug.Models;
using ByteSizeLib;
using NUnit.Framework;
using Spectre.Console.Testing;

namespace BattleNetPrefill.Test.DownloadTests.Blizzard
{
    [TestFixture]
    [Parallelizable(ParallelScope.All)]
    public class Hearthstone
    {
        private ComparisonResult _results;

        [OneTimeSetUp]
        public async Task Setup()
        {
            // Run the download process only once
            var debugConfig = new DebugConfig { UseCdnDebugMode = true, CompareAgainstRealRequests = true};
            _results = await TactProductHandler.ProcessProductAsync(TactProduct.Hearthstone, new TestConsole(), debugConfig: debugConfig);
        }

        [Test]
        public void Misses()
        {
            //TODO improve
            Assert.LessOrEqual(_results.MissCount, 3);
        }

        [Test]
        public void MissedBandwidth()
        {
            //TODO improve
            var expected = ByteSize.FromMegaBytes(30);

            Assert.Less(_results.MissedBandwidth.Bytes, expected.Bytes);
        }

        [Test]
        public void WastedBandwidth()
        {
            //TODO improve this
            var expected = ByteSize.FromMegaBytes(30);

            Assert.Less(_results.WastedBandwidth.Bytes, expected.Bytes);
        }
    }
}
