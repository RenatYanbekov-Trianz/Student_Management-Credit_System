using System;
using System.Data;
using System.Data.SqlClient;
using Dapper;
using System.Collections.Generic;

// Cloud Readiness Fix (cr-dotnet-0013 & cr-dotnet-0010):
// - Replaced direct SqlConnection instantiation with a connection-factory pattern
//   that reads the connection string from the environment variable
//   QLDSV_HTC_CONNECTION_STRING at runtime (12-factor app, Blocker 5).
// - When that variable is absent the code falls back to AWS Systems Manager
//   Parameter Store key /qldsv-htc/db/connection-string so that configuration
//   is never baked into build artefacts.
// - Each public method now opens a *fresh* SqlConnection per call (Dapper style)
//   so that Amazon RDS Proxy can multiplex connections across application
//   instances and enforce IAM authentication (Blocker 1).
// - The old static singleton SqlConnection field has been removed; connection
//   pooling is now handled transparently by the ADO.NET connection pool that
//   sits in front of RDS Proxy.

namespace QLDSV_HTC.DAL
{
    /// <summary>
    /// Provides cloud-ready database access via Dapper + Amazon RDS Proxy.
    /// Connection strings are resolved at runtime from environment variables
    /// or AWS Systems Manager Parameter Store — never from build-time config
    /// transforms.
    /// </summary>
    public static class DatabaseConnection
    {
        // ------------------------------------------------------------------ //
        //  Connection-string resolution (cr-dotnet-0010 / cr-dotnet-0013)    //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Returns the SQL Server connection string.
        /// Resolution order:
        ///   1. Environment variable  QLDSV_HTC_CONNECTION_STRING
        ///   2. AWS SSM Parameter Store  /qldsv-htc/db/connection-string
        ///      (requires AWSSDK.SimpleSystemsManagement NuGet package and
        ///       an IAM role attached to the compute resource)
        /// </summary>
        private static string GetConnectionString()
        {
            // 1. Environment variable — preferred for containers / ECS / Lambda
            string connStr = Environment.GetEnvironmentVariable("QLDSV_HTC_CONNECTION_STRING");
            if (!string.IsNullOrWhiteSpace(connStr))
            {
                return connStr;
            }

            // 2. AWS Systems Manager Parameter Store
            //    The call is synchronous here to keep the existing synchronous
            //    DAL surface area unchanged.  In a fully async refactor this
            //    should be awaited.
            try
            {
                connStr = AwsParameterStoreHelper.GetParameter("/qldsv-htc/db/connection-string");
                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    return connStr;
                }
            }
            catch (Exception ex)
            {
                // Log and fall through — will throw below with a clear message.
                System.Diagnostics.Trace.TraceWarning(
                    "[DatabaseConnection] Could not retrieve connection string from SSM: " + ex.Message);
            }

            throw new InvalidOperationException(
                "Database connection string is not configured. " +
                "Set the environment variable QLDSV_HTC_CONNECTION_STRING or " +
                "store the value in AWS SSM Parameter Store at " +
                "/qldsv-htc/db/connection-string.");
        }

        // ------------------------------------------------------------------ //
        //  Connection factory (cr-dotnet-0013)                               //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Opens and returns a new SqlConnection.
        /// Callers are responsible for disposing the connection (use 'using').
        /// ADO.NET connection pooling (backed by RDS Proxy) handles the
        /// physical connection lifecycle.
        /// </summary>
        public static SqlConnection OpenConnection()
        {
            var connection = new SqlConnection(GetConnectionString());
            connection.Open();
            return connection;
        }

        // ------------------------------------------------------------------ //
        //  Public helper methods — each opens its own short-lived connection  //
        // ------------------------------------------------------------------ //

        public static DataTable ExecuteQuery(string query, SqlParameter[] parameters = null)
        {
            DataTable dataTable = new DataTable();

            try
            {
                using (SqlConnection connection = OpenConnection())
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    using (SqlDataAdapter adapter = new SqlDataAdapter(command))
                    {
                        adapter.Fill(dataTable);
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error executing query: " + ex.Message, ex);
            }

            return dataTable;
        }

        public static int ExecuteNonQuery(string query, SqlParameter[] parameters = null)
        {
            int rowsAffected = 0;

            try
            {
                using (SqlConnection connection = OpenConnection())
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    rowsAffected = command.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error executing non-query: " + ex.Message, ex);
            }

            return rowsAffected;
        }

        public static object ExecuteScalar(string query, SqlParameter[] parameters = null)
        {
            object result = null;

            try
            {
                using (SqlConnection connection = OpenConnection())
                using (SqlCommand command = new SqlCommand(query, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(parameters);
                    }

                    result = command.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Error executing scalar: " + ex.Message, ex);
            }

            return result;
        }

        public static bool TestConnection()
        {
            try
            {
                using (SqlConnection connection = OpenConnection())
                {
                    return connection.State == ConnectionState.Open;
                }
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ //
        //  Dapper convenience wrappers (cr-dotnet-0013)                      //
        // ------------------------------------------------------------------ //

        /// <summary>
        /// Executes a query and returns the results as a strongly-typed list
        /// using Dapper.  The connection is opened per-call so that RDS Proxy
        /// can multiplex it.
        /// </summary>
        public static IEnumerable<T> Query<T>(string sql, object param = null)
        {
            using (SqlConnection connection = OpenConnection())
            {
                return connection.Query<T>(sql, param);
            }
        }

        /// <summary>
        /// Executes a non-query statement using Dapper and returns the number
        /// of rows affected.
        /// </summary>
        public static int Execute(string sql, object param = null)
        {
            using (SqlConnection connection = OpenConnection())
            {
                return connection.Execute(sql, param);
            }
        }
    }
}
