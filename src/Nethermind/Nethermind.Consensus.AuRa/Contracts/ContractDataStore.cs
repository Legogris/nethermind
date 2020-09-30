﻿//  Copyright (c) 2018 Demerzel Solutions Limited
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

using System;
using System.Collections.Generic;
using Nethermind.Blockchain.Processing;
using Nethermind.Core;
using Nethermind.Core.Crypto;

namespace Nethermind.Consensus.AuRa.Contracts
{
    public class ContractDataStore<T> : IDisposable, IContractDataStore<T>
    {
        private readonly IDataContract<T> _dataContract;
        private readonly IBlockProcessor _blockProcessor;
        private List<T> _items;
        private Keccak _lastHash;
        
        public ContractDataStore(IDataContract<T> dataContract, IBlockProcessor blockProcessor)
        {
            _dataContract = dataContract ?? throw new ArgumentNullException(nameof(dataContract));
            _blockProcessor = blockProcessor;
            _blockProcessor.BlockProcessed += OnBlockProcessed;
        }

        public IEnumerable<T> GetItems(BlockHeader parent)
        {
            GetItems(parent, parent.Hash == _lastHash);
            return _items;
        }
        
        private void OnBlockProcessed(object sender, BlockProcessedEventArgs e)
        {
            BlockHeader header = e.Block.Header;
            GetItems(header, header.ParentHash == _lastHash, e.TxReceipts);
        }
        
        private void GetItems(BlockHeader blockHeader, bool isConsecutiveBlock, TxReceipt[] receipts = null)
        {
            _items ??= new List<T>();
            bool fromReceipts = receipts != null;
            
            if (fromReceipts || !isConsecutiveBlock)
            {
                if (!fromReceipts || !isConsecutiveBlock || !_dataContract.IncrementalChanges)
                {
                    _items.Clear();
                }

                bool canGetFullStateFromReceipts = fromReceipts && (isConsecutiveBlock || !_dataContract.IncrementalChanges);
                
                IEnumerable<T> items = canGetFullStateFromReceipts
                    ? _dataContract.GetChangesFromBlock(blockHeader, receipts)
                    : _dataContract.GetAll(blockHeader);
                
                _items.AddRange(items);
                _lastHash = blockHeader.Hash;
            }
        }

        public void Dispose()
        {
            _blockProcessor.BlockProcessed -= OnBlockProcessed;
        }
    }
}