using Npgsql;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace digital_kitabain
{
    public class DbContext
    {
        private readonly string _connectionString;

        public DbContext()
        {
            _connectionString = BuildConnectionString();
        }

        private string BuildConnectionString()
        {
            DotNetEnv.Env.Load();

            var host = Environment.GetEnvironmentVariable("DB_HOST");
            var port = Environment.GetEnvironmentVariable("DB_PORT");
            var user = Environment.GetEnvironmentVariable("DB_USER");
            var password = Environment.GetEnvironmentVariable("DB_PASSWORD");
            var db = Environment.GetEnvironmentVariable("DB_NAME");

            return $"Host={host};Port={port};Username={user};Password={password};Database={db};";
        }

        // For INSERT, UPDATE, DELETE
        public int ExecuteNonQuery(string query, Dictionary<string, object> parameters = null)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    AddParameters(cmd, parameters);
                    conn.Open();
                    return cmd.ExecuteNonQuery();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return -1;
            }
        }

        // For SELECT queries returning List<T>
        public List<T> ExecuteQuery<T>(string query, Dictionary<string, object> parameters = null) where T : new()
        {
            try
            {
                var result = new List<T>();

                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    AddParameters(cmd, parameters);
                    conn.Open();

                    using (var reader = cmd.ExecuteReader())
                    {
                        var props = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance);

                        while (reader.Read())
                        {
                            var obj = new T();
                            foreach (var prop in props)
                            {
                                if (!reader.HasColumn(prop.Name) || reader[prop.Name] is DBNull) continue;
                                prop.SetValue(obj, Convert.ChangeType(reader[prop.Name], prop.PropertyType));
                            }
                            result.Add(obj);
                        }
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        // For single value (like COUNT(*))
        public object ExecuteScalar(string query, Dictionary<string, object> parameters = null)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                using (var cmd = new NpgsqlCommand(query, conn))
                {
                    AddParameters(cmd, parameters);
                    conn.Open();
                    return cmd.ExecuteScalar();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return null;
            }
        }

        public bool ExecuteTransaction(List<(string query, Dictionary<string, object> parameters)> commands)
        {
            try
            {
                using (var conn = new NpgsqlConnection(_connectionString))
                {
                    conn.Open();
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            foreach (var (query, parameters) in commands)
                            {
                                using (var cmd = new NpgsqlCommand(query, conn, transaction))
                                {
                                    AddParameters(cmd, parameters);
                                    cmd.ExecuteNonQuery();
                                }
                            }

                            transaction.Commit();
                            return true;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Transaction failed: " + ex.Message);
                            transaction.Rollback();
                            return false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                return false;
            }
        }

        private void AddParameters(NpgsqlCommand cmd, Dictionary<string, object> parameters)
        {
            if (parameters == null) return;

            foreach (var p in cmd.Parameters)
            {
                Console.WriteLine(((NpgsqlParameter)p).ParameterName + ": " + ((NpgsqlParameter)p).Value);
            }

            foreach (var kvp in parameters)
                cmd.Parameters.AddWithValue("@" + kvp.Key, kvp.Value ?? DBNull.Value);
        }
    }

    public static class NpgsqlExtensions
    {
        public static bool HasColumn(this NpgsqlDataReader reader, string columnName)
        {
            for (int i = 0; i < reader.FieldCount; i++)
                if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
