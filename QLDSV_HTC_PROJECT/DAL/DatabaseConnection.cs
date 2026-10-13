using System;
using System.Configuration;
using System.Data;
using Microsoft.Data.SqlClient;

namespace QLDSV_HTC.DAL
{
    /// <summary>
    /// Provides database connection and query execution utilities.
    /// Updated for .NET 8 compatibility using Microsoft.Data.SqlClient.
    /// </summary>
    public class DatabaseConnection
    {
        private static readonly string connectionString = ConfigurationManager.ConnectionStrings["QLDSV_HTCConnectionString"]?.ConnectionString
            ?? throw new InvalidOperationException("Connection string 'QLDSV_HTCConnectionString' not found in configuration.");

        /// <summary>
        /// Creates and returns a new open SqlConnection.
        /// Caller is responsible for disposing the connection.
        /// </summary>
        public static SqlConnection GetConnection()
        {
            SqlConnection connection = new SqlConnection(connectionString);
            connection.Open();
            return connection;
        }

        /// <summary>
        /// Tests whether a database connection can be established.
        /// </summary>
        public static bool TestConnection()
        {
            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Executes a SELECT query and returns the result as a DataTable.
        /// </summary>
        public static DataTable ExecuteQuery(string query, SqlParameter[]? parameters = null)
        {
            DataTable dataTable = new DataTable();

            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();
                using SqlCommand command = new SqlCommand(query, connection);

                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }

                using SqlDataAdapter adapter = new SqlDataAdapter(command);
                adapter.Fill(dataTable);
            }
            catch (Exception ex)
            {
                throw new Exception("Error executing query: " + ex.Message, ex);
            }

            return dataTable;
        }

        /// <summary>
        /// Executes an INSERT, UPDATE, or DELETE query and returns the number of rows affected.
        /// </summary>
        public static int ExecuteNonQuery(string query, SqlParameter[]? parameters = null)
        {
            int rowsAffected = 0;

            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();
                using SqlCommand command = new SqlCommand(query, connection);

                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }

                rowsAffected = command.ExecuteNonQuery();
            }
            catch (Exception ex)
            {
                throw new Exception("Error executing non-query: " + ex.Message, ex);
            }

            return rowsAffected;
        }

        /// <summary>
        /// Executes a query and returns the first column of the first row.
        /// </summary>
        public static object? ExecuteScalar(string query, SqlParameter[]? parameters = null)
        {
            object? result = null;

            try
            {
                using SqlConnection connection = new SqlConnection(connectionString);
                connection.Open();
                using SqlCommand command = new SqlCommand(query, connection);

                if (parameters != null)
                {
                    command.Parameters.AddRange(parameters);
                }

                result = command.ExecuteScalar();
            }
            catch (Exception ex)
            {
                throw new Exception("Error executing scalar: " + ex.Message, ex);
            }

            return result;
        }

        // Legacy compatibility methods - kept for backward compatibility
        [Obsolete("Use the parameterless GetConnection() instead. Ensure you dispose the connection after use.")]
        public static void CloseConnection()
        {
            // No-op: connections are now managed per-operation with using statements
        }
    }
}
