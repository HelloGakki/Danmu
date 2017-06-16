using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.ComponentModel;
using System.Windows;
using System.Threading;
using System.Windows.Threading;
using System.Text.RegularExpressions;
using System.IO;

namespace DouyuDanmu.ViewModel
{
    public class TcpDanmu : INotifyPropertyChanged
    {
        private string _roomId, _danmuText,_gifText;
        private string ip = "openbarrage.douyutv.com";
        private int port = 8601;
        //private TcpClient tcpClient;
        TcpClient tcpClient;
        NetworkStream networkStream;

        private string loginMessage = "type@=loginreq/roomid@=3{0}/";
        private string keepLiveMessage = "type@=keeplive/tick@={0}/";
        private string joinGroupMessage = "type@=joingroup/rid@={0}/gid@=-9999/";
        private string logoutMessage = "type@=logout/";

        private int bufferLength = 1024;
        private byte[] receiveBuffer = null;
        private byte[] sendBuffer = null;

        //ManualResetEvent a = new ManualResetEvent(false);

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }
        public string RoomId
        {
            get
            {
                return _roomId;
            }

            set
            {
                _roomId = value;
                OnPropertyChanged("RoomId");
            }
        }
        public string DanmuText
        {
            get
            {
                return _danmuText;
            }

            set
            {
                _danmuText = value;
                OnPropertyChanged("DanmuText");
            }
        }
        public string GifText
        {
            get
            {
                return _gifText;
            }

            set
            {
                _gifText = value;
                OnPropertyChanged("GifText");
            }
        }

        private byte[] StringConvert(string message)
        {
            byte[] messageByte = Encoding.UTF8.GetBytes(message);
            int length = 4 + 4 + message.Length + 1;
            byte[] lengthByte = BitConverter.GetBytes(length);

            byte[] headerByte = new byte[4];
            byte[] codeByte = BitConverter.GetBytes(689);   // 客户端发送给服务器识别码689
            codeByte.CopyTo(headerByte, 0);                 // 后两个字节都为0x00

            // 协议规定内容为:两次数据长度+header+字符串+0x00；
            byte[] buffer = new byte[length + 4];
            lengthByte.CopyTo(buffer, 0);
            lengthByte.CopyTo(buffer, lengthByte.Length);
            headerByte.CopyTo(buffer, lengthByte.Length * 2);
            messageByte.CopyTo(buffer, lengthByte.Length * 2 + headerByte.Length);

            return buffer;
        }
        /// <summary>
        /// 请求登陆弹幕服务器
        /// </summary>
        /// <returns></returns>
        public void LinkStart()
        {
            // 链接斗鱼弹幕服务器
            //tcpClient = new TcpClient();
            //tcpClient.Connect(ip, port);
            //if (tcpClient.Connected != true)
            //    return tcpClient.Connected;

            //NetworkStream networkStream = tcpClient.GetStream();
            //// 发送登陆请求
            //string sendMessage = string.Format(loginMessage, RoomId);
            //byte[] buffer = StringConvert(sendMessage);
            //byte[] reBuffer = new byte[4096];
            //int readByte = 0;
            //networkStream.Write(buffer, 0, buffer.Length);
            //networkStream.Flush();
            //// 获取服务器回复的信息（表示登陆成功
            //try
            //{
            //    lock (networkStream)
            //        readByte = networkStream.Read(reBuffer, 0, 4096);
            //    if (readByte == -1)
            //    {
            //        MessageBox.Show("链接失败");
            //        return false;
            //    }
            //    else
            //        DanmuText = "链接成功\r\n";
            //}
            //catch (Exception e)
            //{
            //    MessageBox.Show(e.Message);
            //    return false;
            //}

            //// 发送入组请求
            //sendMessage = string.Format(joinGroupMessage, RoomId);
            //buffer = StringConvert(sendMessage);
            //networkStream.Write(buffer, 0, buffer.Length);
            ////networkStream.Close();

            //return true;

            DanmuText = "";
            DanmuText = "正在连接斗鱼服务器...\r\n";
            GifText = "";
            tcpClient = new TcpClient();
            //tcpClient.ReceiveTimeout = 10;
            tcpClient.BeginConnect(ip, port, new AsyncCallback(connectCallBack), tcpClient);
        }



        private void ReceiveCallBack(IAsyncResult ar)
        {
            var receiveStream = ar.AsyncState as NetworkStream;
            int receiveLength = receiveStream.EndRead(ar);
            byte[] temp = receiveBuffer.Skip(12).Take(bufferLength - 1 - 12).ToArray(); // 获取正文内容的数组
            string receiveString = Encoding.UTF8.GetString(temp, 0, bufferLength - 1 - 12);   // 获取正文字符串
            Dictionary<string, string> data = ReceiveDataDecode(receiveString);     // 获取键值对应的字典列表
            if (DanmuText.Length > 1024 * 20)
                DanmuText = "";
            if (GifText.Length > 1024 * 20)
                GifText = "";
            try
            {
                if (data.ContainsKey("type"))
                {
                    switch (data["type"])
                    {
                        case "loginres":
                            new Thread(new ThreadStart(() => DanmuText += "进入房间:" + RoomId + "成功...\r\n")).Start();
                            
                            break;
                        case "chatmsg":
                            new Thread(new ThreadStart(() => DanmuText += "[弹幕]    " + data["nn"] + "[" + data["level"] + "]:  " + data["txt"] + "\r\n")).Start();
                            break;
                        case "keeplive":
                            //心跳消息，不做处理
                            break;
                        case "dgb":
                            if (!data.ContainsKey("gfcnt"))
                            {
                                data.Add("gfcnt", "1");
                            }
                            if (!data.ContainsKey("hits"))
                            {
                                data.Add("hits", "1");
                            }
                            new Thread(new ThreadStart(() => GifText += "[礼物]    " + data["nn"] + "[" + data["level"] + "] "
                                 + "赠送礼物: " + data["gfid"] + " 数量: " + data["gfcnt"] + "连击: " + data["hits"] + "\r\n")).Start();
                            break;
                        case "uenter":
                            new Thread(new ThreadStart(() => DanmuText += "[进入]    " + data["nn"] + "[" + data["level"] + "] " + "进入房间\r\n")).Start();
                            break;
                        default:
                            break;
                    }
                }
            }
            catch
            {

            }
            try
            {
                //BinaryReader br = new BinaryReader(receiveStream);
                //int lengthByte = br.ReadInt32();
                Array.Clear(receiveBuffer, 0, bufferLength);
                receiveStream.BeginRead(receiveBuffer, 0, bufferLength, new AsyncCallback(ReceiveCallBack), receiveStream);
            }
            catch (Exception e)
            {
                DanmuText += e.Message + "错误1\r\n";
            }
        }

        private Dictionary<string, string> ReceiveDataDecode(string receiveString)
        {
            if (receiveString == "" || receiveString == null)
                return null;

            Dictionary<string, string> data = new Dictionary<string, string>();
            string key = "";
            string value = "";
            Regex typeRegex = new Regex("type@=[^/]+");
            Regex nnRegex = new Regex("nn@=[^/]+");
            Regex txtRegex = new Regex("txt@=[^/]+");
            Regex levelRegex = new Regex("level@=[^/]+");
            Regex gfidRegex = new Regex("gfid@=[^/]+");
            Regex gfcntRegex = new Regex("gfcnt@=[^/]+");
            Regex hitsRegex = new Regex("hits@=[^/]+");
            Regex tickRegex = new Regex("tick@=[^/]+");
            List<Regex> regexList = new List<Regex> { typeRegex, nnRegex, txtRegex, levelRegex, gfidRegex, gfcntRegex, hitsRegex, tickRegex };
            foreach (var regex in regexList)
            {
                if (!regex.IsMatch(receiveString))
                    continue;

                string stringTemp = regex.Match(receiveString).Value;
                int index = stringTemp.IndexOf('@');
                if (index == -1)
                {
                    index = stringTemp.IndexOf('=');
                    key = stringTemp.Substring(0, index);
                    value = stringTemp.Substring(index + 1);
                }
                else
                {
                    key = stringTemp.Substring(0, index);
                    value = stringTemp.Substring(index + 2);
                }
                if (!data.ContainsKey(key))
                    data.Add(key, value);
                else
                    data[key] = value;
            }

            return data;
        }

        private void SendCallBack(IAsyncResult ar)
        {
            var sendStream = ar.AsyncState as NetworkStream;
            sendStream.EndWrite(ar);

            //DanmuText += "正在登陆房间:" + RoomId + "...\r\n";
        }

        private void connectCallBack(IAsyncResult ar)
        {
            //DanmuText += "链接成功\r\n";
            var tcpConnectClient = ar.AsyncState as TcpClient;

            try
            {
                if (tcpConnectClient.Connected)
                {
                    DanmuText += "链接成功\r\n";
                    tcpConnectClient.EndConnect(ar);
                    networkStream = tcpConnectClient.GetStream();
                    receiveBuffer = new byte[bufferLength];
                    // 登陆房间
                    sendBuffer = StringConvert(string.Format(loginMessage, RoomId));
                    networkStream.BeginWrite(sendBuffer, 0, sendBuffer.Length, new AsyncCallback(SendCallBack), networkStream);

                    // 进入房间分组
                    sendBuffer = StringConvert(string.Format(joinGroupMessage, RoomId));
                    networkStream.BeginWrite(sendBuffer, 0, sendBuffer.Length, new AsyncCallback(SendCallBack), networkStream);

                    // 接收弹幕信息

                    networkStream.BeginRead(receiveBuffer, 0, bufferLength, new AsyncCallback(ReceiveCallBack), networkStream);

                    Thread keepLiveThread = new Thread(new ThreadStart(() =>
                    {
                        while (true)
                        {
                            Thread.Sleep(45000);
                            DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1, 0, 0, 0, 0));
                            DateTime nowTime = DateTime.Now;
                            string t = ((long)Math.Round((nowTime - startTime).TotalMilliseconds, MidpointRounding.AwayFromZero)).ToString();

                            sendBuffer = StringConvert(string.Format(keepLiveMessage, t));
                            networkStream.BeginWrite(sendBuffer, 0, sendBuffer.Length, new AsyncCallback(SendCallBack), networkStream);
                        }
                    }));
                    keepLiveThread.IsBackground = true;
                    keepLiveThread.Start();
                }
                else
                {
                    DanmuText += "链接失败\r\n";
                    tcpConnectClient.EndConnect(ar);
                }
            }
            catch (SocketException e)
            {
                DanmuText += "链接发生错误ConnCallBack...:" + e.Message + "\r\n";
            }
            //}

            //public void GetDanmu()
            //{
            //    Thread getDanmuThread = new Thread(new ParameterizedThreadStart(GetDanmuHandler));
            //    getDanmuThread.IsBackground = true;
            //   // getDanmuThread.Start(tcpClient);
            //}

            //private void GetDanmuHandler(object tcpClient)
            //{
            //    var receiveCient = tcpClient as TcpClient;
            //    NetworkStream networkStream = receiveCient.GetStream();
            //    byte[] receiveBuffer = new byte[1024 * 4];
            //    int readByte = 0;
            //    string receiveMessage = "";
            //    string nameAndText = "";
            //    //"type@=chatmsg\\S+level@=\\d{1,3}"
            //    Regex regex = new Regex("type@=chatmsg\\S+level@=\\d{1,3}");
            //    Regex nameAndTextRegex = new Regex("nn@=\\S+/txt@=[^/]+");
            //    MatchCollection matches;

            //    KeepLive(); // 心跳链接
            //    while (true)
            //    {
            //        try
            //        {
            //            readByte = networkStream.Read(receiveBuffer, 0, 1024 * 4);
            //            matches = regex.Matches(Encoding.UTF8.GetString(receiveBuffer, 0, readByte));
            //            if (readByte == -1)
            //            {
            //                //Dis
            //                break;
            //            }
            //            else if (matches.Count > 0)
            //            {
            //                receiveMessage = "";
            //                foreach (Match match in matches)
            //                {
            //                    //string nameAndText = nameAndTextRegex.Match(match.Value).Value;
            //                    nameAndText = Regex.Replace(nameAndTextRegex.Match(match.Value).Value, "nn@=", "");
            //                    nameAndText = Regex.Replace(nameAndText, "/txt@=", " :   ");
            //                    receiveMessage += "[弹幕]：   " + nameAndText + "\r\n";
            //                    //receiveMessage += match.Value + "\r\n";
            //                }
            //                //Thread updataThread = new Thread(new ThreadStart(() => DanmuText += receiveMessage));
            //                //updataThread.IsBackground = true;
            //                //updataThread.Start();
            //                DanmuText += receiveMessage;
            //            }
            //        }
            //        catch (Exception e)
            //        {
            //            MessageBox.Show(e.Message);
            //            break;
            //        }
            //    }
            //}

            //private void KeepLive()
            //{
            //    System.Timers.Timer timer = new System.Timers.Timer(40000);
            //    timer.Elapsed += Timer_Elapsed;
            //    timer.Start();
            //}

            //private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
            //{
            //    //DanmuText = "";
            //    DateTime startTime = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1, 0, 0, 0, 0));
            //    DateTime nowTime = DateTime.Now;
            //    string t = ((long)Math.Round((nowTime - startTime).TotalMilliseconds, MidpointRounding.AwayFromZero)).ToString();
            //    NetworkStream keepLiveStream = tcpClient.GetStream();
            //    string sendMessage = string.Format(keepLiveMessage, t);
            //    byte[] buffer = StringConvert(sendMessage);
            //    keepLiveStream.Write(buffer, 0, buffer.Length);
            //}
        }
    }
}
