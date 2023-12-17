using System;
using Windows.UI.Core;
using Windows.Devices.Gpio;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Shapes;
using Windows.Networking;
using Windows.Networking.Connectivity;
using System.Threading;
using System.Data;
using System.Data.Sql;
using System.Data.SqlClient;
using System.Reflection;
using System.Timers;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Text;
using Windows.UI;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409
//https://stackoverflow.com/questions/40126286/windows-store-app-test-certificate-expired

namespace RangeTrainerTutor
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        private const int BACKWARDS_PIN = 19;
        private const int FORWARDS_PIN = 20;
        private const int OUTWARDS_PIN = 21;
        private const int FEET_PER_SEC = 6;
        private const int FEETPERSECONDS = 700;
        private GpioPin Forwards_pin;
        private GpioPin Backwards_pin;
        private GpioPin Outwards_pin;
        private GpioPinValue Outwards_pinvalue;

        private string BAYNUMBER;
        private string LANENUMBER;
        private string BAYID;
        private string LANEID;
        private bool DATABASESSERVEREXISTS = false;

        //private const string SERVER = "192.168.251.63";
        private const string SERVER = "sql.onling.com";
        private const string DATABASE = "SHOP";
        private const string USERID = "lanes";
        private const string PASSWORD = "!lanes123!";

        // Setup a timer
        System.Timers.Timer myTimer = new System.Timers.Timer();
        //private string connectionString = "Data Source=sql04.onling.com;Initial Catalog=SHOP;Database=SHOP;User Id=sa;Password=FFLFordF750!;Integrated Security=SSPI";

        #region Main Load
        public MainPage()
        {
            this.InitializeComponent();

            var hostNames = NetworkInformation.GetHostNames();
            //var hostName = hostNames.FirstOrDefault(name => name.Type == HostNameType.DomainName)?.DisplayName ?? "???";

            //Get teh clock going
            DispatcherTimer Timer = new DispatcherTimer();

            Timer.Tick += Timer_Tick;
            Timer.Interval = new TimeSpan(0, 0, 1);
            Timer.Start();

            //Add Handler not sure why I did
            //Forwardbutton.Click += Forwardbutton_Click;
            //Forwardbutton.PointerReleased += Forwardbutton_PointerReleased;

            //Reversebutton.Click += Reversebutton_Click;
            //Reversebutton.PointerReleased += Reversebutton_PointerReleased;

            StatustextBox.Text = "Let the Lead Therapy begin!";

            var HostDeviceName = hostNames.FirstOrDefault(name => name.Type == HostNameType.DomainName)?.DisplayName ?? "???";

            //Get Bay and Lane from hostname entry
            string[] separators = { "bay", "BAY", "lane", "LANE" };
            string[] words = HostDeviceName.Split(separators, StringSplitOptions.RemoveEmptyEntries);

            BAYNUMBER = words[0];
            LANENUMBER = words[1];

            //Set the Host Name on Screen
            LanetextBox.Text = "Bay " + BAYNUMBER + " Lane " + LANENUMBER;

            // Default opening application
            Forwardbutton.IsEnabled = false;
            Goyardsbutton.IsEnabled = false;

            //Check for Raspberry Pi3
            InitGPIO();

            //var result = ExecuteCommandLineString("ping -t " +SERVER);

            //Check if the server is set
            if ((SERVER == "")) {
                DATABASESSERVEREXISTS = false;
                //HelpButton.IsEnabled = false;
                TimeLeftBox.Text = "OFF";
            }
            else {
                DATABASESSERVEREXISTS = true;
                //HelpButton.IsEnabled = true;
            }
            //var test = ExecuteCommandLineString("/c ping.exe " + SERVER);

            //Check to make sure DB Server is there
            if (DATABASESSERVEREXISTS == true )
            {
                //Check Database
                if (CheckDatabaseExist())
                {
                    StatustextBox.Background = new SolidColorBrush( Colors.Green);
                    StatustextBox.Foreground = new SolidColorBrush( Colors.White);

                    // Timer Setup for Lane In Use every 30 seconds
                    myTimer.Elapsed += new ElapsedEventHandler(BaylaneTimer_Tick);
                    myTimer.Interval = 30000;
                    myTimer.Enabled = true;

                    //Get Bay and Lane Info and Get if lane is in use
                    BAY_ID();
                    LANE_ID();

                }

            }
            else
            {
                StatustextBox.Text = "MyFFLBook";
            }

        }
            #endregion

        #region Initialize GPIO
        private void InitGPIO()
        {
            var gpio = GpioController.GetDefault();
            // Show an error if there is no GPIO controller
            if (gpio == null)
            {
                StatustextBox.Text = "There is no GPIO controller on this device.";
                return;
            }

            Backwards_pin = gpio.OpenPin(BACKWARDS_PIN);
            Backwards_pin.Write(GpioPinValue.High);
            Backwards_pin.SetDriveMode(GpioPinDriveMode.Output);

            Forwards_pin = gpio.OpenPin(FORWARDS_PIN);
            Forwards_pin.Write(GpioPinValue.High);
            Forwards_pin.SetDriveMode(GpioPinDriveMode.Output);

            Outwards_pin = gpio.OpenPin(OUTWARDS_PIN);
            Outwards_pin.Write(GpioPinValue.High);
            Outwards_pin.SetDriveMode(GpioPinDriveMode.Output);

        }
        #endregion

        #region Command Line
        //*************************************************
        //*************************************************
        //********** EXECUTE COMMAND LINE STRING **********
        //*************************************************
        //*************************************************
        //
        //net localgroup Administrators DefaultAccount /add
        //Run first
        //http://www.iot-developer.net/windows-iot/uwp-programming-in-c/command-line-uwp-programming-in-c/executing-command-line-commands
        // Enable
        //reg ADD "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\EmbeddedMode\ProcessLauncher" /v AllowedExecutableFilesList /t REG_MULTI_SZ /d "c:\windows\system32\cmd.exe\0"
        // Disable
        //reg QUERY "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\EmbeddedMode\ProcessLauncher" /v AllowedExecutableFilesList
        //http://www.iot-developer.net/windows-iot/command-line/enabling-command-line

        private async Task<string> ExecuteCommandLineString(string CommandString)
    {
        const string CommandLineProcesserExe = "c:\\windows\\system32\\cmd.exe";
        const uint CommandStringResponseBufferSize = 8192;
        string currentDirectory = "C:\\";

        StringBuilder textOutput = new StringBuilder((int)CommandStringResponseBufferSize);
        uint bytesLoaded = 0;

        if (string.IsNullOrWhiteSpace(CommandString))
            return ("");

        var commandLineText = CommandString.Trim();

        var standardOutput = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var standardError = new Windows.Storage.Streams.InMemoryRandomAccessStream();
        var options = new Windows.System.ProcessLauncherOptions
        {
            StandardOutput = standardOutput,
            StandardError = standardError
        };

        try
        {
            var args = "/C \"cd \"" + currentDirectory + "\" & " + commandLineText + "\"";
            var result = await Windows.System.ProcessLauncher.RunToCompletionAsync(CommandLineProcesserExe, args, options);

            //First write std out
            using (var outStreamRedirect = standardOutput.GetInputStreamAt(0))
            {
                using (var dataReader = new Windows.Storage.Streams.DataReader(outStreamRedirect))
                {
                    while ((bytesLoaded = await dataReader.LoadAsync(CommandStringResponseBufferSize)) > 0)
                        textOutput.Append(dataReader.ReadString(bytesLoaded));

                    new System.Threading.ManualResetEvent(false).WaitOne(10);
                    if ((bytesLoaded = await dataReader.LoadAsync(CommandStringResponseBufferSize)) > 0)
                        textOutput.Append(dataReader.ReadString(bytesLoaded));
                }
            }

            //Then write std err
            using (var errStreamRedirect = standardError.GetInputStreamAt(0))
            {
                using (var dataReader = new Windows.Storage.Streams.DataReader(errStreamRedirect))
                {
                    while ((bytesLoaded = await dataReader.LoadAsync(CommandStringResponseBufferSize)) > 0)
                        textOutput.Append(dataReader.ReadString(bytesLoaded));

                    new System.Threading.ManualResetEvent(false).WaitOne(10);
                    if ((bytesLoaded = await dataReader.LoadAsync(CommandStringResponseBufferSize)) > 0)
                        textOutput.Append(dataReader.ReadString(bytesLoaded));
                }
            }

            return (textOutput.ToString());
        }
        catch (UnauthorizedAccessException uex)
        {
            return ("ERROR - " + uex.Message + "\n\nCmdNotEnabled");
        }
        catch (Exception ex)
        {
            return ("ERROR - " + ex.Message + "\n");
        }
    }
    #endregion

        #region Ping CommandLine

    #endregion

        #region PingServer
    public static bool PingHost(string nameOrAddress)
    {
        bool pingable = false;
        Ping pinger = new Ping();
        try
        {
            PingReply reply = pinger.Send("192.168.251.63",10);
            pingable = reply.Status == IPStatus.Success;
        }
        catch (PingException ex)
        {
            // Discard PingExceptions and return false;
            return pingable ;
        }
        return pingable;
    }
#endregion

        #region CheckDatabase
    //public static bool CheckDatabaseExists(string databaseName)
    //{
    //    using (var connection = new SqlConnection(connectionString))
    //    {
    //        using (var command = new SqlCommand($"SELECT db_id('{databaseName}')", connection))
    //        {
    //            connection.Open();
    //            return (command.ExecuteScalar() != DBNull.Value);
    //        }
    //    }
    //}

    public static bool CheckDatabaseExist()
        {
            try
            {
                string connString = "Data Source=" + SERVER + ";Initial Catalog=" + DATABASE + ";Database=" + DATABASE + ";User Id=" + USERID + ";Password=" + PASSWORD + ";Connect Timeout=15;";
                string cmdText = "select * from master.dbo.sysdatabases where name=\'" + DATABASE + "\'";
                bool bRet = false;
                using (SqlConnection sqlConnection = new SqlConnection(connString))
                {
                    sqlConnection.Open();
                    using (SqlCommand sqlCmd = new SqlCommand(cmdText, sqlConnection))
                    {
                        SqlDataReader reader = sqlCmd.ExecuteReader();
                        bRet = reader.HasRows;
                        reader.Close();
                    }
                    sqlConnection.Close();
                    sqlConnection.Dispose();
                }
                return bRet;
            }
            catch (Exception)
            {

                throw;
            }
        }

        //public int CheckDatabase()
        //{
        //    int count = 0;
        //    SqlConnection conn = new SqlConnection(connectionString);
        //    String sqlQuery = "Select Count(id) from sndbay;";
        //    SqlCommand cmd = new SqlCommand(sqlQuery, conn);
        //    try
        //    {
        //        conn.Open();
        //        //Since return type is System.Object, a typecast is must
        //        count = (Int32)cmd.ExecuteScalar();
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine(ex.Message);
        //    }
        //    finally
        //    {
        //        conn.Close();
        //    }
        //    return count;
        //}
        #endregion

        #region Get BAYID
        private void BAY_ID()
        {
            string connString = "Data Source=" + SERVER + ";Initial Catalog=" + DATABASE + ";Database=" + DATABASE + ";User Id=" + USERID + ";Password=" + PASSWORD + ";Connect Timeout=15;";
            string cmdText = "Select isnull(id,0) from sndbay where bay=" + BAYNUMBER + ";";
            using (SqlConnection sqlConnection = new SqlConnection(connString))
            {
                sqlConnection.Open();
                using (SqlCommand sqlCmd = new SqlCommand(cmdText, sqlConnection))
                {
                    BAYID = sqlCmd.ExecuteScalar().ToString();
                }
                sqlConnection.Close();
                sqlConnection.Dispose();
            }
        }
        #endregion

        #region Get LANEID
        private void LANE_ID()
        {
            string connString = "Data Source=" + SERVER + ";Initial Catalog=" + DATABASE + ";Database=" + DATABASE + ";User Id=" + USERID + ";Password=" + PASSWORD + ";Connect Timeout=15;";
            string cmdText = "Select isnull(id,0) from sndlane where lanenumber=" + LANENUMBER + " and bayid=" + BAYID + ";";
            using (SqlConnection sqlConnection = new SqlConnection(connString))
            {
                sqlConnection.Open();
                using (SqlCommand sqlCmd = new SqlCommand(cmdText, sqlConnection))
                {
                    LANEID = sqlCmd.ExecuteScalar().ToString();
                }
                sqlConnection.Close();
                sqlConnection.Dispose();
            }
        }
        #endregion

        #region Is Lane In Use
        public static string IsLaneInUse(string laneid)
        {
            string connString = "Data Source=" + SERVER + ";Initial Catalog=" + DATABASE + ";Database=" + DATABASE + ";User Id=" + USERID + ";Password=" + PASSWORD + ";Connect Timeout=15;";
            string cmdText = "Select count(id) from sndlaneusage where laneid=" + laneid + " and InUse=1;";
            string LANEOPEN = "";
            using (SqlConnection sqlConnection = new SqlConnection(connString))
            {
                sqlConnection.Open();
                using (SqlCommand sqlCmd = new SqlCommand(cmdText, sqlConnection))
                {
                    LANEOPEN = sqlCmd.ExecuteScalar().ToString();
                }
                sqlConnection.Close();
                sqlConnection.Dispose();
            }
            return LANEOPEN;
        }
        #endregion

        #region Time Left On Lane
        public static string TimeLeftOnLane(string laneid)
        {
            string connString = "Data Source=" + SERVER + ";Initial Catalog=" + DATABASE + ";Database=" + DATABASE + ";User Id=" + USERID + ";Password=" + PASSWORD + ";Connect Timeout=15;";
            string cmdText = "if exists (select convert(varchar(30),datediff(n,getdate(),ExpectedEndDateTime)) from sndlaneusage where " +
                @"LaneId=" + laneid + " and InUse=1) select convert(varchar(30),datediff(n,getdate(),ExpectedEndDateTime)) from sndlaneusage where " +
                @"LaneId=" + laneid + " and InUse=1 else select 'OFF';";
            string TIMELEFT = "";
            using (SqlConnection sqlConnection = new SqlConnection(connString))
            {
                sqlConnection.Open();
                using (SqlCommand sqlCmd = new SqlCommand(cmdText, sqlConnection))
                {
                    TIMELEFT = sqlCmd.ExecuteScalar().ToString();
                }
                sqlConnection.Close();
                sqlConnection.Dispose();
            }

            return TIMELEFT;
        }
        #endregion

        #region Timer
        private async void BaylaneTimer_Tick(object sender, EventArgs e)
        {
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {

                if (Convert.ToInt32(IsLaneInUse(LANEID)) > 0 && Convert.ToInt32(TimeLeftOnLane(LANEID)) >= 0)
                {
                    //turn on forward
                    //Outwards_pin.Write(GpioPinValue.Low);
                    //Outwards_pinvalue = GpioPinValue.Low;
                    Forwardbutton.IsEnabled = true;
                    Goyardsbutton.IsEnabled = true;
                    //Tutor1.IsEnabled = true;
                    //Tutor2.IsEnabled = true;
                    //Tutor3.IsEnabled = true;
                    //Tutor4.IsEnabled = true;
                    //Tutor5.IsEnabled = true;
                    //Tutor6.IsEnabled = true;
                }
                else
                {
                    //turn off forward
                    //Outwards_pin.Write(GpioPinValue.High);
                    //Outwards_pinvalue = GpioPinValue.High;
                    Forwardbutton.IsEnabled = false;
                    Goyardsbutton.IsEnabled = false;
                    //Tutor1.IsEnabled = false;
                    //Tutor2.IsEnabled = false;
                    //Tutor3.IsEnabled = false;
                    //Tutor4.IsEnabled = false;
                    //Tutor5.IsEnabled = false;
                    //Tutor6.IsEnabled = false;
                }
            });
            // Check and update value for time left and color
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                TimeLeftBox.Text = TimeLeftOnLane(LANEID);
            });
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (TimeLeftBox.Text == "OFF")
                {
                    TimeLeftBox.Text = "OFF";
                    TimeLeftBox.Foreground = new SolidColorBrush( Colors.Black);
                    TimeLeftBox.Background = new SolidColorBrush( Colors.White);
                }
                else if (Convert.ToInt32(TimeLeftBox.Text) > 5)
                {
                    TimeLeftBox.Foreground = new SolidColorBrush( Colors.White);
                    TimeLeftBox.Background = new SolidColorBrush( Colors.Green);
                }
                else if (Convert.ToInt32(TimeLeftBox.Text) >= 3 && Convert.ToInt32(TimeLeftBox.Text) <= 5)
                {
                    TimeLeftBox.Foreground = new SolidColorBrush( Colors.Black);
                    TimeLeftBox.Background = new SolidColorBrush( Colors.Yellow);
                }
                else if (Convert.ToInt32(TimeLeftBox.Text) >= 0 && Convert.ToInt32(TimeLeftBox.Text) <= 3)
                {
                    TimeLeftBox.Foreground = new SolidColorBrush( Colors.White);
                    TimeLeftBox.Background = new SolidColorBrush( Colors.HotPink);
                }
                else if (Convert.ToInt32(TimeLeftBox.Text) <= 0)
                {
                    TimeLeftBox.Foreground = new SolidColorBrush( Colors.White);
                    TimeLeftBox.Background = new SolidColorBrush( Colors.Purple);
                }
            });
        }
        #endregion

        #region Reverse Button
        private void Reversebutton_PointerEntered(object sender, PointerRoutedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StatustextBox.Text = "Pressing Reverse...";
                Reversebutton.Background = new SolidColorBrush(Colors.Red);
                Forwardbutton.IsEnabled = false;
                Goyardsbutton.IsEnabled = false;
                //Tutor1.IsEnabled = false;
                //Tutor2.IsEnabled = false;
                //Tutor3.IsEnabled = false;
                //Tutor4.IsEnabled = false;
                //Tutor5.IsEnabled = false;
                //Tutor6.IsEnabled = false;
                Backwards_pin.Write(GpioPinValue.Low);
            });
        }

        private void Reversebutton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StatustextBox.Text = "Released Reverse...";
                Reversebutton.Background = new SolidColorBrush(Colors.Green);
                if (TimeLeftBox.Text == "OFF" || TimeLeftBox.Text.Contains("0") || TimeLeftBox.Text.Contains("-"))
                {
                    Forwardbutton.IsEnabled = false;
                    Goyardsbutton.IsEnabled = false;
                }
                else
                {
                    Forwardbutton.IsEnabled = true;
                    Goyardsbutton.IsEnabled = true;
                };
                //Tutor1.IsEnabled = true;
                //Tutor2.IsEnabled = true;
                //Tutor3.IsEnabled = true;
                //Tutor4.IsEnabled = true;
                //Tutor5.IsEnabled = true;
                //Tutor6.IsEnabled = true;
                Backwards_pin.Write(GpioPinValue.High);
            });
        }

        #endregion

        #region Forward Button
        private void Forwardbutton_PointerEntered(object sender, PointerRoutedEventArgs e)
            {
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    StatustextBox.Text = "Pressing Forward...";
                    Forwardbutton.Background = new SolidColorBrush( Colors.Red);
                    Reversebutton.IsEnabled = false;
                    Goyardsbutton.IsEnabled = false;
                    //Tutor1.IsEnabled = false;
                    //Tutor2.IsEnabled = false;
                    //Tutor3.IsEnabled = false;
                    //Tutor4.IsEnabled = false;
                    //Tutor5.IsEnabled = false;
                    //Tutor6.IsEnabled = false;
                    Forwards_pin.Write(GpioPinValue.Low);
                });
            }

        private async void Forwardbutton_PointerExited(object sender, PointerRoutedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StatustextBox.Text = "Released Forward...";
                Forwardbutton.Background = new SolidColorBrush(Colors.Green);
                Reversebutton.IsEnabled = true;
                Goyardsbutton.IsEnabled = true;
                //Tutor1.IsEnabled = true;
                //Tutor2.IsEnabled = true;
                //Tutor3.IsEnabled = true;
                //Tutor4.IsEnabled = true;
                //Tutor5.IsEnabled = true;
                //Tutor6.IsEnabled = true;
                Forwards_pin.Write(GpioPinValue.High);
            });
        }
        #endregion

        #region Upward Arrow Auto
        private void Upyardsbutton_Click(object sender, RoutedEventArgs e)
        {
            if (Convert.ToInt32(Numbertextbox.Text) <= Convert.ToInt32(20))
            {
                    var value = int.Parse(Numbertextbox.Text);
                    Numbertextbox.Text = (value + 1).ToString();
                    StatustextBox.Text = "Moved yard Up...";
            }
        }
        #endregion

        #region Inward Arrow Auto
        private void Downyardsbutton_Click(object sender, RoutedEventArgs e)
        {
            if (Convert.ToInt32(Numbertextbox.Text) >= Convert.ToInt32(1))
            {
                var value = int.Parse(Numbertextbox.Text);
                Numbertextbox.Text = (value - 1).ToString();
                StatustextBox.Text = "Moved yard Down...";
            }
        }
    #endregion

    #region Go Yards
        private async void Goyardsbutton_Click(object sender, RoutedEventArgs e)
        {
        ContentDialog CarrierAtZero = new ContentDialog
        {
            Title = "This will move the target "+ Numbertextbox.Text +" yards forward?",
            Content = "Are you sure that is the number of yards you desire...",
            CloseButtonText = "No",
            PrimaryButtonText = "Yes",
            DefaultButton = ContentDialogButton.Close
        };

        ContentDialogResult result = await CarrierAtZero.ShowAsync();

        if (result == ContentDialogResult.Primary)
        {
            Goyardsbutton.Background = new SolidColorBrush( Colors.Red);

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                Reversebutton.IsEnabled = false;
                Forwardbutton.IsEnabled = false;
                //Tutor1.IsEnabled = false;
                //Tutor2.IsEnabled = false;
                //Tutor3.IsEnabled = false;
                //Tutor4.IsEnabled = false;
                //Tutor5.IsEnabled = false;
                //Tutor6.IsEnabled = false;
                //Turn on Toggle
                Forwards_pin.Write(GpioPinValue.Low);
                    
                // 700 is 2/3 sec to a yard for 6feet per seconds
                Task.Delay(FEETPERSECONDS * Convert.ToInt32(Numbertextbox.Text)).Wait();
                Forwards_pin.Write(GpioPinValue.High);

                Numbertextbox.Text = "0";
                Reversebutton.IsEnabled = true;
                Forwardbutton.IsEnabled = true;
                //Tutor1.IsEnabled = true;
                //Tutor2.IsEnabled = true;
                //Tutor3.IsEnabled = true;
                //Tutor4.IsEnabled = true;
                //Tutor5.IsEnabled = true;
                //Tutor6.IsEnabled = true;
            });

            Goyardsbutton.Background = new SolidColorBrush(GetColorFromHex("#FFF4A460"));
            StatustextBox.Text = "Auto Sendout Mode Completed...";
        }
        else
        {
            StatustextBox.Text = "Auto Sendout Mode could not be used...";
        }
    }
            
    #endregion

        #region Tutor 1
        private void Tutor1_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //Tutor1.Background = new SolidColorBrush( Colors.Red);
                StatustextBox.Text = "Pressed Turtor 1...";
            });
        }

        private async void Tutor1_Click(object sender, RoutedEventArgs e)
        {
            //Tutor1.Background = new SolidColorBrush(Colors.Red);
            Reversebutton.IsEnabled = false;
            Forwardbutton.IsEnabled = false;
            Goyardsbutton.IsEnabled = false;
            //Tutor1.IsEnabled = false;
            //Tutor2.IsEnabled = false;
            //Tutor3.IsEnabled = false;
            //Tutor4.IsEnabled = false;
            //Tutor5.IsEnabled = false;
            //Tutor6.IsEnabled = false;

            ContentDialog CarrierAtZero = new ContentDialog
            {
                Title = "Is the Carrier over the Shelf?",
                Content = "The Carrier has to be all the way back for this option to work. If not then you must bring all the way back first...",
                CloseButtonText = "No",
                PrimaryButtonText = "Yes",
                DefaultButton = ContentDialogButton.Close
            };

            ContentDialogResult result = await CarrierAtZero.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                                
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    
                    //Turn on Toggle
                    Forwards_pin.Write(GpioPinValue.Low);
                    Task.Delay(FEETPERSECONDS * 6).Wait();
                    StatustextBox.Text = "Moved Target Forward "+ FEETPERSECONDS * 6 + "...";

                    //Turn off Toggle
                    Forwards_pin.Write(GpioPinValue.High);
                    Task.Delay(FEETPERSECONDS * 6).Wait();
                    StatustextBox.Text = "Waiting...";

                    //Turn on Toggle
                    Backwards_pin.Write(GpioPinValue.Low);
                        Task.Delay(FEETPERSECONDS * 4).Wait();
                    StatustextBox.Text = "Moved Target Backwards " + FEETPERSECONDS * 4 + "...";

                    //Trun off Toggle
                    Backwards_pin.Write(GpioPinValue.High);

                });

            StatustextBox.Text = "Tutor 1 Completed...";
            }
            else
            {
                StatustextBox.Text = "Tutor 1 could not be used...";
            }
            //Tutor1.Background = new SolidColorBrush(Colors.Orange);
            Reversebutton.IsEnabled = true;
            Forwardbutton.IsEnabled = true;
            Goyardsbutton.IsEnabled = true;
            //Tutor1.IsEnabled = true;
            //Tutor2.IsEnabled = true;
            //Tutor3.IsEnabled = true;
            //Tutor4.IsEnabled = true;
            //Tutor5.IsEnabled = true;
            //Tutor6.IsEnabled = true;
        }
    #endregion

        #region Tutor 2
        private async void Tutor2_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //Tutor2.Background = new SolidColorBrush(Colors.Red);
                StatustextBox.Text = "Pressed Turtor 2...";
            });
        }

        private async void Tutor2_Click(object sender, RoutedEventArgs e)
        {
            //Tutor2.Background = new SolidColorBrush(Colors.Red);
            Reversebutton.IsEnabled = false;
            Forwardbutton.IsEnabled = false;
            Goyardsbutton.IsEnabled = false;
            //Tutor1.IsEnabled = false;
            //Tutor2.IsEnabled = false;
            //Tutor3.IsEnabled = false;
            //Tutor4.IsEnabled = false;
            //Tutor5.IsEnabled = false;
            //Tutor6.IsEnabled = false;

            ContentDialog CarrierAtZero = new ContentDialog
            {
                Title = "Is the Carrier over the Shelf?",
                Content = "The Carrier has to be all the way back for this option to work. If not then you must bring all the way back first...",
                CloseButtonText = "No",
                PrimaryButtonText = "Yes",
                DefaultButton = ContentDialogButton.Close
            };

            ContentDialogResult result = await CarrierAtZero.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
               
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    //Turn on Toggle
                    Forwards_pin.Write(GpioPinValue.Low);
                    Task.Delay(FEETPERSECONDS * 9).Wait();
                    StatustextBox.Text = "Moved Target Forward " + FEETPERSECONDS * 6 + "...";

                    //Turn off Toggle
                    Forwards_pin.Write(GpioPinValue.High);
                    Task.Delay(FEETPERSECONDS * 3).Wait();
                    StatustextBox.Text = "Waiting...";

                    //Turn on Toggle
                    Backwards_pin.Write(GpioPinValue.Low);
                    Task.Delay(FEETPERSECONDS * 8).Wait();
                    StatustextBox.Text = "Moved Target Backwards " + FEETPERSECONDS * 4 + "...";

                    //Trun off Toggle
                    Backwards_pin.Write(GpioPinValue.High);

                });

                StatustextBox.Text = "Tutor 2 Completed...";
            }
            else
            {
                StatustextBox.Text = "Tutor 2 could not be used...";
            }
            //Tutor2.Background = new SolidColorBrush(Colors.Orange);
            Reversebutton.IsEnabled = true;
            Forwardbutton.IsEnabled = true;
            Goyardsbutton.IsEnabled = true;
            //Tutor1.IsEnabled = true;
            //Tutor2.IsEnabled = true;
            //Tutor3.IsEnabled = true;
            //Tutor4.IsEnabled = true;
            //Tutor5.IsEnabled = true;
            //Tutor6.IsEnabled = true;
        }
        #endregion

        #region Tutor 3
        private async void Tutor3_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //Tutor3.Background = new SolidColorBrush(Colors.Red);
                StatustextBox.Text = "Pressed Turtor 3...";
            });
        }

        private async void Tutor3_Click(object sender, RoutedEventArgs e)
        {
            //Tutor3.Background = new SolidColorBrush(Colors.Red);
            Reversebutton.IsEnabled = false;
            Forwardbutton.IsEnabled = false;
            Goyardsbutton.IsEnabled = false;
            //Tutor1.IsEnabled = false;
            //Tutor2.IsEnabled = false;
            //Tutor3.IsEnabled = false;
            //Tutor4.IsEnabled = false;
            //Tutor5.IsEnabled = false;
            //Tutor6.IsEnabled = false;

            ContentDialog CarrierAtZero = new ContentDialog
            {
                Title = "Is the Carrier over the Shelf?",
                Content = "The Carrier has to be all the way back for this option to work. If not then you must bring all the way back first...",
                CloseButtonText = "No",
                PrimaryButtonText = "Yes",
                DefaultButton = ContentDialogButton.Close
            };

            ContentDialogResult result = await CarrierAtZero.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                
                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    //Turn on Toggle
                    Forwards_pin.Write(GpioPinValue.Low);
                    Task.Delay(FEETPERSECONDS * 6).Wait();
                    StatustextBox.Text = "Moved Target Forward " + FEETPERSECONDS * 6 + "...";

                    //Turn off Toggle
                    Forwards_pin.Write(GpioPinValue.High);
                    Task.Delay(FEETPERSECONDS * 6).Wait();
                    StatustextBox.Text = "Waiting...";

                    //Turn on Toggle
                    Backwards_pin.Write(GpioPinValue.Low);
                    Task.Delay(FEETPERSECONDS * 4).Wait();
                    StatustextBox.Text = "Moved Target Backwards " + FEETPERSECONDS * 4 + "...";

                    //Trun off Toggle
                    Backwards_pin.Write(GpioPinValue.High);

                });
                
                StatustextBox.Text = "Tutor 3 Completed...";
            }
            else
            {
                StatustextBox.Text = "Tutor 3 could not be used...";
            }
            //Forwardbutton.IsEnabled = true;
            Goyardsbutton.IsEnabled = true;
            //Tutor1.IsEnabled = true;
            //Tutor2.IsEnabled = true;
            //Tutor3.IsEnabled = true;
            //Tutor4.IsEnabled = true;
            //Tutor5.IsEnabled = true;
            //Tutor6.IsEnabled = true;
        }
        #endregion

        #region Tutor 4
        private async void Tutor4_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //Tutor4.Background = new SolidColorBrush(Colors.Red);
                StatustextBox.Text = "Pressed Turtor 4...";
            });
        }

        private async void Tutor4_Click(object sender, RoutedEventArgs e)
        {
            //Tutor4.Background = new SolidColorBrush(Colors.Red);
            Reversebutton.IsEnabled = false;
            Forwardbutton.IsEnabled = false;
            Goyardsbutton.IsEnabled = false;
            //Tutor1.IsEnabled = false;
            //Tutor2.IsEnabled = false;
            //Tutor3.IsEnabled = false;
            //Tutor4.IsEnabled = false;
            //Tutor5.IsEnabled = false;
            //Tutor6.IsEnabled = false;

            ContentDialog CarrierAtZero = new ContentDialog
            {
                Title = "Is the Carrier over the Shelf?",
                Content = "The Carrier has to be all the way back for this option to work. If not then you must bring all the way back first...",
                CloseButtonText = "No",
                PrimaryButtonText = "Yes",
                DefaultButton = ContentDialogButton.Close
            };

            ContentDialogResult result = await CarrierAtZero.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    //Turn on Toggle
                    Forwards_pin.Write(GpioPinValue.Low);
                    Task.Delay(FEETPERSECONDS * 12).Wait();
                    StatustextBox.Text = "Moved Target Forward " + FEETPERSECONDS * 6 + "...";

                    //Turn off Toggle
                    Forwards_pin.Write(GpioPinValue.High);
                    Task.Delay(FEETPERSECONDS * 9).Wait();
                    StatustextBox.Text = "Waiting...";

                    //Turn on Toggle
                    Backwards_pin.Write(GpioPinValue.Low);
                    Task.Delay(FEETPERSECONDS * 8).Wait();
                    StatustextBox.Text = "Moved Target Backwards " + FEETPERSECONDS * 4 + "...";

                    //Trun off Toggle
                    Backwards_pin.Write(GpioPinValue.High);

                });

                StatustextBox.Text = "Tutor 4 Completed...";
            }
            else
            {
                StatustextBox.Text = "Tutor 4 could not be used...";
            }
            //Tutor4.Background = new SolidColorBrush(Colors.Orange);
            Reversebutton.IsEnabled = true;
            Forwardbutton.IsEnabled = true;
            Goyardsbutton.IsEnabled = true;
            //Tutor1.IsEnabled = true;
            //Tutor2.IsEnabled = true;
            //Tutor3.IsEnabled = true;
            //Tutor4.IsEnabled = true;
            //Tutor5.IsEnabled = true;
            //Tutor6.IsEnabled = true;
        }
        #endregion

        #region Tutor 5
        private async void Tutor5_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //Tutor5.Background = new SolidColorBrush(Colors.Red);
                StatustextBox.Text = "Pressed Turtor 5...";
            });
        }

        private async void Tutor5_Click(object sender, RoutedEventArgs e)
        {
            //Tutor5.Background = new SolidColorBrush(Colors.Red);
            Reversebutton.IsEnabled = false;
            Forwardbutton.IsEnabled = false;
            Goyardsbutton.IsEnabled = false;
            //Tutor1.IsEnabled = false;
            //Tutor2.IsEnabled = false;
            //Tutor3.IsEnabled = false;
            //Tutor4.IsEnabled = false;
            //Tutor5.IsEnabled = false;
            //Tutor6.IsEnabled = false;

            ContentDialog CarrierAtZero = new ContentDialog
            {
                Title = "Is the Carrier over the Shelf?",
                Content = "The Carrier has to be all the way back for this option to work. If not then you must bring all the way back first...",
                CloseButtonText = "No",
                PrimaryButtonText = "Yes",
                DefaultButton = ContentDialogButton.Close
            };

            ContentDialogResult result = await CarrierAtZero.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    //Turn on Toggle
                    Forwards_pin.Write(GpioPinValue.Low);
                    Task.Delay(FEETPERSECONDS * 14).Wait();
                    StatustextBox.Text = "Moved Target Forward " + FEETPERSECONDS * 6 + "...";

                    //Turn off Toggle
                    Forwards_pin.Write(GpioPinValue.High);
                    Task.Delay(FEETPERSECONDS * 7).Wait();
                    StatustextBox.Text = "Waiting...";

                    //Turn on Toggle
                    Backwards_pin.Write(GpioPinValue.Low);
                    Task.Delay(FEETPERSECONDS * 12).Wait();
                    StatustextBox.Text = "Moved Target Backwards " + FEETPERSECONDS * 4 + "...";

                    //Trun off Toggle
                    Backwards_pin.Write(GpioPinValue.High);

                });

                StatustextBox.Text = "Tutor 5 Completed...";
            }
            else
            {
                StatustextBox.Text = "Tutor 5 could not be used...";
            }
            //Tutor5.Background = new SolidColorBrush(Colors.Orange);
            Reversebutton.IsEnabled = true;
            Forwardbutton.IsEnabled = true;
            Goyardsbutton.IsEnabled = true;
            //Tutor1.IsEnabled = true;
            //Tutor2.IsEnabled = true;
            //Tutor3.IsEnabled = true;
            //Tutor4.IsEnabled = true;
            //Tutor5.IsEnabled = true;
            //Tutor6.IsEnabled = true;
        }
        #endregion

        #region Tutor 6
        private async void Tutor6_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                //Tutor6.Background = new SolidColorBrush(Colors.Red);
                StatustextBox.Text = "Pressed Turtor 6...";
            });
        }

        private async void Tutor6_Click(object sender, RoutedEventArgs e)
        {
            //Tutor6.Background = new SolidColorBrush(Colors.Red);
            Reversebutton.IsEnabled = false;
            Forwardbutton.IsEnabled = false;
            Goyardsbutton.IsEnabled = false;
            //Tutor1.IsEnabled = false;
            //Tutor2.IsEnabled = false;
            //Tutor3.IsEnabled = false;
            //Tutor4.IsEnabled = false;
            //Tutor5.IsEnabled = false;
            //Tutor6.IsEnabled = false;

            ContentDialog CarrierAtZero = new ContentDialog
            {
                Title = "Is the Carrier over the Shelf?",
                Content = "The Carrier has to be all the way back for this option to work. If not then you must bring all the way back first...",
                CloseButtonText = "No",
                PrimaryButtonText = "Yes",
                DefaultButton = ContentDialogButton.Close
            };

            ContentDialogResult result = await CarrierAtZero.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {

                await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    //Turn on Toggle
                    Forwards_pin.Write(GpioPinValue.Low);
                    Task.Delay(FEETPERSECONDS * 13).Wait();
                    StatustextBox.Text = "Moved Target Forward " + FEETPERSECONDS * 6 + "...";

                    //Turn off Toggle
                    Forwards_pin.Write(GpioPinValue.High);
                    Task.Delay(FEETPERSECONDS * 9).Wait();
                    StatustextBox.Text = "Waiting...";

                    //Turn on Toggle
                    Backwards_pin.Write(GpioPinValue.Low);
                    Task.Delay(FEETPERSECONDS * 8).Wait();
                    StatustextBox.Text = "Moved Target Backwards " + FEETPERSECONDS * 4 + "...";

                    //Trun off Toggle
                    Backwards_pin.Write(GpioPinValue.High);

                });

                StatustextBox.Text = "Tutor 6 Completed...";
            }
            else
            {
                StatustextBox.Text = "Tutor 6 could not be used...";
            }
            //Tutor6.Background = new SolidColorBrush(Colors.Orange);
            Reversebutton.IsEnabled = true;
            Forwardbutton.IsEnabled = true;
            Goyardsbutton.IsEnabled = true;
            //Tutor1.IsEnabled = true;
            //Tutor2.IsEnabled = true;
            //Tutor3.IsEnabled = true;
            //Tutor4.IsEnabled = true;
            //Tutor5.IsEnabled = true;
            //Tutor6.IsEnabled = true;
        }
        #endregion

        #region Assistance Button
        private void HelpButton_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StatustextBox.Text = "Released CallForAssistance...";
                //HelpButton.Background = new SolidColorBrush( Colors.Yellow);

                string connString = "Data Source=" + SERVER + ";Initial Catalog=" + DATABASE + ";Database=" + DATABASE + ";User Id=" + USERID + ";Password=" + PASSWORD + ";Connect Timeout=15;";
                string cmdText = "update sndlaneusage set ishelprequired=0 where bayID=" + BAYID + " and laneID=" + LANEID + " and inuse=1;";
                using (SqlConnection sqlConnection = new SqlConnection(connString))
                {
                    sqlConnection.Open();
                    using (SqlCommand sqlCmd = new SqlCommand(cmdText, sqlConnection))
                    {
                        BAYID = sqlCmd.ExecuteScalar().ToString();
                    }
                    sqlConnection.Close();
                    sqlConnection.Dispose();
                }

            });
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e)
        {
            var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                StatustextBox.Text = "Pressing CallForAssistance...";
                //HelpButton.Background = new SolidColorBrush( Colors.Red);

                string connString = "Data Source=" + SERVER + ";Initial Catalog=" + DATABASE + ";Database=" + DATABASE + ";User Id=" + USERID  + ";Password=" + PASSWORD + ";Connect Timeout=15;";
                string cmdText = "update sndlaneusage set ishelprequired=1 where bayID=" + BAYID + " and laneID="+ LANEID +" and inuse=1;";
                using (SqlConnection sqlConnection = new SqlConnection(connString))
                {
                    sqlConnection.Open();
                    using (SqlCommand sqlCmd = new SqlCommand(cmdText, sqlConnection))
                    {
                        BAYID = sqlCmd.ExecuteScalar().ToString();
                    }
                    sqlConnection.Close();
                    sqlConnection.Dispose();
                }

            });
        }
        #endregion

        #region Converting Hex Color
        public static Color GetColorFromHex(string hexString)
        {
            //add default transparency to ignore exception
            if (!string.IsNullOrEmpty(hexString) && hexString.Length > 6)
            {
                if (hexString.Length == 7)
                {
                    hexString = "FF" + hexString;
                }

                hexString = hexString.Replace("#", string.Empty);
                byte a = (byte)(Convert.ToUInt32(hexString.Substring(0, 2), 16));
                byte r = (byte)(Convert.ToUInt32(hexString.Substring(2, 2), 16));
                byte g = (byte)(Convert.ToUInt32(hexString.Substring(4, 2), 16));
                byte b = (byte)(Convert.ToUInt32(hexString.Substring(6, 2), 16));
                Color color = Color.FromArgb(a, r, g, b);
                return color;
            }

            //return black if hex is null or invalid
            return Color.FromArgb(255, 0, 0, 0);
        }
        #endregion

        #region Clock
        //https://stackoverflow.com/questions/38562704/make-clock-uwp-c
        private void Timer_Tick(object sender, object e)
        {
            Time.Text = DateTime.Now.ToString("h:mm:ss tt");
        }
        #endregion

    }
}
