using System;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;
using OpenFileDialog = Microsoft.Win32.OpenFileDialog;
using System.IO;

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
        }

        static readonly int port = 8001;
        static readonly string ip = "127.0.0.1";
        User credential = null;
        IFormatter formatter = new BinaryFormatter();



        private TcpClient client = null;
        private IPAddress ipAddress = null;
        private NetworkStream stream = null;
        private const int BufferSize = 1024;

        CancellationTokenSource cancelTokenSource = new CancellationTokenSource();
        CancellationToken token;// будет ли работать поток для приема
        Task receiveTask;

        string saveFolder = "";

        private void Dis()
        {
            if (stream == null || client == null) return;

            byte[] data = Encoding.Unicode.GetBytes("<dis>");
            stream.Write(data, 0, data.Length);
            cancelTokenSource.Cancel();
            if (stream != null)
            {
                stream.Close();
                stream = null;
            }
            if (client != null)
            {
                client.Close();
                client = null;
            }
           
            tbxLogin.IsEnabled = true;
            PasBox.IsEnabled = true;

            //tblChat.Text = DateTime.Now.ToShortTimeString() + tbxLogin.Text + " покинул чат" + "\r\n" + tblChat.Text;

        }

        private bool IsConnected(TcpClient client)
        {
            try
            {
                formatter.Serialize(stream, credential);
                // получаем ответ
                var data = new byte[BufferSize]; // буфер для получаемых данных
                StringBuilder builder = new StringBuilder();
                int bytes = 0;
                do
                {
                    bytes = stream.Read(data, 0, data.Length);
                    builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                }
                while (stream.DataAvailable);
                if (!builder.ToString().Contains("Pass Ok")) 
                {
                    throw new Exception(builder.ToString());
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                return false;
            }
        }

        private void btConnect_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (stream != null || client != null) return;
                if (tbxLogin.Text == string.Empty|| PasBox.Password == string.Empty) { MessageBox.Show("Enter Login\\Password!", "Error!"); return; }
                credential = new User(tbxLogin.Text, PasBox.Password);
              
                ipAddress = IPAddress.Parse(ip);
                client = new TcpClient(ip, port);
                stream = client.GetStream();
               


                if (!IsConnected(client)) //проверка на факт подключения
                { 
                    stream.Close();
                    stream = null;
                    client.Close();
                    client = null;
                }
                else
                {
                    tbxLogin.IsEnabled = false;
                    PasBox.IsEnabled = false;
                    receiveTask = new Task(ReceiveMessages);
                    receiveTask.Start();

                    string time = DateTime.Now.ToShortTimeString();
                    //tblChat.Text = time + tbxLogin.Text + " вошел в чат" + "\r\n" + tblChat.Text;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Dis();
            }
            
        }

        private void btDisConnect_Click(object sender, RoutedEventArgs e)
        {
            Dis();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            Dis();
        }

        private void btnSend_Click(object sender, RoutedEventArgs e)
        {
            if (stream == null || client == null || tblSend.Text == string.Empty) return;
            try
            {
                // ввод сообщения
                string message = tblSend.Text;

                // преобразуем сообщение в массив байтов
                byte[] data = Encoding.Unicode.GetBytes(message);
                // отправка сообщения
                stream.Write(data, 0, data.Length);
                //tblChat.Text = DateTime.Now.ToShortTimeString() + " " + message + "\r\n" + tblChat.Text;
                tblSend.Clear();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Dis();
            }
        }

        private void ReceiveMessages()
        {
            try
            {
                token = cancelTokenSource.Token;
                while (!token.IsCancellationRequested)
                {
                    // получаем ответ
                    var data = new byte[8192]; // буфер для получаемых данных
                    StringBuilder builder = new StringBuilder();
                    int bytes = 0;
                    do
                    {
                        bytes = stream.Read(data, 0, data.Length);
                        builder.Append(Encoding.Unicode.GetString(data, 0, bytes));
                    }
                    while (stream.DataAvailable);
                    string message = builder.ToString();

                    // добавляем полученное сообщение в текстовое поле
                    Dispatcher.BeginInvoke((Action)(() =>
                    {
                        string time = DateTime.Now.ToShortTimeString();
                        tblChat.Text = time + " " + message + "\r\n" + tblChat.Text;
                    }));

                    if (message.Contains("<file>"))//маркер файл, прием и пересылка файла
                    {
                        var fileName = message.Substring(message.IndexOf('>') + 1);
                        saveFolder += "\\" + fileName;
                        using (var output = File.Create(saveFolder))
                        {
                            // read the file divided by 1KB
                            var buffer = new byte[8192];
                            int bytesRead;
                            do
                            {
                                bytesRead = stream.Read(buffer, 0, buffer.Length);
                                output.Write(buffer, 0, bytesRead);
                            }
                            while (stream.DataAvailable);
                        }
                    }
                }
            }
            catch (Exception)
            {
                //MessageBox.Show(ex.Message);
                Dis();
            }
        }
        private void btnOpen_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog openFileDlg = new FolderBrowserDialog();
            var result = openFileDlg.ShowDialog();
            if (result.ToString() != string.Empty)
            {
                saveFolder = openFileDlg.SelectedPath.ToString();
            }
        }

        private void btnSendFile_Click(object sender, RoutedEventArgs e)
        {
            if (stream == null || client == null) return;
            try
            {
                OpenFileDialog openFileDialog = new OpenFileDialog();
                openFileDialog.Filter = "All Files|*.*";
                openFileDialog.Title = "Select a File";
                if (openFileDialog.ShowDialog() == true)
                {
                    var FileName = openFileDialog.FileName;  //file path
                    var SafeFileName = openFileDialog.SafeFileName; //file name only.

                    // ввод сообщения
                    string message = $"<file>{SafeFileName}";
                    // преобразуем сообщение в массив байтов
                    byte[] data = Encoding.Unicode.GetBytes(message);
                    // отправка сообщения
                    stream.Write(data, 0, data.Length);
                    //tblChat.Text = DateTime.Now.ToShortTimeString() + " " + message + "\r\n" + tblChat.Text;
                    client.Client.SendFile(FileName);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                Dis();
            }
        }
    }
}
