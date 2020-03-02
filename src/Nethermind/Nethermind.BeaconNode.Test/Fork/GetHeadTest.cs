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

using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nethermind.Core2.Configuration;
using Nethermind.BeaconNode.Storage;
using Nethermind.BeaconNode.Test.Helpers;
using Nethermind.Core2;
using Nethermind.Core2.Containers;
using Nethermind.Core2.Crypto;
using Nethermind.Core2.Json;
using Nethermind.Core2.Types;
using Shouldly;
namespace Nethermind.BeaconNode.Test.Fork
{
    [TestClass]
    public class GetHeadTest
    {
        [TestMethod]
        public async Task GenesisHead()
        {
            // Arrange
            IServiceProvider testServiceProvider = TestSystem.BuildTestServiceProvider(useStore: true);
            BeaconState state = TestState.PrepareTestState(testServiceProvider);

            InitialValues initialValues = testServiceProvider.GetService<IOptions<InitialValues>>().Value;
            SignatureDomains signatureDomains = testServiceProvider.GetService<IOptions<SignatureDomains>>().Value;

            JsonSerializerOptions options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
            options.ConfigureNethermindCore2();
            string debugState = System.Text.Json.JsonSerializer.Serialize(state, options);
            
            // Initialization
            BeaconChainUtility beaconChainUtility = testServiceProvider.GetService<BeaconChainUtility>();
            ICryptographyService cryptographyService = testServiceProvider.GetService<ICryptographyService>();
            ForkChoice forkChoice = testServiceProvider.GetService<ForkChoice>();
            IStore store = forkChoice.GetGenesisStore(state);

            // Act
            Root headRoot = await forkChoice.GetHeadAsync(store);

            // Assert
            Domain domain =
                beaconChainUtility.ComputeDomain(signatureDomains.BeaconProposer, initialValues.GenesisForkVersion);
            Root stateRoot = cryptographyService.HashTreeRoot(state);
            BeaconBlock genesisBlock = new BeaconBlock(stateRoot);
            Root expectedRoot =
                beaconChainUtility.ComputeSigningRoot(cryptographyService.HashTreeRoot(genesisBlock), domain);
            
            headRoot.ShouldBe(expectedRoot);
        }

        [TestMethod]
        public async Task ChainNoAttestations()
        {
            // Arrange
            IServiceProvider testServiceProvider = TestSystem.BuildTestServiceProvider(useStore: true);
            BeaconState state = TestState.PrepareTestState(testServiceProvider);

            InitialValues initialValues = testServiceProvider.GetService<IOptions<InitialValues>>().Value;
            SignatureDomains signatureDomains = testServiceProvider.GetService<IOptions<SignatureDomains>>().Value;

            // Initialization
            BeaconChainUtility beaconChainUtility = testServiceProvider.GetService<BeaconChainUtility>();
            ICryptographyService cryptographyService = testServiceProvider.GetService<ICryptographyService>();
            ForkChoice forkChoice = testServiceProvider.GetService<ForkChoice>();
            IStore store = forkChoice.GetGenesisStore(state);

            // On receiving a block of `GENESIS_SLOT + 1` slot
            BeaconBlock block1 = TestBlock.BuildEmptyBlockForNextSlot(testServiceProvider, state);
            SignedBeaconBlock signedBlock1 = TestState.StateTransitionAndSignBlock(testServiceProvider, state, block1);
            await AddBlockToStore(testServiceProvider, store, signedBlock1);

            // On receiving a block of next epoch
            BeaconBlock block2 = TestBlock.BuildEmptyBlockForNextSlot(testServiceProvider, state);
            SignedBeaconBlock signedBlock2 = TestState.StateTransitionAndSignBlock(testServiceProvider, state, block2);
            await AddBlockToStore(testServiceProvider, store, signedBlock2);

            // Act
            Root headRoot = await forkChoice.GetHeadAsync(store);

            // Assert
            Domain domain =
                beaconChainUtility.ComputeDomain(signatureDomains.BeaconProposer, initialValues.GenesisForkVersion);
            Root expectedRoot =
                beaconChainUtility.ComputeSigningRoot(cryptographyService.HashTreeRoot(block2), domain);
            headRoot.ShouldBe(expectedRoot);
        }

        [TestMethod]
        public async Task SplitTieBreakerNoAttestations()
        {
            // Arrange
            IServiceProvider testServiceProvider = TestSystem.BuildTestServiceProvider(useStore: true);
            BeaconState state = TestState.PrepareTestState(testServiceProvider);

            InitialValues initialValues = testServiceProvider.GetService<IOptions<InitialValues>>().Value;
            SignatureDomains signatureDomains = testServiceProvider.GetService<IOptions<SignatureDomains>>().Value;

            // Initialization
            BeaconChainUtility beaconChainUtility = testServiceProvider.GetService<BeaconChainUtility>();
            ICryptographyService cryptographyService = testServiceProvider.GetService<ICryptographyService>();
            ForkChoice forkChoice = testServiceProvider.GetService<ForkChoice>();
            IStore store = forkChoice.GetGenesisStore(state);
            BeaconState genesisState = BeaconState.Clone(state);

            Domain domain =
                beaconChainUtility.ComputeDomain(signatureDomains.BeaconProposer, initialValues.GenesisForkVersion);

            // block at slot 1
            BeaconState block1State = BeaconState.Clone(genesisState);
            BeaconBlock block1 = TestBlock.BuildEmptyBlockForNextSlot(testServiceProvider, block1State);
            SignedBeaconBlock signedBlock1 = TestState.StateTransitionAndSignBlock(testServiceProvider, block1State, block1);
            await AddBlockToStore(testServiceProvider, store, signedBlock1);
            Root block1Root = beaconChainUtility.ComputeSigningRoot(cryptographyService.HashTreeRoot(block1), domain);

            // build short tree
            BeaconState block2State = BeaconState.Clone(genesisState);
            BeaconBlock block2 = TestBlock.BuildEmptyBlockForNextSlot(testServiceProvider, block2State);
            block2.Body.SetGraffiti(new Bytes32(Enumerable.Repeat((byte)0x42, 32).ToArray()));
            TestBlock.SignBlock(testServiceProvider, block2State, block2, ValidatorIndex.None);
            SignedBeaconBlock signedBlock2 = TestState.StateTransitionAndSignBlock(testServiceProvider, block2State, block2);
            await AddBlockToStore(testServiceProvider, store, signedBlock2);
            Root block2Root = beaconChainUtility.ComputeSigningRoot(cryptographyService.HashTreeRoot(block2), domain);

            // Act
            Root headRoot = await forkChoice.GetHeadAsync(store);

            // Assert
            Console.WriteLine("block1 {0}", block1Root);
            Console.WriteLine("block2 {0}", block2Root);
            Root highestRoot = block1Root.CompareTo(block2Root) > 0 ? block1Root : block2Root;
            Console.WriteLine("highest {0}", highestRoot);
            headRoot.ShouldBe(highestRoot);
        }

        [TestMethod]
        public async Task ShorterChainButHeavierWeight()
        {
            // Arrange
            IServiceProvider testServiceProvider = TestSystem.BuildTestServiceProvider(useStore: true);
            BeaconState state = TestState.PrepareTestState(testServiceProvider);

            InitialValues initialValues = testServiceProvider.GetService<IOptions<InitialValues>>().Value;
            SignatureDomains signatureDomains = testServiceProvider.GetService<IOptions<SignatureDomains>>().Value;

            // Initialization
            BeaconChainUtility beaconChainUtility = testServiceProvider.GetService<BeaconChainUtility>();
            ICryptographyService cryptographyService = testServiceProvider.GetService<ICryptographyService>();
            ForkChoice forkChoice = testServiceProvider.GetService<ForkChoice>();
            IStore store = forkChoice.GetGenesisStore(state);
            BeaconState genesisState = BeaconState.Clone(state);

            Domain domain =
                beaconChainUtility.ComputeDomain(signatureDomains.BeaconProposer, initialValues.GenesisForkVersion);

            // build longer tree
            Root longRoot = Root.Zero;
            BeaconState longState = BeaconState.Clone(genesisState);
            for (int i = 0; i < 3; i++)
            {
                BeaconBlock longBlock = TestBlock.BuildEmptyBlockForNextSlot(testServiceProvider, longState);
                SignedBeaconBlock signedLongBlock = TestState.StateTransitionAndSignBlock(testServiceProvider, longState, longBlock);
                await AddBlockToStore(testServiceProvider, store, signedLongBlock);
                if (i == 2)
                {
                    longRoot = beaconChainUtility.ComputeSigningRoot(cryptographyService.HashTreeRoot(longBlock), domain);
                }
            }

            // build short tree
            BeaconState shortState = BeaconState.Clone(genesisState);
            BeaconBlock shortBlock = TestBlock.BuildEmptyBlockForNextSlot(testServiceProvider, shortState);
            shortBlock.Body.SetGraffiti(new Bytes32(Enumerable.Repeat((byte)0x42, 32).ToArray()));
            TestBlock.SignBlock(testServiceProvider, shortState, shortBlock, ValidatorIndex.None);
            SignedBeaconBlock signedShortBlock = TestState.StateTransitionAndSignBlock(testServiceProvider, shortState, shortBlock);
            await AddBlockToStore(testServiceProvider, store, signedShortBlock);

            Attestation shortAttestation = TestAttestation.GetValidAttestation(testServiceProvider, shortState, shortBlock.Slot, CommitteeIndex.None, signed: true);
            await AddAttestationToStore(testServiceProvider, store, shortAttestation);

            // Act
            Root headRoot = await forkChoice.GetHeadAsync(store);

            // Assert
            Root expectedRoot = beaconChainUtility.ComputeSigningRoot(cryptographyService.HashTreeRoot(shortBlock), domain);
            headRoot.ShouldBe(expectedRoot);
            headRoot.ShouldNotBe(longRoot);
        }

        private async Task AddAttestationToStore(IServiceProvider testServiceProvider, IStore store, Attestation attestation)
        {
            TimeParameters timeParameters = testServiceProvider.GetService<IOptions<TimeParameters>>().Value;
            InitialValues initialValues = testServiceProvider.GetService<IOptions<InitialValues>>().Value;
            SignatureDomains signatureDomains = testServiceProvider.GetService<IOptions<SignatureDomains>>().Value;

            BeaconChainUtility beaconChainUtility = testServiceProvider.GetService<BeaconChainUtility>();
            ICryptographyService cryptographyService = testServiceProvider.GetService<ICryptographyService>();
            ForkChoice forkChoice = testServiceProvider.GetService<ForkChoice>();

            BeaconBlock parentBlock = await store.GetBlockAsync(attestation.Data.BeaconBlockRoot);

            Domain domain =
                beaconChainUtility.ComputeDomain(signatureDomains.BeaconProposer, initialValues.GenesisForkVersion);
            Root parentSigningRoot = beaconChainUtility.ComputeSigningRoot(cryptographyService.HashTreeRoot(parentBlock), domain);
            BeaconState preState = await store.GetBlockStateAsync(parentSigningRoot);
            
            ulong blockTime = preState.GenesisTime + (ulong)parentBlock.Slot * timeParameters.SecondsPerSlot;
            ulong nextEpochTime = blockTime + (ulong)timeParameters.SlotsPerEpoch * timeParameters.SecondsPerSlot;

            if (store.Time < blockTime)
            {
                await forkChoice.OnTickAsync(store, blockTime);
            }

            await forkChoice.OnAttestationAsync(store, attestation);
        }

        private async Task AddBlockToStore(IServiceProvider testServiceProvider, IStore store, SignedBeaconBlock signedBlock)
        {
            TimeParameters timeParameters = testServiceProvider.GetService<IOptions<TimeParameters>>().Value;
            ForkChoice forkChoice = testServiceProvider.GetService<ForkChoice>();

            BeaconState preState = await store.GetBlockStateAsync(signedBlock.Message.ParentRoot);
            
            ulong blockTime = preState!.GenesisTime + (ulong)signedBlock.Message.Slot * timeParameters.SecondsPerSlot;

            if (store.Time < blockTime)
            {
                await forkChoice.OnTickAsync(store, blockTime);
            }

            await forkChoice.OnBlockAsync(store, signedBlock);
        }
    }
}
