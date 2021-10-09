using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Alesoft2token
{
    /// <summary>
    /// Interaction logic for Login.xaml
    /// </summary>
    public partial class Login : Window
    {
        public Login()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            string username = UserBox1.Text;
            string password = PasswordBox1.Password;
            get_jwt_token(username, password);
        }

        private void get_jwt_token(String username,String password) 
        {
            var url = get_api_url();
            var client = new RestClient(url + "token/");
            var request = new RestRequest("", RestSharp.DataFormat.Json);
            request.AddJsonBody(new { username = username, password = password });
            var response = client.Post(request);
            var token = JsonSerializer.Deserialize<Token>(response.Content);
            Properties.Settings.Default.token_access = token.access;
            Properties.Settings.Default.token_refresh = token.refresh;
            Properties.Settings.Default.Save();

            if (token.access != null){
                MainWindow _window2 = new MainWindow();
                _window2.Show();
                this.Close();
            }
            else
            {
                MessageBox.Show("Error en las credenciales, intente de nuevo.");
                UserBox1.Clear();
                PasswordBox1.Clear();
            }
        }

        private string get_api_url() {
            string config = Properties.Settings.Default["api_root"].ToString();
            return config;
        }
    }

    public class Token
    {
        public string refresh { get; set; }
        public string access { get; set; }
    }
}
