using libzkfpcsharp;
using RestSharp;
using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Alesoft2token
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        IntPtr mDevHandle = IntPtr.Zero;


        IntPtr FormHandle = IntPtr.Zero;
        bool bIsTimeToDie = false;
        bool IsRegister = false;
        bool bIdentify = true;
        byte[] FPBuffer;
        int RegisterCount = 0;
        String strShows;
        const int REGISTER_FINGER_COUNT = 3;

        byte[][] RegTmps = new byte[3][];
        byte[] RegTmp = new byte[2048];
        byte[] CapTmp = new byte[2048];
        byte[] paramValue1 = new byte[4];

        int cbCapTmp = 2048;
        int cbRegTmp = 1;
        IntPtr mDBHandle = IntPtr.Zero;
        int iFid = 1;
        int profileid = 0;

        private int mfpWidth = 0;
        private int mfpHeight = 0;
        private int mfpDpi = 0;
        const int MESSAGE_CAPTURED_OK = 0x0400 + 6;

        public MainWindow()
        {
            
            InitializeComponent();
            Task.Run(() => {
                Set_available();
            });
            Task.Run(() => {
                DoCapture();
            });
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //funcion incial al cargar la ventana
            await GetUsers();
        }

        private async Task GetUsers()
        {
            string token_access = Properties.Settings.Default["token_access"].ToString();
            string api_url = Properties.Settings.Default["api_root"].ToString();
            RestClient client = new RestClient(api_url + "worker/");
            RestRequest request = new RestRequest("", Method.GET, RestSharp.DataFormat.Json);
            request.AddHeader("authorization", "Bearer " + token_access);
            List<Profile> response = await client.GetAsync<List<Profile>>(request);
            ComboBoxUsers.SelectedValuePath = "Key";
            ComboBoxUsers.DisplayMemberPath = "Value";
            response.Sort((s1, s2) => s1.full_name.CompareTo(s2.full_name));
            foreach (Profile profile in response)
            {
                if (!ComboBoxUsers.Items.Contains(new KeyValuePair<int, string>(profile.pk, profile.full_name)))
                    ComboBoxUsers.Items.Add(new KeyValuePair<int, string>(profile.pk, profile.full_name));
            }
        }

        private async Task PostToken(int user,String token)
        {
            string token_access = Properties.Settings.Default["token_access"].ToString();
            string api_url = Properties.Settings.Default["api_root"].ToString();
            RestClient client = new RestClient(api_url + "hash/");
            RestRequest request = new RestRequest("", Method.POST, RestSharp.DataFormat.Json);
            request.AddHeader("authorization", "Bearer " + token_access);
            Hash new_hash = new Hash();
            new_hash.user = user;
            new_hash.hash = token;
            request.AddJsonBody(new_hash);
            var response = client.Post(request);
            ComboBoxUsers.SelectedValuePath = "Key";
            ComboBoxUsers.DisplayMemberPath = "Value";

            txtBox.Text = response.Content.ToString();
        }

        private void DoCapture()
        {
            while (!bIsTimeToDie)
            {
                cbCapTmp = 2048;
                int ret = zkfp2.AcquireFingerprint(mDevHandle, FPBuffer, CapTmp, ref cbCapTmp);

                zkfp.Int2ByteArray(1, paramValue1);
                zkfp2.SetParameters(mDevHandle, 101, paramValue1, 4);
                if (ret == zkfp.ZKFP_ERR_OK)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        Register();
                    });
                    
                }
                Thread.Sleep(200);
            }
        }

        public void Register()
        {
            if (IsRegister)
            {
                int ret = zkfp.ZKFP_ERR_OK;
                int fid = 0, score = 0;
                ret = zkfp2.DBIdentify(mDBHandle, CapTmp, ref fid, ref score);
                if (zkfp.ZKFP_ERR_OK == ret)
                {
                    leftstatusBar.Text = "Huella registrada por " + fid + "!";
                    return;
                }

                if (RegisterCount > 0 && zkfp2.DBMatch(mDBHandle, CapTmp, RegTmps[RegisterCount - 1]) <= 0)
                {
                    leftstatusBar.Text = "Error, la huella no coincide con la anterior, intente de nuevo.";
                    return;
                }

                Array.Copy(CapTmp, RegTmps[RegisterCount], cbCapTmp);
                String strBase64 = zkfp2.BlobToBase64(CapTmp, cbCapTmp);
                //txtBox.Text = strBase64;
                byte[] blob = zkfp2.Base64ToBlob(strBase64);
                RegisterCount++;
                if (RegisterCount >= REGISTER_FINGER_COUNT)
                {
                    RegisterCount = 0;
                    if (zkfp.ZKFP_ERR_OK == (ret = zkfp2.DBMerge(mDBHandle, RegTmps[0], RegTmps[1], RegTmps[2], RegTmp, ref cbRegTmp)) &&
                           zkfp.ZKFP_ERR_OK == (ret = zkfp2.DBAdd(mDBHandle, iFid, RegTmp)))
                    {
                        iFid++;
                        rightstatusBar.Text = "Registro exitoso.";
                        leftstatusBar.Text = "En espera.";
                        String finger_data = zkfp2.BlobToBase64(RegTmp, cbRegTmp);
                        //txtBox.Text = finger_data;
                        //int UserControl = Int32.Parse(ComboBoxUsers.SelectedItem.ToString());
                        int user = Int32.Parse(ComboBoxUsers.SelectedValue.ToString());
                        _ = PostToken(user, finger_data);
                    }
                    else
                    {
                        rightstatusBar.Text = "Registro fallido. Codigo de error:" + ret;
                        leftstatusBar.Text = "En espera.";
                    }
                    IsRegister = false;
                    return;
                }
                else
                {
                    leftstatusBar.Text = "Coloque el dedo en el sensor " + (REGISTER_FINGER_COUNT - RegisterCount) + " veces";
                }
            }
        }

        private void Set_available()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render,
            new Action(() =>
            {
                BnInitMethod();
            }));
        }



        private async Task BnInitMethod()
        {
            int ret = zkfperrdef.ZKFP_ERR_OK;
            if ((ret = zkfp2.Init()) == zkfperrdef.ZKFP_ERR_OK)
            {
                int nCount = zkfp2.GetDeviceCount();
                if (nCount > 0)
                {
                    for (int i = 0; i < nCount; i++)
                    {
                    }
                }
                else
                {
                    zkfp2.Terminate();

                }
            }
            else
            {
                //MessageBox.Show("Initialize fail, ret=" + ret + " !");
                //this.statusStrip1.Text = ("Initialize fail, ret=" + ret + " !");
            }
            if (IntPtr.Zero == (mDBHandle = zkfp2.DBInit()))
            {
                MessageBox.Show("Init DB fail");
                zkfp2.CloseDevice(mDevHandle);
                mDevHandle = IntPtr.Zero;
                return;
            }

            if (IntPtr.Zero == (mDevHandle = zkfp2.OpenDevice(0)))
            {
                //MessageBox.Show("OpenDevice fail");
                return;
            }
            RegisterCount = 0;
            cbRegTmp = 1;
            iFid = 1;
            for (int i = 0; i < 3; i++)
            {
                RegTmps[i] = new byte[2048];
            }
            byte[] paramValue = new byte[4];
            int size = 4;
            zkfp2.GetParameters(mDevHandle, 1, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpWidth);

            size = 4;
            zkfp2.GetParameters(mDevHandle, 2, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpHeight);

            FPBuffer = new byte[mfpWidth * mfpHeight];

            size = 4;
            zkfp2.GetParameters(mDevHandle, 3, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpDpi);


            bIsTimeToDie = false;
            leftstatusBar.Text = ("Lector de huella listo");
            zkfp2.SetParameters(mDevHandle, 101, paramValue1, 4);
            //users_to_db();
        }



        public class Profile
        {
            public int pk { get; set; }
            public string full_name { get; set; }
        }

        public class Hash
        {
            public int user { get; set; }
            public string hash { get; set; }
        }

        private void TokenFingerprint(object sender, RoutedEventArgs e)
        {
            txtBox.Text = "";
            leftstatusBar.Text = "Coloque el dedo en el sensor 3 veces.";
            if (!IsRegister)
            {
                IsRegister = true;
                RegisterCount = 0;
                cbRegTmp = 0;
            }
        }
    }
}
