﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Text;

namespace Simple.Data
{
    using Commands;

    /// <summary>
    /// Provides an abstraction over the underlying data adapter, if it is transaction-capable.
    /// </summary>
    public sealed class SimpleTransaction : DataStrategy, IDisposable
    {
        private readonly Database _database;

        private readonly IAdapterWithTransactions _adapter;
        private IAdapterTransaction _adapterTransaction;

        private SimpleTransaction(IAdapterWithTransactions adapter, Database database)
        {
            if (adapter == null) throw new ArgumentNullException("adapter");
            if (database == null) throw new ArgumentNullException("database");
            _adapter = adapter;
            _database = database;
        }

        private void Begin()
        {
            _adapterTransaction = _adapter.BeginTransaction();
        }

        private void Begin(string name)
        {
            _adapterTransaction = _adapter.BeginTransaction(name);
        }

        internal static SimpleTransaction Begin(Database database)
        {
            SimpleTransaction transaction = CreateTransaction(database);
            transaction.Begin();
            return transaction;
        }

        internal static SimpleTransaction Begin(Database database, string name)
        {
            SimpleTransaction transaction = CreateTransaction(database);
            transaction.Begin(name);
            return transaction;
        }

        private static SimpleTransaction CreateTransaction(Database database)
        {
            var adapterWithTransactions = database.GetAdapter() as IAdapterWithTransactions;
            if (adapterWithTransactions == null) throw new NotSupportedException();
            return new SimpleTransaction(adapterWithTransactions, database);
        }


        internal Database Database
        {
            get { return _database; }
        }

        /// <summary>
        /// Gets the name assigned to the transaction.
        /// </summary>
        /// <value>The name.</value>
        public string Name
        {
            get { return _adapterTransaction.Name; }
        }

        public IAdapterTransaction AdapterTransaction
        {
            get { return _adapterTransaction; }
        }

        /// <summary>
        /// Commits all changes to the database and cleans up resources associated with the transaction.
        /// </summary>
        public void Commit()
        {
            _adapterTransaction.Commit();
        }

        /// <summary>
        /// Rolls back all changes to the database and cleans up resources associated with the transaction.
        /// </summary>
        public void Rollback()
        {
            _adapterTransaction.Rollback();
        }

        internal override IDictionary<string, object> FindOne(string tableName, SimpleExpression criteria)
        {
            return Find(tableName, criteria).FirstOrDefault();
        }

        internal override int UpdateMany(string tableName, IList<IDictionary<string, object>> dataList)
        {
            return _adapter.UpdateMany(tableName, dataList, AdapterTransaction);
        }

        internal override int UpdateMany(string tableName, IList<IDictionary<string, object>> dataList, IEnumerable<string> criteriaFieldNames)
        {
            return _adapter.UpdateMany(tableName, dataList, criteriaFieldNames, AdapterTransaction);
        }

        internal override IEnumerable<IDictionary<string, object>> Find(string tableName, SimpleExpression criteria)
        {
            return _adapter.Find(tableName, criteria, AdapterTransaction);
        }

        internal override IDictionary<string, object> Insert(string tableName, IDictionary<string, object> data, bool resultRequired)
        {
            return _adapter.Insert(tableName, data, AdapterTransaction, resultRequired);
        }

        /// <summary>
        ///  Inserts a record into the specified "table".
        ///  </summary><param name="tableName">Name of the table.</param><param name="data">The values to insert.</param><returns>If possible, return the newly inserted row, including any automatically-set values such as primary keys or timestamps.</returns>
        internal override IEnumerable<IDictionary<string, object>> InsertMany(string tableName, IEnumerable<IDictionary<string, object>> data, ErrorCallback onError, bool resultRequired)
        {
            return _adapter.InsertMany(tableName, data, AdapterTransaction, (dict, exception) => onError(new SimpleRecord(dict), exception), resultRequired);
        }

        internal override int Update(string tableName, IDictionary<string, object> data, SimpleExpression criteria)
        {
            return _adapter.Update(tableName, data, criteria, AdapterTransaction);
        }

        public override IDictionary<string, object> Upsert(string tableName, IDictionary<string, object> dict, SimpleExpression criteriaExpression, bool isResultRequired)
        {
            return _adapter.Upsert(tableName, dict, criteriaExpression, isResultRequired, AdapterTransaction);
        }

        public override IEnumerable<IDictionary<string, object>> UpsertMany(string tableName, IList<IDictionary<string, object>> list, bool isResultRequired, ErrorCallback errorCallback)
        {
            return _adapter.UpsertMany(tableName, list, AdapterTransaction, isResultRequired, (dict, exception) => errorCallback(new SimpleRecord(dict), exception));
        }

        public override IDictionary<string, object> Get(string tableName, object[] args)
        {
            return _adapter.Get(tableName, AdapterTransaction, args);
        }

        public override IEnumerable<IDictionary<string, object>> UpsertMany(string tableName, IList<IDictionary<string, object>> list, IEnumerable<string> keyFieldNames, bool isResultRequired, ErrorCallback errorCallback)
        {
            return _adapter.UpsertMany(tableName, list, keyFieldNames, AdapterTransaction, isResultRequired, (dict, exception) => errorCallback(new SimpleRecord(dict), exception));
        }

        internal override int UpdateMany(string tableName, IList<IDictionary<string, object>> newValuesList, IList<IDictionary<string, object>> originalValuesList)
        {
            int count = 0;
            for (int i = 0; i < newValuesList.Count; i++)
            {
                count += Update(tableName, newValuesList[i], originalValuesList[i]);
            }
            return count;
        }

        public override int Update(string tableName, IDictionary<string, object> newValuesDict, IDictionary<string, object> originalValuesDict)
        {
            SimpleExpression criteria = CreateCriteriaFromOriginalValues(tableName, newValuesDict, originalValuesDict);
            var changedValuesDict = CreateChangedValuesDict(newValuesDict, originalValuesDict);
            return _adapter.Update(tableName, changedValuesDict, criteria, AdapterTransaction);
        }

        internal override int Delete(string tableName, SimpleExpression criteria)
        {
            return _adapter.Delete(tableName, criteria, AdapterTransaction);
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            try
            {
                _adapterTransaction.Dispose();
            }
            catch (Exception ex)
            {
                Trace.WriteLine("IAdapterTransaction Dispose threw exception: " + ex.Message);
            }
        }

        public override Adapter GetAdapter()
        {
            return _adapter as Adapter;
        }

        protected override bool ExecuteFunction(out object result, ExecuteFunctionCommand command)
        {
            return command.Execute(out result, _adapterTransaction);
        }

        protected internal override Database GetDatabase()
        {
            return _database;
        }
    }
}
