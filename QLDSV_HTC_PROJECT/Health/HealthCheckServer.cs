using System;
using System.Net;
using System.Text;
using System.Threading;

namespace QLDSV_HTC.Health
{
    /// <summary>
    /// Lightweight HTTP health check server for container liveness/readiness probes.
    /// Listens on the port specified by HEALTH_CHECK_PORT environment variable (default: 8080).
    /// Exposes GET /health endpoint returning JSON status for Kubernetes probes.
    /// </summary>
    public class HealthCheckServer
    {
        private static HttpListener _listener;
        private static Thread _listenerThread;
        private static volatile bool _running = false;

        private static readonly string _port =
            Environment.GetEnvironmentVariable("HEALTH_CHECK_PORT") ?? "8080";

        /// <summary>
        /// Starts the health check HTTP listener in a background thread.
        /// </summary>
        public static void Start()
        {
            try
            {
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://+:{_port}/health/");
                _listener.Start();
                _running = true;

                _listenerThread = new Thread(HandleRequests)
                {
                    IsBackground = true,
                    Name = "HealthCheckListener"
                };
                _listenerThread.Start();
            }
            catch (Exception ex)
            {
                // Health check server failure should not crash the application
                Console.WriteLine($"[HealthCheck] Failed to start health check server on port {_port}: {ex.Message}");
            }
        }

        /// <summary>
        /// Stops the health check HTTP listener.
        /// </summary>
        public static void Stop()
        {
            _running = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HealthCheck] Error stopping health check server: {ex.Message}");
            }
        }

        private static void HandleRequests()
        {
            while (_running)
            {
                try
                {
                    HttpListenerContext context = _listener.GetContext();
                    ThreadPool.QueueUserWorkItem(_ => ProcessRequest(context));
                }
                catch (HttpListenerException)
                {
                    // Listener was stopped
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[HealthCheck] Error handling request: {ex.Message}");
                }
            }
        }

        private static void ProcessRequest(HttpListenerContext context)
        {
            try
            {
                HttpListenerRequest request = context.Request;
                HttpListenerResponse response = context.Response;

                if (request.HttpMethod == "GET" &&
                    request.Url.AbsolutePath.TrimEnd('/').Equals("/health", StringComparison.OrdinalIgnoreCase))
                {
                    bool dbHealthy = QLDSV_HTC.DAL.DatabaseConnection.TestConnection();

                    string status = dbHealthy ? "healthy" : "degraded";
                    int statusCode = dbHealthy ? 200 : 503;

                    string responseBody = $"{{\"status\":\"{status}\",\"application\":\"QLDSV_HTC_PROJECT\",\"timestamp\":\"{DateTime.UtcNow:O}\"}}";
                    byte[] buffer = Encoding.UTF8.GetBytes(responseBody);

                    response.StatusCode = statusCode;
                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }
                else
                {
                    response.StatusCode = 404;
                    byte[] buffer = Encoding.UTF8.GetBytes("{\"error\":\"Not Found\"}");
                    response.ContentType = "application/json";
                    response.ContentLength64 = buffer.Length;
                    response.OutputStream.Write(buffer, 0, buffer.Length);
                }

                response.OutputStream.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[HealthCheck] Error processing request: {ex.Message}");
            }
        }
    }
}
