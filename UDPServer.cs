using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO;
using System.Collections;
using System.Net.NetworkInformation;

namespace MobaServer
{
    class UDPServer
    {
        public static Socket UDPSocket = null;
        public static Thread UDPListeningThread;
        public static Thread FrameSyncThread;
        public static int serverFrameID  = 0;
        public static int FrameLength = 8;
        public class Player//玩家数据
        {
            public int userid;
            public byte playerRoomid = 0;//玩家在房间内的ID
            public int frameid = 1;//玩家下一帧要进入的ID
            //public string ipport;
            public EndPoint endPoint;
           

        }
        //List<Player> playerList = new List<Player>();//保存所有玩家信息
        public static Player[] players = new Player[5];
        public static int playerCount = 0;
        //static object playerDictionaryLock = new object();
        //每帧数据 = 玩家ID（4字节）+遥感角度（2字节）+ 按键操作（N）
        public static byte[][] match_syncFrames = new byte[65536][];//保存所有帧数据


        static byte[] defaultFrameData = new byte[] { 5, 100, 100, 0, 0, 0, 0, 0, 5, 100, 100, 0, 0, 0, 0, 0, 5, 100, 100, 0, 0, 0, 0, 0, 5, 100, 100, 0, 0, 0, 0, 0, 5, 100, 100, 0, 0, 0, 0, 0 };//每帧采集的客户端的操作;
        static byte[] nextFrameData = defaultFrameData;
        static object nextFrameListLock = new object();
        //static object frameidLock = new object();
        public static void StartUDPServer()    //开始服务器
        {
            Console.WriteLine("StartUDPServer1.0");
            //端口号（用来监听的）
            int port = 55566;
            IPAddress ip = IPAddress.Any;//"192.168.0.106";
            //nextFrameData = defaultFrameData;
            //IPAddress.Parse(ip)
            //将IP地址和端口号绑定到网络节点point上  
            IPEndPoint ipe = new IPEndPoint(ip, port);

            //定义一个套接字用于监听客户端发来的消息，包含三个参数（IP4寻址协议，流式连接，Tcp协议）  
            UDPSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            UDPSocket.Bind(ipe);//绑定端口号和IP
            Console.WriteLine("服务端已经开启");
            UDPListeningThread = new Thread(new ThreadStart(UDPListening_Thread));    //创建监听线程
            UDPListeningThread.Start();

            //AvailablePort();
        }
        static void UDPListening_Thread()     //端口监听线程
        {
            
            while (true)
            {
                
                EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);//用来保存发送方的ip和端口号

                byte[] UDPData = new byte[1024];
                int SIO_UDP_CONNRESET = -1744830452;
                UDPSocket.IOControl((IOControlCode)SIO_UDP_CONNRESET, new byte[] { 0, 0, 0, 0 }, null);
                int dataLength = UDPSocket.ReceiveFrom(UDPData, ref endPoint);//接收数据报
                //Console.WriteLine("用户" + endPoint +" 发送了长度为"+ dataLength);
                string ipport = endPoint.ToString();

                string[] ip_port = ipport.Split(':');
                

                if (UDPData[0] == 1)//为帧数据
                {
                    byte playerRoomid = UDPData[5];
                    //Console.WriteLine("playerRoomid=" + playerRoomid);

                    int frameid = BitConverter.ToInt32(UDPData, 1);
                    if (UDPServer.players[playerRoomid] != null)  //如果玩家存在
                    {
                        //Console.WriteLine("玩家存在");
                        if (UDPServer.players[playerRoomid].endPoint != endPoint)//如果玩家IP发生变化，更新玩家IP地址
                        {
                            UDPServer.players[playerRoomid].endPoint = endPoint;
                        }
                        lock (nextFrameListLock)
                        {
                            UDPServer.players[playerRoomid].frameid = frameid;
                        }
                    }
                    else    //进来未分配ID玩家
                    {
                        
                        
                        UDPServer.Player p = new UDPServer.Player();
                        Console.WriteLine("新玩家进入 ipport=" + ipport);
                        IPAddress ip = IPAddress.Parse(ip_port[0]);
                        int port = int.Parse(ip_port[1]);
                        p.playerRoomid = playerRoomid;
                        p.endPoint = endPoint;
                        p.endPoint = new IPEndPoint(ip, port);
                        p.frameid = 0;
                        UDPServer.players[p.playerRoomid]=p;
                        playerCount += 1;
                        
                        if (playerCount==1)
                        {
                            Console.WriteLine("当前玩家" + playerCount + "，新建玩家并启动帧同步");
                            FrameSyncThread = new Thread(new ThreadStart(FrameSyncThread_Thread));    //创建帧同步线程
                            FrameSyncThread.Start();
                        }
                    }
                    //if (dataLength>6)
                    //{
                    //byte[] framedata = new byte[8];
                    //Console.WriteLine("id=" + id + " ipport " + ipport);
                    //Array.Copy(UDPData, 5, framedata, 0, 8);
                    lock (nextFrameListLock)
                    {
                        Array.Copy(UDPData, 5, nextFrameData, (int)playerRoomid * 8, 8);
                    }


                    // }


                }
                
            }
        }
        //public static void SendFrame(byte[] frameData)
        //{
        //    //byte[] frameData = new Byte[16];
        //    IPAddress ip = IPAddress.Parse("127.0.0.1");
        //    IPEndPoint endPoint = new IPEndPoint(ip, 6666);
        //    UDPSocket.SendTo(frameData, endPoint);
        //}
        static int allx = 0;
        static int allx2 = 0;
         static void FrameSyncThread_Thread()    //帧同步线程
        {
            
            while (true)
            {

                lock (nextFrameListLock)
                {
                    //组合这50毫秒内收到的所有同步帧
                    //Console.WriteLine("添加1 第" + serverFrameID + "帧=" + " = " + allx);
                    byte[] tempFrameData = new byte[40];
                    Array.Copy(nextFrameData,0, tempFrameData,0,40);

                    match_syncFrames[serverFrameID] = tempFrameData;//保存当前帧的操作放入所有帧中去，
                    allx += match_syncFrames[serverFrameID][1] + match_syncFrames[serverFrameID][2] - 200;
                    //Console.WriteLine("添加 第" + serverFrameID + "帧=" + match_syncFrames[serverFrameID][1] + " " + match_syncFrames[serverFrameID][2] + " = " + allx);
                    
                    //给每个玩家发送同步帧
                    //lock (UDPServer.playerDictionary)
                    //{
                    //Console.WriteLine("验证-1 第" + serverFrameID + "帧=" + match_syncFrames[serverFrameID][1] + " " + match_syncFrames[serverFrameID][2] + " = " + allx);

                    foreach (Player player in players)
                    {

                        if (player != null)
                        {
                            
                            int playerFrameID = player.frameid;
                            //Console.WriteLine("player"+ player.playerRoomid + " " + player.endPoint + " FrameID=" + playerFrameID);
                            byte[] sendSyncFrameData;
                            if (playerFrameID == serverFrameID - 2 || playerFrameID == serverFrameID - 1 || playerFrameID == serverFrameID)
                            {
                                //Console.WriteLine("给玩家" + player.Value.playerRoomid + "   发送" + playerFrameID + "/" + serverFrameID);
                                sendSyncFrameData = new byte[match_syncFrames[playerFrameID].Length + 7];     //Type1+同步帧数量1+服务器帧index4+操作指令数量1   约47字节
                                sendSyncFrameData[0] = 1;
                                sendSyncFrameData[1] = 1;//同步帧数量（考虑部分玩家丢帧情况）目前备用
                                byte[] syncFrameIDByte = BitConverter.GetBytes(playerFrameID);//帧ID号,从0开始
                                Array.Copy(syncFrameIDByte, 0, sendSyncFrameData, 2, 4);//服务器帧index
                                Console.WriteLine("验证0 第" + serverFrameID + "帧=" + match_syncFrames[serverFrameID][1] + " " + match_syncFrames[serverFrameID][2] + " = " + allx);

                                sendSyncFrameData[6] = (byte)(match_syncFrames[playerFrameID].Length / FrameLength);     //操作指令数量
                                Array.Copy(match_syncFrames[playerFrameID], 0, sendSyncFrameData, 7, match_syncFrames[playerFrameID].Length);//单个当前的同步帧
                                //Console.WriteLine("验证1 第" + serverFrameID + "帧=" + match_syncFrames[serverFrameID][1] + " " + match_syncFrames[serverFrameID][2] + " = " + allx);

                                //SocketAsyncEventArgs e = new SocketAsyncEventArgs();
                                //e.RemoteEndPoint = player.endPoint;
                                //e.SetBuffer(sendSyncFrameData, 0, sendSyncFrameData.Length);
                                //UDPSocket.SendToAsync(e);
                                UDPSocket.SendTo(sendSyncFrameData, player.endPoint);
                                if (match_syncFrames[playerFrameID][1] != sendSyncFrameData[8]|| match_syncFrames[playerFrameID][2] != sendSyncFrameData[9])
                                {
                                    Console.WriteLine(
                                        " 帧数据发生变化 **************************  match_syncFrames[serverFrameID][1]=" + match_syncFrames[serverFrameID][1] 
                                        + "  sendSyncFrameData[8]=" + sendSyncFrameData[8]
                                        + "  match_syncFrames[serverFrameID][2]=" + match_syncFrames[serverFrameID][2]
                                        + "  sendSyncFrameData[9]=" + sendSyncFrameData[9]

                                        );
                                }
                                //Console.WriteLine(BitConverter.ToString(sendSyncFrameData));
                                //Console.WriteLine("验证2 第" + serverFrameID + "帧=" + match_syncFrames[serverFrameID][1] + " " + match_syncFrames[serverFrameID][2] + " = " + allx);
                            }
                            else if (playerFrameID < serverFrameID - 2)//如果客户端缺帧，则发多帧
                            {

                                int lostSyncFrameCount = serverFrameID - playerFrameID;
                                Console.WriteLine("玩家  " + player.playerRoomid + "  " + playerFrameID + "缺"+ lostSyncFrameCount + "帧");
                                if (lostSyncFrameCount > 20)
                                {
                                    lostSyncFrameCount = 20;
                                }
                                //连续单发方案
                                for (int i = 0; i < lostSyncFrameCount; i++)  //发lostSyncFrameCount次
                                {

                                    int syncFrameIndex = playerFrameID + i;
                                    //string tempstr = "";
                                    //for (int s = 0; s < serverFrameID; s++)
                                    //{
                                    //    tempstr += match_syncFrames[s][1].ToString()+" ";
                                    //}
                                    //Console.WriteLine("玩家  " + player.playerRoomid + "  " + playerFrameID + "缺帧 发送 " + syncFrameIndex + "/" + serverFrameID
                                    //    +"  "+ match_syncFrames[syncFrameIndex][1] + " " + match_syncFrames[syncFrameIndex][2]+"\r\n");
                                    sendSyncFrameData = new byte[match_syncFrames[syncFrameIndex].Length + 7];     //Type1+同步帧数量1+服务器帧index4+操作指令数量1   约47字节
                                    sendSyncFrameData[0] = 1;
                                    sendSyncFrameData[1] = 1;//同步帧数量（考虑部分玩家丢帧情况）目前备用
                                    byte[] syncFrameIDByte = BitConverter.GetBytes(syncFrameIndex);//帧ID号,从0开始
                                    Array.Copy(syncFrameIDByte, 0, sendSyncFrameData, 2, 4);//服务器帧index
                                    sendSyncFrameData[6] = (byte)(match_syncFrames[syncFrameIndex].Length / FrameLength);     //操作指令数量
                                    Array.Copy(match_syncFrames[syncFrameIndex], 0, sendSyncFrameData, 7, match_syncFrames[syncFrameIndex].Length);//单个当前的同步帧
                                                                                                                                                   //UDPSocket.SendTo(sendSyncFrameData, player.Value.endPoint);//发送单个当前的同步帧
                                    UDPSocket.SendTo(sendSyncFrameData, player.endPoint);
                                    if (match_syncFrames[syncFrameIndex][1] != sendSyncFrameData[8] || match_syncFrames[syncFrameIndex][2] != sendSyncFrameData[9])
                                    {
                                        Console.WriteLine(
                                            " 帧数据发生变化 **************************  match_syncFrames[serverFrameID][1]=" + match_syncFrames[serverFrameID][1]
                                            + "  sendSyncFrameData[8]=" + sendSyncFrameData[8]
                                            + "  match_syncFrames[serverFrameID][2]=" + match_syncFrames[serverFrameID][2]
                                            + "  sendSyncFrameData[9]=" + sendSyncFrameData[9]

                                            );
                                    }
                                    //Console.WriteLine("验证 第" + serverFrameID + "帧=" + match_syncFrames[serverFrameID][1] + " " + match_syncFrames[serverFrameID][2] + " = " + allx);

                                    //SocketAsyncEventArgs args = new SocketAsyncEventArgs();
                                    //args.RemoteEndPoint = player.Value.endPoint;
                                    //args.SetBuffer(new byte[1], 0, 1);
                                    ////args.UserToken = waitHandle;
                                    ////args.Completed += AsyncCompleted;
                                    //UDPSocket.SendToAsync(args);//发送单个当前的同步帧
                                    //SocketAsyncEventArgs e = new SocketAsyncEventArgs();
                                    //e.RemoteEndPoint = player.endPoint;
                                    //e.SetBuffer(sendSyncFrameData, 0, sendSyncFrameData.Length);
                                    //UDPSocket.SendToAsync(e);

                                    //Console.WriteLine("验证4 第" + serverFrameID + "帧=" + match_syncFrames[serverFrameID][1] + " " + match_syncFrames[serverFrameID][2] + " = " + allx);

                                    //Console.WriteLine(BitConverter.ToString(sendSyncFrameData));
                                }

                                //end

                                if (playerFrameID != player.frameid)
                                {
                                    Console.WriteLine(" 帧ID发生变化 **************************  playerFrameID" + playerFrameID + "     player.frameid=" + player.frameid);
                                }


                                //组合发送方案
                                //int lostSyncFramesCount = serverFrameID - player.Value.frameid;
                                //if (lostSyncFramesCount>20)
                                //{
                                //    lostSyncFramesCount = 20;
                                //}
                                //byte[] tempSyncFramesData = new byte[1024];//组合缺失的帧

                                //int copyIndex = 0;
                                //int oneSyncFramesLength = 0;
                                //for (int i = player.Value.frameid; i < player.Value.frameid+lostSyncFramesCount; i++)//组合
                                //{
                                //    oneSyncFramesLength = match_syncFrames[i].Length;
                                //    Array.Copy(match_syncFrames[i], 0, tempSyncFramesData, copyIndex, oneSyncFramesLength);
                                //    copyIndex += oneSyncFramesLength;
                                //}
                                //Console.WriteLine("组合 copyIndex=" + copyIndex);
                                //byte[] lostSyncFramesData = new byte[copyIndex+2];//缺失的帧    //数据类型+同步帧数量+（帧ID号+操作帧数量+所有玩家每帧的数据）
                                //lostSyncFramesData[0] = 1;
                                //lostSyncFramesData[1] = (byte)lostSyncFramesCount;
                                //Array.Copy(tempSyncFramesData,0,lostSyncFramesData,2, copyIndex);

                                //UDPSocket.SendTo(lostSyncFramesData, player.Value.endPoint);//发送组合的多个同步帧
                                //end
                            }


                        }




                    }
                    //}
                    //nextFrameData = new byte[40];
                    allx2 += match_syncFrames[serverFrameID][1] + match_syncFrames[serverFrameID][2] - 200;
                    //if (allx != allx2)
                    //{
                    //    Console.WriteLine("不一样*********************************************************************************************************");
                    //}
                    
                    //Console.WriteLine("验证 第" + serverFrameID + "帧=" + match_syncFrames[serverFrameID][1] + " " + match_syncFrames[serverFrameID][2] + " = " + allx);
                    serverFrameID += 1;
                    nextFrameData = defaultFrameData;
                }

                Thread.Sleep(50);//服务器启动定时器，每隔一帧的时间触发一次逻辑帧
            }
        }
        public long ipToLong(String ip)
        {
            String[] ips = ip.Split('.');
            StringBuilder strip = new StringBuilder();
            for (int i = 0; i < ips.Length; i++)
            {
                strip.Append(ips[i]);
            }
            return long.Parse(strip.ToString());
        }



    }


}
