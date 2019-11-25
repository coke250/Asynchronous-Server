using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace Asynchronous_Server
{
    public class StateObject
    {
        public Socket workSocket = null;                // 클라이언트 소켓
        public const int BufferSize = 1024;             // 버퍼 사이즈
        public byte[] buffer = new byte[BufferSize];    // 버퍼
        public StringBuilder sb = new StringBuilder();  // 수신 받은 데이터
    }

    public class AsynchronousSocketListener
    {
        public static ManualResetEvent allDone = new ManualResetEvent(false);

        public AsynchronousSocketListener(){ }

        public static void StartListening()
        {
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 10000);
            Console.WriteLine("HostName : " + Dns.GetHostName());
            // TCP/IP 소켓 생성
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // 연결을 위한 리스너 및 로컬 앤드포인트 바인드
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while(true)
                {
                    allDone.Reset();

                    // 수신을 위한 비동기 소켓 시작
                    Console.WriteLine("Waiting for a connection...");
                    listener.BeginAccept(new AsyncCallback(AcceptCallBack), listener);

                    // 연결될 때까지 기다림
                    allDone.WaitOne();
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }

        public static void AcceptCallBack(IAsyncResult ar)
        {
            // 메인 스레드에 계속하는 신호 보냄
            allDone.Set();

            // 클라이언트 요청, 핸들 소켓을 가져옴
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // StateObject 생성
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallBack), state);
        }

        public static void ReadCallBack(IAsyncResult ar)
        {
            string content = string.Empty;

            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // 수신 받은 데이터 
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                content = state.sb.ToString();

                // end-of-file 태그가 있는지 확인한다
                // end-of-file 태그가 없을 경우 계속 수신한다
                if (content.IndexOf("<EOF>") > -1)
                {
                    Console.WriteLine("Read {0} bytes from socket. \n Data : {1}", content.Length, content);
                    // 클라이언트에게 데이터를 전송한다.
                    // 전체 전송으로 변경해야함.
                    Send(handler, content);
                }
                else
                {
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0, new AsyncCallback(ReadCallBack), state);
                }
            }
        }

        private static void Send(Socket handler, string data)
        {
            // string을 bate 배열로 변환
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // 데이터 전송 
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallBack), handler);
        }

        private static void SendCallBack(IAsyncResult ar)
        {
            try
            {
                Socket handler = (Socket)ar.AsyncState;

                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
    }


    class Program
    {
        static void Main(string[] args)
        {
            AsynchronousSocketListener.StartListening();
        }
    }
}
