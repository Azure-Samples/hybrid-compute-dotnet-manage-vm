namespace VirtualMachine
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;

    using Profile2018Compute = Microsoft.Azure.Management.Profiles.hybrid_2018_03_01.Compute;
    using Profile2018Network = Microsoft.Azure.Management.Profiles.hybrid_2018_03_01.Network;
    using Profile2018ResourceManager = Microsoft.Azure.Management.Profiles.hybrid_2018_03_01.ResourceManager;
    using Profile2018Storage = Microsoft.Azure.Management.Profiles.hybrid_2018_03_01.Storage;
    using Microsoft.Azure.Management.ResourceManager.Fluent;
    using Microsoft.Rest;
    using Microsoft.Rest.Azure.Authentication;
    using Newtonsoft.Json.Linq;

    class Program
    {
        private const string ComponentName = "DotnetSDKVirtualMachineManagementSample";
        private const string vhdURItemplate = "https://{0}.blob.{1}/vhds/{2}.vhd";

        static void runSample(string tenantId, string subscriptionId, string servicePrincipalId, string servicePrincipalSecret, string location, string armEndpoint)
        {
            var resourceGroupName = SdkContext.RandomResourceName("rgDotnetSdk", 24);
            var vmName = SdkContext.RandomResourceName("vmDotnetSdk", 24);
            var vmNameManagedDisk = SdkContext.RandomResourceName("vmManagedDotnetSdk", 24);
            var vnetName = SdkContext.RandomResourceName("vnetDotnetSdk", 24);
            var subnetName = SdkContext.RandomResourceName("subnetDotnetSdk", 24);
            var subnetAddress = "10.0.0.0/24";
            var vnetAddresses = "10.0.0.0/16";
            var ipName = SdkContext.RandomResourceName("ipDotnetSdk", 24);
            var nicName = SdkContext.RandomResourceName("nicDotnetSdk", 24); ;
            var diskName = SdkContext.RandomResourceName("diskDotnetSdk", 24);
            var storageAccountName = SdkContext.RandomResourceName("storageaccount", 18);
            var username = "tirekicker";
            var password = "12NewPA$$w0rd!";

            Console.WriteLine("Get credential token");
            var adSettings = getActiveDirectoryServiceSettings(armEndpoint);
            var credentials = ApplicationTokenProvider.LoginSilentAsync(tenantId, servicePrincipalId, servicePrincipalSecret, adSettings).GetAwaiter().GetResult();

            Console.WriteLine("Instantiate resource management client");
            var rmClient = GetResourceManagementClient(new Uri(armEndpoint), credentials, subscriptionId);

            Console.WriteLine("Instantiate storage account client");
            var storageClient = GetStorageClient(new Uri(armEndpoint), credentials, subscriptionId);

            Console.WriteLine("Instantiate network client");
            var networkClient = GetNetworkClient(new Uri(armEndpoint), credentials, subscriptionId);

            Console.WriteLine("Instantiate compute client");
            var computeClient = GetComputeClient(new Uri(armEndpoint), credentials, subscriptionId);

            // Create a resource group
            try
            {
                Console.WriteLine("Create resource group");
                var rmTask = rmClient.ResourceGroups.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    new Profile2018ResourceManager.Models.ResourceGroup
                    {
                        Location = location
                    });
                rmTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create resource group. Exception: {0}", ex.Message));
            }

            // Create a Storage Account
            var storageAccount = new Profile2018Storage.Models.StorageAccount();
            try
            {
                Console.WriteLine(String.Format("Creating a storage account with name:{0}", storageAccountName));
                var storageProperties = new Profile2018Storage.Models.StorageAccountCreateParameters
                {
                    Location = location,
                    Kind = Profile2018Storage.Models.Kind.Storage,
                    Sku = new Profile2018Storage.Models.Sku(Profile2018Storage.Models.SkuName.StandardLRS)
                };

                var storageTask = storageClient.StorageAccounts.CreateWithHttpMessagesAsync(resourceGroupName, storageAccountName, storageProperties);
                storageTask.Wait();
                storageAccount = storageTask.Result.Body;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create storage account {0}. Exception: {1}", storageAccountName, ex.Message));
            }

            var subnet = new Profile2018Network.Models.Subnet();

            // Create virtual network
            try
            {
                Console.WriteLine("Create vitual network");
                var vnet = new Profile2018Network.Models.VirtualNetwork
                {
                    Location = location,
                    AddressSpace = new Profile2018Network.Models.AddressSpace
                    {
                        AddressPrefixes = new List<string> { vnetAddresses }
                    }
                };
                var vnetTask = networkClient.VirtualNetworks.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    vnetName,
                    vnet);
                vnetTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create virtual network. Exception: {0}", ex.Message));
            }

            // Create subnet in the virtual network
            try
            {
                Console.WriteLine("Create a subnet");
                var subnetTask = networkClient.Subnets.CreateOrUpdateWithHttpMessagesAsync(resourceGroupName, vnetName, subnetName, new Profile2018Network.Models.Subnet
                {
                    AddressPrefix = subnetAddress,
                    Name = subnetName
                });
                subnetTask.Wait();
                subnet = subnetTask.Result.Body;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create subnet. Exception: {0}", ex.Message));
            }

            // Create a public address
            var ip = new Profile2018Network.Models.PublicIPAddress();
            try
            {
                Console.WriteLine("Create IP");
                var ipProperties = new Profile2018Network.Models.PublicIPAddress
                {
                    Location = location,
                    PublicIPAllocationMethod = Profile2018Network.Models.IPAllocationMethod.Dynamic,
                };
                var ipTask = networkClient.PublicIPAddresses.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    ipName,
                    ipProperties);
                ipTask.Wait();
                ip = ipTask.Result.Body;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create IP. Exception: {0}", ex.Message));
            }

            // Create a network interface
            var nic = new Profile2018Network.Models.NetworkInterface();
            var vmStorageProfile = new Profile2018Compute.Models.StorageProfile();
            try
            {
                Console.WriteLine("Create network interface");
                var nicProperties = new Profile2018Network.Models.NetworkInterface
                {
                    Location = location,
                    IpConfigurations = new List<Profile2018Network.Models.NetworkInterfaceIPConfiguration>
                    {
                        new Profile2018Network.Models.NetworkInterfaceIPConfiguration
                        {
                            Name = string.Format("{0}-ipconfig", nicName),
                            PrivateIPAllocationMethod = "Dynamic",
                            PublicIPAddress = ip,
                            Subnet = subnet
                        }
                    }

                };

                var nicTask = networkClient.NetworkInterfaces.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    nicName,
                    nicProperties);
                nicTask.Wait();
                nic = nicTask.Result.Body;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create network interface. Exception: {0}", ex.Message));
            }

            // Create a data disk
            var disk = new Profile2018Compute.Models.Disk();
            try
            {
                Console.WriteLine("Create a data disk");
                var diskProperties = new Profile2018Compute.Models.Disk
                {
                    CreationData = new Profile2018Compute.Models.CreationData
                    {
                        CreateOption = Profile2018Compute.Models.DiskCreateOption.Empty,
                    },
                    Location = location,
                    Sku = new Profile2018Compute.Models.DiskSku
                    {
                        Name = Profile2018Compute.Models.StorageAccountTypes.StandardLRS
                    },
                    DiskSizeGB = 1,
                };
                var diskTask = computeClient.Disks.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    diskName,
                    diskProperties);
                diskTask.Wait();
                disk = diskTask.Result.Body;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create data disk. Exception: {0}", ex.Message));
            }

            // VM Hardware profile
            var vmHardwareProfile = new Profile2018Compute.Models.HardwareProfile
            {
                VmSize = "Standard_A1"
            };

            // VM OS Profile
            var vmOsProfile = new Profile2018Compute.Models.OSProfile
            {
                ComputerName = vmName,
                AdminUsername = username,
                AdminPassword = password
            };

            // VM Network profile
            var vmNetworkProfile = new Profile2018Compute.Models.NetworkProfile
            {
                NetworkInterfaces = new List<Profile2018Compute.Models.NetworkInterfaceReference>
                {
                    new Profile2018Compute.Models.NetworkInterfaceReference
                    {
                        Id = nic.Id,
                        Primary = true
                    }
                }
            };

            // VM Storage profile
            string diskUri = string.Format("{0}test/{1}.vhd", storageAccount.PrimaryEndpoints.Blob, diskName);
            var osDiskName = "osDisk";
            string osDiskUri = string.Format("{0}test/{1}.vhd", storageAccount.PrimaryEndpoints.Blob, osDiskName);
            vmStorageProfile = new Profile2018Compute.Models.StorageProfile
            {
                OsDisk = new Profile2018Compute.Models.OSDisk
                {
                    Name = osDiskName,
                    CreateOption = Profile2018Compute.Models.DiskCreateOptionTypes.FromImage,
                    Caching = Profile2018Compute.Models.CachingTypes.ReadWrite,
                    OsType = Profile2018Compute.Models.OperatingSystemTypes.Linux,
                    Vhd = new Profile2018Compute.Models.VirtualHardDisk
                    {
                        Uri = osDiskUri
                    }
                },
                ImageReference = new Profile2018Compute.Models.ImageReference
                {
                    Publisher = "Canonical",
                    Offer = "UbuntuServer",
                    Sku = "16.04-LTS",
                    Version = "latest"
                },
                DataDisks = null
            };

            // Create Linux VM
            var linuxVm = new Profile2018Compute.Models.VirtualMachine();
            try
            {
                Console.WriteLine("Create a virtual machine");
                var t1 = DateTime.Now;
                var vmTask = computeClient.VirtualMachines.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    vmName,
                    new Profile2018Compute.Models.VirtualMachine
                    {
                        Location = location,
                        NetworkProfile = vmNetworkProfile,
                        StorageProfile = vmStorageProfile,
                        OsProfile = vmOsProfile,
                        HardwareProfile = vmHardwareProfile
                    });
                vmTask.Wait();
                linuxVm = vmTask.Result.Body;
                var t2 = DateTime.Now;
                vmStorageProfile = linuxVm.StorageProfile;
                Console.WriteLine(String.Format("Create virtual machine {0} took {1} seconds", linuxVm.Id, (t2 - t1).TotalSeconds.ToString()));
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create virtual machine. Exception: {0}", ex.Message));
            }

            // Update - Tag the virtual machine
            try
            {
                Console.WriteLine("Tag virtual machine");
                var vmTagTask = computeClient.VirtualMachines.CreateOrUpdateWithHttpMessagesAsync(resourceGroupName, vmName, new Profile2018Compute.Models.VirtualMachine
                {
                    Location = location,
                    Tags = new Dictionary<string, string> { { "who-rocks", "java" }, { "where", "on azure stack" } }
                });
                vmTagTask.Wait();
                linuxVm = vmTagTask.Result.Body;
                Console.WriteLine(string.Format("Taged virtual machine {0}", linuxVm.Id));
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not tag virtual machine. Exception: {0}", ex.Message));
            }

            // Update - Add data disk
            try
            {
                Console.WriteLine("Attach data disk to virtual machine");
                string newDataDiskName = "dataDisk2";
                string newDataDiskVhdUri = string.Format("{0}test/{1}.vhd", storageAccount.PrimaryEndpoints.Blob, newDataDiskName);
                var dataDisk = new Profile2018Compute.Models.DataDisk
                {
                    CreateOption = Profile2018Compute.Models.DiskCreateOptionTypes.Empty,
                    Caching = Profile2018Compute.Models.CachingTypes.ReadOnly,
                    DiskSizeGB = 1,
                    Lun = 2,
                    Name = newDataDiskName,
                    Vhd = new Profile2018Compute.Models.VirtualHardDisk
                    {
                        Uri = newDataDiskVhdUri
                    }
                };
                vmStorageProfile.DataDisks.Add(dataDisk);
                var addTask = computeClient.VirtualMachines.CreateOrUpdateWithHttpMessagesAsync(resourceGroupName, vmName, new Profile2018Compute.Models.VirtualMachine
                {
                    Location = location,
                    StorageProfile = vmStorageProfile
                });
                addTask.Wait();
                vmStorageProfile = addTask.Result.Body.StorageProfile;
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not add data disk to virtual machine. Exception: {0}", ex.Message));
            }

            // Update - detach data disk
            try
            {
                Console.WriteLine("Detach data disk from virtual machine");
                vmStorageProfile.DataDisks.RemoveAt(0);
                var detachTask = computeClient.VirtualMachines.CreateOrUpdateWithHttpMessagesAsync(resourceGroupName, vmName, new Profile2018Compute.Models.VirtualMachine {
                    Location = location,
                    StorageProfile = vmStorageProfile
                });
                detachTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not detach data disk from virtual machine. Exception: {0}", ex.Message));
            }

            // Restart the virtual machine
            try
            {
                Console.WriteLine("Restart virtual machine");
                var restartTask = computeClient.VirtualMachines.RestartWithHttpMessagesAsync(resourceGroupName, vmName);
                restartTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not restart virtual machine. Exception: {0}", ex.Message));
            }

            // Stop(powerOff) the virtual machine
            try
            {
                Console.WriteLine("Power off virtual machine");
                var stopTask = computeClient.VirtualMachines.PowerOffWithHttpMessagesAsync(resourceGroupName, vmName);
                stopTask.Wait();

            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not power off virtual machine. Exception: {0}", ex.Message));
            }

            // Delete VM
            try
            {
                Console.WriteLine("Delete virtual machine");
                var deleteTask = computeClient.VirtualMachines.DeleteWithHttpMessagesAsync(resourceGroupName, vmName);
                deleteTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not delete virtual machine. Exception: {0}", ex.Message));
            }

            // VM Storage profile managed disk
            vmStorageProfile = new Profile2018Compute.Models.StorageProfile
            {
                DataDisks = new List<Profile2018Compute.Models.DataDisk>
                {
                    new Profile2018Compute.Models.DataDisk
                    {
                        CreateOption = Profile2018Compute.Models.DiskCreateOptionTypes.Attach,
                        ManagedDisk = new Profile2018Compute.Models.ManagedDiskParameters
                        {
                            StorageAccountType = Profile2018Compute.Models.StorageAccountTypes.StandardLRS,
                            Id = disk.Id
                        },
                        Caching = Profile2018Compute.Models.CachingTypes.ReadOnly,
                        DiskSizeGB = 1,
                        Lun = 1,
                        Name = diskName,
                    }
                },
                OsDisk = new Profile2018Compute.Models.OSDisk
                {
                    Name = osDiskName,
                    CreateOption = Profile2018Compute.Models.DiskCreateOptionTypes.FromImage,
                },
                ImageReference = new Profile2018Compute.Models.ImageReference
                {
                    Publisher = "Canonical",
                    Offer = "UbuntuServer",
                    Sku = "16.04-LTS",
                    Version = "latest"
                }
            };

            // Create Linux VM with managed disks
            var linuxVmManagedDisk = new Profile2018Compute.Models.VirtualMachine();
            try
            {
                Console.WriteLine("Create a virtual machine with managed disk");
                var t1 = DateTime.Now;
                var vmTask = computeClient.VirtualMachines.CreateOrUpdateWithHttpMessagesAsync(
                    resourceGroupName,
                    vmNameManagedDisk,
                    new Profile2018Compute.Models.VirtualMachine
                    {
                        Location = location,
                        NetworkProfile = vmNetworkProfile,
                        StorageProfile = vmStorageProfile,
                        OsProfile = vmOsProfile,
                        HardwareProfile = vmHardwareProfile
                    });
                vmTask.Wait();
                linuxVmManagedDisk = vmTask.Result.Body;
                var t2 = DateTime.Now;
                vmStorageProfile = linuxVm.StorageProfile;
                Console.WriteLine(String.Format("Create virtual machine with managed disk {0} took {1} seconds", linuxVmManagedDisk.Id, (t2 - t1).TotalSeconds.ToString()));
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not create virtual machine with managed disk. Exception: {0}", ex.Message));
            }

            // Delete VM with managed disk
            try
            {
                Console.WriteLine("Delete virtual machine with managed disk");
                var deleteTask = computeClient.VirtualMachines.DeleteWithHttpMessagesAsync(resourceGroupName, vmNameManagedDisk);
                deleteTask.Wait();
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not delete virtual machine with managed disk. Exception: {0}", ex.Message));
            }
        }

        static ActiveDirectoryServiceSettings getActiveDirectoryServiceSettings(string armEndpoint)
        {
            var settings = new ActiveDirectoryServiceSettings();

            try
            {
                var request = (HttpWebRequest)HttpWebRequest.Create(string.Format("{0}/metadata/endpoints?api-version=1.0", armEndpoint));
                request.Method = "GET";
                request.UserAgent = ComponentName;
                request.Accept = "application/xml";

                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    using (StreamReader sr = new StreamReader(response.GetResponseStream()))
                    {
                        var rawResponse = sr.ReadToEnd();
                        var deserialized = JObject.Parse(rawResponse);
                        var authenticationObj = deserialized.GetValue("authentication").Value<JObject>();
                        var loginEndpoint = authenticationObj.GetValue("loginEndpoint").Value<string>();
                        var audiencesObj = authenticationObj.GetValue("audiences").Value<JArray>();

                        settings.AuthenticationEndpoint = new Uri(loginEndpoint);
                        settings.TokenAudience = new Uri(audiencesObj[0].Value<string>());
                        settings.ValidateAuthority = loginEndpoint.TrimEnd('/').EndsWith("/adfs", StringComparison.OrdinalIgnoreCase) ? false : true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(String.Format("Could not get AD service settings. Exception: {0}", ex.Message));
            }
            return settings;
        }

        static void Main(string[] args)
        {
            var location = Environment.GetEnvironmentVariable("RESOURCE_LOCATION");
            var baseUriString = Environment.GetEnvironmentVariable("ARM_ENDPOINT");
            var servicePrincipalId = Environment.GetEnvironmentVariable("AZURE_CLIENT_ID");
            var servicePrincipalSecret = Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET");
            var tenantId = Environment.GetEnvironmentVariable("AZURE_TENANT_ID");
            var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");

            runSample(tenantId, subscriptionId, servicePrincipalId, servicePrincipalSecret, location, baseUriString);
        }

        private static Profile2018Storage.StorageManagementClient GetStorageClient(Uri baseUri, ServiceClientCredentials credential, string subscriptionId)
        {
            var client = new Profile2018Storage.StorageManagementClient(baseUri: baseUri, credentials: credential)
            {
                SubscriptionId = subscriptionId
            };
            client.SetUserAgent(ComponentName);

            return client;
        }

        private static Profile2018ResourceManager.ResourceManagementClient GetResourceManagementClient(Uri baseUri, ServiceClientCredentials credential, string subscriptionId)
        {
            var client = new Profile2018ResourceManager.ResourceManagementClient(baseUri: baseUri, credentials: credential)
            {
                SubscriptionId = subscriptionId
            };
            client.SetUserAgent(ComponentName);

            return client;
        }

        private static Profile2018Network.NetworkManagementClient GetNetworkClient(Uri baseUri, ServiceClientCredentials credential, string subscriptionId)
        {
            var client = new Profile2018Network.NetworkManagementClient(baseUri: baseUri, credentials: credential)
            {
                SubscriptionId = subscriptionId
            };
            client.SetUserAgent(ComponentName);

            return client;
        }

        private static Profile2018Compute.ComputeManagementClient GetComputeClient(Uri baseUri, ServiceClientCredentials credential, string subscriptionId)
        {
            var client = new Profile2018Compute.ComputeManagementClient(baseUri: baseUri, credentials: credential)
            {
                SubscriptionId = subscriptionId
            };
            client.SetUserAgent(ComponentName);
            
            return client;
        }
    }
}
