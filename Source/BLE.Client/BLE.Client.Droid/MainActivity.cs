using System;
using Acr.UserDialogs;
using Android.App;
using Android.Content.PM;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using Android.OS;
using MvvmCross.Forms.Platforms.Android.Views;
using shimmer.Services;
using System.Threading.Tasks;
using Android;

namespace BLE.Client.Droid
{
    [Activity(ScreenOrientation = ScreenOrientation.Portrait
        ,ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation,
        LaunchMode = LaunchMode.SingleTask)]
    public class MainActivity 
		: MvxFormsAppCompatActivity
    {
        private readonly string[] Permissions =
        {
            Manifest.Permission.Bluetooth,
            Manifest.Permission.BluetoothAdmin,
            Manifest.Permission.AccessCoarseLocation,
            Manifest.Permission.AccessFineLocation,
            Manifest.Permission.ReadExternalStorage,
            Manifest.Permission.WriteExternalStorage
        };
        protected override void OnCreate(Bundle bundle)
        {
            ToolbarResource = Resource.Layout.toolbar;
            TabLayoutResource = Resource.Layout.tabs;

            base.OnCreate(bundle);
            CheckPermissions();
            //TestSpeed();
            //DownloadData();
        }
        private void CheckPermissions()
        {
            bool minimumPermissionsGranted = true;

            foreach (string permission in Permissions)
            {
                if (CheckSelfPermission(permission) != Permission.Granted)
                {
                    minimumPermissionsGranted = false;
                }
            }

            if (!minimumPermissionsGranted)
            {
                RequestPermissions(Permissions, 0);
            }
        }


        protected async void DownloadData()
        {
            ForegroundSyncService serv = new ForegroundSyncService("00000000-0000-0000-0000-e7452c6d6f14", "JC", shimmer.Models.CommunicationState.CommunicationMode.ForceDataTransferSync);
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