

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

using shimmer.Services;
using System.Threading.Tasks;

namespace BLE.Client.UWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage
    {
        public MainPage()
        {
            this.InitializeComponent();
            //TestSpeed();
            //DownloadData();
        }

        protected async void DownloadData()
        {
            ForegroundSyncService serv = new ForegroundSyncService("00000000-0000-0000-0000-e7452c6d6f14","JC",shimmer.Models.CommunicationState.CommunicationMode.ForceDataTransferSync);
            bool success = await serv.GetKnownDevice();
            if (success)
            {
                var data = await serv.ExecuteDataRequest();
            }
        }

        protected async void TestSpeed()
        {
            SpeedTestService serv = new SpeedTestService("00000000-0000-0000-0000-e7452c6d6f14");
            await serv.GetKnownDevice();
            if (serv.ConnectedASM != null)
            {
                while (true)
                {
                    System.Console.WriteLine("Memory Lookup Execution");
                    await serv.ExecuteMemoryLookupTableCommand();
                    await Task.Delay(60000);
                }
            }
            else
            {
                System.Console.WriteLine("Connect Fail");
            }
        }
    }
}
