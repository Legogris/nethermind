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

using System.Diagnostics;
using System.Text;
using Nethermind.Core.Crypto;
using Nethermind.Core.Extensions;
using Nethermind.Int256;

namespace Nethermind.Core
{
    [DebuggerDisplay("{Hash}, Value: {Value}, To: {To}, Gas: {GasLimit}")]
    public class Transaction
    {
        public const int BaseTxGasCost = 21000;

        public UInt256 Nonce { get; set; }
        public UInt256 GasPrice { get; set; }
        public long GasLimit { get; set; }
        public Address? To { get; set; }
        public UInt256 Value { get; set; }
        public byte[]? Data { get; set; }
        public byte[]? Init { get; set; }
        public Address? SenderAddress { get; set; }
        public Signature? Signature { get; set; }
        public bool IsSigned => Signature != null;
        public bool IsContractCreation => Init != null;
        public bool IsMessageCall => Data != null;
        public Keccak? Hash { get; set; }
        public PublicKey? DeliveredBy { get; set; } // tks: this is added so we do not send the pending tx back to original sources, not used yet
        public UInt256 Timestamp { get; set; }
        
        /// <summary>
        /// In-memory only property, representing order of transactions going to TxPool.
        /// </summary>
        /// <remarks>Used for sorting in edge cases.</remarks>
        public ulong PoolIndex { get; set; }

        public string ToShortString() => 
            $"[TX: hash {Hash} from {SenderAddress} to {To} with data {Data?.ToHexString() ?? Init?.ToHexString()}, gas price {GasPrice} and limit {GasLimit}, nonce {Nonce}]";

        public string ToString(string indent)
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"{indent}Hash:      {Hash}");
            builder.AppendLine($"{indent}From:      {SenderAddress}");
            builder.AppendLine($"{indent}To:        {To}");
            builder.AppendLine($"{indent}Gas Price: {GasPrice}");
            builder.AppendLine($"{indent}Gas Limit: {GasLimit}");
            builder.AppendLine($"{indent}Nonce:     {Nonce}");
            builder.AppendLine($"{indent}Value:     {Value}");
            builder.AppendLine($"{indent}Data:      {(Data ?? new byte[0]).ToHexString()}");
            builder.AppendLine($"{indent}Init:      {(Init ?? new byte[0]).ToHexString()}");
            builder.AppendLine($"{indent}Signature: {(Signature?.Bytes ?? new byte[0]).ToHexString()}");
            builder.AppendLine($"{indent}V:         {Signature?.V ?? -1}");
            builder.AppendLine($"{indent}ChainId:   {Signature?.ChainId ?? -1}");
            builder.AppendLine($"{indent}Timestamp: {Timestamp}");
            return builder.ToString();
        }

        public override string ToString() => ToString(string.Empty);
    }

    /// <summary>
    /// Transaction that is generated by the node to be included in future block. After included in the block can be handled as regular <see cref="Transaction"/>.
    /// </summary>
    public class GeneratedTransaction : Transaction { }

    /// <summary>
    /// System transaction that is to be executed by the node without including in the block. 
    /// </summary>
    public class SystemTransaction : Transaction { }

    public static class TransactionExtensions
    {
        public static bool IsSystem(this Transaction tx)
        {
            return tx is SystemTransaction || tx.SenderAddress == Address.SystemUser;
        }
    }
}
