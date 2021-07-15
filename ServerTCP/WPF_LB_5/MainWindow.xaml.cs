using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WPF_LB_5
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            usersList.Add(new User ("Nanit", "1234"));//имитируем базу пользователей
            usersList.Add(new User("Jane", "1234"));
            usersList.Add(new User("User", "1234"));

        }

       
        List<ClientObject> clients = new List<ClientObject>(); // все подключения

        static TcpListener tcpListener = null;
        CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
        CancellationToken token;
        IFormatter formatter = new BinaryFormatter();
        List<User> usersList = new List<User>();


        public void StartTcpListener()
        {          
            try
            {
                token = cancelTokenSource.Token;
                int backlog = 100;//Максимальная длина очереди ожидающих подключений.

                IPAddress ipAddress = IPAddress.Parse("127.0.0.1");
                IPEndPoint ipLocalEndPoint = new IPEndPoint(ipAddress, 8001);
                tcpListener = new TcpListener(ipLocalEndPoint);
                tcpListener.Start(backlog);
                Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate ()
                {
                    tbxHis.Text = string.Empty;
                    tbxHis.Text += $"{DateTime.Now} Waiting for a connection...\n";
                });
                int countClient = 0;
                const int maxClients = 100;//максимальное количесто подключенных клиентов

                while (!token.IsCancellationRequested)
                {
                    if (countClient < maxClients)
                    {
                        ++countClient;
                        byte[] data;
                        TcpClient client = tcpListener.AcceptTcpClient();
                        NetworkStream strm = client.GetStream();
                        User cred = (User)formatter.Deserialize(strm);
                        if (usersList.AsParallel().Any(u => u.Name == cred.Name && u.Password == cred.Password)) 
                        {
                            data = Encoding.Unicode.GetBytes("Pass Ok");
                            strm.Write(data, 0, data.Length);
                        }
                        else
                        {
                            data = Encoding.Unicode.GetBytes("Password or Login Error!");
                            strm.Write(data, 0, data.Length);
                            strm.Close();
                            client.Close();
                            continue;
                        }
                        

                        ClientObject clientObject = new ClientObject(client, this, cred.Name);
                        // создаем новый поток для обслуживания нового клиента
                        var strIp = IPAddress.Parse(((IPEndPoint)client.Client.LocalEndPoint).Address.ToString());
                        Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate ()
                        {
                            tbxHis.Text += $"{DateTime.Now} Client {cred.Name} ({strIp}) Connected\n";
                        });
                       
                        Task.Run(clientObject.Process).ContinueWith(t=>
                        { 
                            Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)delegate ()
                            {
                                --countClient;
                                tbxLog.Text += t.Result;
                                tbxHis.Text += $"{DateTime.Now} Client {cred.Name} ({strIp}) Disconnected\n";
                               
                            });
                        });
                    }
                                        
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
            finally
            {
                if (tcpListener != null){ Disconnect(); }
            }
        }



        protected internal void AddConnection(ClientObject clientObject)
        {
            clients.Add(clientObject);
        }
        protected internal void RemoveConnection(string id)
        {
            // получаем по id закрытое подключение
            ClientObject client = clients.FirstOrDefault(c => c.Id == id);
            // и удаляем его из списка подключений
            if (client != null) { clients.Remove(client); }
        }

        private void btConnect_Click(object sender, RoutedEventArgs e)
        {
            if (tcpListener != null) return;
            Task.Run(() => { StartTcpListener(); });
        }

        protected internal void BroadcastMessage(string message, string id)
        {
            byte[] data = Encoding.Unicode.GetBytes(message);
            for (int i = 0; i < clients.Count; i++)
            {
                clients[i].Stream.Write(data, 0, data.Length); //передача данных
                /*if (clients[i].Id != id) // если id клиента не равно id отправляющего
                {
                    clients[i].Stream.Write(data, 0, data.Length); //передача данных
                }*/
            }
        }

        protected internal void BroadcastFile(string fileName, string id)
        {
            if (!File.Exists(fileName)) { return; }
            for (int i = 0; i < clients.Count; i++)
            {
                clients[i].client.Client.SendFile(fileName); //передача данных
                /*if (clients[i].Id != id) // если id клиента не равно id отправляющего
                {
                    clients[i].client.Client.SendFile(fileName); //передача данных
                }*/
            }


        }

        // отключение всех клиентов
        protected internal void Disconnect()
        {
            tcpListener.Stop(); //остановка сервера
            for (int i = 0; i < clients.Count; i++)
            {
                clients[i].Close(); //отключение клиента
            }
            Environment.Exit(0); //завершение процесса
        }



        private void btStop_Click(object sender, RoutedEventArgs e)
        {
            cancelTokenSource.Cancel();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            if (tbxUser.Text == string.Empty || tbxPassword.Text == string.Empty) return;
          
            if (usersList.All(c => c.Name != tbxUser.Text))
            {
                usersList.Add(new User (tbxUser.Text, tbxPassword.Text));
                tbxHis.Text += $"{DateTime.Now} User {tbxUser.Text} Add\n";
                tbxUser.Text = string.Empty;
                tbxPassword.Text = string.Empty;
            }
            else { MessageBox.Show($" User {tbxUser.Text} Exists"); }

        }

        private void btnDel_Click(object sender, RoutedEventArgs e)
        {
            if (tbxUser.Text == string.Empty) return;
            var user = usersList.FirstOrDefault(c => c.Name == tbxUser.Text);
            if (user != null)
            {
                usersList.Remove(user);
                tbxHis.Text += $"{DateTime.Now} User {tbxUser.Text} Remove\n";
                tbxUser.Text = string.Empty;
            }
            else { MessageBox.Show($" User {tbxUser.Text} Not Exists"); }
        }
    }
}
