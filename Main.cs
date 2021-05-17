using libdebug;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using PS4Saves.Form.Dialogs.UnmountAll;

namespace PS4Saves
{
    public partial class Main : System.Windows.Forms.Form
    {
        class MountPointStruct
        {
            public object Directory { get; set; }
            public string Title { get; set; }
            public string MountPoint { get; set; }
        }

        PS4DBG ps4 = new PS4DBG();
        private int _pid;
        private ulong _stub;
        private ulong _libSceUserServiceBase = 0x0;
        private ulong _libSceSaveDataBase = 0x0;
        private ulong _executableBase = 0x0;
        private ulong _libSceLibcInternalBase = 0x0;
        private ulong _getSaveDirectoriesAddr = 0;
        private ulong _getUsersAddr = 0;
        private int _user = 0x0;
        private string _selectedGame = null;
        private readonly Dictionary<string, object> _currentMountPointList;

        bool log = false;
        
        public Main()
        {
            InitializeComponent();
            _currentMountPointList = new Dictionary<string, object>();
            var args = Environment.GetCommandLineArgs();
            if (args.Length == 2 && args[1] == "-log")
            {
                log = true;
            }

            if (File.Exists("ip"))
            {
                ipTextBox.Text = File.ReadAllText("ip");
            }
        }

        private static string FormatSize(double size)
        {
            const long bytesInKilobytes = 1024;
            const long bytesInMegabytes = bytesInKilobytes * 1024;
            const long bytesInGigabytes = bytesInMegabytes * 1024;
            double value;
            string str;
            if (size < bytesInGigabytes)
            {
                value = size / bytesInMegabytes;
                str = "MB";
            }
            else
            {
                value = size / bytesInGigabytes;
                str = "GB";
            }
            
            return $"{value:0.##} {str}";
        }
        private void sizeTrackBar_Scroll(object sender, EventArgs e)
        {
            sizeToolTip.SetToolTip(sizeTrackBar, FormatSize((double)(sizeTrackBar.Value * 32768)));
        }
        private void SetStatus(string msg)
        {
            statusLabel.Text = $"Status: {msg}";
        }
        private void WriteLog(string msg)
        {
            if(log)
            {

                msg = $"|{msg}|";
                var a = msg.Length / 2;
                for (var i = 0; i < 48 - a; i++)
                {
                    msg = msg.Insert(0, " ");
                }

                for (var i = msg.Length; i < 96; i++)
                {
                    msg += " ";
                }

                var dateAndTime = DateTime.Now;
                var logStr = $"|{dateAndTime:MM/dd/yyyy} {dateAndTime:hh:mm:ss tt}| |{msg}|";

                if (File.Exists(@"log.txt"))
                {
                    File.AppendAllText(@"log.txt",
                        $"{logStr}{Environment.NewLine}");
                }
                else
                {
                    using (var sw = File.CreateText(@"log.txt"))
                    {
                        sw.WriteLine(logStr);
                    }
                }

                Console.WriteLine(logStr);
            }
        }
        private void connectButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Functions.CheckIp(ipTextBox.Text))
                {
                    SetStatus("Invalid IP");
                    return;
                }
                
                ps4 = new PS4DBG(IPAddress.Parse(ipTextBox.Text));
                ps4.Connect();
                if (!ps4.IsConnected)
                {
                    throw new Exception();
                }
                
                SetStatus("Connected");
                if (!File.Exists("ip"))
                {
                    File.WriteAllText("ip", ipTextBox.Text);
                }
                else
                {
                    using var sw = File.CreateText(@"log.txt");
                    sw.Write(ipTextBox.Text);
                }
            }
            catch
            {
                SetStatus("Failed To Connect");
            }
        }

        private void setupButton_Click(object sender, EventArgs e)
        {
            if (!ps4.IsConnected)
            {
                SetStatus("Not connected to ps4");
                return;
            }
            
            var pl = ps4.GetProcessList();
            var su = pl.FindProcess("SceShellUI");
            if (su == null)
            {
                SetStatus("Couldn't find SceShellUI");
                return;
            }
            
            _pid = su.pid;
            var pm = ps4.GetProcessMaps(_pid);
            var tmp = pm.FindEntry("libSceSaveData.sprx")?.start;
            if (tmp == null)
            {
                MessageBox.Show("savedata lib not found", "Error");
                return;
            }
            
            _libSceSaveDataBase = (ulong)tmp;
            tmp = pm.FindEntry("libSceUserService.sprx")?.start;
            if (tmp == null)
            {
                MessageBox.Show("user service lib not found", "Error");
                return;
            }
            
            _libSceUserServiceBase = (ulong)tmp;
            tmp = pm.FindEntry("executable")?.start;
            if (tmp == null)
            {
                MessageBox.Show("executable not found", "Error");
                return;
            }
            
            _executableBase = (ulong)tmp;
            tmp = pm.FindEntry("libSceLibcInternal.sprx")?.start;
            if (tmp == null)
            {
                MessageBox.Show("libc not found", "Error");
                return;
            }
            
            _libSceLibcInternalBase = (ulong)tmp;
            _stub = pm.FindEntry("(NoName)clienthandler") == null ? ps4.InstallRPC(_pid) : pm.FindEntry("(NoName)clienthandler").start;
            var ret = ps4.Call(_pid, _stub, _libSceSaveDataBase + offsets.sceSaveDataInitialize3);
            WriteLog($"sceSaveDataInitialize3 ret = 0x{ret:X}");
            
            //PATCHES
            //SAVEDATA LIBRARY PATCHES
            ps4.WriteMemory(_pid, _libSceSaveDataBase + 0x00038AE8, (byte)0x00); // 'sce_' patch
            ps4.WriteMemory(_pid, _libSceSaveDataBase + 0x000377D9, (byte)0x00); // 'sce_sdmemory' patch
            ps4.WriteMemory(_pid, _libSceSaveDataBase + 0x00000ED9, (byte)0x30); // '_' patch

            var l = ps4.GetProcessList();
            var s = l.FindProcess("SceShellCore");
            var m = ps4.GetProcessMaps(s.pid);
            var ex = m.FindEntry("executable");
            
            //SHELLCORE PATCHES
            ps4.WriteMemory(s.pid, ex.start + 0x01600060, (byte)0x00); // 'sce_sdmemory' patch
            ps4.WriteMemory(s.pid, ex.start + 0x0087F840, new byte[]{0x48, 0x31, 0xC0, 0xC3}); //verify keystone patch
            ps4.WriteMemory(s.pid, ex.start + 0x00071130, new byte[] {0x31, 0xC0, 0xC3}); //transfer mount permission patch eg mount foreign saves with write permission
            ps4.WriteMemory(s.pid, ex.start + 0x000D6830, new byte[] { 0x31, 0xC0, 0xC3 });//patch psn check to load saves saves foreign to current account
            ps4.WriteMemory(s.pid, ex.start + 0x0007379E, new byte[] { 0x90, 0x90 }); // ^
            ps4.WriteMemory(s.pid, ex.start + 0x00070C38, new byte[] {0x90, 0x90, 0x90, 0x90, 0x90, 0x90}); // something something patches... 
            ps4.WriteMemory(s.pid, ex.start + 0x00070855, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90, 0x90 }); // don't even remember doing this
            ps4.WriteMemory(s.pid, ex.start + 0x00070054, new byte[] { 0x90, 0x90}); //nevah jump
            ps4.WriteMemory(s.pid, ex.start + 0x00070260, new byte[] { 0x90, 0xE9 }); //always jump
            
            //WRITE CUSTOM FUNCTIONS
            _getSaveDirectoriesAddr = ps4.AllocateMemory(_pid, 0x8000);
            ps4.WriteMemory(_pid, _getSaveDirectoriesAddr, Functions.GetSaveDirectories);
            ps4.WriteMemory(_pid, _getSaveDirectoriesAddr + 0x12, _libSceLibcInternalBase + 0x000B3F40); //opendir
            ps4.WriteMemory(_pid, _getSaveDirectoriesAddr + 0x20, _libSceLibcInternalBase + 0x000B4CE0); //readdir
            ps4.WriteMemory(_pid, _getSaveDirectoriesAddr + 0x2E, _libSceLibcInternalBase + 0x000B2D20);//closedir
            ps4.WriteMemory(_pid, _getSaveDirectoriesAddr + 0x3C, _libSceLibcInternalBase + 0x000C0A40); //strcpy

            _getUsersAddr = _getSaveDirectoriesAddr + (uint)Functions.GetSaveDirectories.Length + 0x20;
            ps4.WriteMemory(_pid, _getUsersAddr, Functions.GetUsers);
            ps4.WriteMemory(_pid, _getUsersAddr + 0x15, _libSceUserServiceBase + offsets.sceUserServiceGetLoginUserIdList);
            ps4.WriteMemory(_pid, _getUsersAddr + 0x23, _libSceUserServiceBase + offsets.sceUserServiceGetUserName);
            ps4.WriteMemory(_pid, _getUsersAddr + 0x31, _libSceLibcInternalBase + 0x000C0A40); //strcpy


            var users = GetUsers();
            userComboBox.DataSource = users;

            SetStatus("Setup Done :)");
        }

        private void searchButton_Click(object sender, EventArgs e)
        {

            if (!ps4.IsConnected)
            {
                SetStatus("Not connected to ps4");
                return;
            }
            
            if (_pid == 0)
            {
                SetStatus("dont forget to click setup");
                return;
            }
            
            if (_selectedGame == null)
            {
                SetStatus("No game selected");
                return;
            }
            
            var pm = ps4.GetProcessMaps(_pid);
            if (pm.FindEntry("(NoName)clienthandler") == null)
            {
                SetStatus("RPC Stub Not Found");
                return;
            }
            
            var dirNameAddr = ps4.AllocateMemory(_pid, Marshal.SizeOf(typeof(SceSaveDataDirName)) * 1024 + 0x10 + Marshal.SizeOf(typeof(SceSaveDataParam)) * 1024);
            var titleIdAddr = dirNameAddr + (uint) Marshal.SizeOf(typeof(SceSaveDataDirName)) * 1024;
            var paramAddr = titleIdAddr + 0x10;
            SceSaveDataDirNameSearchCond searchCond = new SceSaveDataDirNameSearchCond
            {
                userId = GetUser(),
                titleId = titleIdAddr
            };
            SceSaveDataDirNameSearchResult searchResult = new SceSaveDataDirNameSearchResult
            {
                dirNames = dirNameAddr,
                dirNamesNum = 1024,
                param = paramAddr,
            };
            
            ps4.WriteMemory(_pid, titleIdAddr, _selectedGame);
            dirsComboBox.DataSource = Find(searchCond, searchResult);
            ps4.FreeMemory(_pid, dirNameAddr, Marshal.SizeOf(typeof(SceSaveDataDirName)) * 1024 + 0x10 + Marshal.SizeOf(typeof(SceSaveDataParam)) * 1024);
            SetStatus(dirsComboBox.Items.Count > 0
                ? $"Found {dirsComboBox.Items.Count} Save Directories :D"
                : "Found 0 Save Directories :(");
        }

        private void mountButton_Click(object sender, EventArgs e)
        {
            if (!ps4.IsConnected)
            {
                SetStatus("Not connected to ps4");
                return;
            }
            
            if (dirsComboBox.Items.Count == 0)
            {
                SetStatus("No save selected");
                return;
            }
            
            if (_selectedGame == null)
            {
                SetStatus("No game selected");
                return;
            }
            
            var dirNameAddr = ps4.AllocateMemory(_pid, Marshal.SizeOf(typeof(SceSaveDataDirName)) + 0x10 + 0x41);
            var titleIdAddr = dirNameAddr + (uint)Marshal.SizeOf(typeof(SceSaveDataDirName));
            var fingerprintAddr = titleIdAddr + 0x10;
            ps4.WriteMemory(_pid, titleIdAddr, _selectedGame);
            ps4.WriteMemory(_pid, fingerprintAddr, "0000000000000000000000000000000000000000000000000000000000000000");
            var dirName = new SceSaveDataDirName
            {
                data = dirsComboBox.Text
            };

            var mount = new SceSaveDataMount
            {
                userId = GetUser(),
                dirName = dirNameAddr,
                blocks = 32768,
                mountMode = 0x8 | 0x2,
                titleId = titleIdAddr,
                fingerprint = fingerprintAddr

            };
            var mountResult = new SceSaveDataMountResult
            {

            };
            ps4.WriteMemory(_pid, dirNameAddr, dirName);
            var mountPointLocation = Mount(mount, mountResult);
            
            ps4.FreeMemory(_pid, dirNameAddr, Marshal.SizeOf(typeof(SceSaveDataDirName)) + 0x10 + 0x41);
            if (mountPointLocation != "")
            {
                _currentMountPointList?.Add($"{_selectedGame}_{((SearchEntry)dirsComboBox.SelectedItem).DirName}", new MountPointStruct()
                {
                    Directory = dirsComboBox.SelectedItem,
                    Title = _selectedGame,
                    MountPoint = mountPointLocation
                });
                
                dirsComboBox.BorderColor = Color.LimeGreen;
                
                WriteLog($"Current Mount Point list: {_currentMountPointList}");
                SetStatus($"Save Mounted in {mountPointLocation}");
            }
            else
            {
                SetStatus("Mounting Failed");
            }
        }

        private void unmountButton_Click(object sender, EventArgs e)
        {
            if (!ps4.IsConnected)
            {
                SetStatus("Not connected to ps4");
                return;
            }
            
            if (_currentMountPointList.Count == 0)
            {
                SetStatus("No save mounted");
                return;
            }

            var currentSaveDirectory = ((SearchEntry) dirsComboBox.SelectedItem).DirName;

            if (!_currentMountPointList.ContainsKey($"{_selectedGame}_{currentSaveDirectory}"))
            {
                SetStatus("Current selected save not mounted");
                return;
            }

            _currentMountPointList.TryGetValue($"{_selectedGame}_{currentSaveDirectory}", out var currentMountPoint);
            var mountPoint = new SceSaveDataMountPoint
            {
                data = ((MountPointStruct)currentMountPoint)?.MountPoint,
            };

            Unmount(mountPoint);
            dirsComboBox.BorderColor = Color.LightGray;
            _currentMountPointList.Remove($"{_selectedGame}_{currentSaveDirectory}");
            SetStatus("Save Unmounted");
        }

        private void unmountAllButton_Click(object sender, EventArgs e)
        {
            if (!ps4.IsConnected)
            {
                SetStatus("Not connected to ps4");
                return;
            }
            
            if (dirsComboBox.Items.Count == 0)
            {
                SetStatus("No save selected");
                return;
            }
            
            if (_selectedGame == null)
            {
                SetStatus("No game selected");
                return;
            }

            var unmountTypeDialog = new UnmountAllTypeDialog(this)
            {
                StartPosition = FormStartPosition.CenterParent
            };
            unmountTypeDialog.Show();
        }

        public void TryDirtyUnmount()
        {
            if (!ps4.IsConnected)
            {
                SetStatus("Not connected to ps4");
                return;
            }
            
            if (dirsComboBox.Items.Count == 0)
            {
                SetStatus("No save selected");
                return;
            }
            
            if (_selectedGame == null)
            {
                SetStatus("No game selected");
                return;
            }
            
            for (var i = 0; i < 9; i++)
            {
                
                SceSaveDataMountPoint mountPoint = new SceSaveDataMountPoint
                {
                    data = $"/savedata{i}",
                };

                Unmount(mountPoint);
            }
            
            dirsComboBox.BorderColor = Color.LightGray;
            _currentMountPointList.Clear();
            SetStatus("All save unmounted");
        }

        public void TryUnmountExists()
        {
            if (!ps4.IsConnected)
            {
                SetStatus("Not connected to ps4");
                return;
            }
            
            if (dirsComboBox.Items.Count == 0)
            {
                SetStatus("No save selected");
                return;
            }
            
            if (_selectedGame == null)
            {
                SetStatus("No game selected");
                return;
            }

            if (_currentMountPointList.Count == 0)
            {
                SetStatus("No have mounted save on this session");
                return;
            }

            var saveDictionary = new Dictionary<string, object>(_currentMountPointList);
            foreach (var currentMountPoint in saveDictionary)
            {
                SceSaveDataMountPoint mountPoint = new SceSaveDataMountPoint
                {
                    data = ((MountPointStruct)currentMountPoint.Value).MountPoint,
                };

                Unmount(mountPoint);
                _currentMountPointList.Remove(currentMountPoint.Key);
            }
            
            dirsComboBox.BorderColor = Color.LightGray;
            SetStatus("All save unmounted");
        }

        private void createButton_Click(object sender, EventArgs e)
        {
            if (!ps4.IsConnected)
            {
                SetStatus("Not connected to ps4");
                return;
            }
            
            if (_pid == 0)
            {
                SetStatus("Don't forget to setup");
                return;
            }
            
            if (nameTextBox.Text == "")
            {
                SetStatus("No Save Name");
                return;
            }
            
            if (_selectedGame == null)
            {
                SetStatus("No game selected");
                return;
            }
            
            var pm = ps4.GetProcessMaps(_pid);
            if (pm.FindEntry("(NoName)clienthandler") == null)
            {
                SetStatus("RPC Stub Not Found");
                return;
            }

            var dirNameAddr = ps4.AllocateMemory(_pid, Marshal.SizeOf(typeof(SceSaveDataDirName)) + 0x10 + 0x41);
            var titleIdAddr = dirNameAddr + (uint)Marshal.SizeOf(typeof(SceSaveDataDirName));
            var fingerprintAddr = titleIdAddr + 0x10;
            ps4.WriteMemory(_pid, fingerprintAddr, "0000000000000000000000000000000000000000000000000000000000000000");
            ps4.WriteMemory(_pid, titleIdAddr, _selectedGame);
            var dirName = new SceSaveDataDirName
            {
                data = nameTextBox.Text
            };

            SceSaveDataMount mount = new SceSaveDataMount
            {
                userId = GetUser(),
                dirName = dirNameAddr,
                blocks = (ulong) sizeTrackBar.Value,
                mountMode = 4 | 2 | 8,
                titleId = titleIdAddr,
                fingerprint = fingerprintAddr

            };
            SceSaveDataMountResult mountResult = new SceSaveDataMountResult
            {

            };
            ps4.WriteMemory(_pid, dirNameAddr, dirName);
            var tempMountPoint = Mount(mount, mountResult);
            ps4.FreeMemory(_pid, dirNameAddr, Marshal.SizeOf(typeof(SceSaveDataDirName)) + 0x10 + 0x41);
            if (tempMountPoint != "")
            {
                SetStatus("Save Created");
                var mountPoint = new SceSaveDataMountPoint
                {
                    data = tempMountPoint,
                };
                Unmount(mountPoint);
            }
            else
            {
                SetStatus("Save Creation Failed");
            }
        }

        private int GetUser()
        {
            return _user != 0 ? _user : InitialUser();
        }

        private int InitialUser()
        {
            var bufferAddrObject = ps4.AllocateMemory(_pid, sizeof(int)) as object;

            ps4.Call(_pid, _stub, _libSceUserServiceBase + offsets.sceUserServiceGetInitialUser, bufferAddrObject);

            var bufferAddr = (ulong) bufferAddrObject;
            var id = ps4.ReadMemory<int>(_pid, bufferAddr);

            ps4.FreeMemory(_pid, bufferAddr, sizeof(int));

            return id;
        }

        private SearchEntry[] Find(SceSaveDataDirNameSearchCond searchCond, SceSaveDataDirNameSearchResult searchResult)
        {
            var searchCondAddr = ps4.AllocateMemory(_pid, Marshal.SizeOf(typeof(SceSaveDataDirNameSearchCond)) + Marshal.SizeOf(typeof(SceSaveDataDirNameSearchResult)));
            var searchResultAddr = searchCondAddr + (uint)Marshal.SizeOf(typeof(SceSaveDataDirNameSearchCond));

            ps4.WriteMemory(_pid, searchCondAddr, searchCond);
            ps4.WriteMemory(_pid, searchResultAddr, searchResult);
            var ret = ps4.Call(_pid, _stub, _libSceSaveDataBase + offsets.sceSaveDataDirNameSearch, searchCondAddr, searchResultAddr);
            WriteLog($"sceSaveDataDirNameSearch ret = 0x{ret:X}");
            if ( ret == 0)
            {
                searchResult = ps4.ReadMemory<SceSaveDataDirNameSearchResult>(_pid, searchResultAddr);
                SearchEntry[] sEntries = new SearchEntry[searchResult.hitNum];
                var paramMemory = ps4.ReadMemory(_pid, searchResult.param, (int)searchResult.hitNum * Marshal.SizeOf(typeof(SceSaveDataParam)));
                var dirNamesMemory = ps4.ReadMemory(_pid, searchResult.dirNames, (int)searchResult.hitNum * 32);
                for (var i = 0; i < searchResult.hitNum; i++)
                {
                    SceSaveDataParam tmp = (SceSaveDataParam)PS4DBG.GetObjectFromBytes(PS4DBG.SubArray(paramMemory, i * Marshal.SizeOf(typeof(SceSaveDataParam)), Marshal.SizeOf(typeof(SceSaveDataParam))), typeof(SceSaveDataParam));
                    sEntries[i] = new SearchEntry
                    {
                        DirName = System.Text.Encoding.UTF8.GetString(PS4DBG.SubArray(dirNamesMemory, i * 32, 32)),
                        Title = System.Text.Encoding.UTF8.GetString(tmp.title),
                        Subtitle = System.Text.Encoding.UTF8.GetString(tmp.subTitle),
                        Detail = System.Text.Encoding.UTF8.GetString(tmp.detail),
                        Time = new DateTime(1970, 1, 1).ToLocalTime().AddSeconds(tmp.mtime).ToString(),
                    };
                }
                ps4.FreeMemory(_pid, searchCondAddr, Marshal.SizeOf(typeof(SceSaveDataDirNameSearchCond)) + Marshal.SizeOf(typeof(SceSaveDataDirNameSearchResult)));
                return sEntries;
            }

            ps4.FreeMemory(_pid, searchCondAddr, Marshal.SizeOf(typeof(SceSaveDataDirNameSearchCond)) + Marshal.SizeOf(typeof(SceSaveDataDirNameSearchResult)));

            return new SearchEntry[0];
        }

        private void Unmount(SceSaveDataMountPoint mountPoint)
        {
            var mountPointAddr = ps4.AllocateMemory(_pid, Marshal.SizeOf(typeof(SceSaveDataMountPoint)));

            ps4.WriteMemory(_pid, mountPointAddr, mountPoint);
            var ret = ps4.Call(_pid, _stub, _libSceSaveDataBase + offsets.sceSaveDataUmount, mountPointAddr);
            WriteLog($"sceSaveDataUmount ret = 0x{ret:X}");
            ps4.FreeMemory(_pid, mountPointAddr, Marshal.SizeOf(typeof(SceSaveDataMountPoint)));
        }

        private string Mount(SceSaveDataMount mount, SceSaveDataMountResult mountResult)
        {
            var mountAddr = ps4.AllocateMemory(_pid, Marshal.SizeOf(typeof(SceSaveDataMount)) + Marshal.SizeOf(typeof(SceSaveDataMountResult)));
            var mountResultAddr = mountAddr + (uint)Marshal.SizeOf(typeof(SceSaveDataMount));
            ps4.WriteMemory(_pid, mountAddr, mount);
            ps4.WriteMemory(_pid, mountResultAddr, mountResult);

            var ret = ps4.Call(_pid, _stub, _libSceSaveDataBase + offsets.sceSaveDataMount, mountAddr, mountResultAddr);
            WriteLog($"sceSaveDataMount ret = 0x{ret:X}");
            if (ret == 0)
            {
                mountResult = ps4.ReadMemory<SceSaveDataMountResult>(_pid, mountResultAddr);

                ps4.FreeMemory(_pid, mountAddr, Marshal.SizeOf(typeof(SceSaveDataMount)) + Marshal.SizeOf(typeof(SceSaveDataMountResult)));

                return mountResult.mountPoint.data;
            }

            ps4.FreeMemory(_pid, mountAddr, Marshal.SizeOf(typeof(SceSaveDataMount)) + Marshal.SizeOf(typeof(SceSaveDataMountResult)));

            return "";
        }

        private class SearchEntry
        {
            public string DirName;
            public string Title;
            public string Subtitle;
            public string Detail;
            public string Time;
            public override string ToString()
            {
                return DirName;
            }
        }

        private void dirsComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            dirsComboBox.BorderColor = Color.LightGray;
            titleTextBox.Text = ((SearchEntry)dirsComboBox.SelectedItem).Title;
            subtitleTextBox.Text = ((SearchEntry)dirsComboBox.SelectedItem).Subtitle;
            detailsTextBox.Text = ((SearchEntry)dirsComboBox.SelectedItem).Detail;
            dateTextBox.Text = ((SearchEntry)dirsComboBox.SelectedItem).Time;
            if (_currentMountPointList.ContainsKey($"{_selectedGame}_{((SearchEntry)dirsComboBox.SelectedItem).DirName}"))
            {
                dirsComboBox.BorderColor = Color.LimeGreen;
            }
        }
        
        private void userComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            _user = ((User)userComboBox.SelectedItem).Id;
        }

        private class User
        {
            public int Id;
            public string Name;

            public override string ToString()
            {
                return Name;
            }
        }
        private string[] GetSaveDirectories()
        {
            var dirs = new List<string>();
            var mem = ps4.AllocateMemory(_pid, 0x8000);
            var path = mem;
            var buffer = mem + 0x101;

            ps4.WriteMemory(_pid, path, $"/user/home/{GetUser():x}/savedata/");
            var ret = (int)ps4.Call(_pid, _stub, _getSaveDirectoriesAddr, path, buffer);
            if (ret != -1 && ret != 0)
            {
                var bDirs = ps4.ReadMemory(_pid, buffer, ret * 0x10);
                for (var i = 0; i < ret; i++)
                {
                    var sDir = System.Text.Encoding.UTF8.GetString(PS4DBG.SubArray(bDirs, i * 10, 9));
                    dirs.Add(sDir);
                }
            }
            
            ps4.FreeMemory(_pid, mem, 0x8000);
            
            return dirs.ToArray();
        }

        private User[] GetUsers()
        {
            List<User> users = new List<User>();
            var mem = ps4.AllocateMemory(_pid, 0x1);
            var ret = (int)ps4.Call(_pid, _stub, _getUsersAddr, mem);
            
            if (ret != -1 && ret != 0)
            {
                var buffer = ps4.ReadMemory(_pid, mem, (21) * 4);
                for (int i = 0; i < 4; i++)
                {
                    var id = BitConverter.ToInt32(buffer, 21 * i);
                    if (id == 0)
                    {
                        continue;
                    }
                    var name = System.Text.Encoding.UTF8.GetString(PS4DBG.SubArray(buffer, i * 21 + 4, 16));
                    users.Add(new User { Id = id, Name = name });
                }
            }
            
            ps4.FreeMemory(_pid, mem, 0x1);
            
            return users.ToArray();
        }
        private void payloadButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (!Functions.CheckIp(ipTextBox.Text))
                {
                    SetStatus("Invalid IP");
                    return;
                }

                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("PS4Saves.ps4debug.bin"))
                {
                    var buffer = new byte[stream.Length];
                    stream.Read(buffer, 0, buffer.Length);
                    var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                    IAsyncResult result = socket.BeginConnect(new IPEndPoint(IPAddress.Parse(ipTextBox.Text), 9021), null, null);
                    var connected = result.AsyncWaitHandle.WaitOne(3000);
                    if (connected)
                    {
                        socket.Send(buffer, buffer.Length, SocketFlags.None);
                    }

                    SetStatus(connected ? "Payload sent" : "Failed to connect");
                    socket.Close();
                }
            }
            catch
            {
                SetStatus("Sending payload failed");
            }
        }

        private void getGamesButton_Click(object sender, EventArgs e)
        {
            if (!ps4.IsConnected)
            {
                SetStatus("Not connected to ps4");
                return;
            }
            
            if (_pid == 0)
            {
                SetStatus("Don't forget to press setup");
                return;
            }
            
            var pm = ps4.GetProcessMaps(_pid);
            if (pm.FindEntry("(NoName)clienthandler") == null)
            {
                SetStatus("RPC Stub Not Found");
                return;
            }
            
            var dirs = GetSaveDirectories();
            gamesComboBox.DataSource = dirs;
        }

        private void gamesComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            _selectedGame = (string)gamesComboBox.SelectedItem;
        }
    }
}
