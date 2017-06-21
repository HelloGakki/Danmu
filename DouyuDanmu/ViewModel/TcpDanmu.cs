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
        private string _roomId, _danmuText, _gifText;
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
            //byte[] temp = receiveBuffer.Skip(12).Take(bufferLength - 1 - 12).ToArray(); // 获取正文内容的数组
            //string receiveString = Encoding.UTF8.GetString(temp, 0, bufferLength - 1 - 12);   // 获取正文字符串
            Regex regex = new Regex("type@=[\\w /@=\\u3002\\uff1b\\uff0c\\uff1a\\u201c\\u201d\\uff08\\uff09\\u3001\\uff1f\\u300a\\u300b\\u4e00-\\u9fa5]+"); 
            string receiveString = Encoding.UTF8.GetString(receiveBuffer, 0, receiveLength);
            MatchCollection matches = regex.Matches(receiveString); // 匹配type@=开头的字符串
            foreach (Match match in matches)
            {
                Thread outputThread = new Thread(new ParameterizedThreadStart(StringOutput));
                outputThread.Start(match.Value);
                outputThread.IsBackground = true;
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

        private void StringOutput(object value)
        {
            var receiveString = value as string;
            Dictionary<string, string> data = ReceiveDataDecode(receiveString);     // 获取键值对应的字典列表
            if (DanmuText.Length > 1024 * 20)
                DanmuText = "";
            if (GifText.Length > 1024 * 20)
                GifText = "";
            if (data.ContainsKey("type"))
            {
                try
                {
                    switch (data["type"])
                    {
                        case "loginres":
                            DanmuText += "进入房间:" + RoomId + "成功...\r\n";
                            break;
                        case "chatmsg":
                            DanmuText += "[弹幕]    " + data["nn"] + "[" + data["level"] + "]:  " + data["txt"] + "\r\n";
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
                            GifText += "[礼物]    " + data["nn"] + "[" + data["level"] + "] "
                                 + "赠送礼物: " + data["gfid"] + " 数量: " + data["gfcnt"] + "连击: " + data["hits"] + "\r\n";
                            break;
                        case "uenter":
                            DanmuText += "[进入]    " + data["nn"] + "[" + data["level"] + "] " + "进入房间\r\n";
                            break;
                        default:
                            break;
                    }
                }
                catch(Exception e)
                {
                   // GifText += e.Message; // 用于显示错误信息
                }
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
        }
    }
}
