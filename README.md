---
services: Azure Stack
platforms: dotnet
author: seyadava
---

# Getting Started with compute - Manage Virtual Machines in Hybrid Environment - in C# asynchronously #  

Azure Stack Compute sample for managing virtual machines - 
- Create a resource group
- Create a storage account
- Create a virtual network
- Create a subnet in a virtual network
- Create a public IP address
- Create a network interface
- Create a data disk
- Create a linux virtual machine (unmanaged disk)
- Tag the virtual machine
- Add a data disk to the virtual machine
- Detach a data disk from the virtual machine
- Restart the virtual machine
- Power off the virtual machine 
- Delete the virtual machine
- Create a linux virtual machine with managed disk
- Delete the linux virtual machine with managed disk

## Running this sample ##

To run this sample:

1. Clone the repository using the following command:

    git clone https://github.com/Azure-Samples/hybrid-compute-dotnet-manage-vm.git

2. Create an Azure service principal and assign a role to access the subscription. For instructions on creating a service principal in Azure Stack, see [Use Azure PowerShell to create a service principal with a certificate](https://docs.microsoft.com/en-us/azure/azure-stack/azure-stack-create-service-principals). 

3. Set the following required environment variable values:

    * AZURE_TENANT_ID

    * AZURE_CLIENT_ID

    * AZURE_CLIENT_SECRET

    * AZURE_SUBSCRIPTION_ID

    * ARM_ENDPOINT

    * RESOURCE_LOCATION

4. Change directory to sample:

    * cd hybrid-compute-dotnet-manage-vm

5. Run the sample:

    dotnet restore

    dotnet run

## More information ##

[Azure .Net Developer Center](https://azure.microsoft.com/en-us/develop/net/)

---

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.