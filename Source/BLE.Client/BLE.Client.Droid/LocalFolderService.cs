﻿
using BLE.Client.Droid;
using shimmer.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

[assembly: Xamarin.Forms.Dependency(typeof(LocalFolderService))]
namespace BLE.Client.Droid
{
    class LocalFolderService : ILocalFolderService
    {
        public string GetBinFileDirectory()
        {
            return Android.OS.Environment.GetExternalStoragePublicDirectory(Android.OS.Environment.DirectoryDocuments).Path;
            //return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }
    }
}
