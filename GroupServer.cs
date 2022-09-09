using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;
namespace MobaServer
{
    public class GroupServer
    {
        public static Socket UDPSocket = null;
        public static Thread UDPListeningThread;
        public static Thread FrameSyncThread;

        public static void StartUDPServer()    //开始服务器
        {
            Console.WriteLine("StartGroupServer1.0");
            //端口号（用来监听的）
            int port = 55566;
            IPAddress ip = IPAddress.Any;//"192.168.0.106";
            //IPAddress.Parse(ip)
            //将IP地址和端口号绑定到网络节点point上  
            IPEndPoint ipe = new IPEndPoint(ip, port);

            //定义一个套接字用于监听客户端发来的消息，包含三个参数（IP4寻址协议，流式连接，Tcp协议）  
            UDPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            UDPSocket.Bind(ipe);//绑定端口号和IP
            Console.WriteLine("服务端已经开启");
            UDPListeningThread = new Thread(new ThreadStart(UDPListening_Thread));    //创建监听线程
            UDPListeningThread.Start();

        }
        static void UDPListening_Thread()     //端口监听线程
        {
            while (true)
            {
                EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);//用来保存发送方的ip和端口号

                byte[] buffer = new byte[1024];
                int dataLength = UDPSocket.ReceiveFrom(buffer, ref endPoint);//接收数据报
                //Console.WriteLine("endPoint " + endPoint);
                string ipport = endPoint.ToString();

                string[] ip_port = ipport.Split(':');
                byte id = buffer[0];

                byte[] framedata = new byte[dataLength];
                //Console.WriteLine("id=" + id + " ipport " + ipport);
                
                if (id != 0 && UDPServer.players[(int)id] !=null)  //如果玩家存在
                {
                    if (UDPServer.players[id].endPoint != endPoint)//如果玩家IP发生变化，更新玩家IP地址
                    {
                        UDPServer.players[id].endPoint = endPoint;
                    }
                }
                else    //进来未分配ID玩家
                {
                    bool playerexist = false;

                    foreach (var item in UDPServer.players)   //检查当前用户是否已经在玩家列表
                    {
                        if (item.endPoint.ToString() == ipport)
                        {
                            playerexist = true;
                            framedata[0] = item.playerRoomid;

                            break;
                        }
                    }

                    if (!playerexist)   //如果不存在，添加新玩家并分配房间内ID
                    {
                        if (id == 0)
                        {
                            framedata[0] += 100;
                        }
                        UDPServer.Player p = new UDPServer.Player();
                        Console.WriteLine("新玩家进入 ipport=" + ipport);
                        IPAddress ip = IPAddress.Parse(ip_port[0]);
                        //Console.WriteLine(" ip " + ip);
                        int port = int.Parse(ip_port[1]);
                        p.playerRoomid = (byte)(UDPServer.players.Length + 1);
                        //
                        p.endPoint = endPoint;
                        p.endPoint = new IPEndPoint(ip, port);
                        UDPServer.players[p.playerRoomid]= p;
                    }

                }
            }
        }
    }

}
