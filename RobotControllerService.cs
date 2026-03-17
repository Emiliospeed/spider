using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace SPIGAIICode
{
    /// <summary>
    /// TCP server that waits for the robot to connect.
    /// Sends commands and receives status messages.
    /// </summary>
    public class RobotControllerService
    {
        private TcpListener server;
        private TcpClient client;
        private NetworkStream stream;

        private bool running = false;
        public bool Connected { get; private set; } = false;
        public bool RobotReady { get; private set; } = true;

        public event Action<string> MessageReceived;
        public event Action<bool> ConnectionChanged;
        public event Action<bool> RobotReadyChanged;

        private Thread serverThread;

        /// <summary>
        /// Starts the TCP server on the given port.
        /// </summary>
        public void StartServer(int port)
        {
            if (running)
            {
                MessageReceived?.Invoke("Server already running.");
                return;
            }

            serverThread = new Thread(() =>
            {
                try
                {
                    running = true;
                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();

                    MessageReceived?.Invoke($"Server listening on port {port}...");

                    while (running)
                    {
                        if (server.Pending())
                        {
                            client = server.AcceptTcpClient();
                            stream = client.GetStream();
                            Connected = true;
                            ConnectionChanged?.Invoke(true);
                            MessageReceived?.Invoke("Robot connected");

                            // Start receiving messages
                            ReceiveLoop();
                        }
                        else
                        {
                            Thread.Sleep(100);
                        }
                    }
                }
                catch (Exception ex)
                {
                    if (running)
                        MessageReceived?.Invoke("Server error: " + ex.Message);
                }
            });

            serverThread.IsBackground = true;
            serverThread.Start();
        }

        /// <summary>
        /// Sends a command to the robot, respecting RobotReady state.
        /// </summary>
        public void SendCommand(string cmd)
        {
            if (!Connected)
            {
                MessageReceived?.Invoke("No robot connected yet.");
                return;
            }

            if (!RobotReady)
            {
                MessageReceived?.Invoke("Robot is in motion. Command ignored.");
                return;
            }

            try
            {
                byte[] data = Encoding.ASCII.GetBytes(cmd + "\r\n");
                stream.Write(data, 0, data.Length);
                MessageReceived?.Invoke("Sent: " + cmd);
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke("Send error: " + ex.Message);
            }
        }

        /// <summary>
        /// Sends a command immediately, bypassing RobotReady.
        /// </summary>
        public void SendImmediateCommand(string cmd)
        {
            try
            {
                if (!Connected || stream == null)
                {
                    MessageReceived?.Invoke("No robot connected yet.");
                    return;
                }

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        byte[] data = Encoding.ASCII.GetBytes(cmd + "\r\n");
                        stream.Write(data, 0, data.Length);
                        MessageReceived?.Invoke("Sent immediate: " + cmd);
                    }
                    catch (Exception ex)
                    {
                        MessageReceived?.Invoke("Immediate send error: " + ex.Message);
                    }
                });
            }
            catch (Exception ex)
            {
                MessageReceived?.Invoke("Immediate send setup error: " + ex.Message);
            }
        }

        /// <summary>
        /// Loop to receive messages from the robot.
        /// </summary>
        private void ReceiveLoop()
        {
            byte[] buffer = new byte[1024];
            StringBuilder sb = new StringBuilder();

            while (running && Connected)
            {
                try
                {
                    if (!stream.DataAvailable)
                    {
                        Thread.Sleep(50);
                        continue;
                    }

                    int bytesRead = stream.Read(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                    {
                        Connected = false;
                        ConnectionChanged?.Invoke(false);
                        MessageReceived?.Invoke("Robot disconnected");
                        break;
                    }

                    sb.Append(Encoding.ASCII.GetString(buffer, 0, bytesRead));
                    string content = sb.ToString();

                    while (content.Contains("\r\n"))
                    {
                        int index = content.IndexOf("\r\n");
                        string msg = content.Substring(0, index).Trim().ToLower();
                        content = content.Substring(index + 2);
                        sb = new StringBuilder(content);

                        HandleMessage(msg);
                    }
                }
                catch (Exception ex)
                {
                    if (running)
                        MessageReceived?.Invoke("Receive error: " + ex.Message);
                    Connected = false;
                    ConnectionChanged?.Invoke(false);
                    break;
                }
            }
        }

        /// <summary>
        /// Processes robot messages for RobotReady state or generic messages.
        /// </summary>
        private void HandleMessage(string msg)
        {
            if (msg == "robot in motion")
            {
                RobotReady = false;
                RobotReadyChanged?.Invoke(false);
                MessageReceived?.Invoke("robot_ready = False");
            }
            else if (msg == "robot ready")
            {
                RobotReady = true;
                RobotReadyChanged?.Invoke(true);
                MessageReceived?.Invoke("robot_ready = True");
            }
            else
            {
                MessageReceived?.Invoke("Robot: " + msg);
            }
        }

        /// <summary>
        /// Stops the server and disconnects robot.
        /// </summary>
        public void Stop()
        {
            running = false;
            Connected = false;

            try
            {
                stream?.Close();
                client?.Close();
                server?.Stop();
            }
            catch { }

            serverThread?.Join();
            MessageReceived?.Invoke("Server stopped.");
        }
    }
}
