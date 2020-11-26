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

using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Nethermind.Api;
using Nethermind.Blockchain.Synchronization;
using Nethermind.Core;
using Nethermind.Db;
using Nethermind.Db.Rocks;
using Nethermind.Db.Rocks.Config;
using Nethermind.Db.Rpc;
using Nethermind.JsonRpc.Client;
using Nethermind.Logging;
using Nethermind.Synchronization.BeamSync;
using Nethermind.Synchronization.ParallelSync;

namespace Nethermind.Runner.Ethereum.Steps
{
    public class InitDatabase : IStep
    {
        private readonly IBasicApi _api;

        public InitDatabase(INethermindApi api)
        {
            _api = api;
        }

        public async Task Execute(CancellationToken _)
        {
            ILogger logger = _api.LogManager.GetClassLogger();

            /* sync */
            IDbConfig dbConfig = _api.Config<IDbConfig>();
            ISyncConfig syncConfig = _api.Config<ISyncConfig>();
            IInitConfig initConfig = _api.Config<IInitConfig>();

            foreach (PropertyInfo propertyInfo in typeof(IDbConfig).GetProperties())
            {
                if (logger.IsDebug) logger.Debug($"DB {propertyInfo.Name}: {propertyInfo.GetValue(dbConfig)}");
            }

            try
            {
                _api.DbProvider = await GetDbProvider(initConfig, dbConfig, initConfig.StoreReceipts || syncConfig.DownloadReceiptsInFastSync);
                if (syncConfig.BeamSync)
                {
                    _api.SyncModeSelector = new PendingSyncModeSelector();
                    BeamSyncDbProvider beamSyncProvider = new BeamSyncDbProvider(_api.SyncModeSelector, _api.DbProvider, _api.Config<ISyncConfig>(), _api.LogManager);
                    _api.DbProvider = beamSyncProvider;
                }
            }
            catch(TypeInitializationException)
            {
                if(logger.IsError)
                    logger.Error("RocksDb was not found, please make sure it is installed on your machine. \n On macOs : 'brew install rocksdb'");
            }
        }

        private IMemDbFactory GetMemDbFactory(IInitConfig initConfig)
        {
                return null;
        }

        private IRocksDbFactory GetRocksDbFactory(IInitConfig initConfig)
        {
            switch (initConfig.DiagnosticMode)
            {
                case DiagnosticMode.RpcDb:
                    return new RpcDbFactory();
                default:
                    return new RocksDbFactory();
            }
        }

        private async Task<IDbProvider> GetDbProvider(IInitConfig initConfig, IDbConfig dbConfig, bool storeReceipts)
        {
            RocksDbProvider rocksDb;
            switch (initConfig.DiagnosticMode)
            {
                case DiagnosticMode.RpcDb:
                    rocksDb = await GetRocksDbProvider(dbConfig, Path.Combine(initConfig.BaseDbPath, "debug"), storeReceipts);
                    return new RpcDbProvider(_api.EthereumJsonSerializer, new BasicJsonRpcClient(new Uri(initConfig.RpcDbUrl), _api.EthereumJsonSerializer, _api.LogManager), _api.LogManager, rocksDb);
                case DiagnosticMode.ReadOnlyDb:
                    rocksDb = await GetRocksDbProvider(dbConfig, Path.Combine(initConfig.BaseDbPath, "debug"), storeReceipts);
                    return new ReadOnlyDbProvider(rocksDb, storeReceipts);
                case DiagnosticMode.MemDb:
                    return new MemDbProvider();
                default:
                    return await GetRocksDbProvider(dbConfig, initConfig.BaseDbPath, storeReceipts);
            }
        }

        private async Task<RocksDbProvider> GetRocksDbProvider(IDbConfig dbConfig, string basePath, bool useReceiptsDb)
        {
            RocksDbProvider debugRecorder = new RocksDbProvider(_api.LogManager, dbConfig, basePath);
            ThisNodeInfo.AddInfo("DB location  :", $"{basePath}");
            await debugRecorder.Init(useReceiptsDb);
            return debugRecorder;
        }
    }
}
