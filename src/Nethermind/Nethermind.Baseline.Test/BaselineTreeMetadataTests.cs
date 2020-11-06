//  Copyright (c) 2018 Demerzel Solutions Limited
//  This file is part of the Nethermind library.
// 
//  The Nethermind library is free software: you can redistribute it and/or modify
//  it under the terms of the GNU Lesser General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
// 
//  The Nethermind library is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the
//  GNU Lesser General Public License for more details.
// 
//  You should have received a copy of the GNU Lesser General Public License
//  along with the Nethermind. If not, see <http://www.gnu.org/licenses/>.
// 

using Nethermind.Baseline.Tree;
using Nethermind.Core.Crypto;
using Nethermind.Core.Test.Builders;
using Nethermind.Db;
using NUnit.Framework;

namespace Nethermind.Baseline.Test
{
    [Parallelizable(ParallelScope.All)]
    public class BaselineTreeMetadataTests
    {
        [TestCase(0, 13)]
        [TestCase(1, 0)]
        [TestCase(2, 3)]
        [TestCase(3, 100)]
        public void Saving_loading_current_block(int keccakIndex, long lastBlockWithLeaves)
        {
            var lastBlockDbHash = TestItem.Keccaks[keccakIndex];
            var baselineMetaData = new BaselineTreeMetadata(new MemDb(), new byte[] { });
            baselineMetaData.SaveCurrentBlockInDb(lastBlockDbHash, lastBlockWithLeaves);
            var actual = baselineMetaData.LoadCurrentBlockInDb();
            Assert.AreEqual(lastBlockDbHash, actual.LastBlockDbHash);
            Assert.AreEqual(lastBlockWithLeaves, actual.LastBlockWithLeaves);
        }

        [TestCase(0, (uint)13, 4)]
        [TestCase(1, (uint)0, 5)]
        [TestCase(2, (uint)3, 6)]
        [TestCase(3, (uint)100, 6)]
        public void Saving_loading_block_number_count(long blockNumber, uint count, long previousBlockWithLeaves)
        {
            var baselineMetaData = new BaselineTreeMetadata(new MemDb(), new byte[] { });
            baselineMetaData.SaveBlockNumberCount(blockNumber, count, previousBlockWithLeaves);
            var actual = baselineMetaData.LoadBlockNumberCount(blockNumber);
            Assert.AreEqual(count, actual.Count);
            Assert.AreEqual(previousBlockWithLeaves, actual.PreviousBlockWithLeaves);
        }
    }
}