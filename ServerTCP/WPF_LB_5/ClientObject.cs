using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace WPF_LB_5
{
    public class ClientObject
    {
        protected internal string Id { get; private set; }
        protected internal NetworkStream Stream { get; private set; }
        string userName;
        public TcpClient client;
        MainWindow server; // объект сервера

        public ClientObject(TcpClient tcpClient, MainWindow serverObject, string name)
        {
            Id = Guid.NewGuid().ToString();
            client = tcpClient;
            server = serverObject;
            serverObject.AddConnection(this);
            userName = name;
        }

        public string Process()
        {
            StringBuilder log = new StringBuilder();//записи логирования
            try
            {
                
                Stream = client.GetStream();
               
                // получаем имя пользователя
                //string message = GetMessage();
                string message;
                //userName = message;

                message = userName + " вошел в чат";
                // посылаем сообщение о входе в чат всем подключенным пользователям
                server.BroadcastMessage(message, this.Id);
                log.Append($"{DateTime.Now}: Resend: {message}\n");
                // в бесконечном цикле получаем сообщения от клиента
                while (true)
                {
                    try
                    {
                        string savePath = "";
                        string fileName = "";
                        message = GetMessage();
                        log.Append($"{DateTime.Now}: Read from {userName}: {message}\n");
                        if (message.Contains("<dis>")) { throw new IOException(); }

                        if (message.Contains("<file>"))//маркер файл, прием и пересылка файла
                        {
                            fileName = message.Substring(message.IndexOf('>') + 1);
                            savePath = "C:\\" + fileName;
                            using (var output = File.Create(fileName))
                            {

                                // read the file divided by 1KB
                                var buffer = new byte[8192];
                                int bytesRead;
                                do
                                {
                                    bytesRead = Stream.Read(buffer, 0, buffer.Length);
                                    output.Write(buffer, 0, bytesRead);
                                }
                                while (Stream.DataAvailable);
                                log.Append($"{DateTime.Now}: Save + {fileName}\n");
                            }
                            server.BroadcastMessage(message, this.Id);
                            log.Append($"{DateTime.Now}: Resend: {message}\n");
                            server.BroadcastFile(fileName, this.Id);
                        }
                        else 
                        {
                            message = String.Format("{0}: {1}", userName, message);
                            server.BroadcastMessage(message, this.Id);
                            log.Append($"{DateTime.Now}: Resend: {message}\n");
                        }



                        
                    }
                    catch(IOException)
                    {
                        message = String.Format("{0}: покинул чат", userName);
                        server.BroadcastMessage(message, this.Id);
                        log.Append($"{DateTime.Now}: Resend: {message}\n");
                        break;
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show(ex.Message); }
            finally
            {
                // в случае выхода из цикла закрываем ресурсы
                server.RemoveConnection(this.Id);
                Close();
            }
            return log.ToString();
        }

        // чтение входящего сообщения и преобразование в строку
        private string GetMessage()
        {
            byte[] data = new byte[8192]; // буфер для получаемых данных




            StringBuilder builder = new StringBuilder();
            int bytes = 0;
            do
            {
                bytes = Stream.Read(data, 0, data.Length);
                builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
            }
            while (Stream.DataAvailable);

            return builder.ToString();
        }

        // закрытие подключения
        protected internal void Close()
        {
            if (Stream != null)
            {
                Stream.Close();
                Stream = null;
            }
            if (client != null)
            {
                client.Close();
                client = null;
            }
        }
    }     
}
