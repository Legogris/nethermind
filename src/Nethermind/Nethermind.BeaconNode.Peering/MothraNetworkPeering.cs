﻿using System;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethermind.Core2;
using Nethermind.Core2.Crypto;
using Nethermind.Core2.P2p;
using Nethermind.Core2.Types;
using Nethermind.Logging.Microsoft;
using Nethermind.Peering.Mothra;

namespace Nethermind.BeaconNode.Peering
{
    public class MothraNetworkPeering : INetworkPeering
    {
        private readonly ILogger _logger;
        private readonly IMothraLibp2p _mothraLibp2p;
        private readonly PeerManager _peerManager;

        public MothraNetworkPeering(ILogger<MothraNetworkPeering> logger, IMothraLibp2p mothraLibp2p,
            PeerManager peerManager)
        {
            _logger = logger;
            _mothraLibp2p = mothraLibp2p;
            _peerManager = peerManager;
        }

        public Slot HighestPeerSlot => _peerManager.HighestPeerSlot;

        public Slot SyncStartingSlot => _peerManager.SyncStartingSlot;

        public Task DisconnectPeerAsync(string peerId)
        {
            // NOTE: Mothra does not support peer disconnect, so nothing to do.
            _peerManager.DisconnectSession(peerId);
            return Task.CompletedTask;
        }

        public Task PublishBeaconBlockAsync(SignedBeaconBlock signedBlock)
        {
            // TODO: Validate signature before broadcasting (if not already validated)

            Span<byte> encoded = new byte[Ssz.Ssz.SignedBeaconBlockLength(signedBlock)];
            Ssz.Ssz.Encode(encoded, signedBlock);

            if (_logger.IsDebug()) LogDebug.GossipSend(_logger, nameof(TopicUtf8.BeaconBlock), encoded.Length, null);
            if (!_mothraLibp2p.SendGossip(TopicUtf8.BeaconBlock, encoded))
            {
                if (_logger.IsWarn())
                    Log.GossipNotPublishedAsPeeeringNotStarted(_logger, nameof(TopicUtf8.BeaconBlock), null);
            }

            return Task.CompletedTask;
        }

        public Task RequestBlocksAsync(string peerId, Root peerHeadRoot, Slot finalizedSlot, Slot peerHeadSlot)
        {
            // NOTE: Currently just requests entire range, one at a time, to get small testnet working.
            // Will need more sophistication in future, e.g. request interleaved blocks and stuff.

            ulong count = peerHeadSlot - finalizedSlot;
            ulong step = 1;
            BeaconBlocksByRange beaconBlocksByRange = new BeaconBlocksByRange(peerHeadRoot, finalizedSlot, count, step);

            byte[] peerUtf8 = Encoding.UTF8.GetBytes(peerId);
            Span<byte> encoded = new byte[Ssz.Ssz.BeaconBlocksByRangeLength];
            Ssz.Ssz.Encode(encoded, beaconBlocksByRange);

            if (_logger.IsDebug())
                LogDebug.RpcSend(_logger, RpcDirection.Request, nameof(MethodUtf8.BeaconBlocksByRange), peerId,
                    encoded.Length, null);

            if (!_mothraLibp2p.SendRpcRequest(MethodUtf8.BeaconBlocksByRange, peerUtf8, encoded))
            {
                if (_logger.IsWarn())
                    Log.RpcRequestNotSentAsPeeeringNotStarted(_logger, nameof(MethodUtf8.BeaconBlocksByRange), null);
            }

            return Task.CompletedTask;
        }

        public Task SendBlockAsync(string peerId, SignedBeaconBlock signedBlock)
        {
            byte[] peerUtf8 = Encoding.UTF8.GetBytes(peerId);

            Span<byte> encoded = new byte[Ssz.Ssz.SignedBeaconBlockLength(signedBlock)];
            Ssz.Ssz.Encode(encoded, signedBlock);

            if (_logger.IsDebug())
                LogDebug.RpcSend(_logger, RpcDirection.Response, nameof(MethodUtf8.BeaconBlocksByRange), peerId,
                    encoded.Length, null);

            if (!_mothraLibp2p.SendRpcResponse(MethodUtf8.BeaconBlocksByRange, peerUtf8, encoded))
            {
                if (_logger.IsWarn())
                    Log.RpcResponseNotSentAsPeeeringNotStarted(_logger, nameof(MethodUtf8.BeaconBlocksByRange), null);
            }

            return Task.CompletedTask;
        }

        public Task SendStatusAsync(string peerId, RpcDirection rpcDirection, PeeringStatus peeringStatus)
        {
            byte[] peerUtf8 = Encoding.UTF8.GetBytes(peerId);
            Span<byte> encoded = new byte[Ssz.Ssz.PeeringStatusLength];
            Ssz.Ssz.Encode(encoded, peeringStatus);

            if (_logger.IsDebug())
                LogDebug.RpcSend(_logger, rpcDirection, nameof(MethodUtf8.Status), peerId, encoded.Length, null);
            if (rpcDirection == RpcDirection.Request)
            {
                if (!_mothraLibp2p.SendRpcRequest(MethodUtf8.Status, peerUtf8, encoded))
                {
                    if (_logger.IsWarn())
                        Log.RpcRequestNotSentAsPeeeringNotStarted(_logger, nameof(MethodUtf8.Status), null);
                }
            }
            else
            {
                if (!_mothraLibp2p.SendRpcResponse(MethodUtf8.Status, peerUtf8, encoded))
                {
                    if (_logger.IsWarn())
                        Log.RpcResponseNotSentAsPeeeringNotStarted(_logger, nameof(MethodUtf8.Status), null);
                }
            }

            return Task.CompletedTask;
        }
    }
}