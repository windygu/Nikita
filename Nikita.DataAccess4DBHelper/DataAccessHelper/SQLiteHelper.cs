/*
 * 说明: 对SQLite数据库的增删查改操作的封装类
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.SQLite;
using System.Text;

namespace Nikita.DataAccess4DBHelper
{
    public partial class SQLiteHelper : IDbHelper
    {
        private  SQLiteConnection _conn;
        private SQLiteCommand _cmd;
        private SQLiteDataAdapter _sda;
        private SQLiteDataReader _sdr;

        /// <summary>构造函数
        /// 构造函数
        /// </summary>
        /// <param name="connStr">数据库连接字符串</param>
        public SQLiteHelper(string connStr)
        {
            _conn = new SQLiteConnection(connStr);
        }

        /// <summary>添加参数
        ///
        /// </summary>
        /// <param name="paramName">参数名称</param>
        /// <param name="value">值</param>
        public void AddParameter(string paramName, object value)
        {
            _cmd.Parameters.Add(new SQLiteParameter(paramName, value));
        }

        /// <summary>将 DataTable 的数据批量插入到数据库中。
        /// 将 DataTable 的数据批量插入到数据库中。
        /// </summary>
        /// <param name="dataTable">要批量插入的 <see cref="DataTable"/>。</param>
        /// <param name="batchSize">每批次写入的数据量。</param>
        public void BatchInsert(DataTable dataTable, int batchSize = 10000)
        {
            if (dataTable == null || dataTable.Rows.Count == 0)
            {
                return;
            }

            DbTransaction transcation = null;
            try
            {
                if (_conn.State != ConnectionState.Open)
                {
                    _conn.Open();
                }
                transcation = _conn.BeginTransaction();
                using (var command = new SQLiteCommand(_conn))
                {
                    command.CommandText = GenerateInserSql(dataTable);
                    if (command.CommandText == string.Empty)
                    {
                        return;
                    }
                    List<string> lstName = new List<string>();
                    List<string> lstAutoName = new List<string>();
                    for (int i = 0; i < dataTable.Columns.Count; i++)
                    {
                        if (dataTable.Columns[i].AutoIncrement)
                        {
                            lstAutoName.Add(dataTable.Columns[i].ToString());
                        }
                        else
                        {
                            lstName.Add(dataTable.Columns[i].ColumnName);
                        }
                    }

                    foreach (string t in lstAutoName)
                    {
                        dataTable.Columns.Remove(t);
                    }

                    SQLiteParameter[] paras = new SQLiteParameter[lstName.Count];
                    for (int i = 0; i < paras.Length; i++)
                    {
                        paras[i] = command.CreateParameter();
                        paras[i].ParameterName = lstName[i];
                    }
                    command.Parameters.AddRange(paras);
                    for (int i = 0; i < dataTable.Rows.Count; i++)
                    {
                        foreach (SQLiteParameter t in paras)
                        {
                            t.Value = dataTable.Rows[i][t.ParameterName];
                        }
                        command.ExecuteNonQuery();
                    }
                }
                transcation.Commit();
            }
            catch (Exception)
            {
                if (transcation != null)
                {
                    transcation.Rollback();
                }
                throw;
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                {
                    _conn.Close();
                }
            }
        }

        /// <summary>关闭数据库
        ///
        /// </summary>
        public void CloseConn()
        {
            if (_conn.State == ConnectionState.Open)
            {
                _conn.Close();
            }
        }

        /// <summary>创建Command对象
        ///
        /// </summary>
        /// <param name="sql">SQL语句</param>
        public void CreateCommand(string sql)
        {
            _conn.Open();
            _cmd = new SQLiteCommand(sql, _conn);
        }

        /// <summary>创建Command对象【SQLite不支持存储过程】
        ///
        /// </summary>
        /// <param name="sql">SQL语句</param>
        public void CreateStoredCommand(string sql)
        {
            throw new Exception("SQLite不支持存储过程");
        }

        ///  <summary>执行不带参数的增删改SQL语句
        ///
        ///  </summary>
        /// <returns></returns>
        public bool ExecuteNonQuery()
        {
            try
            {
                var res = _cmd.ExecuteNonQuery();
                if (res > 0)
                {
                    return true;
                }
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                {
                    _conn.Close();
                }
            }
            return false;
        }

        ///  <summary>执行查询SQL语句
        ///
        ///  </summary>
        /// <returns></returns>
        public DataTable ExecuteQuery()
        {
            DataTable dt = new DataTable();
            using (_sdr = _cmd.ExecuteReader(CommandBehavior.CloseConnection))
            {
                dt.Load(_sdr);
            }
            if (_conn.State==ConnectionState.Open)
            {
                _conn.Close();
            }
            return dt;
        }

        /// <summary>执行查询SQL语句或存储过程返回DataSet
        ///
        /// </summary>
        /// <returns></returns>
        public DataSet ExecuteQueryDataSet()
        {
            DataSet ds = new DataSet();
            try
            {
                _sda = new SQLiteDataAdapter(_cmd);
                _sda.Fill(ds);
            }
            catch (Exception ex)
            {
                if (!ex.ToString().Contains("操作已被用户取消"))
                {
                    throw;
                }
            }
            finally
            {
                _conn.Close();
            }
            return ds;
        }

        /// <summary>返回IDataReader
        ///
        /// </summary>
        /// <returns></returns>
        public IDataReader ExecuteReader()
        {
            return _cmd.ExecuteReader(CommandBehavior.CloseConnection);
        }

        /// <summary>返回查询SQL语句查询出的结果的第一行第一列的值
        ///
        /// </summary>
        /// <returns></returns>
        public string ExecuteScalar()
        {
            string res = "";
            try
            {
                object obj = _cmd.ExecuteScalar();
                if (obj != null)
                {
                    res = obj.ToString();
                }
            }
            finally
            {
                if (_conn.State == ConnectionState.Open)
                {
                    _conn.Close();
                }
            }
            return res;
        }

        /// <summary>分页
        ///
        /// </summary>
        /// <param name="strTabName">表名</param>
        /// <param name="strFiledName">字段名</param>
        /// <param name="strOrderFiledName">排序字段名</param>
        /// <param name="strOrderType">排序方式：asc或者desc</param>
        /// <param name="strWhere">条件，不用加where</param>
        /// <param name="intPageSize">页大小</param>
        /// <param name="intPageIndex">页索引</param>
        /// <returns></returns>
        public DataTable FengYe(string strTabName, string strFiledName, string strOrderFiledName, string strOrderType, string strWhere, int intPageSize, int intPageIndex)
        {
            int start = (intPageIndex - 1) * intPageSize;
            string sql;
            if (!string.IsNullOrEmpty(strWhere))
            {
                // 条件不为空
                sql = string.Format("select {0} from {1} where {2} order by {3} {4} limit {5},{6}", strFiledName,
                    strTabName, strWhere, strOrderFiledName, strOrderType, start, intPageSize);
            }
            else
            {
                // 条件为空
                sql = string.Format("select {0} from {1} order by {2} {3} limit {4},{5}", strFiledName,
                    strTabName, strOrderFiledName, strOrderType, start, intPageSize);
            }
            CreateCommand(sql);
            var dt = ExecuteQuery();
            return dt;
        }

        /// <summary>返回执行统计信息
        /// 返回执行统计信息
        /// </summary>
        /// <returns>统计信息</returns>
        public string ReturnRunMessage()
        {
            return "SQLite暂不返回执行信息";
        }

        /// <summary>终止执行
        /// 终止执行
        /// </summary>
        public void StopRunSql()
        {
            if (_cmd != null)
            {
                _cmd.Cancel();
            }
        }

        /// <summary>测试连接
        /// 测试连接
        /// </summary>
        /// <returns>True 连接成功</returns>
        public bool TestConn()
        {
            bool blnIsConnect = true;
            try
            {
                if (_conn != null)
                {
                    _conn.Open();
                }
            }
            catch (Exception)
            {
                blnIsConnect = false;
            }
            finally
            {
                if (_conn != null && _conn.State == ConnectionState.Open)
                {
                    _conn.Close();
                }
            }
            return blnIsConnect;
        }

        /// <summary>生成插入语句
        /// 生成插入语句
        /// </summary>
        /// <param name="dt">数据表DataTable</param>
        /// <returns>插入语句</returns>
        private string GenerateInserSql(DataTable dt)
        {
            StringBuilder strInsert = new StringBuilder();
            strInsert.Append("INSERT INTO ");
            strInsert.Append(dt.TableName);
            strInsert.Append("  (");
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                if (dt.Columns[i].AutoIncrement)
                {
                    continue;
                }
                if (i == dt.Columns.Count - 1)
                {
                    strInsert.Append(dt.Columns[i].ColumnName);
                }
                else
                {
                    strInsert.Append(dt.Columns[i].ColumnName + ",");
                }
            }
            strInsert.Append(" )");
            strInsert.Append(" VALUES (");
            for (int i = 0; i < dt.Columns.Count; i++)
            {
                if (dt.Columns[i].AutoIncrement)
                {
                    continue;
                }
                if (i == dt.Columns.Count - 1)
                {
                    strInsert.Append("@" + dt.Columns[i].ColumnName);
                }
                else
                {
                    strInsert.Append("@" + dt.Columns[i].ColumnName + ",");
                }
            }
            strInsert.Append("   ) ");
            return strInsert.ToString();
        }
    }
}