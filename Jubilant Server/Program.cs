﻿using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Jubilant_Server
{

    class Program
    {
        private const int PORT = 36187;
        private const int MAX_CONNECTION_QUEUE = 20;

        //Thread signal.
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public static void StartListening()
        {
            //Establish the local endpoint for the socket.
            //The DNS name of the server
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];

            Console.WriteLine("My IP is {0}", ipAddress.ToString());

            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, PORT);

            //Create TCP/IP socket
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            //Bind the local socket to the local endpoint and listen for incoming connections
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(MAX_CONNECTION_QUEUE);

                while (true)
                {
                    //Set the event to nonsignaled state
                    allDone.Reset();

                    //Start an asynchronous socket to listen for connections
                    Console.WriteLine("Waiting for a connection...");

                    listener.BeginAccept(
                        new AsyncCallback(AcceptCallback),
                        listener);

                    //Wait until a connection is made before continuing
                    allDone.WaitOne();
                }
            }catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\n Press ENTER to stop the server...");
            Console.Read();
        }

        public static void AcceptCallback(IAsyncResult ar)
        {
            //Signal the main thread to continue
            allDone.Set();

            //Get the socket that handles the client request
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            //
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
        }

        private static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;
            Console.WriteLine("New connection...");

            //Retrieve the state object and the handler socket
            //from the asynchronous state object
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            //Read the data from the client socket
            int bytesRead = handler.EndReceive(ar);

            if(bytesRead > 0)
            {
                //There might be more data, so store the data received so far
                state.sb.Append(Encoding.ASCII.GetString(
                    state.buffer, 0, bytesRead));

                //Check for end-of-file tag. If not there, read more data
                content = state.sb.ToString();
                if(content.IndexOf("<EOF>") > -1)
                {
                    //All the data has been read from the client.
                    //TODO this might be wrong, because UTF 8 is not a byte per word afaik
                    Console.WriteLine("Read {0} bytes form socket. \n Data: {1}", content.Length, content);

                    Send(handler, content);
                }
                else
                {
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallback), state);
                }
            }
        }

        private static void Send(Socket handler, String data)
        {
            //Convert the string data to byte data using ASCII encoding
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            //Begin sending the data to the remote device
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
        }

        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object.  
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device.  
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        static void Main(string[] args)
        {
            StartListening();
            Console.WriteLine("Hello World!");
        }
    }
}